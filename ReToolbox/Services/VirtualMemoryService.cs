using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    // Manages the Windows paging file (virtual memory) via the registry. Changes
    // take effect after a reboot. The system-managed mode lets Windows size the
    // page file automatically; a custom mode pins min/max sizes.
    public class VirtualMemoryService
    {
        private const string MemoryMgmtKey = @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
        private const string PagingFilesValue = "PagingFiles";
        private const string AutoManagedValue = "AutoManagedPagefile";

        // Pointer-based signature: this project disables runtime marshalling
        // (DisableRuntimeMarshalling=true), which rejects by-ref managed structs.
        [DllImport("kernel32.dll", SetLastError = false, EntryPoint = "GlobalMemoryStatusEx")]
        private static extern unsafe int GlobalMemoryStatusEx(MEMORYSTATUSEX* lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
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

        public unsafe double GetTotalPhysicalGb()
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
            return GlobalMemoryStatusEx(&status) != 0 ? status.ullTotalPhys / (1024d * 1024d * 1024d) : 0;
        }

        // Predicted commit limit (提交上限) after applying a given pagefile
        // min size. Windows sizes the commit limit as physical RAM + the page
        // file's current allocation, which starts at the configured minimum.
        // Returns GB. Used to preview what a setting change will yield before
        // the user commits to it (and reboots).
        public double GetPredictedCommitLimitGb(int pagefileMinMb)
        {
            return GetTotalPhysicalGb() + pagefileMinMb / 1024d;
        }

        public PagefileStatus GetStatus()
        {
            var status = new PagefileStatus();

            object? autoObj = RegistryHelper.GetValue(MemoryMgmtKey, AutoManagedValue);
            status.IsSystemManaged = autoObj is int i ? i == 1 : true;

            object? paging = RegistryHelper.GetValue(MemoryMgmtKey, PagingFilesValue);
            if (paging is string[] arr && arr.Length > 0)
            {
                // Entries look like "C:\pagefile.sys 4096 4096" (path min max).
                var parts = arr[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                status.FilePath = parts.Length > 0 ? parts[0] : "C:\\pagefile.sys";

                if (parts.Length >= 3 &&
                    int.TryParse(parts[1], out int min) &&
                    int.TryParse(parts[2], out int max))
                {
                    status.MinMb = min;
                    status.MaxMb = max;
                    status.HasExplicitSize = true;
                }
            }

            return status;
        }

        // Game-oriented recommendation (MB). Two presets tuned for gamers:
        //
        //   普通方案 (Stable): enough headroom for smooth daily + light gaming.
        //   高级方案 (Hardcore): maximises the 1% low framerate for memory-hungry
        //                        AAA titles with heavy background apps.
        //
        //   RAM        普通方案(min=max)   高级方案(min/max)
        //   <= 8 GB    12288 (1.5x RAM)     16384 - 24576
        //   <=16 GB    8192 - 12288         16384 - 24576
        //   <=32 GB    16384                24576 - 32768
        //   >=64 GB    (系统管理即可)         8192 - 16384
        //
        // Above 64 GB physical RAM is effectively never exhausted, so the
        // pagefile is just an insurance policy — a small fixed size suffices.
        public (int MinMb, int MaxMb) GetRecommendedSize() => GetStandardPreset();

        public (int MinMb, int MaxMb) GetStandardPreset()
        {
            int physMb = (int)Math.Round(GetTotalPhysicalGb() * 1024);
            if (physMb <= 0) return (12288, 12288);

            if (physMb <= 8 * 1024) return (12288, 12288);            // 8GB:  12 / 12
            if (physMb <= 16 * 1024) return (8192, 12288);            // 16GB: 8 / 12
            if (physMb <= 32 * 1024) return (16384, 16384);           // 32GB: 16 / 16
            return (8192, 16384);                                     // 64GB+: 系统管理更佳
        }

        public (int MinMb, int MaxMb) GetHardcorePreset()
        {
            int physMb = (int)Math.Round(GetTotalPhysicalGb() * 1024);
            if (physMb <= 0) return (16384, 24576);

            if (physMb <= 8 * 1024) return (16384, 24576);            // 8GB:  16 / 24
            if (physMb <= 16 * 1024) return (16384, 24576);           // 16GB: 16 / 24
            if (physMb <= 32 * 1024) return (24576, 32768);           // 32GB: 24 / 32
            return (8192, 16384);                                     // 64GB+: 8 / 16
        }

        public bool ApplyCustomPagefile(int minMb, int maxMb)
        {
            try
            {
                int clampedMin = Math.Clamp(minMb, 16, int.MaxValue);
                int clampedMax = Math.Max(clampedMin, maxMb);

                // System-managed flag off, then write the sized entry.
                RegistryHelper.SetValue(MemoryMgmtKey, AutoManagedValue, 0);
                RegistryHelper.SetValue(
                    MemoryMgmtKey,
                    PagingFilesValue,
                    new[] { $"C:\\pagefile.sys {clampedMin} {clampedMax}" },
                    RegistryValueKind.MultiString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ApplySystemManaged()
        {
            try
            {
                RegistryHelper.SetValue(MemoryMgmtKey, AutoManagedValue, 1);
                RegistryHelper.DeleteValue(MemoryMgmtKey, PagingFilesValue);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class PagefileStatus
    {
        public bool IsSystemManaged { get; set; }
        public bool HasExplicitSize { get; set; }
        public string FilePath { get; set; } = "C:\\pagefile.sys";
        public int MinMb { get; set; }
        public int MaxMb { get; set; }

        public string Summary => IsSystemManaged
            ? "系统管理(自动)"
            : (HasExplicitSize ? $"自定义 {MinMb} - {MaxMb} MB" : "自定义(大小未知)");
    }
}
