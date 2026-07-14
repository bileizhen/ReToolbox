using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class EdgeRemoverService
    {
        public bool IsEdgeInstalled()
        {
            return !string.IsNullOrWhiteSpace(GetInstalledEdgePath());
        }

        public string GetEdgeVersion()
        {
            try
            {
                string? edgePath = GetInstalledEdgePath();
                if (string.IsNullOrWhiteSpace(edgePath))
                {
                    return string.Empty;
                }

                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(edgePath);
                return versionInfo.FileVersion?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public Task<bool> UninstallEdgeAsync(IProgress<string>? progress = null)
        {
            progress?.Report(
                "为保护管理员权限安全，第三方 EdgeRemover 脚本下载与执行已禁用。" +
                "请等待提供带固定摘要的受信版本。");
            return Task.FromResult(false);
        }

        public async Task<bool> InstallEdgeAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在安装 Microsoft Edge...");

            try
            {
                if (IsEdgeInstalled())
                {
                    progress?.Report("Microsoft Edge 已安装");
                    return true;
                }

                string result = await Task.Run(() =>
                    CommandHelper.RunCommand(
                        "winget install --id Microsoft.Edge --accept-package-agreements --accept-source-agreements --silent",
                        true, true));

                ReportScriptOutput(result, progress);

                if (ContainsKnownFailure(result))
                {
                    progress?.Report("Microsoft Edge 安装失败");
                    return false;
                }

                progress?.Report("Microsoft Edge 安装完成");
                return IsEdgeInstalled();
            }
            catch (Exception ex)
            {
                progress?.Report($"安装失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveEdgeIconsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在清理 Edge 残留...");

            try
            {
                await Task.Run(() =>
                {
                    CommandHelper.RunCommand(
                        "reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Desktop\\NameSpace\\{2E7B1E3E-47D1-410C-94EC-D85E5E2E1DAA}\" /f",
                        true, true);
                    CommandHelper.RunCommand(
                        "reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run\" /v MicrosoftEdgeAutoLaunch /f 2>nul",
                        true, true);
                });

                progress?.Report("清理完成");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"清理失败: {ex.Message}");
                return false;
            }
        }

        private static bool ContainsKnownFailure(string result)
        {
            return result.Contains("No package found", StringComparison.OrdinalIgnoreCase) ||
                   result.Contains("未找到", StringComparison.OrdinalIgnoreCase) ||
                   result.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
                   result.Contains("[CRITICAL]", StringComparison.OrdinalIgnoreCase) ||
                   result.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                   result.Contains("失败", StringComparison.OrdinalIgnoreCase);
        }

        private static void ReportScriptOutput(string result, IProgress<string>? progress)
        {
            if (progress is null || string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            string[] lines = result.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    progress.Report(trimmed);
                }
            }
        }

        private static string? GetInstalledEdgePath()
        {
            string[] candidatePaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
            };

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
