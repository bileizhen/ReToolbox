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
    public sealed partial class EdgeRemoverPage : Page
    {
        public EdgeRemoverPageViewModel ViewModel { get; }

        public EdgeRemoverPage()
        {
            ViewModel = App.Services.GetService<EdgeRemoverPageViewModel>()
                ?? new EdgeRemoverPageViewModel(App.Services.GetService<EdgeRemoverService>()!);

            InitializeComponent();
            UpdatePrimaryActionButton();
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }

        private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsEdgeInstalled)
            {
                // Uninstall depends on a remote third-party script and is disabled for
                // administrator-mode supply-chain safety until a verified pinned artifact
                // is available. Surface a clear warning instead of a broken action.
                StatusInfoBar.IsOpen = true;
                StatusInfoBar.Message = "出于管理员权限与供应链安全，第三方 EdgeRemover 卸载已禁用。请等待提供带固定摘要的受信版本。";
                StatusInfoBar.Severity = InfoBarSeverity.Warning;
                await Task.CompletedTask;
            }
            else
            {
                await RunInstallAsync();
            }
        }

        private async Task RunInstallAsync()
        {
            PrimaryActionButton.IsEnabled = false;
            UninstallProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在安装 Microsoft Edge...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            try
            {
                await ViewModel.InstallEdgeCommand.ExecuteAsync(null);
                StatusInfoBar.Message = ViewModel.StatusMessage;
                StatusInfoBar.Severity = ViewModel.IsEdgeInstalled ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            }
            catch (Exception ex)
            {
                StatusInfoBar.Message = $"Microsoft Edge 安装失败：{ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                UninstallProgress.Visibility = Visibility.Collapsed;
                UpdatePrimaryActionButton();
            }
        }

        private async void Cleanup_Click(object sender, RoutedEventArgs e)
        {
            Button? actionButton = sender as Button;
            if (actionButton is not null)
            {
                actionButton.IsEnabled = false;
            }

            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在清理...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            try
            {
                await ViewModel.CleanupCommand.ExecuteAsync(null);
                StatusInfoBar.Message = ViewModel.StatusMessage;
                StatusInfoBar.Severity = ViewModel.StatusMessage.Contains("完成", StringComparison.OrdinalIgnoreCase)
                    ? InfoBarSeverity.Success
                    : InfoBarSeverity.Warning;
            }
            catch (Exception ex)
            {
                StatusInfoBar.Message = $"清理失败：{ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                if (actionButton is not null)
                {
                    actionButton.IsEnabled = true;
                }
            }
        }

        private void UpdatePrimaryActionButton()
        {
            // Edge uninstall depends on a remote third-party script and is disabled until
            // a verified pinned artifact is available. When Edge is installed we keep the
            // button visible but disabled so the limitation is discoverable; when Edge is
            // absent the install (winget) path remains available.
            if (ViewModel.IsEdgeInstalled)
            {
                PrimaryActionButton.Content = "卸载 Edge（已禁用）";
                PrimaryActionButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                PrimaryActionButton.IsEnabled = false;
            }
            else
            {
                PrimaryActionButton.Content = "安装 Edge";
                PrimaryActionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                PrimaryActionButton.IsEnabled = true;
            }
        }
    }
}
