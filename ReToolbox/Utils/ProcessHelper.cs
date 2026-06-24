using System;
using System.Diagnostics;
using System.Linq;

namespace ReToolbox.Utils
{
    public class ProcessHelper
    {
        public static bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        public static void RunAsAdmin(string filePath, string arguments = "")
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }
}
