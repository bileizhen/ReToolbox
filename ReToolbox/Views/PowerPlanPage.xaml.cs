using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ReToolbox.Models;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class PowerPlanPage : Page
    {
        public PowerPlanPageViewModel ViewModel { get; }

        public PowerPlanPage()
        {
            ViewModel = App.Services.GetService<PowerPlanPageViewModel>()
                ?? throw new InvalidOperationException("PowerPlanPageViewModel not registered");

            ViewModel.StatusReported += OnStatusReported;

            InitializeComponent();
            Loaded += PowerPlanPage_Loaded;
        }

        private void PowerPlanPage_Loaded(object sender, RoutedEventArgs e)
        {
            PageAnimations.StaggerIn(this);
            ViewModel.Refresh();
            SyncAutoSwitchControls();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.Refresh();
            SyncAutoSwitchControls();
        }

        // Marshal ViewModel status reports into the bottom-right InfoBar.
        private void OnStatusReported(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Refresh();
            SyncAutoSwitchControls();
        }

        // Resolve the clicked plan from its GUID and switch to it after confirming.
        private async void ApplyPlan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string guid) return;

            PowerPlan? target = null;
            foreach (var plan in ViewModel.PowerPlans)
            {
                if (plan.Guid == guid) { target = plan; break; }
            }
            if (target == null) return;

            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "切换电源计划？",
                Content = $"将切换到电源计划「{target.Name}」。是否继续？",
                PrimaryButtonText = "切换",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            BusyProgress.Visibility = Visibility.Visible;
            await ViewModel.SetActiveCommand.ExecuteAsync(target);
            BusyProgress.Visibility = Visibility.Collapsed;
            Bindings.Update();
        }

        // Resolve the clicked plan from its GUID and delete it after a clear
        // confirmation. Deletion is irreversible, so the destructive button is
        // not the dialog default.
        private async void DeletePlan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string guid) return;

            PowerPlan? target = null;
            foreach (var plan in ViewModel.PowerPlans)
            {
                if (plan.Guid == guid) { target = plan; break; }
            }
            if (target == null) return;

            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "删除电源计划？",
                Content = $"将永久删除电源计划「{target.Name}」，此操作不可撤销。是否继续？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            BusyProgress.Visibility = Visibility.Visible;
            await ViewModel.DeleteCommand.ExecuteAsync(target);
            BusyProgress.Visibility = Visibility.Collapsed;
            Bindings.Update();
        }

        private async void AddUltimate_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "添加卓越性能计划？",
                Content = "将解锁隐藏的「卓越性能」电源计划，适用于工作站极致性能场景。是否继续？",
                PrimaryButtonText = "添加",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            BusyProgress.Visibility = Visibility.Visible;
            await ViewModel.AddUltimatePerformanceCommand.ExecuteAsync(null);
            BusyProgress.Visibility = Visibility.Collapsed;

            // Reflect whether Ultimate Performance now exists on the button label.
            AddUltimateButton.Content = ViewModel.UltimatePerformancePresent ? "已添加" : "一键添加";
            AddUltimateButton.IsEnabled = ViewModel.IsAdmin && !ViewModel.UltimatePerformancePresent;
            Bindings.Update();
        }

        private void AutoSwitchToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (AutoSwitchPlanCombo == null) return; // guard during initial layout

            string guid = AutoSwitchPlanCombo.SelectedValue as string ?? ViewModel.AutoSwitchPlanGuid;
            ViewModel.ApplyAutoSwitch(AutoSwitchToggle.IsOn, guid);

            // The target picker only matters while enabled.
            AutoSwitchPlanCombo.IsEnabled = AutoSwitchToggle.IsOn;
            Bindings.Update();
        }

        private void AutoSwitchPlan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoSwitchToggle == null) return; // guard during initial layout
            if (e.AddedItems.Count == 0) return;

            if (AutoSwitchPlanCombo.SelectedValue is string guid)
            {
                // Only persist when the feature is actually on.
                if (AutoSwitchToggle.IsOn)
                {
                    ViewModel.ApplyAutoSwitch(true, guid);
                }
                ViewModel.AutoSwitchPlanGuid = guid;
                Bindings.Update();
            }
        }

        // Sync the toggle/combo to the persisted preference after a refresh.
        private void SyncAutoSwitchControls()
        {
            AutoSwitchToggle.IsOn = ViewModel.AutoSwitchEnabled;
            AutoSwitchPlanCombo.IsEnabled = ViewModel.AutoSwitchEnabled;

            if (!string.IsNullOrEmpty(ViewModel.AutoSwitchPlanGuid))
            {
                AutoSwitchPlanCombo.SelectedValue = ViewModel.AutoSwitchPlanGuid;
            }

            AddUltimateButton.Content = ViewModel.UltimatePerformancePresent ? "已添加" : "一键添加";
            AddUltimateButton.IsEnabled = ViewModel.IsAdmin && !ViewModel.UltimatePerformancePresent;
        }
    }
}
