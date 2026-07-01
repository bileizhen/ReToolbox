using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class DefenderPageViewModel : ObservableObject
    {
        private readonly DefenderService _defenderService;

        [ObservableProperty]
        private bool _isDefenderActive;

        [ObservableProperty]
        private bool _isRemoving;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _heroTitle = "Windows Defender";

        [ObservableProperty]
        private string _heroStatusText = "检测中";

        [ObservableProperty]
        private string _heroStatusDetail = "检测中";

        [ObservableProperty]
        private string _heroVersionText = string.Empty;

        [ObservableProperty]
        private string _heroStatusGlyph = "\uE946";

        [ObservableProperty]
        private string _heroStatusForeground = "#9CA3AF";

        public DefenderPageViewModel(DefenderService defenderService)
        {
            _defenderService = defenderService;
            RefreshStatus();
        }

        [RelayCommand]
        private void RefreshStatus()
        {
            IsDefenderActive = _defenderService.IsDefenderActive();
            if (IsDefenderActive)
            {
                StatusMessage = "Windows Defender 正在运行";
                HeroStatusText = "正在保护";
                HeroTitle = "Windows Defender";
                HeroStatusGlyph = "\uE73E";
                HeroStatusForeground = "#2EA043";
                HeroStatusDetail = "正在保护";
                HeroVersionText = string.Empty;
            }
            else
            {
                StatusMessage = "Windows Defender 已禁用或已移除";
                HeroStatusText = "已禁用";
                HeroTitle = "Windows Defender";
                HeroStatusGlyph = "\uE711";
                HeroStatusForeground = "#FF5F57";
                HeroStatusDetail = "已禁用或已移除";
                HeroVersionText = string.Empty;
            }
        }

        [RelayCommand]
        private async Task RemoveDefenderAsync()
        {
            IsRemoving = true;
            StatusMessage = "正在移除 Windows Defender...";
            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
            });

            bool success = await _defenderService.RemoveDefenderAsync(progress);

            if (success)
            {
                StatusMessage = "Windows Defender 移除完成，建议重启电脑";
            }
            else
            {
                StatusMessage = "Windows Defender 移除未完成，请重试或检查输出";
            }

            IsRemoving = false;
            RefreshStatus();
        }
    }
}
