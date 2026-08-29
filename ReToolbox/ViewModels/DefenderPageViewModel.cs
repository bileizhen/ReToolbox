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
            HeroVersionText = DefenderRemovalWorkflow.CurrentRelease.Tag;
            if (IsDefenderActive)
            {
                StatusMessage = "Windows Defender 正在运行";
                HeroStatusText = "正在保护";
                HeroTitle = "Windows Defender";
                HeroStatusGlyph = "\uE73E";
                HeroStatusForeground = "#2EA043";
                HeroStatusDetail = "正在保护";
            }
            else
            {
                StatusMessage = "Windows Defender 已禁用或已移除";
                HeroStatusText = "已禁用";
                HeroTitle = "Windows Defender";
                HeroStatusGlyph = "\uE711";
                HeroStatusForeground = "#FF5F57";
                HeroStatusDetail = "已禁用或已移除";
            }
        }

        [RelayCommand]
        private async Task RemoveDefenderAsync(DefenderRemovalMode mode)
        {
            IsRemoving = true;
            DefenderRemovalProfile profile = DefenderRemovalWorkflow.GetProfile(mode);
            StatusMessage = $"正在执行：{profile.DisplayName}...";
            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
            });

            try
            {
                DefenderRemovalResult result = await _defenderService.RemoveDefenderAsync(mode, progress);
                StatusMessage = result.Message;
            }
            finally
            {
                IsRemoving = false;
            }
        }
    }
}
