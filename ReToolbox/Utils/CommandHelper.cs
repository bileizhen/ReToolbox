using System;
using System.Diagnostics;

namespace ReToolbox.Utils
{
    public class CommandHelper
    {
        public static string RunCommand(string command, bool noWindow = true, bool waitForExit = true)
        {
            using Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {command}";
            process.StartInfo.CreateNoWindow = noWindow;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (waitForExit)
                process.WaitForExit();

            return output + (string.IsNullOrWhiteSpace(error) ? "" : "\n[Error]\n" + error);
        }

        public static string RunPowerShellCommand(string command, bool waitForExit = true)
        {
            using Process process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (waitForExit)
                process.WaitForExit();

            return output + (string.IsNullOrWhiteSpace(error) ? "" : "\n[Error]\n" + error);
        }

        public static Process RunCommandAsync(string command)
        {
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {command}";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            return process;
        }
    }
}
