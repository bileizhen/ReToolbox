using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class SystemInfoPageViewModel : ObservableObject
    {
        private readonly SystemInfoService _systemInfoService;

        [ObservableProperty]
        private string _windowsEdition = "正在加载...";

        [ObservableProperty]
        private string _processorName = "正在加载...";

        [ObservableProperty]
        private string _memoryText = "正在加载...";

        [ObservableProperty]
        private string _architecture = string.Empty;

        [ObservableProperty]
        private string _computerName = string.Empty;

        [ObservableProperty]
        private string _userName = string.Empty;

        [ObservableProperty]
        private string _uptimeText = string.Empty;

        [ObservableProperty]
        private string _adminStatusText = string.Empty;

        [ObservableProperty]
        private string _adminStatusGlyph = "\uE946";

        [ObservableProperty]
        private string _adminStatusForeground = "#F3F3F3";

        public ObservableCollection<DriveInfoItem> Drives { get; }

        public SystemInfoPageViewModel(SystemInfoService systemInfoService)
        {
            _systemInfoService = systemInfoService;
            Drives = new ObservableCollection<DriveInfoItem>();
            Refresh();
        }

        [RelayCommand]
        private void Refresh()
        {
            WindowsEdition = _systemInfoService.GetWindowsEdition();
            ProcessorName = _systemInfoService.GetProcessorName();
            Architecture = _systemInfoService.GetSystemArchitecture();
            ComputerName = _systemInfoService.GetComputerName();
            UserName = _systemInfoService.GetUserName();
            UptimeText = _systemInfoService.FormatUptime(_systemInfoService.GetUptime());

            var (totalGb, availableGb) = _systemInfoService.GetMemoryInfo();
            MemoryText = totalGb > 0
                ? $"{availableGb:F1} GB 可用 / {totalGb:F1} GB 总计"
                : "无法获取内存信息";

            bool isAdmin = _systemInfoService.IsRunningAsAdmin();
            AdminStatusText = isAdmin ? "已以管理员身份运行" : "未以管理员身份运行";
            AdminStatusGlyph = isAdmin ? "\uE73E" : "\uE7BA";
            AdminStatusForeground = isAdmin ? "#4CCB5F" : "#FFB900";

            Drives.Clear();
            foreach (var drive in _systemInfoService.GetDriveInfos())
            {
                Drives.Add(drive);
            }
        }
    }
}
