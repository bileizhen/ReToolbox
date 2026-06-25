using System;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class WindowsUpdateService
    {
        private const string WU_KEY = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        private const string WU_AU_KEY = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
        private const string WU_UX_KEY = @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        private const string POLICY_DISABLE_UX_ACCESS = "SetDisableUXWUAccess";
        private const string POLICY_DISABLE_PAUSE_UX = "SetDisablePauseUXAccess";

        private static readonly string[] PauseValueNames =
        {
            "PauseFeatureUpdatesStartTime",
            "PauseFeatureUpdatesEndTime",
            "PauseQualityUpdatesStartTime",
            "PauseQualityUpdatesEndTime",
            "PauseUpdatesStartTime",
            "PauseUpdatesExpiryTime"
        };

        private static readonly string[] PolicyValueNames =
        {
            "ManagePreviewBuilds",
            "ManagePreviewBuildsPolicyValue",
            "TargetReleaseVersion",
            "ProductVersion",
            "AUPowerManagement",
            "SetDisableUXWUAccess",
            "SetDisablePauseUXAccess",
            "DeferQualityUpdates",
            "DeferQualityUpdatesPeriodInDays",
            "DeferFeatureUpdates",
            "DeferFeatureUpdatesPeriodInDays",
            "ExcludeWUDriversInQualityUpdate",
            "BranchReadinessLevel"
        };

        private static readonly string[] AuPolicyValueNames =
        {
            "NoAutoUpdate",
            "NoAUAsDefaultShutdownOption",
            "AUOptions",
            "NoAutoRebootWithLoggedOnUsers",
            "ScheduledInstallDay",
            "ScheduledInstallTime",
            "UseWUServer"
        };

        private static readonly string[] UxCleanupValueNames =
        {
            "HideMCTLink",
            "FlightSettingsMaxPauseDays"
        };

        public bool IsUpdatePaused()
        {
            return RegistryHelper.IsMatch(WU_AU_KEY, "NoAutoUpdate", 1) ||
                   RegistryHelper.IsMatch(WU_KEY, POLICY_DISABLE_UX_ACCESS, 1);
        }

        public void PauseUpdates()
        {
            RegistryHelper.SetValue(WU_AU_KEY, "NoAutoUpdate", 1, Microsoft.Win32.RegistryValueKind.DWord);
            RegistryHelper.SetValue(WU_KEY, POLICY_DISABLE_UX_ACCESS, 1, Microsoft.Win32.RegistryValueKind.DWord);
            RegistryHelper.DeleteValue(WU_KEY, POLICY_DISABLE_PAUSE_UX);
            ClearUxPauseState();
        }

        public void ResumeUpdates()
        {
            RegistryHelper.SetValue(WU_AU_KEY, "NoAutoUpdate", 0, Microsoft.Win32.RegistryValueKind.DWord);
            RegistryHelper.DeleteValue(WU_AU_KEY, "NoAutoUpdate");
            RegistryHelper.DeleteValue(WU_KEY, POLICY_DISABLE_UX_ACCESS);
            RegistryHelper.DeleteValue(WU_KEY, POLICY_DISABLE_PAUSE_UX);
            ClearUxPauseState();
            CleanupPolicyKeys();
        }

        public void SetUpdateDeferal(int days)
        {
            days = Math.Clamp(days, 1, 35);
            ApplyPauseDuration(days);
        }

        public void ExtendPauseForTenYears()
        {
            const int tenYearsInDays = 3650;

            RegistryHelper.SetValue(WU_UX_KEY, "FlightSettingsMaxPauseDays", tenYearsInDays, Microsoft.Win32.RegistryValueKind.DWord);
            ApplyPauseDuration(tenYearsInDays);
        }

        public bool IsPauseExtendedForTenYears()
        {
            object? maxPauseDays = RegistryHelper.GetValue(WU_UX_KEY, "FlightSettingsMaxPauseDays");
            return maxPauseDays is int days && days >= 3650 && GetDeferalDays() > 365;
        }

        public void ClearUpdateDeferal()
        {
            ClearUxPauseState();
            RegistryHelper.DeleteValue(WU_UX_KEY, "FlightSettingsMaxPauseDays");
            RegistryHelper.DeleteValue(WU_KEY, "DeferQualityUpdates");
            RegistryHelper.DeleteValue(WU_KEY, "DeferQualityUpdatesPeriodInDays");
            RegistryHelper.DeleteValue(WU_KEY, "DeferFeatureUpdates");
            RegistryHelper.DeleteValue(WU_KEY, "DeferFeatureUpdatesPeriodInDays");
            CleanupPolicyKeys();
        }

        public int GetDeferalDays()
        {
            DateTimeOffset? expiryTime = GetPauseExpiryTime();
            if (expiryTime is not null)
            {
                TimeSpan remaining = expiryTime.Value - DateTimeOffset.UtcNow;
                return (int)Math.Ceiling(remaining.TotalDays);
            }

            return 0;
        }

        public DateTimeOffset? GetPauseExpiryTime()
        {
            object? expiryValue = RegistryHelper.GetValue(WU_UX_KEY, "PauseUpdatesExpiryTime");
            if (expiryValue is string expiryText &&
                DateTimeOffset.TryParse(expiryText, out DateTimeOffset expiryTime) &&
                expiryTime > DateTimeOffset.UtcNow)
            {
                return expiryTime;
            }

            return null;
        }

        public void DisableDriverUpdates()
        {
            RegistryHelper.SetValue(WU_KEY, "ExcludeWUDriversInQualityUpdate", 1, Microsoft.Win32.RegistryValueKind.DWord);
        }

        public void EnableDriverUpdates()
        {
            RegistryHelper.DeleteValue(WU_KEY, "ExcludeWUDriversInQualityUpdate");
            CleanupPolicyKeys();
        }

        public bool AreDriverUpdatesDisabled()
        {
            return RegistryHelper.IsMatch(WU_KEY, "ExcludeWUDriversInQualityUpdate", 1);
        }

        public void ResetToDefaultPolicies()
        {
            foreach (string valueName in PolicyValueNames)
            {
                RegistryHelper.DeleteValue(WU_KEY, valueName);
            }

            foreach (string valueName in AuPolicyValueNames)
            {
                RegistryHelper.DeleteValue(WU_AU_KEY, valueName);
            }

            foreach (string valueName in PauseValueNames)
            {
                RegistryHelper.DeleteValue(WU_UX_KEY, valueName);
            }

            foreach (string valueName in UxCleanupValueNames)
            {
                RegistryHelper.DeleteValue(WU_UX_KEY, valueName);
            }

            CleanupPolicyKeys();
        }

        private void ClearUxPauseState()
        {
            foreach (string valueName in PauseValueNames)
            {
                RegistryHelper.DeleteValue(WU_UX_KEY, valueName);
            }
        }

        private void ApplyPauseDuration(int days)
        {
            RegistryHelper.DeleteValue(WU_KEY, "SetDisablePauseUXAccess");
            RegistryHelper.DeleteValue(WU_KEY, "DeferQualityUpdates");
            RegistryHelper.DeleteValue(WU_KEY, "DeferQualityUpdatesPeriodInDays");
            RegistryHelper.DeleteValue(WU_KEY, "DeferFeatureUpdates");
            RegistryHelper.DeleteValue(WU_KEY, "DeferFeatureUpdatesPeriodInDays");

            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            DateTimeOffset endTime = startTime.AddDays(days);
            string startText = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string endText = endTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

            RegistryHelper.SetValue(WU_UX_KEY, "PauseFeatureUpdatesStartTime", startText);
            RegistryHelper.SetValue(WU_UX_KEY, "PauseFeatureUpdatesEndTime", endText);
            RegistryHelper.SetValue(WU_UX_KEY, "PauseQualityUpdatesStartTime", startText);
            RegistryHelper.SetValue(WU_UX_KEY, "PauseQualityUpdatesEndTime", endText);
            RegistryHelper.SetValue(WU_UX_KEY, "PauseUpdatesStartTime", startText);
            RegistryHelper.SetValue(WU_UX_KEY, "PauseUpdatesExpiryTime", endText);
        }

        private void CleanupPolicyKeys()
        {
            if (RegistryHelper.IsKeyEmpty(WU_AU_KEY))
            {
                RegistryHelper.DeleteKey(WU_AU_KEY);
            }

            if (RegistryHelper.IsKeyEmpty(WU_KEY))
            {
                RegistryHelper.DeleteKey(WU_KEY);
            }

            if (RegistryHelper.IsKeyEmpty(WU_UX_KEY))
            {
                RegistryHelper.DeleteKey(WU_UX_KEY);
            }
        }
    }
}
