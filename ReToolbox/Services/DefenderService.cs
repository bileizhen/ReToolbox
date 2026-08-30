using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
            return IsDefenderActive() ? "正在保护" : "未运行或已移除";
        }

        // Downloads the pinned reviewed release, verifies it, and selects the
        // requested upstream mode without requiring menu input from the user.
        public async Task<DefenderRemovalResult> RemoveDefenderAsync(
            DefenderRemovalMode mode,
            IProgress<string>? progress = null)
        {
            DefenderRemovalProfile profile = DefenderRemovalWorkflow.GetProfile(mode);
            DefenderRemoverRelease release = DefenderRemovalWorkflow.CurrentRelease;
            progress?.Report("正在下载 Defender Remover...");

            string? stagingDirectory = null;

            try
            {
                stagingDirectory = SecureStagingDirectory.Create();
                string archivePath = Path.Combine(stagingDirectory, release.FileName);
                string payloadDirectory = Path.Combine(stagingDirectory, "payload");

                progress?.Report("正在校验并解压 Defender Remover 源码包...");
                await using (FileStream downloaded =
                    await VerifiedArtifactDownloader.DownloadAndOpenAsync(
                        release.DownloadUri,
                        archivePath,
                        release.Size,
                        release.Sha256,
                        progress))
                {
                    Directory.CreateDirectory(payloadDirectory);
                    using ZipArchive archive = new ZipArchive(
                        downloaded,
                        ZipArchiveMode.Read,
                        leaveOpen: true);
                    archive.ExtractToDirectory(payloadDirectory);
                }

                string scriptDirectory = Path.Combine(
                    payloadDirectory,
                    release.ExtractedRootDirectory,
                    "script");
                string scriptPath = Path.Combine(scriptDirectory, "Script_Run.ps1");
                string powerRunPath = Path.Combine(scriptDirectory, "PowerRun.exe");
                if (!File.Exists(scriptPath) || !File.Exists(powerRunPath))
                {
                    return Failure("安全校验失败：固定源码包结构不完整，已阻止执行");
                }

                progress?.Report($"正在启动 Defender Remover：{profile.DisplayName}...");

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell",
                        "v1.0",
                        "powershell.exe");
                    process.StartInfo.WorkingDirectory = scriptDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.ArgumentList.Add("-NoProfile");
                    process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
                    process.StartInfo.ArgumentList.Add("Bypass");
                    process.StartInfo.ArgumentList.Add("-File");
                    process.StartInfo.ArgumentList.Add(scriptPath);
                    process.StartInfo.ArgumentList.Add(profile.UpstreamSelection);
                    if (!process.Start())
                    {
                        return Failure("无法启动 Defender Remover");
                    }

                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        return Failure($"Defender Remover 已退出，代码：{process.ExitCode}");
                    }
                }

                string completionMessage = profile.KeepsWindowsSecurity
                    ? $"{profile.DisplayName}脚本已执行，等待重启验证；Windows 安全中心仍存在是预期结果，上游通用验证器可能将此项显示为未完全移除"
                    : $"{profile.DisplayName}脚本已执行，等待重启后验证实际移除结果";
                progress?.Report(completionMessage);
                return new DefenderRemovalResult(
                    DefenderRemovalOutcome.AwaitingRestartVerification,
                    completionMessage);
            }
            catch (Exception ex)
            {
                return Failure($"移除失败: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (stagingDirectory is not null && Directory.Exists(stagingDirectory))
                    {
                        Directory.Delete(stagingDirectory, true);
                    }
                }
                catch
                {
                    // Best-effort cleanup; the protected staging directory contains
                    // only the already verified upstream payload.
                }
            }

            DefenderRemovalResult Failure(string message)
            {
                progress?.Report(message);
                return new DefenderRemovalResult(
                    DefenderRemovalOutcome.Failed,
                    message);
            }
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
