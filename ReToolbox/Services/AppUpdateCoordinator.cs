using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ReToolbox.Services
{
    public sealed class AppUpdateCoordinator
    {
        private readonly AppUpdateService _updateService;
        private readonly DiagnosticLogService _diagnosticLog;
        private readonly SemaphoreSlim _runGate = new SemaphoreSlim(1, 1);

        public AppUpdateCoordinator(
            AppUpdateService updateService,
            DiagnosticLogService diagnosticLog)
        {
            _updateService = updateService;
            _diagnosticLog = diagnosticLog;
        }

        public async Task RunStartupAsync(
            XamlRoot xamlRoot,
            Action closeWindow,
            CancellationToken cancellationToken = default)
        {
            if (!_updateService.CheckOnStartupEnabled)
            {
                return;
            }

            await CheckAndHandleAsync(
                xamlRoot,
                closeWindow,
                progress: null,
                showCheckFailure: false,
                cancellationToken).ConfigureAwait(true);
        }

        public Task<string> RunManualAsync(
            XamlRoot xamlRoot,
            Action closeWindow,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return CheckAndHandleAsync(
                xamlRoot,
                closeWindow,
                progress,
                showCheckFailure: true,
                cancellationToken);
        }

        private async Task<string> CheckAndHandleAsync(
            XamlRoot xamlRoot,
            Action closeWindow,
            IProgress<string>? progress,
            bool showCheckFailure,
            CancellationToken cancellationToken)
        {
            await _runGate.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                return await CheckAndHandleCoreAsync(
                    xamlRoot,
                    closeWindow,
                    progress,
                    showCheckFailure,
                    cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                _runGate.Release();
            }
        }

        private async Task<string> CheckAndHandleCoreAsync(
            XamlRoot xamlRoot,
            Action closeWindow,
            IProgress<string>? progress,
            bool showCheckFailure,
            CancellationToken cancellationToken)
        {
            _diagnosticLog.WriteInformation(
                DiagnosticLogSource.Updater,
                showCheckFailure
                    ? "开始手动检查更新"
                    : "开始启动时检查更新");
            progress?.Report("正在检查更新...");
            UpdateCheckResult check = await _updateService.CheckForUpdatesAsync(
                cancellationToken).ConfigureAwait(true);
            progress?.Report(check.Message);
            _diagnosticLog.WriteInformation(
                DiagnosticLogSource.Updater,
                check.Message);

            if (check.State == UpdateCheckState.Failed)
            {
                if (showCheckFailure)
                {
                    await ShowErrorAsync(xamlRoot, check.Message).ConfigureAwait(true);
                }

                return check.Message;
            }

            if (check.State != UpdateCheckState.UpdateAvailable ||
                check.Release is null)
            {
                return check.Message;
            }

            if (!_updateService.AutomaticDownloadEnabled)
            {
                await ShowAvailableUpdateAsync(
                    xamlRoot,
                    check.Release).ConfigureAwait(true);
                return check.Message;
            }

            DownloadedUpdate? downloaded = null;
            bool installerStarted = false;
            try
            {
                try
                {
                    downloaded = await _updateService.DownloadUpdateAsync(
                        check.Release,
                        progress,
                        cancellationToken).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    string detail = $"自动下载失败：{ex.Message}";
                    _diagnosticLog.WriteError(
                        DiagnosticLogSource.Updater,
                        "自动下载更新失败",
                        ex);
                    progress?.Report(detail);
                    await ShowAvailableUpdateAsync(
                        xamlRoot,
                        check.Release,
                        detail).ConfigureAwait(true);
                    return detail;
                }

                cancellationToken.ThrowIfCancellationRequested();
                ContentDialog installDialog = new ContentDialog
                {
                    XamlRoot = xamlRoot,
                    Title = $"{downloaded.Release.TagName} 已准备就绪",
                    Content = "更新已下载并通过文件大小与 SHA-256 校验。是否关闭 ReToolbox 并启动安装程序？",
                    PrimaryButtonText = "立即安装",
                    CloseButtonText = "稍后",
                    DefaultButton = ContentDialogButton.Primary
                };
                if (await installDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return "更新已取消安装，下载缓存已清理";
                }

                try
                {
                    await _updateService.LaunchInstallerAsync(
                        downloaded,
                        cancellationToken).ConfigureAwait(true);
                    installerStarted = true;
                    _diagnosticLog.WriteInformation(
                        DiagnosticLogSource.Updater,
                        $"已启动 {downloaded.Release.TagName} 安装程序");
                    closeWindow();
                    return $"已启动 {downloaded.Release.TagName} 安装程序";
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    string message = $"无法启动更新安装器：{ex.Message}";
                    _diagnosticLog.WriteError(
                        DiagnosticLogSource.Updater,
                        "无法启动更新安装器",
                        ex);
                    await ShowErrorAsync(xamlRoot, message).ConfigureAwait(true);
                    return message;
                }
            }
            finally
            {
                if (downloaded is not null && !installerStarted)
                {
                    _updateService.DiscardDownloadedUpdate(downloaded);
                }
            }
        }

        private async Task ShowAvailableUpdateAsync(
            XamlRoot xamlRoot,
            UpdateRelease release,
            string? detail = null)
        {
            string message = $"发现新版本 {release.TagName}。";
            if (!string.IsNullOrWhiteSpace(detail))
            {
                message += $"\n\n{detail}";
            }

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "发现 ReToolbox 更新",
                Content = message,
                PrimaryButtonText = "查看发布页",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                _updateService.OpenReleasePage(release);
            }
        }

        private static async Task ShowErrorAsync(
            XamlRoot xamlRoot,
            string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "更新失败",
                Content = message,
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close
            };
            await dialog.ShowAsync();
        }
    }
}
