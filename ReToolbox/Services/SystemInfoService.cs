using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class SystemInfoService
    {
        private const string CurrentVersionKey = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public string GetWindowsEdition()
        {
            string? productName = RegistryHelper.GetValue(CurrentVersionKey, "ProductName") as string;
            string? displayVersion = RegistryHelper.GetValue(CurrentVersionKey, "DisplayVersion") as string;
            string? build = RegistryHelper.GetValue(CurrentVersionKey, "CurrentBuildNumber") as string;

            if (!string.IsNullOrWhiteSpace(productName) && !string.IsNullOrWhiteSpace(displayVersion))
            {
                return $"{productName} {displayVersion} (Build {build})";
            }

            return productName ?? "Windows";
        }

        public string GetProcessorName()
        {
            string? name = RegistryHelper.GetValue(
                @"HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString") as string;

            return string.IsNullOrWhiteSpace(name) ? "未知处理器" : name.Trim();
        }

        public (double TotalGb, double AvailableGb) GetMemoryInfo()
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref status))
            {
                return (0, 0);
            }

            const double gb = 1024d * 1024d * 1024d;
            return (status.ullTotalPhys / gb, status.ullAvailPhys / gb);
        }

        public string GetSystemArchitecture()
        {
            return Environment.Is64BitOperatingSystem ? "64 位" : "32 位";
        }

        public string GetComputerName() => Environment.MachineName;

        public string GetUserName() => Environment.UserName;

        public TimeSpan GetUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

        public bool IsRunningAsAdmin()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public IReadOnlyList<DriveInfoItem> GetDriveInfos()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new DriveInfoItem
                {
                    Name = d.Name,
                    Label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "本地磁盘" : d.VolumeLabel,
                    TotalGb = d.TotalSize / (1024d * 1024d * 1024d),
                    FreeGb = d.AvailableFreeSpace / (1024d * 1024d * 1024d),
                })
                .ToList();
        }

        public string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
            {
                return $"{(int)uptime.TotalDays} 天 {uptime.Hours} 小时";
            }

            if (uptime.TotalHours >= 1)
            {
                return $"{(int)uptime.TotalHours} 小时 {uptime.Minutes} 分钟";
            }

            return $"{uptime.Minutes} 分钟";
        }

        [DllImport("kernel32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
    }

    public class DriveInfoItem
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double TotalGb { get; set; }
        public double FreeGb { get; set; }
        public double UsedPercent => TotalGb <= 0 ? 0 : (TotalGb - FreeGb) / TotalGb * 100;
        public string HeaderText => $"{Name} {Label}";
        public string DetailText => $"{FreeGb:F1} GB 可用 / {TotalGb:F1} GB 总计";
    }
}
