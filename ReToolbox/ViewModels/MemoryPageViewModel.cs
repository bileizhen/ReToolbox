using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;
using ReToolbox.Utils;

namespace ReToolbox.ViewModels
{
    public partial class MemoryPageViewModel : ObservableObject
    {
        private readonly MemoryService _memoryService;
        private readonly VirtualMemoryService _virtualMemoryService;
        private readonly MemoryAutoCleanService _autoCleanService;

        // Dashboard
        [ObservableProperty]
        private double _physicalTotalGb;

        [ObservableProperty]
        private double _physicalAvailableGb;

        [ObservableProperty]
        private double _physicalLoadPercent;

        [ObservableProperty]
        private double _virtualTotalGb;

        [ObservableProperty]
        private double _virtualAvailableGb;

        [ObservableProperty]
        private double _virtualLoadPercent;

        [ObservableProperty]
        private string _physicalSummary = string.Empty;

        // Commit limit summary: shows the current commit ceiling and how much
        // is committed, expressed plainly so it's clear what the pagefile size
        // actually controls.
        [ObservableProperty]
        private string _virtualSummary = string.Empty;

        [ObservableProperty]
        private string _commitSummary = string.Empty;

        // What the commit limit would become if the recommended preset were
        // applied — shown next to the preset so the user sees the trade-off.
        [ObservableProperty]
        private string _predictedCommitText = string.Empty;

        // Admin
        [ObservableProperty]
        private bool _isAdmin;

        [ObservableProperty]
        private string _adminHint = string.Empty;

        // Clean results
        [ObservableProperty]
        private bool _isCleaning;

        [ObservableProperty]
        private string _lastCleanText = "尚未清理";

        // Virtual memory
        [ObservableProperty]
        private string _pagefileStatus = string.Empty;

        [ObservableProperty]
        private string _pagefileRecommendation = string.Empty;

        // Preset selector: false = 普通方案(求稳省心), true = 高级方案(极致防卡顿)
        [ObservableProperty]
        private bool _useHardcorePreset;

        [ObservableProperty]
        private int _recommendedMinMb;

        [ObservableProperty]
        private int _recommendedMaxMb;

        [ObservableProperty]
        private string _pagefileMinInput = string.Empty;

        [ObservableProperty]
        private string _pagefileMaxInput = string.Empty;

        [ObservableProperty]
        private string _pagefileResultText = string.Empty;

        // Auto clean
        [ObservableProperty]
        private bool _autoCleanEnabled;

        [ObservableProperty]
        private int _autoCleanIntervalMinutes = 10;

        // True = deep clean on the schedule, False = light clean.
        [ObservableProperty]
        private bool _autoCleanDeep;

        // Physical memory load percent above which a clean is triggered.
        // 0 = always clean.
        [ObservableProperty]
        private int _autoCleanThreshold = 80;

        [ObservableProperty]
        private string _autoCleanStatus = string.Empty;

        public ObservableCollection<ProcessMemoryEntry> TopProcesses { get; } = new();

        // Raised when a background auto-clean tick fires so the page can update
        // the status bar.
        public event Action<string>? StatusReported;

