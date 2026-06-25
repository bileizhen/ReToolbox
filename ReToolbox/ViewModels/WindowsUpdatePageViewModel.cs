using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class WindowsUpdatePageViewModel : ObservableObject
    {
        private readonly WindowsUpdateService _updateService;

        [ObservableProperty]
        private bool _isUpdatePaused;

        [ObservableProperty]
        private bool _areDriverUpdatesDisabled;

        [ObservableProperty]
        private int _deferalDays;

        [ObservableProperty]
        private bool _isTenYearPauseEnabled;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _summaryTitle = "Windows 更新未受限制";

        [ObservableProperty]
        private string _summarySubtitle = "当前未启用 ReToolbox 更新策略";

        [ObservableProperty]
        private string _summaryDetail = "可以通过下方选项管理更新策略。";

        [ObservableProperty]
        private string _summaryBadgeBackground = "#3BA55D";

        [ObservableProperty]
        private string _summaryBadgeForeground = "White";

        [ObservableProperty]
        private string _summaryBadgeGlyph = "\uE73E";

        [ObservableProperty]
        private string _summaryActionText = "刷新状态";

        [ObservableProperty]
        private string _summaryBadgeKind = "check";

        public WindowsUpdatePageViewModel(WindowsUpdateService updateService)
        {
            _updateService = updateService;
            LoadCurrentState();
        }

        public void RefreshState()
        {
            LoadCurrentState();
        }

        private void LoadCurrentState()
        {
            IsUpdatePaused = _updateService.IsUpdatePaused();
            AreDriverUpdatesDisabled = _updateService.AreDriverUpdatesDisabled();
            DeferalDays = _updateService.GetDeferalDays();
            IsTenYearPauseEnabled = _updateService.IsPauseExtendedForTenYears();
            UpdateSummary();
        }

        [RelayCommand]
        private void ToggleUpdatePause()
        {
            try
            {
                if (IsUpdatePaused)
                {
                    _updateService.PauseUpdates();
                    StatusMessage = "Windows 更新已通过策略禁用，手动检查入口也会被限制";
                }
                else
                {
                    _updateService.ResumeUpdates();
                    StatusMessage = "Windows 更新已恢复默认策略";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"操作失败: {ex.Message}";
            }
        }

        public void SetUpdatePauseState(bool isPaused)
        {
            IsUpdatePaused = isPaused;

            try
            {
                if (isPaused)
                {
                    _updateService.PauseUpdates();
                    StatusMessage = "Windows 更新已通过策略禁用，手动检查入口也会被限制";
                }
                else
                {
                    _updateService.ResumeUpdates();
                    StatusMessage = "Windows 更新已恢复默认策略";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"操作失败: {ex.Message}";
            }

            UpdateSummary();
        }

        public void SetTenYearPauseState(bool isEnabled)
        {
            IsTenYearPauseEnabled = isEnabled;

            try
            {
                if (isEnabled)
                {
                    _updateService.ExtendPauseForTenYears();
                    StatusMessage = "系统更新已暂停 10 年，请重新打开 Windows 设置查看";
                }
                else
                {
                    _updateService.ClearUpdateDeferal();
                    StatusMessage = "10 年暂停已清除";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置失败: {ex.Message}";
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var activePolicies = new List<string>();

            if (IsUpdatePaused)
            {
                activePolicies.Add("策略禁更");
            }

            if (IsTenYearPauseEnabled)
            {
                activePolicies.Add("暂停 10 年");
            }

            if (AreDriverUpdatesDisabled)
            {
                activePolicies.Add("禁用驱动更新");
            }

            if (activePolicies.Count == 0)
            {
                SummaryTitle = "Windows 更新未受限制";
                SummarySubtitle = "当前未启用 ReToolbox 更新策略";
                SummaryDetail = "可以通过下方选项管理更新策略。";
                SummaryBadgeBackground = "#3BA55D";
                SummaryBadgeForeground = "White";
                SummaryBadgeGlyph = "\uE73E";
                SummaryActionText = "刷新状态";
                SummaryBadgeKind = "check";
                return;
            }

            if (IsTenYearPauseEnabled)
            {
                DateTimeOffset? pauseExpiry = _updateService.GetPauseExpiryTime();
                SummaryTitle = pauseExpiry is not null
                    ? $"更新已暂停，直到 {pauseExpiry.Value.LocalDateTime:yyyy/M/d} 为止"
                    : "更新已暂停";
                SummarySubtitle = "在继续更新之前，你的设备将不会保持最新状态";
                SummaryDetail = activePolicies.Count > 1
                    ? $"其他生效项：{string.Join("、", activePolicies.FindAll(policy => policy != "暂停 10 年"))}"
                    : string.Empty;
                SummaryBadgeBackground = "#FFB900";
                SummaryBadgeForeground = "White";
                SummaryBadgeGlyph = "\uE769";
                SummaryActionText = "继续更新";
                SummaryBadgeKind = "pause";
                return;
            }

            if (IsUpdatePaused)
            {
                SummaryTitle = "Windows 更新已被禁止";
                SummarySubtitle = $"当前生效：{string.Join("、", activePolicies)}";
                SummaryDetail = "系统设置中的检查更新入口可能被限制，自动更新也会被阻止。";
                SummaryBadgeBackground = "#D13438";
                SummaryBadgeForeground = "White";
                SummaryBadgeGlyph = "\uE711";
                SummaryActionText = "刷新状态";
                SummaryBadgeKind = "error";
                return;
            }

            SummaryTitle = "Windows 更新由 ReToolbox 管理";
            SummarySubtitle = $"当前生效：{string.Join("、", activePolicies)}";
            SummaryDetail = "这些设置可能会影响系统设置中的 Windows 更新可用项。";
            SummaryBadgeBackground = "#3BA55D";
            SummaryBadgeForeground = "White";
            SummaryBadgeGlyph = "\uE73E";
            SummaryActionText = "刷新状态";
            SummaryBadgeKind = "check";
        }
    }
}
