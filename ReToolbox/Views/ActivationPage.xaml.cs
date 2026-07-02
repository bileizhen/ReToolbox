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
            ActivateButton.IsEnabled = false;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在打开 MAS 汉化版，请在弹出的中文窗口中操作...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await ViewModel.ActivateCommand.ExecuteAsync(null);

            // 用户关闭 MAS 窗口后回到这里，刷新状态以反映激活结果。
            StatusInfoBar.Message = ViewModel.StatusMessage;
            StatusInfoBar.Severity = ViewModel.IsActivated ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            ActivateButton.IsEnabled = true;
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshStatusCommand.Execute(null);
        }
    }
}
