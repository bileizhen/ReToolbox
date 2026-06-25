using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Services;
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
        }

        private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsEdgeInstalled)
            {
                await RunUninstallAsync();
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

            await ViewModel.UninstallEdgeCommand.ExecuteAsync(null);

            UninstallProgress.Visibility = Visibility.Collapsed;
            StatusInfoBar.Message = ViewModel.StatusMessage;
            StatusInfoBar.Severity = ViewModel.IsEdgeInstalled ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            UpdatePrimaryActionButton();
        }

        private async Task RunInstallAsync()
        {
            PrimaryActionButton.IsEnabled = false;
            UninstallProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在安装 Microsoft Edge...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await ViewModel.InstallEdgeCommand.ExecuteAsync(null);

            UninstallProgress.Visibility = Visibility.Collapsed;
            StatusInfoBar.Message = ViewModel.StatusMessage;
            StatusInfoBar.Severity = ViewModel.IsEdgeInstalled ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            UpdatePrimaryActionButton();
        }

        private async void Cleanup_Click(object sender, RoutedEventArgs e)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在清理...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await ViewModel.CleanupCommand.ExecuteAsync(null);

            StatusInfoBar.Message = ViewModel.StatusMessage;
            StatusInfoBar.Severity = InfoBarSeverity.Success;
        }

        private void UpdatePrimaryActionButton()
        {
            PrimaryActionButton.Content = ViewModel.IsEdgeInstalled ? "卸载 Edge" : "安装 Edge";
            PrimaryActionButton.Style = ViewModel.IsEdgeInstalled
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : (Style)Application.Current.Resources["DefaultButtonStyle"];
            PrimaryActionButton.IsEnabled = true;
        }
    }
}
