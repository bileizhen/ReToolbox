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
            // Defender removal depends on a remote third-party tool and is disabled for
            // administrator-mode supply-chain safety until a verified pinned artifact is
            // available. Surface a clear warning instead of attempting removal.
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "出于管理员权限与供应链安全，Defender Remover 下载与执行已禁用。请等待提供带固定摘要的受信版本。";
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            await Task.CompletedTask;
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
            // Removal is disabled pending a trusted pinned artifact; keep the button
            // visible-but-disabled so the limitation is discoverable.
            PrimaryActionButton.Content = "移除 Defender（已禁用）";
            PrimaryActionButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
            PrimaryActionButton.IsEnabled = false;
        }
    }
}
