using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Utils;
using ReToolbox.Services;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class SettingsPage : Page
    {
        private CancellationTokenSource? _updateCheckLifetime;

        public SettingsPageViewModel ViewModel { get; }

        public SettingsPage()
        {
            ViewModel = App.Services.GetRequiredService<SettingsPageViewModel>();

            InitializeComponent();
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
            Unloaded += (_, _) =>
            {
                _updateCheckLifetime?.Cancel();
                _updateCheckLifetime?.Dispose();
                _updateCheckLifetime = null;
            };
        }

        private async void CheckForUpdates_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ViewModel.BeginUpdateCheck())
            {
                return;
            }

            CancellationTokenSource lifetime = new CancellationTokenSource();
            _updateCheckLifetime = lifetime;
            try
            {
                AppUpdateCoordinator coordinator =
                    App.Services.GetRequiredService<AppUpdateCoordinator>();
                var progress = new Progress<string>(ViewModel.ReportUpdateStatus);
                string status = await coordinator.RunManualAsync(
                    XamlRoot,
                    () => App.MainWindow?.Close(),
                    progress,
                    lifetime.Token);
                ViewModel.ReportUpdateStatus(status);
            }
            catch (OperationCanceledException)
                when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                ViewModel.ReportUpdateStatus($"检查更新失败：{ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_updateCheckLifetime, lifetime))
                {
                    _updateCheckLifetime = null;
                }

                lifetime.Dispose();
                ViewModel.EndUpdateCheck();
            }
        }
    }
}
