using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class ActivationService
    {
        // LicenseStatus values from SoftwareLicensingProduct:
        //   0 = Unlicensed, 1 = Licensed (permanently activated),
        //   2 = OOB grace, 3 = Out-of-box grace / KMS activated,
        //   4 = Non-genuine grace, 5 = Notification (not activated),
        //   6 = Extended grace expired.
        // Windows is considered activated when a product holding a partial
        // product key reports status 1 (permanent) or 3 (KMS).
        public bool IsActivated()
        {
            try
            {
                return GetWindowsLicenseStatus() is 1 or 3;
            }
            catch
            {
                return false;
            }
        }

        // Returns the LicenseStatus of the active Windows product, or null if it
        // cannot be determined. Uses WMI instead of slmgr.vbs because slmgr.vbs
        // pops up a message box (no stdout) in a non-interactive session.
        private static int? GetWindowsLicenseStatus()
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMv2",
                "SELECT LicenseStatus, PartialProductKey, Name FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");

            foreach (ManagementObject item in searcher.Get())
            {
                var name = item["Name"]?.ToString();
                // Skip Office and other non-Windows products.
                if (string.IsNullOrWhiteSpace(name) ||
                    name.Contains("Office", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (int.TryParse(item["LicenseStatus"]?.ToString(), out int status))
                    return status;
            }

            return null;
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
                    3 => "Windows 已激活(KMS)。",
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

            try
            {
                stagingDirectory = SecureStagingDirectory.Create();
                string scriptPath = Path.Combine(stagingDirectory, release.FileName);

                progress?.Report($"正在下载并校验 MAS {release.Tag}...");
                using HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ReToolbox/1.4");
                await DownloadWithRetryAsync(client, release, scriptPath, progress);

                await using (FileStream downloaded = new FileStream(
                    scriptPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
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
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.ArgumentList.Add("/d");
                    process.StartInfo.ArgumentList.Add("/c");
                    process.StartInfo.ArgumentList.Add(scriptPath);
                    process.StartInfo.ArgumentList.Add(release.ActivationSwitch);

                    if (!process.Start())
                    {
                        return Failure("无法启动 MAS 激活脚本");
                    }

                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        return Failure($"MAS 激活脚本已退出，代码：{process.ExitCode}");
                    }
                }

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (IsActivated())
                    {
                        const string activated = "Windows 激活已验证成功";
                        progress?.Report(activated);
                        return new ActivationResult(ActivationOutcome.Activated, activated);
                    }

                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }

                const string awaitingVerification =
                    "MAS 脚本已执行，但尚未检测到激活状态；请稍后刷新状态或查看脚本窗口中的结果";
                progress?.Report(awaitingVerification);
                return new ActivationResult(
                    ActivationOutcome.AwaitingVerification,
                    awaitingVerification);
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
                return new ActivationResult(ActivationOutcome.Failed, message);
            }
        }

        private static async Task DownloadWithRetryAsync(
            HttpClient client,
            ActivationScriptRelease release,
            string localPath,
            IProgress<string>? progress)
        {
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(
                        release.DownloadUri,
                        HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using Stream remote = await response.Content.ReadAsStreamAsync();
                    await using FileStream local = new FileStream(
                        localPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        true);
                    await remote.CopyToAsync(local);
                    return;
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    progress?.Report($"下载失败，正在重试（{attempt}/{maxAttempts}）...");
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
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
