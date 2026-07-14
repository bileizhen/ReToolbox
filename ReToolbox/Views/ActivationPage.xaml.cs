using System.Threading.Tasks;
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

        // Activation via remote MAS scripts is disabled for administrator-mode supply-chain
        // safety. The action button stays disabled; we only surface a clear status message.
        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "出于管理员权限与供应链安全，远程激活脚本执行已禁用。请改用官方渠道获取工具。";
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            await Task.CompletedTask;
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshStatusCommand.Execute(null);
        }
    }
}
