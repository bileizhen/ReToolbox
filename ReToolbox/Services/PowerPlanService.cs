using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using ReToolbox.Models;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    // Manages Windows power schemes (电源计划) via the powercfg command-line tool,
    // and persists a "switch on logon" preference by creating a scheduled task so
    // the switch happens at every sign-in even when ReToolbox is not running.
    //
    // powercfg output on a Chinese Windows looks like:
    //   电源方案 GUID: 381b4222-...  (平衡)
    //   电源方案 GUID: 7c26cac5-...  (卓越性能) *
    // The trailing '*' marks the currently active scheme.
    public class PowerPlanService
    {
        // The well-known GUID for the hidden "Ultimate Performance" (卓越性能)
        // scheme. Duplicating it exposes a user-visible copy that can be edited.
        public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        private const string AppRegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string AutoSwitchEnabledValue = "AutoPowerPlanEnabled";
        private const string AutoSwitchGuidValue = "AutoPowerPlanGuid";
        private const string TaskName = "ReToolbox_AutoPowerPlan";

        // One scheme line: "GUID: <guid>  (<name>)" optionally followed by " *".
        private static readonly Regex SchemeLine =
            new(@"GUID:\s*([0-9a-fA-F\-]{36})\s*\((.+?)\)\s*(\*)?", RegexOptions.Compiled);

        public bool IsRunningAsAdmin()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Enumerates every power scheme on the machine. Active schemes are flagged
        // so the UI can badge them without a second query.
        public List<PowerPlan> ListPowerPlans()
        {
            var plans = new List<PowerPlan>();
            // powercfg emits localized scheme names; pin the console code page to
            // UTF-8 (chcp 65001) so both /list and /getactivescheme decode the same
            // way regardless of the system ANSI code page.
            string output = RunPowerCfg("/list");

            foreach (string raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var match = SchemeLine.Match(raw);
                if (!match.Success) continue;

                plans.Add(new PowerPlan
                {
                    Guid = match.Groups[1].Value,
                    Name = match.Groups[2].Value.Trim(),
                    IsActive = match.Groups[3].Value.Trim() == "*"
                });
            }

            return plans;
        }

        public string GetActivePlanGuid()
        {
            string output = RunPowerCfg("/getactivescheme");
            var match = SchemeLine.Match(output);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public bool SetActivePlan(string guid)
        {
            try
            {
                // Switching schemes requires elevation; report failure otherwise.
                string output = RunPowerCfg($"/setactive {guid}");
                return !output.Contains("[Error]", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // Deletes a power scheme by GUID. Windows refuses to delete the currently
        // active scheme, so the caller must switch away first; we guard against it
        // here as well and surface a distinct result so the UI can advise the user.
        public DeletePlanResult DeletePlan(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return DeletePlanResult.Invalid;
            }

            // Block deletion of the active scheme — powercfg would reject it anyway,
            // but failing up front lets us give a clearer message.
            if (guid == GetActivePlanGuid())
            {
                return DeletePlanResult.IsActive;
            }

            try
            {
                string output = RunPowerCfg($"/delete {guid}");
                bool hasError = output.Contains("[Error]", StringComparison.OrdinalIgnoreCase);
                return hasError ? DeletePlanResult.Failed : DeletePlanResult.Success;
            }
            catch
            {
                return DeletePlanResult.Failed;
            }
        }

        // True when a scheme named "卓越性能" or "Ultimate Performance" already
        // exists. Avoids creating duplicate copies on repeated clicks.
        public bool IsUltimatePerformancePresent()
        {
            foreach (var plan in ListPowerPlans())
            {
                string name = plan.Name;
                if (name.Contains("卓越性能", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // Duplicates the hidden Ultimate Performance scheme into a user-visible one
        // and returns its GUID. If one already exists, returns the existing GUID
        // without duplicating, so the button is safe to press repeatedly.
        public string? AddUltimatePerformance()
        {
            if (!IsRunningAsAdmin()) return null;

            // Re-use the existing copy if present rather than stacking duplicates.
            foreach (var plan in ListPowerPlans())
            {
                string name = plan.Name;
                if (name.Contains("卓越性能", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                {
                    return plan.Guid;
                }
            }

            try
            {
                string output = RunPowerCfg($"/duplicatescheme {UltimatePerformanceGuid}");

                // powercfg prints "电源方案 GUID: <new-guid>" for the new copy.
                var match = SchemeLine.Match(output);
                return match.Success ? match.Groups[1].Value : GetActivePlanGuid();
            }
            catch
            {
                return null;
            }
        }

        // ---- Switch-on-logon scheduled task ------------------------------------

        public bool GetAutoSwitchEnabled() =>
            RegistryHelper.GetValue(AppRegistryPath, AutoSwitchEnabledValue) is int i && i == 1;

        public string GetAutoSwitchPlanGuid() =>
            RegistryHelper.GetValue(AppRegistryPath, AutoSwitchGuidValue) as string ?? string.Empty;

        // Persists the preference to the registry and reconciles the scheduled
        // task: enabled creates it, disabled removes it. The task runs powercfg
        // at the highest privilege on every sign-in so it works for any account.
        public bool SetAutoSwitch(bool enabled, string planGuid)
        {
            try
            {
                RegistryHelper.SetValue(AppRegistryPath, AutoSwitchEnabledValue, enabled ? 1 : 0);
                RegistryHelper.SetValue(AppRegistryPath, AutoSwitchGuidValue, planGuid ?? string.Empty);

                if (enabled && !string.IsNullOrEmpty(planGuid))
                {
                    return EnsureAutoSwitchTask(planGuid);
                }
                else
                {
                    RemoveAutoSwitchTask();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool IsAutoSwitchTaskPresent()
        {
            string output = CommandHelper.RunCommand(
                $"schtasks /query /tn \"{TaskName}\"", true, true);
            return !output.Contains("错误", StringComparison.OrdinalIgnoreCase) &&
                   !output.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
                   !output.Contains("[Error]", StringComparison.OrdinalIgnoreCase);
        }

        // Creates the logon task with /rl highest so the powercfg call succeeds,
        // and /f to overwrite any prior copy (e.g. a different target GUID).
        private bool EnsureAutoSwitchTask(string planGuid)
        {
            string command = $"powercfg /setactive {planGuid}";
            string cmd = $"schtasks /create /tn \"{TaskName}\" /tr \"{command}\" " +
                         $"/sc onlogon /rl highest /f";

            string output = CommandHelper.RunCommand(cmd, true, true);
            return !output.Contains("[Error]", StringComparison.OrdinalIgnoreCase);
        }

        private void RemoveAutoSwitchTask()
        {
            CommandHelper.RunCommand($"schtasks /delete /tn \"{TaskName}\" /f", true, true);
        }

        // Runs powercfg with the console code page forced to UTF-8 (chcp 65001)
        // and decodes stdout as UTF-8. This is necessary because powercfg's two
        // queries emit localized text under different encodings on a Chinese
        // Windows — /getactivescheme uses the ANSI code page while /list uses
        // UTF-8 — so relying on the system default mojibakes one or the other.
        // Pinning 65001 makes both queries return UTF-8 consistently.
        private static string RunPowerCfg(string arguments)
        {
            using Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c chcp 65001 >nul & powercfg {arguments}";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return output + (string.IsNullOrWhiteSpace(error) ? "" : "\n[Error]\n" + error);
        }
    }

    // Outcome of a delete attempt, distinct so the UI can tailor its message.
    public enum DeletePlanResult
    {
        Success,
        // The targeted scheme is currently active; Windows won't delete it.
        IsActive,
        Failed,
        Invalid
    }
}
