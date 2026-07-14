using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class ActivationService
    {
        // Remote activation scripts are intentionally not downloaded or executed.
        // Running mutable third-party PowerShell as administrator is a supply-chain
        // boundary that cannot be made safe without a pinned, independently verified
        // artifact digest. Keep the status/query functionality available and fail closed
        // until a trusted artifact can be shipped with the application.

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

        public Task<bool> ActivateAsync(IProgress<string>? progress = null)
        {
            progress?.Report(
                "为保护管理员权限安全，远程 MAS 脚本执行已禁用。" +
                "请从 MAS 官方渠道手动获取并核验工具，或等待 ReToolbox 提供带固定摘要的受信版本。");
            return Task.FromResult(false);
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
