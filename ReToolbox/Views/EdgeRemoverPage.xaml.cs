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
                ContentDialog confirmation = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "确认卸载 Microsoft Edge",
                    Content = "此操作会运行经过固定版本与 SHA-256 校验的 EdgeRemover 管理员脚本。卸载 Edge 可能影响依赖它的系统功能，是否继续？",
                    PrimaryButtonText = "继续卸载",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close
                };
                if (await confirmation.ShowAsync() == ContentDialogResult.Primary)
                {
                    await RunUninstallAsync();
                }
            }
            else
            {
                await RunInstallAsync();
            }
        }

        private async Task RunUninstallAsync()
        {
            PrimaryActionButton.IsEnabled = false;
            UninstallProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在卸载 Microsoft Edge...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            try
            {
                await ViewModel.UninstallEdgeCommand.ExecuteAsync(null);
                StatusInfoBar.Message = ViewModel.StatusMessage;
                StatusInfoBar.Severity = ViewModel.IsEdgeInstalled
                    ? InfoBarSeverity.Error
                    : InfoBarSeverity.Success;
            }
            catch (Exception ex)
            {
                StatusInfoBar.Message = $"Microsoft Edge 卸载失败：{ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                UninstallProgress.Visibility = Visibility.Collapsed;
                UpdatePrimaryActionButton();
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
            if (ViewModel.IsEdgeInstalled)
            {
                PrimaryActionButton.Content = "卸载 Edge";
                PrimaryActionButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                PrimaryActionButton.IsEnabled = !ViewModel.IsUninstalling;
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
