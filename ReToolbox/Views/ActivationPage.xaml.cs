using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Services;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class ActivationPage : Page
    {
        private bool _isActivationFlowOpen;

        public ActivationPageViewModel ViewModel { get; }

        public ActivationPage()
        {
            ViewModel = App.Services.GetService<ActivationPageViewModel>()
                ?? new ActivationPageViewModel(App.Services.GetService<ActivationService>()!);

            InitializeComponent();
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            if (_isActivationFlowOpen)
            {
                return;
            }

            _isActivationFlowOpen = true;
            ActivateButton.IsEnabled = false;

            try
            {
                ActivationScriptRelease release = ActivationWorkflow.CurrentRelease;
                ContentDialog confirmation = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "确认激活 Windows？",
                    Content = $"ReToolbox 将运行固定并校验过的 MAS {release.Tag}，使用 HWID 激活当前 Windows Edition，不会自动更改系统版本。\n\n该操作会修改 Windows 许可状态。请仅在你有权激活此设备时继续。",
                    PrimaryButtonText = "继续激活",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close
                };

                if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                StatusInfoBar.IsOpen = true;
                StatusInfoBar.Message = $"正在准备 MAS {release.Tag}...";
                StatusInfoBar.Severity = InfoBarSeverity.Informational;

                await ViewModel.ActivateCommand.ExecuteAsync(null);
                StatusInfoBar.Message = ViewModel.StatusMessage;
                StatusInfoBar.Severity = ViewModel.LastActivationOutcome switch
                {
                    ActivationOutcome.Activated => InfoBarSeverity.Success,
                    ActivationOutcome.AwaitingVerification => InfoBarSeverity.Informational,
                    ActivationOutcome.Failed => InfoBarSeverity.Error,
                    _ => InfoBarSeverity.Warning
                };
            }
            catch (Exception ex)
            {
                StatusInfoBar.IsOpen = true;
                StatusInfoBar.Message = $"激活失败：{ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                _isActivationFlowOpen = false;
                ActivateButton.IsEnabled = !ViewModel.IsActivating;
            }
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshStatusCommand.Execute(null);
        }
    }
}
