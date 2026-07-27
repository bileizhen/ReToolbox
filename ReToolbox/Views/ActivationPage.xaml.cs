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
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "打开 Windows 激活工具？",
                Content = "将打开 MAS 汉化版窗口。该工具会对 Windows 或 Office 的许可与激活配置进行更改，请确认你有权在此设备上执行这些操作。是否继续？",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ActivateButton.IsEnabled = false;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在打开 MAS 汉化版，请在弹出的中文窗口中操作...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            try
            {
                await ViewModel.ActivateCommand.ExecuteAsync(null);

                StatusInfoBar.Message = ViewModel.StatusMessage;
                StatusInfoBar.Severity = ViewModel.IsActivated ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            }
            catch (Exception ex)
            {
                StatusInfoBar.Message = $"无法打开激活工具：{ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                ActivateButton.IsEnabled = !ViewModel.IsActivating;
            }
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshStatusCommand.Execute(null);
        }
    }
}
