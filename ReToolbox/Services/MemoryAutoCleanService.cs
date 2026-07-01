using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    // App-wide automatic memory cleaner. Runs a background timer for the whole
    // app session (not tied to a page), so cleaning continues even when the user
    // navigates away from the memory page. Settings are persisted in the
    // registry so the schedule survives restarts.
    //
    // A threshold gate skips the clean when physical memory usage is below the
    // configured percent, so we don't churn for no reason.
    public class MemoryAutoCleanService
    {
        private const string AppRegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string EnabledValue = "MemoryAutoClean";
        private const string IntervalValue = "MemoryAutoCleanInterval";
        private const string DeepValue = "MemoryAutoCleanDeep";
        private const string ThresholdValue = "MemoryAutoCleanThreshold";

        private readonly MemoryService _memoryService;
        private readonly DispatcherQueue _dispatcher;

        private Timer? _timer;
        private DateTime _nextRunAt;

        public bool IsRunning => _timer != null;

        // True when the current settings would actually trigger a run (enabled
        // and a timer is active). The UI binds this to show "running/idle".
        public event Action<string>? Log;

        public MemoryAutoCleanService(MemoryService memoryService)
        {
            _memoryService = memoryService;
            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool GetEnabled() =>
            RegistryHelper.GetValue(AppRegistryPath, EnabledValue) is int i && i == 1;

        public int GetIntervalMinutes()
        {
            object? v = RegistryHelper.GetValue(AppRegistryPath, IntervalValue);
            return v is int m && m > 0 ? m : 10;
        }

        public bool GetUseDeepClean() =>
            RegistryHelper.GetValue(AppRegistryPath, DeepValue) is int i && i == 1;

        // 0 = always clean regardless of usage; otherwise the physical memory
        // load percent that must be exceeded to trigger a clean.
        public int GetThreshold() =>
            RegistryHelper.GetValue(AppRegistryPath, ThresholdValue) is int i && i >= 0 && i <= 100 ? i : 80;

        public void SaveSettings(bool enabled, int intervalMinutes, bool deep, int threshold)
        {
            RegistryHelper.SetValue(AppRegistryPath, EnabledValue, enabled ? 1 : 0);
            RegistryHelper.SetValue(AppRegistryPath, IntervalValue, Math.Max(1, intervalMinutes));
            RegistryHelper.SetValue(AppRegistryPath, DeepValue, deep ? 1 : 0);
            RegistryHelper.SetValue(AppRegistryPath, ThresholdValue, Math.Clamp(threshold, 0, 100));

            // Apply immediately so changing the settings doesn't wait for the
            // next tick.
            if (enabled) Start();
            else Stop();
        }

        public void StartIfNeeded()
        {
            if (GetEnabled()) Start();
        }

        public void Start()
        {
            int minutes = Math.Max(1, GetIntervalMinutes());
            var interval = TimeSpan.FromMinutes(minutes);
            _nextRunAt = DateTime.Now + interval;

            _timer?.Dispose();
            _timer = new Timer(Callback, null, interval, interval);
            Log?.Invoke($"自动清理已启动:每 {minutes} 分钟{(GetUseDeepClean() ? "(深度)" : "(轻量)")}{(GetThreshold() > 0 ? $",内存占用超 {GetThreshold()}%" : "")}");
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
                Log?.Invoke("自动清理已停止");
            }
        }

        public DateTime GetNextRunAt() => _nextRunAt;

        // Runs on a ThreadPool timer, so the actual clean happens off the UI
        // thread and only marshals back to log a result.
        private void Callback(object? state)
        {
            int threshold = GetThreshold();
            var snap = _memoryService.GetMemorySnapshot();

            if (threshold > 0 && snap.PhysicalLoadPercent < threshold)
            {
                LogOnDispatcher($"跳过清理:内存占用 {snap.PhysicalLoadPercent}% 未达阈值 {threshold}%");
                _nextRunAt = DateTime.Now + TimeSpan.FromMinutes(GetIntervalMinutes());
                return;
            }

            bool deep = GetUseDeepClean();
            var result = deep ? _memoryService.DeepClean() : _memoryService.CleanWorkingSets();
            string msg = deep
                ? $"自动深度清理完成:释放 {result.FreedGb:F2} GB(占用 {snap.PhysicalLoadPercent}% → 现刷新查看)"
                : $"自动轻量清理完成:处理 {result.ProcessCount} 个进程,释放 {result.ReclaimedMb:F0} MB";

            LogOnDispatcher(msg);
            _nextRunAt = DateTime.Now + TimeSpan.FromMinutes(GetIntervalMinutes());
        }

        private void LogOnDispatcher(string message)
        {
            if (_dispatcher == null)
            {
                Log?.Invoke(message);
                return;
            }

            _dispatcher.TryEnqueue(() => Log?.Invoke(message));
        }
    }
}
