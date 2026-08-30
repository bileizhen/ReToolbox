using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class ActivationService
    {
        // LicenseStatus values from SoftwareLicensingProduct:
        //   0 = Unlicensed, 1 = Licensed,
        //   2 = OOB grace, 3 = OOT grace,
        //   4 = Non-genuine grace, 5 = Notification (not activated),
        //   6 = Extended grace expired.
        // Only the primary Windows product with status 1 is activated. Grace
        // states and dependent add-on licenses must not count as activation.
        public bool IsActivated()
        {
            try
            {
                return ActivationWorkflow.IsWindowsActivated(GetWindowsLicenses());
            }
            catch
            {
                return false;
            }
        }

        private static IReadOnlyList<WindowsLicenseSnapshot> GetWindowsLicenses()
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMv2",
                "SELECT ApplicationID, LicenseDependsOn, LicenseStatus, PartialProductKey " +
                "FROM SoftwareLicensingProduct " +
                $"WHERE ApplicationID = '{ActivationWorkflow.WindowsApplicationId}' " +
                "AND PartialProductKey IS NOT NULL");

            List<WindowsLicenseSnapshot> licenses = new List<WindowsLicenseSnapshot>();
            foreach (ManagementObject item in searcher.Get())
            {
                if (int.TryParse(item["LicenseStatus"]?.ToString(), out int status))
                {
                    licenses.Add(new WindowsLicenseSnapshot(
                        item["ApplicationID"]?.ToString() ?? string.Empty,
                        item["LicenseDependsOn"]?.ToString(),
                        item["PartialProductKey"]?.ToString(),
                        status));
                }
            }

            return licenses;
        }

        // Returns the most relevant primary Windows LicenseStatus, or null when
        // it cannot be determined. WMI avoids the interactive slmgr.vbs dialog.
        private static int? GetWindowsLicenseStatus()
        {
            WindowsLicenseSnapshot[] primaryLicenses = GetWindowsLicenses()
                .Where(license =>
                    string.IsNullOrWhiteSpace(license.LicenseDependsOn) &&
                    !string.IsNullOrWhiteSpace(license.PartialProductKey))
                .ToArray();

            if (ActivationWorkflow.IsWindowsActivated(primaryLicenses))
            {
                return 1;
            }

            return primaryLicenses.Select(license => (int?)license.LicenseStatus).FirstOrDefault();
        }

        public string GetActivationStatus()
        {
            return GetActivationDescription();
        }

        public string GetWindowsEdition()
        {
            const string CurrentVersionKey = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string? productName = RegistryHelper.GetValue(CurrentVersionKey, "ProductName") as string;
            string? editionId = RegistryHelper.GetValue(CurrentVersionKey, "EditionID") as string;
            string? currentBuild = RegistryHelper.GetValue(CurrentVersionKey, "CurrentBuildNumber") as string;

            string windowsName = int.TryParse(currentBuild, out int buildNumber) && buildNumber >= 22000
                ? "Windows 11"
                : "Windows 10";

            string editionName = GetEditionDisplayName(editionId);
            if (!string.IsNullOrWhiteSpace(editionName))
            {
                return $"{windowsName} {editionName}";
            }

            if (!string.IsNullOrWhiteSpace(productName))
            {
                if (productName.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase) && windowsName == "Windows 11")
                {
                    return "Windows 11" + productName["Windows 10".Length..];
                }

                return productName;
            }

            return windowsName;
        }

        public string GetActivationDescription()
        {
            try
            {
                var status = GetWindowsLicenseStatus();
                return status switch
                {
                    1 => "Windows 已使用数字许可证永久激活。",
                    3 => "Windows 处于 OOT 宽限期，尚未正式激活。",
                    2 => "Windows 处于 OOB 宽限期内,尚未永久激活。",
                    4 => "Windows 处于非正版宽限期。",
                    5 => "Windows 当前未激活。",
                    6 => "Windows 宽限期已过。",
                    _ => "无法确定 Windows 激活状态。"
                };
            }
            catch
            {
                return "无法获取激活状态详情。";
            }
        }

        public async Task<ActivationResult> ActivateAsync(IProgress<string>? progress = null)
        {
            if (IsActivated())
            {
                const string alreadyActivated = "Windows 已处于激活状态，无需重复执行";
                progress?.Report(alreadyActivated);
                return new ActivationResult(ActivationOutcome.Activated, alreadyActivated);
            }

            ActivationScriptRelease release = ActivationWorkflow.CurrentRelease;
            string? stagingDirectory = null;
            string? diagnosticLogPath = null;

            try
            {
                stagingDirectory = SecureStagingDirectory.Create();
                string scriptPath = Path.Combine(stagingDirectory, release.FileName);

                progress?.Report($"正在下载并校验 MAS {release.Tag}...");
                await using (FileStream downloaded =
                    await VerifiedArtifactDownloader.DownloadAndOpenAsync(
                        release.DownloadUri,
                        scriptPath,
                        release.Size,
                        release.Sha256,
                        progress))
                {
                    if (downloaded.Length != release.Size ||
                        !await ArtifactIntegrity.HasExpectedSha256Async(
                            downloaded,
                            release.Sha256))
                    {
                        return Failure("安全校验失败：MAS 文件大小或 SHA-256 与固定版本不匹配，已阻止执行");
                    }

                    progress?.Report("校验通过，正在启动 MAS HWID 激活...");
                    using Process process = new Process();
                    process.StartInfo.FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "cmd.exe");
                    process.StartInfo.WorkingDirectory = stagingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.ArgumentList.Add("/d");
                    process.StartInfo.ArgumentList.Add("/c");
                    process.StartInfo.ArgumentList.Add(scriptPath);
                    process.StartInfo.ArgumentList.Add(release.ActivationSwitch);

                    if (!process.Start())
                    {
                        return Failure("无法启动 MAS 激活脚本");
                    }

                    Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
                    Task<string> standardError = process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    string output = await standardOutput;
                    string error = await standardError;
                    diagnosticLogPath = await TryWriteDiagnosticLogAsync(
                        release,
                        output,
                        error,
                        process.ExitCode,
                        progress);

                    if (process.ExitCode != 0)
                    {
                        return Failure(
                            $"MAS 激活脚本已退出，代码：{process.ExitCode}。" +
                            FormatLogHint(diagnosticLogPath));
                    }
                }

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (IsActivated())
                    {
                        const string activated = "Windows 激活已验证成功";
                        progress?.Report(activated);
                        return new ActivationResult(
                            ActivationOutcome.Activated,
                            activated,
                            diagnosticLogPath);
                    }

                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }

                string notActivated =
                    "MAS 脚本已执行，但 Windows 主许可证仍未处于 Licensed 状态。" +
                    FormatLogHint(diagnosticLogPath);
                return Failure(notActivated);
            }
            catch (Exception ex)
            {
                return Failure($"激活失败：{ex.Message}");
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
                    // Best-effort cleanup of the verified payload.
                }
            }

            ActivationResult Failure(string message)
            {
                progress?.Report(message);
                return new ActivationResult(
                    ActivationOutcome.Failed,
                    message,
                    diagnosticLogPath);
            }
        }

        private static async Task<string?> TryWriteDiagnosticLogAsync(
            ActivationScriptRelease release,
            string standardOutput,
            string standardError,
            int exitCode,
            IProgress<string>? progress)
        {
            try
            {
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ReToolbox",
                    "Logs");
                Directory.CreateDirectory(logDirectory);
                string logPath = Path.Combine(
                    logDirectory,
                    $"activation-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
                string content =
                    $"MAS release: {release.Tag}{Environment.NewLine}" +
                    $"MAS commit: {release.Commit}{Environment.NewLine}" +
                    $"Switch: {release.ActivationSwitch}{Environment.NewLine}" +
                    $"Exit code: {exitCode}{Environment.NewLine}" +
                    $"Timestamp: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                    $"{Environment.NewLine}--- stdout ---{Environment.NewLine}{standardOutput}" +
                    $"{Environment.NewLine}--- stderr ---{Environment.NewLine}{standardError}";
                await File.WriteAllTextAsync(logPath, content);
                return logPath;
            }
            catch (Exception ex)
            {
                progress?.Report($"无法保存 MAS 诊断日志：{ex.Message}");
                return null;
            }
        }

        private static string FormatLogHint(string? diagnosticLogPath)
        {
            return string.IsNullOrWhiteSpace(diagnosticLogPath)
                ? "请稍后刷新激活状态。"
                : $"请查看诊断日志：{diagnosticLogPath}";
        }

        private static string GetEditionDisplayName(string? editionId)
        {
            return editionId?.ToLowerInvariant() switch
            {
                "core" => "家庭版",
                "coren" => "家庭版 N",
                "corecountryspecific" => "家庭中文版",
                "coresinglelanguage" => "家庭单语言版",
                "professional" => "专业版",
                "professionaln" => "专业版 N",
                "professionaleducation" => "专业教育版",
                "professionalworkstation" => "专业工作站版",
                "education" => "教育版",
                "educationn" => "教育版 N",
                "enterprise" => "企业版",
                "enterprisen" => "企业版 N",
                "enterpriseg" => "政府版",
                "enterprises" => "LTSC",
                "serverstandard" => "标准版",
                "serverdatacenter" => "数据中心版",
                _ => string.Empty
            };
        }
    }
}
