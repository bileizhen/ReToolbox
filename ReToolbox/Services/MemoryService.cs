using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ReToolbox.Services
{
    // Memory monitoring and cleanup. Cleanup trims per-process working sets
    // (like a "memory optimizer") and, for deep clean, also drops the system
    // file cache. Working-set trimming frees RAM that processes are not
    // actively using; the number will rise again as they touch their pages,
    // so this is presented honestly in the UI.
    public class MemoryService
    {
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_SET_QUOTA = 0x0100;
        private const int PROCESS_VM_READ = 0x0010;

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemFileCacheSize(out uint lpMinimumFileCacheSize, out uint lpMaximumFileCacheSize, out uint lpFlags);

        [DllImport("kernel32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSystemFileCacheSize(uint MinimumFileCacheSize, uint MaximumFileCacheSize, uint Flags);

        // Pointer-based signature: this project disables runtime marshalling
        // (DisableRuntimeMarshalling=true), which rejects by-ref managed structs,
        // so we pass a raw pointer and do the call in an unsafe block.
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

        private const uint FILE_CACHE_MIN_HARD_ENABLE = 0x00000001;
        private const uint FILE_CACHE_MAX_HARD_ENABLE = 0x00000002;

        // Snapshot of physical + virtual (commit) memory usage.
        public unsafe MemorySnapshot GetMemorySnapshot()
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
            if (GlobalMemoryStatusEx(&status) == 0)
            {
                return new MemorySnapshot();
            }

            const double gb = 1024d * 1024d * 1024d;
            return new MemorySnapshot
            {
                TotalPhysicalGb = status.ullTotalPhys / gb,
                AvailablePhysicalGb = status.ullAvailPhys / gb,
                PhysicalLoadPercent = status.dwMemoryLoad,
                TotalVirtualGb = status.ullTotalPageFile / gb,
                AvailableVirtualGb = status.ullAvailPageFile / gb,
                VirtualLoadPercent = status.ullTotalPageFile == 0
                    ? 0
                    : (double)(status.ullTotalPageFile - status.ullAvailPageFile) / status.ullTotalPageFile * 100
            };
        }

        public bool IsRunningAsAdmin()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Light clean: trim the working set of every accessible process. Returns
        // the number of processes trimmed.
        public CleanResult CleanWorkingSets()
        {
            int trimmed = 0;
            long reclaimedBytes = 0;
            var before = GetMemorySnapshot();

            int currentPid = Environment.ProcessId;
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == currentPid || proc.Id == 0) continue;

                    long workingBefore = proc.WorkingSet64;
                    IntPtr handle = OpenProcess(
                        PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA | PROCESS_VM_READ,
                        false,
                        (uint)proc.Id);

                    if (handle != IntPtr.Zero)
                    {
                        try
                        {
                            if (EmptyWorkingSet(handle))
                            {
                                trimmed++;
                                try { reclaimedBytes += Math.Max(0, workingBefore - proc.WorkingSet64); }
                                catch { }
                            }
                        }
                        finally
                        {
                            CloseHandle(handle);
                        }
                    }
                }
                catch { }
                finally
                {
                    proc.Dispose();
                }
            }

            var after = GetMemorySnapshot();
            return new CleanResult
            {
                ProcessCount = trimmed,
                ReclaimedMb = reclaimedBytes / (1024d * 1024d),
                AvailableBeforeGb = before.AvailablePhysicalGb,
                AvailableAfterGb = after.AvailablePhysicalGb
            };
        }

        // Deep clean: light clean + drop the system file cache (standby/modified
        // pages backed by files). The cache-flush may fail without the
        // SeIncreaseQuotaPrivilege / SeProfileSingleProcessPrivilege; failures
        // are swallowed and we still report the working-set gains.
        public CleanResult DeepClean()
        {
            var before = GetMemorySnapshot();
            var result = CleanWorkingSets();

            try
            {
                FlushSystemFileCache();
            }
            catch { }

            var after = GetMemorySnapshot();
            result.AvailableAfterGb = after.AvailablePhysicalGb;
            result.DeepClean = true;
            return result;
        }

        // Sets the file cache to its minimum then restores the defaults so the
        // cache is effectively flushed without permanently crippling it.
        private static void FlushSystemFileCache()
        {
            if (!GetSystemFileCacheSize(out uint min, out uint max, out uint flags))
                return;

            // Flush by setting hard limits to 1 then disabling them again.
            SetSystemFileCacheSize(1, 1, FILE_CACHE_MIN_HARD_ENABLE | FILE_CACHE_MAX_HARD_ENABLE);
            SetSystemFileCacheSize(min, max, 0);
        }

        // Top processes by working set, excluding ourselves.
        public IReadOnlyList<ProcessMemoryEntry> GetProcessMemoryRanking(int top = 10)
        {
            int currentPid = Environment.ProcessId;
            var entries = new List<ProcessMemoryEntry>();

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == currentPid || proc.Id == 0) continue;
                    entries.Add(new ProcessMemoryEntry
                    {
                        ProcessName = proc.ProcessName,
                        ProcessId = proc.Id,
                        WorkingSetMb = proc.WorkingSet64 / (1024d * 1024d),
                        PrivateMemoryMb = proc.PrivateMemorySize64 / (1024d * 1024d)
                    });
                }
                catch { }
                finally
                {
                    proc.Dispose();
                }
            }

            return entries
                .OrderByDescending(e => e.WorkingSetMb)
                .Take(top)
                .ToList();
        }

        // Trim a single process's working set.
        public bool TrimProcess(int processId)
        {
            IntPtr handle = OpenProcess(
                PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA | PROCESS_VM_READ,
                false,
                (uint)processId);

            if (handle == IntPtr.Zero) return false;

            try
            {
                return EmptyWorkingSet(handle);
            }
            finally
            {
                CloseHandle(handle);
            }
        }
    }

    public class MemorySnapshot
    {
        public double TotalPhysicalGb { get; set; }
        public double AvailablePhysicalGb { get; set; }
        public uint PhysicalLoadPercent { get; set; }
        public double TotalVirtualGb { get; set; }
        public double AvailableVirtualGb { get; set; }
        public double VirtualLoadPercent { get; set; }

        public double UsedPhysicalGb => TotalPhysicalGb - AvailablePhysicalGb;
        public double UsedVirtualGb => TotalVirtualGb - AvailableVirtualGb;
    }

    public class CleanResult
    {
        public int ProcessCount { get; set; }
        public double ReclaimedMb { get; set; }
        public double AvailableBeforeGb { get; set; }
        public double AvailableAfterGb { get; set; }
        public bool DeepClean { get; set; }
        public double FreedGb => Math.Max(0, AvailableAfterGb - AvailableBeforeGb);
    }

    public class ProcessMemoryEntry
    {
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public double WorkingSetMb { get; set; }
        public double PrivateMemoryMb { get; set; }
        public string DisplayText => $"{ProcessName}  ({WorkingSetMb:F1} MB)";
    }
}
