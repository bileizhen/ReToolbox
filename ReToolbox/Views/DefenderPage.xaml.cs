using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Services;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class DefenderPage : Page
    {
        public DefenderPageViewModel ViewModel { get; }

        public DefenderPage()
        {
            ViewModel = App.Services.GetService<DefenderPageViewModel>()
                ?? new DefenderPageViewModel(App.Services.GetService<DefenderService>()!);

            InitializeComponent();
            UpdatePrimaryActionButton();
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }

        private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
        {
            // Defender removal is irreversible and forces a reboot, so confirm first.
            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "移除 Windows Defender？",
                Content = "此操作将强制移除或禁用 Windows Defender（含杀毒引擎、SmartScreen、安全中心等），" +
                          "操作不可逆，且完成后会自动重启电脑。建议先创建系统还原点。是否继续？",
                PrimaryButtonText = "移除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await RunRemoveAsync();
        }

        private async Task RunRemoveAsync()
        {
            PrimaryActionButton.IsEnabled = false;
            RemoveProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在移除 Windows Defender...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await ViewModel.RemoveDefenderCommand.ExecuteAsync(null);

            RemoveProgress.Visibility = Visibility.Collapsed;
            StatusInfoBar.Message = ViewModel.StatusMessage;
            StatusInfoBar.Severity = !ViewModel.IsDefenderActive
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Error;
            UpdatePrimaryActionButton();
        }

        private async void OpenSecurity_Click(object sender, RoutedEventArgs e)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在打开 Windows 安全中心...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await Task.Run(() => App.Services.GetService<DefenderService>()!.OpenWindowsSecurity());

            StatusInfoBar.Message = "已打开 Windows 安全中心";
            StatusInfoBar.Severity = InfoBarSeverity.Success;
        }

        private void UpdatePrimaryActionButton()
        {
            // Only removal is supported (no upstream enable path); keep the action fixed.
            PrimaryActionButton.Content = "移除 Defender";
            PrimaryActionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            PrimaryActionButton.IsEnabled = true;
        }
    }
}
