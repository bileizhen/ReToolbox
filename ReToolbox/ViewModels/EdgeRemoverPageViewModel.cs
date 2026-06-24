using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class EdgeRemoverPageViewModel : ObservableObject
    {
        private readonly EdgeRemoverService _edgeService;

        [ObservableProperty]
        private bool _isEdgeInstalled;

        [ObservableProperty]
        private string _edgeVersion = string.Empty;

        [ObservableProperty]
        private bool _isUninstalling;

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _heroStatusText = "检测中";

        [ObservableProperty]
        private string _heroTitle = "Microsoft Edge";

        [ObservableProperty]
        private string _heroStatusDetail = "检测中";

        [ObservableProperty]
        private string _heroVersionText = string.Empty;

        [ObservableProperty]
        private string _heroStatusGlyph = "\uE946";

        [ObservableProperty]
        private string _heroStatusForeground = "#F3F3F3";

        public EdgeRemoverPageViewModel(EdgeRemoverService edgeService)
        {
            _edgeService = edgeService;
            RefreshStatus();
        }

        [RelayCommand]
        private void RefreshStatus()
        {
            IsEdgeInstalled = _edgeService.IsEdgeInstalled();
            if (IsEdgeInstalled)
            {
                EdgeVersion = _edgeService.GetEdgeVersion();
                StatusMessage = $"Microsoft Edge 已安装 (版本: {EdgeVersion})";
                HeroStatusText = "已安装";
                HeroTitle = "Microsoft Edge";
                HeroStatusGlyph = "\uE73E";
                HeroStatusForeground = "#4CCB5F";
                HeroStatusDetail = "已安装";
                HeroVersionText = string.IsNullOrWhiteSpace(EdgeVersion) ? string.Empty : $"版本: {EdgeVersion}";
            }
            else
            {
                StatusMessage = "Microsoft Edge 未安装";
                EdgeVersion = string.Empty;
                HeroStatusText = "未安装";
                HeroTitle = "Microsoft Edge";
                HeroStatusGlyph = "\uE711";
                HeroStatusForeground = "#FF5F57";
                HeroStatusDetail = "未安装";
                HeroVersionText = string.Empty;
            }
        }

        [RelayCommand]
        private async Task UninstallEdgeAsync()
        {
            IsUninstalling = true;
            StatusMessage = "正在卸载 Microsoft Edge...";
            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
            });

            bool success = await _edgeService.UninstallEdgeAsync(progress);

            if (success)
            {
                StatusMessage = "Microsoft Edge 卸载成功";
            }
            else
            {
                StatusMessage = "Microsoft Edge 卸载失败";
            }

            IsUninstalling = false;
            RefreshStatus();
        }

        [RelayCommand]
        private async Task InstallEdgeAsync()
        {
            IsInstalling = true;
            StatusMessage = "正在安装 Microsoft Edge...";
            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
            });

            bool success = await _edgeService.InstallEdgeAsync(progress);

            if (success)
            {
                StatusMessage = "Microsoft Edge 安装成功";
            }
            else
            {
                StatusMessage = "Microsoft Edge 安装失败";
            }

            IsInstalling = false;
            RefreshStatus();
        }

        [RelayCommand]
        private async Task CleanupAsync()
        {
            StatusMessage = "正在清理 Edge 残留...";
            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
            });
            await _edgeService.RemoveEdgeIconsAsync(progress);

            StatusMessage = "清理完成";
        }
    }
}
