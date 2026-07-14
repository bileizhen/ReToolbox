using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class DefenderService
    {
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

        // Defender removal requires executing a third-party administrator tool.
        // No independently verified digest or trusted publisher policy is currently
        // available, so this operation deliberately fails closed.
        public Task<bool> RemoveDefenderAsync(IProgress<string>? progress = null)
        {
            progress?.Report(
                "为保护管理员权限安全，Defender Remover 下载与执行已禁用。" +
                "请等待提供带固定摘要或可信签名验证的版本。");
            return Task.FromResult(false);
        }

        // Opens the Windows Security UWP app ("windowsdefender:").
        public bool OpenWindowsSecurity()
        {
            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = "explorer.exe";
                process.StartInfo.ArgumentList.Add("windowsdefender:");
                process.StartInfo.UseShellExecute = true;
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
