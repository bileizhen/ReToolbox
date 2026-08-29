using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
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

        // Downloads the pinned reviewed release, verifies it, and selects the
        // requested upstream mode without requiring menu input from the user.
        public async Task<bool> RemoveDefenderAsync(
            DefenderRemovalMode mode,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            DefenderRemovalProfile profile = DefenderRemovalWorkflow.GetProfile(mode);
            DefenderRemoverRelease release = DefenderRemovalWorkflow.CurrentRelease;
            progress?.Report("正在下载 Defender Remover...");

            string tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "ReToolbox",
                Guid.NewGuid().ToString("N"));
            string localPath = Path.Combine(tempDirectory, release.FileName);

            try
            {
                Directory.CreateDirectory(tempDirectory);

                using HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ReToolbox/1.4");

                await DownloadWithRetryAsync(client, release, localPath, progress, cancellationToken);

                progress?.Report("正在校验 Defender Remover 完整性...");
                await using (FileStream downloaded = File.OpenRead(localPath))
                {
                    if (downloaded.Length != release.Size ||
                        !await ArtifactIntegrity.HasExpectedSha256Async(
                            downloaded,
                            release.Sha256,
                            cancellationToken))
                    {
                        progress?.Report("安全校验失败：下载文件的大小或 SHA-256 与固定版本不匹配，已阻止执行");
                        return false;
                    }
                }

                progress?.Report($"正在启动 Defender Remover：{profile.DisplayName}...");

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = localPath;
                    process.StartInfo.WorkingDirectory = tempDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.ArgumentList.Add(profile.UpstreamSelection);
                    if (!process.Start())
                    {
                        progress?.Report("无法启动 Defender Remover");
                        return false;
                    }

                    // Current upstream source accepts y/a directly, while the packaged
                    // launcher may still show its interactive menu. Supplying both the
                    // argument and one stdin line keeps the selected mode deterministic.
                    try
                    {
                        await process.StandardInput.WriteLineAsync(profile.UpstreamSelection);
                        await process.StandardInput.FlushAsync();
                        process.StandardInput.Close();
                    }
                    catch (IOException) when (process.HasExited)
                    {
                        // A future upstream build may consume the command-line argument
                        // and exit before reading stdin.
                    }

                    await process.WaitForExitAsync(cancellationToken);
                    if (process.ExitCode != 0)
                    {
                        progress?.Report($"Defender Remover 已退出，代码：{process.ExitCode}");
                        return false;
                    }
                }

                progress?.Report($"{profile.DisplayName}流程已完成；请按上游提示重启，并在登录后查看验证结果");
                return true;
            }
            catch (OperationCanceledException)
            {
                progress?.Report("Defender 移除操作已取消");
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report($"移除失败: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                }
                catch
                {
                    // Best-effort cleanup; the OS temp directory remains the fallback.
                }
            }
        }

        private static async Task DownloadWithRetryAsync(
            HttpClient client,
            DefenderRemoverRelease release,
            string localPath,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(
                        release.DownloadUri,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    response.EnsureSuccessStatusCode();

                    await using Stream remote = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using FileStream local = new FileStream(
                        localPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        true);
                    await remote.CopyToAsync(local, cancellationToken);
                    return;
                }
                catch (Exception) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    progress?.Report($"下载失败，正在重试（{attempt}/{maxAttempts}）...");
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                }
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
