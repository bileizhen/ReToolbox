using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class DefenderService
    {
        // Mirrors the GitHub mirror convention already used by EdgeRemoverService
        // (https://gh.llkk.cc/) so the download works in mainland China.
        private const string DefenderRemoverUrl = "https://gh.llkk.cc/https://github.com/ionuttbara/windows-defender-remover/releases/download/release13/DefenderRemover.exe";

        public bool IsDefenderActive()
        {
            try
            {
                string result = CommandHelper.RunCommand("sc query WinDefend", true, true);
                return result.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public string GetDefenderStatusText()
        {
            return IsDefenderActive() ? "正在保护" : "已禁用或已移除";
        }

        // Downloads the Defender Remover self-extracting exe and launches it
        // without arguments, so the user can pick a menu option (Y/A/S) in the
        // tool's own interactive prompt.
        public async Task<bool> RemoveDefenderAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在下载 Defender Remover...");

            try
            {
                string localPath = Path.Combine(Path.GetTempPath(), "DefenderRemover.exe");

                using HttpClient client = new HttpClient();
                using (HttpResponseMessage response = await client.GetAsync(DefenderRemoverUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using Stream remote = await response.Content.ReadAsStreamAsync();
                    using Stream local = File.Create(localPath);
                    await remote.CopyToAsync(local);
                }

                progress?.Report("正在启动 Defender Remover（需管理员权限），请在弹出的窗口中按提示操作...");

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = localPath;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                    process.WaitForExit();
                }

                bool stillActive = IsDefenderActive();
                progress?.Report(stillActive
                    ? "Windows Defender 仍处于运行状态"
                    : "Windows Defender 已移除或禁用，建议重启电脑以使更改生效");
                return !stillActive;
            }
            catch (Exception ex)
            {
                progress?.Report($"移除失败: {ex.Message}");
                return false;
            }
        }

        // Opens the Windows Security UWP app ("windowsdefender:").
        public bool OpenWindowsSecurity()
        {
            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c start windowsdefender:";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
