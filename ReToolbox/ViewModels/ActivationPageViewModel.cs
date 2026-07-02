using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class ActivationPageViewModel : ObservableObject
    {
        private readonly ActivationService _activationService;

        [ObservableProperty]
        private string _activationStatus = "正在检测...";

        [ObservableProperty]
        private bool _isActivated;

        [ObservableProperty]
        private bool _isActivating;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _windowsEdition = "Windows";

        [ObservableProperty]
        private string _activationDetail = "正在检测激活状态...";

        [ObservableProperty]
        private string _activationStatusText = "检测中";

        [ObservableProperty]
        private string _activationStatusGlyph = "\uE946";

        [ObservableProperty]
        private string _activationStatusForeground = "#9CA3AF";

        public ActivationPageViewModel(ActivationService activationService)
        {
            _activationService = activationService;
            RefreshStatus();
        }

        [RelayCommand]
        private void RefreshStatus()
        {
            try
            {
                WindowsEdition = _activationService.GetWindowsEdition();
                IsActivated = _activationService.IsActivated();
                ActivationStatus = IsActivated ? "Windows 已激活" : "Windows 未激活";
                ActivationStatusText = IsActivated ? "已激活" : "未激活";
                ActivationStatusGlyph = IsActivated ? "\uE73E" : "\uE711";
                ActivationStatusForeground = IsActivated ? "#2EA043" : "#FF5F57";
                ActivationDetail = _activationService.GetActivationDescription();
            }
            catch
            {
                WindowsEdition = "Windows";
                ActivationStatus = "无法检测激活状态";
                IsActivated = false;
                ActivationStatusText = "未知";
                ActivationStatusGlyph = "\uE7BA";
                ActivationStatusForeground = "#FFB900";
                ActivationDetail = "无法获取激活状态详情。";
            }
        }

        [RelayCommand]
        private async Task ActivateAsync()
        {
            IsActivating = true;
            StatusMessage = "正在启动激活脚本...";

            var progress = new Progress<string>(msg => StatusMessage = msg);

            bool success = await _activationService.ActivateAsync(progress);

            if (success)
            {
                StatusMessage = "激活脚本已执行完成，请检查激活状态";
            }
            else
            {
                StatusMessage = "激活失败，请重试";
            }

            IsActivating = false;
            RefreshStatus();
        }
    }
}

