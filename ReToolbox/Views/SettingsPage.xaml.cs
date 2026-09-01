using System;
using System.Diagnostics;
using System.IO;
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
        private readonly DiagnosticLogService _diagnosticLog;

        public SettingsPageViewModel ViewModel { get; }
        public string DiagnosticLogDirectory => _diagnosticLog.LogDirectory;

        public SettingsPage()
        {
            ViewModel = App.Services.GetRequiredService<SettingsPageViewModel>();
            _diagnosticLog =
                App.Services.GetRequiredService<DiagnosticLogService>();

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
                _diagnosticLog.WriteError(
                    "Updater",
                    "设置页手动检查更新失败",
                    ex);
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

        private void OpenDiagnosticLogs_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_diagnosticLog.LogDirectory);
                OpenInExplorer(_diagnosticLog.LogDirectory);
                _diagnosticLog.WriteInformation(
                    "Diagnostics",
                    "用户打开了诊断日志目录");
                ShowDiagnosticStatus(
                    InfoBarSeverity.Success,
                    "已打开日志目录",
                    _diagnosticLog.LogDirectory);
            }
            catch (Exception ex)
            {
                _diagnosticLog.WriteError(
                    "Diagnostics",
                    "无法打开诊断日志目录",
                    ex);
                ShowDiagnosticStatus(
                    InfoBarSeverity.Error,
                    "无法打开日志目录",
                    ex.Message);
            }
        }

        private async void ExportDiagnostics_Click(
            object sender,
            RoutedEventArgs e)
        {
            ExportDiagnosticsButton.IsEnabled = false;
            try
            {
                string desktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
                string exportDirectory = string.IsNullOrWhiteSpace(desktop)
                    ? _diagnosticLog.LogDirectory
                    : desktop;
                string archivePath = Path.Combine(
                    exportDirectory,
                    $"ReToolbox-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
                await _diagnosticLog.CreateFeedbackArchiveAsync(archivePath);
                _diagnosticLog.WriteInformation(
                    "Diagnostics",
                    $"诊断包已导出：{archivePath}");
                SelectInExplorer(archivePath);
                ShowDiagnosticStatus(
                    InfoBarSeverity.Success,
                    "诊断包已导出到桌面",
                    archivePath);
            }
            catch (Exception ex)
            {
                _diagnosticLog.WriteError(
                    "Diagnostics",
                    "导出诊断包失败",
                    ex);
                ShowDiagnosticStatus(
                    InfoBarSeverity.Error,
                    "导出诊断包失败",
                    ex.Message);
            }
            finally
            {
                ExportDiagnosticsButton.IsEnabled = true;
            }
        }

        private static void OpenInExplorer(string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
        }

        private static void SelectInExplorer(string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add($"/select,{path}");
            Process.Start(startInfo);
        }

        private void ShowDiagnosticStatus(
            InfoBarSeverity severity,
            string title,
            string message)
        {
            DiagnosticStatusInfoBar.Severity = severity;
            DiagnosticStatusInfoBar.Title = title;
            DiagnosticStatusInfoBar.Message = message;
            DiagnosticStatusInfoBar.IsOpen = true;
        }
    }
}
