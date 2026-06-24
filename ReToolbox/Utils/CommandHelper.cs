using System;
using System.Diagnostics;
using System.Text;

namespace ReToolbox.Utils
{
    public class CommandHelper
    {
        // Chinese Windows uses GBK (codepage 936) for console output by default;
        // decoding as UTF-8 produces mojibake for localized winget output.
        private static readonly Encoding OutputEncoding = Encoding.GetEncoding(
            Encoding.Default.CodePage == 936 ? 936 : 65001);

        public static string RunCommand(string command, bool noWindow = true, bool waitForExit = true)
        {
            using Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {command}";
            process.StartInfo.CreateNoWindow = noWindow;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = OutputEncoding;
            process.StartInfo.StandardErrorEncoding = OutputEncoding;

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
            process.StartInfo.StandardOutputEncoding = OutputEncoding;
            process.StartInfo.StandardErrorEncoding = OutputEncoding;

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
            process.StartInfo.StandardOutputEncoding = OutputEncoding;
            process.StartInfo.StandardErrorEncoding = OutputEncoding;

            process.Start();
            return process;
        }
    }
}

