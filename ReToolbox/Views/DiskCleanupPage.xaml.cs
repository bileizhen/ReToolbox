using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class DiskCleanupPage : Page
    {
        private CancellationTokenSource? _pageLifetime;
        private bool _initialScanStarted;

        public DiskCleanupPageViewModel ViewModel { get; }

        public DiskCleanupPage()
        {
            ViewModel = App.Services.GetRequiredService<DiskCleanupPageViewModel>();
            InitializeComponent();
            Loaded += DiskCleanupPage_Loaded;
            Unloaded += (_, _) => CancelPageOperations();
        }

        private async void DiskCleanupPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            PageAnimations.StaggerIn(this);
            if (_initialScanStarted)
            {
                return;
            }

            _initialScanStarted = true;
            await RunPageOperationAsync(ViewModel.ScanAsync);
        }

        private async void Scan_Click(object sender, RoutedEventArgs e)
        {
            await RunPageOperationAsync(ViewModel.ScanAsync);
        }

        private async void Clean_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanClean)
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "确认深度清理",
                Content =
                    $"{ViewModel.SelectedSummaryText}\n\n清理会永久删除选中的缓存和临时文件，无法从回收站恢复。正在使用、受保护或超出安全目录的内容会自动跳过。",
                PrimaryButtonText = "确认清理",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await RunPageOperationAsync(ViewModel.CleanSelectedAsync);
        }

        private void Recommended_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectRecommended();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectAll();
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearSelection();
        }

        private async Task RunPageOperationAsync(
            Func<CancellationToken, Task> operation)
        {
            if (ViewModel.IsBusy)
            {
                return;
            }

            _pageLifetime?.Dispose();
            var lifetime = new CancellationTokenSource();
            _pageLifetime = lifetime;
            try
            {
                await operation(lifetime.Token);
            }
            catch (OperationCanceledException)
                when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
            finally
            {
                if (ReferenceEquals(_pageLifetime, lifetime))
                {
                    _pageLifetime = null;
                }

                lifetime.Dispose();
            }
        }

        private async Task ShowErrorAsync(string message)
        {
            if (XamlRoot is null)
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "磁盘清理失败",
                Content = message,
                CloseButtonText = "关闭"
            };
            await dialog.ShowAsync();
        }

        private void CancelPageOperations()
        {
            _pageLifetime?.Cancel();
            _pageLifetime?.Dispose();
            _pageLifetime = null;
        }
    }
}
