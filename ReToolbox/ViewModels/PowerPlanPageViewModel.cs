using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Models;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class PowerPlanPageViewModel : ObservableObject
    {
        private readonly PowerPlanService _powerPlanService;
        private readonly BatteryService _batteryService;

        [ObservableProperty]
        private bool _isAdmin;

        [ObservableProperty]
        private string _adminHint = string.Empty;

        [ObservableProperty]
        private string _activePlanName = string.Empty;

        // Hero status glyph/foreground mirror the Defender page's pattern so the
        // page reads consistently with the other tool pages.
        [ObservableProperty]
        private string _heroStatusDetail = "检测中";

        [ObservableProperty]
        private string _heroStatusGlyph = "\uE83F";

        [ObservableProperty]
        private string _heroStatusForeground = "#9CA3AF";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _ultimatePerformancePresent;

        // Switch-on-logon preference (persisted by the service).
        [ObservableProperty]
        private bool _autoSwitchEnabled;

        [ObservableProperty]
        private string _autoSwitchPlanGuid = string.Empty;

        [ObservableProperty]
        private string _autoSwitchStatus = string.Empty;

        public ObservableCollection<PowerPlan> PowerPlans { get; } = new();

        // ---- Battery ----
        [ObservableProperty]
        private bool _hasBattery;

        // Current battery charge percent (0-100).
        [ObservableProperty]
        private int _chargePercent;

        [ObservableProperty]
        private bool _isCharging;

        // Effective health percent. The numeric value drives the health bar while
        // the formatted value keeps the label concise (for example, "95.2%").
        [ObservableProperty]
        private double _batteryHealthPercent;

        [ObservableProperty]
        private string _batteryHealthText = "--";

        [ObservableProperty]
        private string _batterySummary = string.Empty;

        // Raised when a background operation reports progress so the page can
        // surface it in the InfoBar.
        public event Action<string, InfoBarSeverity>? StatusReported;

        public PowerPlanPageViewModel(PowerPlanService powerPlanService, BatteryService batteryService)
        {
            _powerPlanService = powerPlanService;
            _batteryService = batteryService;
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
        {
            IsAdmin = _powerPlanService.IsRunningAsAdmin();
            AdminHint = IsAdmin ? "已以管理员身份运行" : "未以管理员身份运行，电源功能不可用";

            PowerPlans.Clear();
            string activeGuid = _powerPlanService.GetActivePlanGuid();
            foreach (var plan in _powerPlanService.ListPowerPlans())
            {
                plan.IsActive = plan.Guid == activeGuid;
                PowerPlans.Add(plan);
            }

            UpdateActiveDisplay();
            UltimatePerformancePresent = _powerPlanService.IsUltimatePerformancePresent();
            LoadAutoSwitchSettings();
            RefreshBattery();
        }

        // Reads battery presence, current charge state and health. The health
        // report is formatted here so the UI never displays an unbounded double.
        public void RefreshBattery()
        {
            HasBattery = _batteryService.HasBattery();

            if (HasBattery)
            {
                var (percent, charging) = _batteryService.GetChargeStatus();
                ChargePercent = percent;
                IsCharging = charging;

                var health = _batteryService.GetBatteryHealth();
                BatteryHealthPercent = health.IsValid ? health.HealthPercent : 0;
                BatteryHealthText = health.IsValid ? $"{health.HealthPercent:F1}%" : "--";
                BatterySummary = health.IsValid
                    ? health.Summary
                    : "无法读取电池健康信息";
            }
            else
            {
                ChargePercent = 0;
                IsCharging = false;
                BatteryHealthPercent = 0;
                BatteryHealthText = "--";
                BatterySummary = "未检测到电池（台式机或无电池设备）";
            }

            OnPropertyChanged(nameof(ChargeStatusText));
        }

        // "100% · 正在充电" / "85% · 使用电池" — shown beside health.
        public string ChargeStatusText => HasBattery
            ? $"{ChargePercent}% · {(IsCharging ? "正在充电" : "使用电池")}"
            : string.Empty;

        private void UpdateActiveDisplay()
        {
            string activeGuid = _powerPlanService.GetActivePlanGuid();
            string name = string.Empty;
            foreach (var plan in PowerPlans)
            {
                bool active = plan.Guid == activeGuid;
                plan.IsActive = active;
                if (active) name = plan.Name;
            }

            ActivePlanName = string.IsNullOrEmpty(name) ? "未知" : name;
            HeroStatusDetail = $"当前电源计划：{ActivePlanName}";
            HeroStatusForeground = "#2EA043";
        }

        [RelayCommand]
        public async Task SetActiveAsync(PowerPlan? plan)
        {
            if (plan == null || IsBusy) return;

            if (!IsAdmin)
            {
                StatusMessage = "需要管理员权限才能切换电源计划";
                StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在切换到 {plan.Name}...";
            StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Informational);

            bool ok = await Task.Run(() => _powerPlanService.SetActivePlan(plan.Guid));

            StatusMessage = ok
                ? $"已切换到电源计划「{plan.Name}」"
                : "切换失败，请以管理员身份运行";
            StatusReported?.Invoke(StatusMessage, ok ? InfoBarSeverity.Success : InfoBarSeverity.Error);

            // If auto-switch is on, keep the target in sync with the new active plan.
            if (ok && AutoSwitchEnabled)
            {
                AutoSwitchPlanGuid = plan.Guid;
                _powerPlanService.SetAutoSwitch(true, plan.Guid);
            }

            Refresh();
            IsBusy = false;
        }

        [RelayCommand]
        public async Task<DeletePlanResult> DeleteAsync(PowerPlan? plan)
        {
            if (plan == null || IsBusy) return DeletePlanResult.Invalid;

            if (!IsAdmin)
            {
                StatusMessage = "需要管理员权限才能删除电源计划";
                StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Warning);
                return DeletePlanResult.Failed;
            }

            IsBusy = true;
            StatusMessage = $"正在删除「{plan.Name}」...";
            StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Informational);

            DeletePlanResult result = await Task.Run(() => _powerPlanService.DeletePlan(plan.Guid));

            StatusMessage = result switch
            {
                DeletePlanResult.Success => $"已删除电源计划「{plan.Name}」",
                DeletePlanResult.IsActive => $"无法删除：「{plan.Name}」是当前活动计划，请先切换到其他计划",
                _ => $"删除「{plan.Name}」失败，请以管理员身份运行"
            };
            StatusReported?.Invoke(
                StatusMessage,
                result == DeletePlanResult.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

            Refresh();
            IsBusy = false;
            return result;
        }

        [RelayCommand]
        public async Task AddUltimatePerformanceAsync()
        {
            if (IsBusy) return;

            if (!IsAdmin)
            {
                StatusMessage = "需要管理员权限才能添加卓越性能计划";
                StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "正在添加卓越性能计划...";
            StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Informational);

            string? newGuid = await Task.Run(() => _powerPlanService.AddUltimatePerformance());

            if (!string.IsNullOrEmpty(newGuid))
            {
                StatusMessage = "卓越性能计划已就绪";
                StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Success);
            }
            else
            {
                StatusMessage = "添加失败，请以管理员身份运行";
                StatusReported?.Invoke(StatusMessage, InfoBarSeverity.Error);
            }

            Refresh();
            IsBusy = false;
        }

        // ---- Switch-on-logon --------------------------------------------------

        public void LoadAutoSwitchSettings()
        {
            AutoSwitchEnabled = _powerPlanService.GetAutoSwitchEnabled();
            AutoSwitchPlanGuid = _powerPlanService.GetAutoSwitchPlanGuid();
            UpdateAutoSwitchStatus();
        }

        // Validates the selected GUID against the live list before persisting, so
        // a stale registry value (e.g. after a plan was deleted) can't target a
        // missing scheme.
        public void ApplyAutoSwitch(bool enabled, string planGuid)
        {
            bool ok = _powerPlanService.SetAutoSwitch(enabled, planGuid);
            AutoSwitchEnabled = enabled;
            AutoSwitchPlanGuid = enabled ? planGuid : string.Empty;

            StatusMessage = ok
                ? (enabled
                    ? $"已启用开机自动切换为「{PlanNameByGuid(planGuid)}」"
                    : "已关闭开机自动切换")
                : "设置开机自动切换失败，请以管理员身份运行";
            StatusReported?.Invoke(StatusMessage, ok ? InfoBarSeverity.Success : InfoBarSeverity.Error);

            UpdateAutoSwitchStatus();
        }

        private void UpdateAutoSwitchStatus()
        {
            AutoSwitchStatus = AutoSwitchEnabled
                ? $"已启用：每次登录自动切换为「{PlanNameByGuid(AutoSwitchPlanGuid)}」"
                : "未启用";
        }

        private string PlanNameByGuid(string guid)
        {
            foreach (var plan in PowerPlans)
            {
                if (plan.Guid == guid) return plan.Name;
            }
            return "未知计划";
        }
    }
}
