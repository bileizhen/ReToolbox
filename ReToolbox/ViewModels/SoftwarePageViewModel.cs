using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Models;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class SoftwarePageViewModel : ObservableObject
    {
        private readonly SoftwareInstallService _installService;
        private CancellationTokenSource? _installCts;

        public ObservableCollection<SoftwareItem> SoftwareItems { get; }

        public IReadOnlyList<string> Categories { get; }

        // Lines streamed into the install progress dialog.
        public ObservableCollection<string> InstallLogs { get; } = new();

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private string _currentItemText = string.Empty;

        [ObservableProperty]
        private int _overallProgress;

        [ObservableProperty]
        private int _downloadProgress;

        [ObservableProperty]
        private bool _isDownloading;

        public string BatchResultTitle { get; private set; } = "安装完成";

        public string SearchText { get; set; } = string.Empty;

        public string SelectedCategory { get; set; } = "全部分类";

        public int SelectedCount => SoftwareItems.Count(item => item.IsSelected);

        public bool HasSelection => SelectedCount > 0;

        public int FailedCount => SoftwareItems.Count(item => item.InstallStatus == "安装失败");

        public bool HasFailedItems => FailedCount > 0;

        public string InstallButtonText => SelectedCount == 0 ? "选择要安装的软件" : $"安装所选 ({SelectedCount})";

        public SoftwarePageViewModel(SoftwareInstallService installService)
        {
            _installService = installService;
            SoftwareItems = new ObservableCollection<SoftwareItem>(_installService.GetSoftwareList());
            Categories = new[] { "全部分类" }
                .Concat(SoftwareItems.Select(item => item.Category).Distinct())
                .ToArray();
        }

        public List<SoftwareItem> GetSoftwareByCategory(string category)
        {
            return SoftwareItems.Where(s => s.Category == category).ToList();
        }

        public HashSet<string> GetCategories()
        {
            return SoftwareItems.Select(s => s.Category).ToHashSet();
        }

        public IReadOnlyList<SoftwareItem> GetVisibleSoftware()
        {
            IEnumerable<SoftwareItem> query = SoftwareItems;

            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "全部分类")
            {
                query = query.Where(item => item.Category == SelectedCategory);
            }

            string search = SearchText.Trim();
            if (search.Length > 0)
            {
                query = query.Where(item =>
                    item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Category.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    item.DistributionLabel.Contains(search, StringComparison.CurrentCultureIgnoreCase));
            }

            return query.ToArray();
        }

        public void SelectVisibleSoftware()
        {
            if (IsInstalling)
            {
                return;
            }

            foreach (SoftwareItem item in GetVisibleSoftware())
            {
                item.IsSelected = true;
            }
        }

        public void SelectFailedSoftware()
        {
            if (IsInstalling)
            {
                return;
            }

            foreach (SoftwareItem item in SoftwareItems)
            {
                item.IsSelected = item.InstallStatus == "安装失败";
            }
        }

        [RelayCommand]
        private async Task InstallSelectedAsync()
        {
            var selected = SoftwareItems.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            IsInstalling = true;
            _installCts?.Dispose();
            _installCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _installCts.Token;
            InstallLogs.Clear();
            OverallProgress = 0;
            DownloadProgress = 0;
            IsDownloading = false;
            BatchResultTitle = "正在安装软件";

            int completed = 0;
            int installed = 0;
            int manualActionRequired = 0;
            int failed = 0;
            int total = selected.Count;
            SoftwareItem? activeItem = null;

            try
            {
                foreach (var item in selected)
                {
                    activeItem = item;
                    cancellationToken.ThrowIfCancellationRequested();
                    CurrentItemText = $"{item.Name}  ({completed + 1}/{total})";
                    item.InstallStatus = "正在处理";
                    DownloadProgress = 0;
                    IsDownloading = !string.IsNullOrWhiteSpace(item.WingetId) ||
                                    item.GitHubRelease is not null;

                    var log = new Progress<LogEntry>(entry =>
                    {
                        string line = $"[{DateTime.Now:HH:mm:ss}] {entry.Text}";
                        if (entry.Kind == LogEntryKind.Progress && InstallLogs.Count > 0)
                        {
                            InstallLogs[InstallLogs.Count - 1] = line;
                        }
                        else
                        {
                            InstallLogs.Add(line);
                        }
                    });

                    var download = new Progress<int>(percent =>
                    {
                        DownloadProgress = Math.Clamp(percent, 0, 100);
                        IsDownloading = true;
                    });

                    SoftwareInstallResult result = await _installService.InstallSoftwareAsync(
                        item,
                        log,
                        download,
                        cancellationToken);
                    switch (result.Outcome)
                    {
                        case SoftwareInstallOutcome.Installed:
                            installed++;
                            item.IsSelected = false;
                            break;
                        case SoftwareInstallOutcome.ManualActionRequired:
                            manualActionRequired++;
                            item.IsSelected = false;
                            break;
                        case SoftwareInstallOutcome.InstallerStarted:
                            manualActionRequired++;
                            item.IsSelected = false;
                            break;
                        default:
                            failed++;
                            break;
                    }

                    completed++;
                    OverallProgress = completed * 100 / total;
                    IsDownloading = false;
                    activeItem = null;
                }

                CurrentItemText = $"处理完成：已安装 {installed} 项，需手动操作 {manualActionRequired} 项，失败 {failed} 项";
                BatchResultTitle = failed > 0
                    ? "部分软件安装失败"
                    : manualActionRequired > 0
                        ? "请继续完成手动安装"
                        : "安装完成";
            }
            catch (OperationCanceledException)
            {
                if (activeItem?.InstallStatus == "正在处理")
                {
                    activeItem.InstallStatus = "已取消";
                }
                InstallLogs.Add($"[{DateTime.Now:HH:mm:ss}] 安装已取消");
                CurrentItemText = "安装已取消";
                BatchResultTitle = "安装已取消";
            }
            catch (Exception ex)
            {
                if (activeItem?.InstallStatus == "正在处理")
                {
                    activeItem.InstallStatus = "安装失败";
                }
                string message = $"安装流程异常：{ex.Message}";
                InstallLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                CurrentItemText = message;
                BatchResultTitle = "安装出错";
            }
            finally
            {
                IsDownloading = false;
                IsInstalling = false;
                _installCts?.Dispose();
                _installCts = null;
            }
        }

        [RelayCommand]
        private void CancelInstall()
        {
            _installCts?.Cancel();
        }

        [RelayCommand]
        private void SelectAll()
        {
            SelectVisibleSoftware();
        }

        [RelayCommand]
        private void DeselectAll()
        {
            if (IsInstalling)
            {
                return;
            }

            foreach (var item in SoftwareItems)
                item.IsSelected = false;
        }
    }
}