        public MemoryPageViewModel(MemoryService memoryService, VirtualMemoryService virtualMemoryService, MemoryAutoCleanService autoCleanService)
        {
            _memoryService = memoryService;
            _virtualMemoryService = virtualMemoryService;
            _autoCleanService = autoCleanService;
            LoadAutoCleanSettings();
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
        {
            var snap = _memoryService.GetMemorySnapshot();
            PhysicalTotalGb = snap.TotalPhysicalGb;
            PhysicalAvailableGb = snap.AvailablePhysicalGb;
            PhysicalLoadPercent = snap.PhysicalLoadPercent;
            VirtualTotalGb = snap.TotalVirtualGb;
            VirtualAvailableGb = snap.AvailableVirtualGb;
            VirtualLoadPercent = snap.VirtualLoadPercent;

            PhysicalSummary = $"{PhysicalAvailableGb:F1} GB 可用 / {PhysicalTotalGb:F1} GB";
            VirtualSummary = $"{VirtualAvailableGb:F1} GB 可用 / {VirtualTotalGb:F1} GB";
            double usedCommitGb = VirtualTotalGb - VirtualAvailableGb;
            CommitSummary = $"提交:已用 {usedCommitGb:F1} GB / 上限 {VirtualTotalGb:F1} GB(物理 {PhysicalTotalGb:F0} GB + 分页文件)";

            IsAdmin = _memoryService.IsRunningAsAdmin();
            AdminHint = IsAdmin ? "已以管理员身份运行" : "未以管理员身份运行，部分功能不可用";

            TopProcesses.Clear();
            foreach (var entry in _memoryService.GetProcessMemoryRanking(10))
            {
                TopProcesses.Add(entry);
            }

            RefreshPagefile();
        }

        private void RefreshPagefile()
        {
            var status = _virtualMemoryService.GetStatus();
            PagefileStatus = status.Summary;

            var (min, max) = UseHardcorePreset
                ? _virtualMemoryService.GetHardcorePreset()
                : _virtualMemoryService.GetStandardPreset();
            RecommendedMinMb = min;
            RecommendedMaxMb = max;

            // Show the predicted commit limit (physical RAM + pagefile min) so
            // the user understands what the setting change will actually yield.
            double predictedGb = _virtualMemoryService.GetPredictedCommitLimitGb(min);
            PagefileRecommendation = UseHardcorePreset
                ? $"高级方案: {min} - {max} MB · 应用后提交上限 ≈ {predictedGb:F0} GB"
                : $"普通方案: {min} - {max} MB · 应用后提交上限 ≈ {predictedGb:F0} GB";
            PredictedCommitText = $"应用此方案后,提交上限将从当前 {VirtualTotalGb:F0} GB 变为约 {predictedGb:F0} GB";

            // Pre-fill the input boxes with the current or recommended size.
            if (status.HasExplicitSize && !status.IsSystemManaged)
            {
                PagefileMinInput = status.MinMb.ToString();
                PagefileMaxInput = status.MaxMb.ToString();
            }
            else
            {
                PagefileMinInput = min.ToString();
                PagefileMaxInput = max.ToString();
            }
        }

        // Switch the active preset and refresh the recommendation display.
        public void SetPreset(bool hardcore)
        {
            UseHardcorePreset = hardcore;
            RefreshPagefile();
        }

        [RelayCommand]
        public async Task LightCleanAsync()
        {
            if (IsCleaning) return;
            IsCleaning = true;

            try
            {
                var before = _memoryService.GetMemorySnapshot();
                CleanResult result = await Task.Run(() => _memoryService.CleanWorkingSets());
                LastCleanText = $"轻量清理:处理 {result.ProcessCount} 个进程，释放约 {result.ReclaimedMb:F0} MB，可用内存 {before.AvailablePhysicalGb:F1} → {result.AvailableAfterGb:F1} GB";
                StatusReported?.Invoke(LastCleanText);
                Refresh();
            }
            finally
            {
                IsCleaning = false;
            }
        }

        [RelayCommand]
        public async Task DeepCleanAsync()
        {
            if (IsCleaning) return;
            IsCleaning = true;

            try
            {
                var before = _memoryService.GetMemorySnapshot();
                CleanResult result = await Task.Run(() => _memoryService.DeepClean());
                LastCleanText = $"深度清理:处理 {result.ProcessCount} 个进程并清空系统缓存，可用内存 {before.AvailablePhysicalGb:F1} → {result.AvailableAfterGb:F1} GB(释放 {result.FreedGb:F2} GB)";
                StatusReported?.Invoke(LastCleanText);
                Refresh();
            }
            finally
            {
                IsCleaning = false;
            }
        }

        [RelayCommand]
        public void TrimProcess(ProcessMemoryEntry entry)
        {
            if (entry == null) return;
            bool ok = _memoryService.TrimProcess(entry.ProcessId);
            StatusReported?.Invoke(ok
                ? $"已削减 {entry.ProcessName} (PID {entry.ProcessId}) 的工作集"
                : $"无法削减 {entry.ProcessName} (PID {entry.ProcessId})");
            Refresh();
        }

        [RelayCommand]
        public void ApplyRecommendedPagefile()
        {
            if (!int.TryParse(PagefileMinInput, out int min) || !int.TryParse(PagefileMaxInput, out int max))
            {
                PagefileResultText = "请输入有效的数字(MB)";
                StatusReported?.Invoke(PagefileResultText);
                return;
            }

            bool ok = _virtualMemoryService.ApplyCustomPagefile(min, max);
            PagefileResultText = ok
                ? $"已设置虚拟内存为 {min} - {max} MB，重启后生效"
                : "设置失败，请以管理员身份运行";
            StatusReported?.Invoke(PagefileResultText);
            RefreshPagefile();
        }

        [RelayCommand]
        public void ApplySystemManagedPagefile()
        {
            bool ok = _virtualMemoryService.ApplySystemManaged();
            PagefileResultText = ok
                ? "已恢复为系统管理，重启后生效"
                : "设置失败，请以管理员身份运行";
            StatusReported?.Invoke(PagefileResultText);
            RefreshPagefile();
        }

        // Auto-clean settings are owned by the MemoryAutoCleanService (which runs
        // the background timer for the whole app session). The ViewModel just
        // reads/writes via the service and mirrors the values for binding.
        public void LoadAutoCleanSettings()
        {
            AutoCleanEnabled = _autoCleanService.GetEnabled();
            AutoCleanIntervalMinutes = _autoCleanService.GetIntervalMinutes();
            AutoCleanDeep = _autoCleanService.GetUseDeepClean();
            AutoCleanThreshold = _autoCleanService.GetThreshold();
        }

        public void ApplyAutoCleanSettings()
        {
            _autoCleanService.SaveSettings(AutoCleanEnabled, AutoCleanIntervalMinutes, AutoCleanDeep, AutoCleanThreshold);
            AutoCleanStatus = AutoCleanEnabled
                ? $"已启用:每 {AutoCleanIntervalMinutes} 分钟{(AutoCleanDeep ? "(深度)" : "(轻量)")}{(AutoCleanThreshold > 0 ? $",占用超 {AutoCleanThreshold}% 时触发" : ",无阈值限制")}"
                : "已关闭自动清理";
            StatusReported?.Invoke(AutoCleanStatus);
        }

        public void SetAutoCleanEnabled(bool enabled)
        {
            AutoCleanEnabled = enabled;
            ApplyAutoCleanSettings();
        }

        public void SetAutoCleanInterval(int minutes)
        {
            AutoCleanIntervalMinutes = Math.Max(1, minutes);
            ApplyAutoCleanSettings();
        }

        public void SetAutoCleanDeep(bool deep)
        {
            AutoCleanDeep = deep;
            ApplyAutoCleanSettings();
        }

        public void SetAutoCleanThreshold(int threshold)
        {
            AutoCleanThreshold = Math.Clamp(threshold, 0, 100);
            ApplyAutoCleanSettings();
        }
    }
}
