using System;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class ActivationService
    {
        private const string MAS_COMMAND = "irm https://get.activated.win | iex";

        public bool IsActivated()
        {
            try
            {
                string result = CommandHelper.RunCommand("cscript //nologo %windir%\\system32\\slmgr.vbs /xpr", true, true);
                return result.Contains("已永久激活", StringComparison.OrdinalIgnoreCase) ||
                       result.Contains("permanently activated", StringComparison.OrdinalIgnoreCase) ||
                       result.Contains("已激活", StringComparison.OrdinalIgnoreCase) ||
                       result.Contains("activated", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public string GetActivationStatus()
        {
            try
            {
                string result = CommandHelper.RunCommand("cscript //nologo %windir%\\system32\\slmgr.vbs /dli", true, true);
                return result;
            }
            catch
            {
                return "无法获取激活状态";
            }
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
                string result = CommandHelper.RunCommand("cscript //nologo %windir%\\system32\\slmgr.vbs /xpr", true, true);

                if (result.Contains("已永久激活", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("permanently activated", StringComparison.OrdinalIgnoreCase))
                {
                    return "Windows 已使用数字许可证激活。";
                }

                if (result.Contains("已激活", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("activated", StringComparison.OrdinalIgnoreCase))
                {
                    return "Windows 当前已激活。";
                }

                return "Windows 当前未激活。";
            }
            catch
            {
                return "无法获取激活状态详情。";
            }
        }

        public async Task<bool> ActivateAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在启动 MAS 激活脚本...");

            try
            {
                string result = await Task.Run(() =>
                    CommandHelper.RunPowerShellCommand("irm https://get.activated.win | iex", false));

                progress?.Report("激活脚本执行完成");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"激活失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActivateWindowsAsync(IProgress<string>? progress = null)
        {
            progress?.Report("正在激活 Windows...");

            try
            {
                string result = await Task.Run(() =>
                    CommandHelper.RunCommand(
                        "powershell -NoProfile -ExecutionPolicy Bypass -Command \"irm https://massgrave.dev/Get | iex\"",
                        true, true));

                progress?.Report("激活命令已执行");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"激活失败: {ex.Message}");
                return false;
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
