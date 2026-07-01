using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ReToolbox.Services;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class MemoryPage : Page
    {
        public MemoryPageViewModel ViewModel { get; }

        // x:Bind helper: show the pagefile result line only when there is text.
        public Visibility PagefileResultVisibility =>
            string.IsNullOrWhiteSpace(ViewModel.PagefileResultText) ? Visibility.Collapsed : Visibility.Visible;

        public MemoryPage()
        {
            ViewModel = App.Services.GetService<MemoryPageViewModel>()
                ?? throw new InvalidOperationException("MemoryPageViewModel not registered");

            ViewModel.StatusReported += OnStatusReported;

            InitializeComponent();

            ViewModel.LoadAutoCleanSettings();
            Loaded += MemoryPage_Loaded;
            Unloaded += MemoryPage_Unloaded;
        }

        private void MemoryPage_Loaded(object sender, RoutedEventArgs e)
        {
            PageAnimations.StaggerIn(this);
            SyncAutoCleanControls();
            ViewModel.Refresh();
        }

        private void MemoryPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // The background auto-clean timer is owned by the app-level
            // MemoryAutoCleanService and keeps running after navigating away.
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.Refresh();
        }

        private void OnStatusReported(string message)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.IsOpen = true;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Refresh();
        }

        private async void LightClean_Click(object sender, RoutedEventArgs e)
        {
            CleanProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在轻量清理...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await ViewModel.LightCleanCommand.ExecuteAsync(null);

            CleanProgress.Visibility = Visibility.Collapsed;
            StatusInfoBar.Message = ViewModel.LastCleanText;
        }

        private async void DeepClean_Click(object sender, RoutedEventArgs e)
        {
            // Deep clean flushes the system file cache; confirm before running.
            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "执行深度清理？",
                Content = "深度清理会清空系统文件缓存(standby/已修改页)，期间可能短暂卡顿。是否继续？",
                PrimaryButtonText = "清理",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            CleanProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在深度清理...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await ViewModel.DeepCleanCommand.ExecuteAsync(null);

            CleanProgress.Visibility = Visibility.Collapsed;
            StatusInfoBar.Message = ViewModel.LastCleanText;
        }

        private void TrimProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int pid) return;

            // Find the matching entry from the ranking list.
            foreach (var entry in ViewModel.TopProcesses)
            {
                if (entry.ProcessId == pid)
                {
                    ViewModel.TrimProcessCommand.Execute(entry);
                    return;
                }
            }
        }

        private async void ApplyPagefile_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PagefileMinInput = MinBox.Value.ToString("0");
            ViewModel.PagefileMaxInput = MaxBox.Value.ToString("0");

            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "修改虚拟内存？",
                Content = $"将设置分页文件为 {MinBox.Value:0} - {MaxBox.Value:0} MB，需重启电脑后生效。是否继续？",
                PrimaryButtonText = "应用",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ViewModel.ApplyRecommendedPagefileCommand.Execute(null);
            Bindings.Update();
        }

        private async void RestorePagefile_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "恢复为系统管理？",
                Content = "将交由 Windows 自动管理分页文件大小，需重启电脑后生效。是否继续？",
                PrimaryButtonText = "恢复",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ViewModel.ApplySystemManagedPagefileCommand.Execute(null);
            Bindings.Update();
        }

        private async void AutoAllocate_Click(object sender, RoutedEventArgs e)
        {
            // Read the preset directly from the radio buttons so the value shown
            // in the dialog is exactly what gets written — no stale state.
            bool hardcore = HardcorePresetRadio.IsChecked == true;
            ViewModel.SetPreset(hardcore);

            int minMb = ViewModel.RecommendedMinMb;
            int maxMb = ViewModel.RecommendedMaxMb;

            string presetName = hardcore ? "高级方案(极致防卡顿)" : "普通方案(求稳省心)";
            double predictedGb = (double)(App.Services.GetService(typeof(VirtualMemoryService)) as VirtualMemoryService)!
                .GetPredictedCommitLimitGb(minMb);
            double currentGb = ViewModel.VirtualTotalGb;
            string changeHint = predictedGb > currentGb
                ? $"提交上限将升高 {predictedGb - currentGb:F1} GB"
                : predictedGb < currentGb
                    ? $"提交上限将降低 {currentGb - predictedGb:F1} GB"
                    : "提交上限基本不变";

            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "自动分配虚拟内存？",
                Content = $"将按{presetName}设置分页文件为 {minMb} - {maxMb} MB。\n" +
                          $"应用并重启后，提交上限约 {currentGb:F0} GB → {predictedGb:F0} GB({changeHint})。\n是否继续？",
                PrimaryButtonText = "应用",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            // Apply the exact numbers shown in the dialog.
            MinBox.Value = minMb;
            MaxBox.Value = maxMb;
            ViewModel.PagefileMinInput = minMb.ToString();
            ViewModel.PagefileMaxInput = maxMb.ToString();
            ViewModel.ApplyRecommendedPagefileCommand.Execute(null);
            Bindings.Update();
        }

        private void Preset_Changed(object sender, RoutedEventArgs e)
        {
            if (MinBox == null) return; // guard during initial layout
            ViewModel.SetPreset(HardcorePresetRadio.IsChecked == true);
            // Keep the manual input boxes in sync with the chosen preset.
            MinBox.Value = ViewModel.RecommendedMinMb;
            MaxBox.Value = ViewModel.RecommendedMaxMb;
            Bindings.Update();
        }

        private void AutoCleanToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (IntervalBox == null) return; // guard during initial layout
            ViewModel.SetAutoCleanEnabled(AutoCleanToggle.IsOn);
            Bindings.Update();
        }

        private void IntervalBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (AutoCleanToggle == null) return; // guard during initial layout
            if (args.NewValue < 1) return;
            ViewModel.SetAutoCleanInterval((int)Math.Round(args.NewValue));
        }

        private void CleanType_Changed(object sender, RoutedEventArgs e)
        {
            if (DeepCleanRadio == null) return; // guard during initial layout
            ViewModel.SetAutoCleanDeep(DeepCleanRadio.IsChecked == true);
        }

        private void ThresholdSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (AutoCleanToggle == null) return; // guard during initial layout
            ViewModel.SetAutoCleanThreshold((int)Math.Round(e.NewValue));
        }

        // Sync the auto-clean controls to the loaded settings.
        private void SyncAutoCleanControls()
        {
            AutoCleanToggle.IsOn = ViewModel.AutoCleanEnabled;
            IntervalBox.Value = ViewModel.AutoCleanIntervalMinutes;
            LightCleanRadio.IsChecked = !ViewModel.AutoCleanDeep;
            DeepCleanRadio.IsChecked = ViewModel.AutoCleanDeep;
            ThresholdSlider.Value = ViewModel.AutoCleanThreshold;
        }
    }
}
