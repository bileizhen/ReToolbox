using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public sealed class DiskCleanupItemViewModel : ObservableObject
    {
        private bool _isSelected;

        public DiskCleanupItemViewModel(DiskCleanupScanItem item)
        {
            Id = item.Id;
            Category = item.Category;
            Name = item.Name;
            Description = item.Description;
            Risk = item.Risk;
            IsRecommended = item.IsRecommended;
            SizeBytes = item.SizeBytes;
            FileCount = item.FileCount;
            _isSelected = item.IsRecommended;
        }

        public string Id { get; }
        public string Category { get; }
        public string Name { get; }
        public string Description { get; }
        public DiskCleanupRisk Risk { get; }
        public bool IsRecommended { get; }
        public long SizeBytes { get; }
        public int FileCount { get; }
        public string SizeText => DiskCleanupPageViewModel.FormatBytes(SizeBytes);
        public string FileCountText => $"{FileCount:N0} 个文件";
        public string RiskText => Risk == DiskCleanupRisk.Safe
            ? "安全缓存"
            : "可重新下载";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public sealed class DiskCleanupPageViewModel : ObservableObject
    {
        private readonly DiskCleanupService _cleanupService;
        private bool _isBusy;
        private string _statusText = "点击扫描以查找可安全清理的缓存和临时内容";
        private string _totalSizeText = "0 B";
        private string _selectedSizeText = "0 B";
        private string _selectedSummaryText = "尚未选择清理项";

        public DiskCleanupPageViewModel(DiskCleanupService cleanupService)
        {
            _cleanupService = cleanupService;
        }

        public ObservableCollection<DiskCleanupItemViewModel> Items { get; } = new();

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanScan));
                    OnPropertyChanged(nameof(CanClean));
                }
            }
        }

        public bool CanScan => !IsBusy;
        public bool HasResults => Items.Count > 0;
        public bool CanClean => !IsBusy && SelectedCount > 0;
        public int SelectedCount => Items.Count(item => item.IsSelected);
        public long SelectedBytes => Items
            .Where(item => item.IsSelected)
            .Sum(item => item.SizeBytes);

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string TotalSizeText
        {
            get => _totalSizeText;
            private set => SetProperty(ref _totalSizeText, value);
        }

        public string SelectedSizeText
        {
            get => _selectedSizeText;
            private set => SetProperty(ref _selectedSizeText, value);
        }

        public string SelectedSummaryText
        {
            get => _selectedSummaryText;
            private set => SetProperty(ref _selectedSummaryText, value);
        }

        public async Task ScanAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            StatusText = "正在扫描系统、浏览器和开发工具缓存...";
            try
            {
                IReadOnlyList<DiskCleanupScanItem> results =
                    await _cleanupService.ScanAsync(cancellationToken);
                ReplaceItems(results);
                StatusText = HasResults
                    ? $"扫描完成：找到 {Items.Count} 类、共 {TotalSizeText} 可清理内容"
                    : "扫描完成：暂未发现可清理内容";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task CleanSelectedAsync(
            CancellationToken cancellationToken = default)
        {
            if (!CanClean)
            {
                return;
            }

            string[] selectedIds = Items
                .Where(item => item.IsSelected)
                .Select(item => item.Id)
                .ToArray();
            IsBusy = true;
            StatusText = $"正在清理 {SelectedCount} 类内容...";
            try
            {
                DiskCleanupRunResult result = await _cleanupService.CleanAsync(
                    selectedIds,
                    cancellationToken);
                IReadOnlyList<DiskCleanupScanItem> remaining =
                    await _cleanupService.ScanAsync(cancellationToken);
                ReplaceItems(remaining);

                string failures = result.FailedFiles > 0
                    ? $"，{result.FailedFiles} 个正在使用或受保护的文件已跳过"
                    : string.Empty;
                StatusText =
                    $"清理完成：删除 {result.DeletedFiles:N0} 个文件，释放 {FormatBytes(result.FreedBytes)}{failures}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SelectRecommended()
        {
            SetSelection(item => item.IsRecommended);
        }

        public void SelectAll()
        {
            SetSelection(_ => true);
        }

        public void ClearSelection()
        {
            SetSelection(_ => false);
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0
                ? $"{value:0} {units[unit]}"
                : $"{value:0.##} {units[unit]}";
        }

        private void ReplaceItems(IReadOnlyList<DiskCleanupScanItem> results)
        {
            foreach (DiskCleanupItemViewModel item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            Items.Clear();
            foreach (DiskCleanupScanItem result in results
                         .OrderBy(item => item.Category)
                         .ThenByDescending(item => item.SizeBytes))
            {
                var item = new DiskCleanupItemViewModel(result);
                item.PropertyChanged += Item_PropertyChanged;
                Items.Add(item);
            }

            TotalSizeText = FormatBytes(Items.Sum(item => item.SizeBytes));
            OnPropertyChanged(nameof(HasResults));
            UpdateSelectionSummary();
        }

        private void Item_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiskCleanupItemViewModel.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void SetSelection(Func<DiskCleanupItemViewModel, bool> selector)
        {
            foreach (DiskCleanupItemViewModel item in Items)
            {
                item.IsSelected = selector(item);
            }

            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            int count = SelectedCount;
            SelectedSizeText = FormatBytes(SelectedBytes);
            SelectedSummaryText = count == 0
                ? "尚未选择清理项"
                : $"已选择 {count} 类，预计释放 {SelectedSizeText}";
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedBytes));
            OnPropertyChanged(nameof(CanClean));
        }
    }
}
