using System;
using System.Collections.Generic;
using System.Linq;

namespace ReToolbox.Services
{
    public sealed record ActivationScriptRelease(
        string Tag,
        string Commit,
        string FileName,
        Uri DownloadUri,
        string Sha256,
        long Size,
        string ActivationSwitch);

    public enum ActivationOutcome
    {
        None,
        Activated,
        Failed
    }

    public sealed record ActivationResult(
        ActivationOutcome Outcome,
        string Message,
        string? DiagnosticLogPath = null);

    public sealed record WindowsLicenseSnapshot(
        string ApplicationId,
        string? LicenseDependsOn,
        string? PartialProductKey,
        int LicenseStatus);

    public static class ActivationWorkflow
    {
        public const string WindowsApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

        public static ActivationScriptRelease CurrentRelease { get; } = new ActivationScriptRelease(
            "3.11",
            "b9906472628468de9f6e53b00cf5b06c318e8b96",
            "MAS_AIO.cmd",
            new Uri("https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/b9906472628468de9f6e53b00cf5b06c318e8b96/MAS/All-In-One-Version-KL/MAS_AIO.cmd"),
            "a0a6f670c9eb25468e9d41c9c2fc511b310250b31b43d02ef7c5694532dbba95",
            762_453,
            "/HWID-NoEditionChange");

        public static bool IsWindowsActivated(
            IEnumerable<WindowsLicenseSnapshot> licenses)
        {
            return licenses.Any(license =>
                string.Equals(
                    license.ApplicationId,
                    WindowsApplicationId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(license.LicenseDependsOn) &&
                !string.IsNullOrWhiteSpace(license.PartialProductKey) &&
                license.LicenseStatus == 1);
        }
    }
}
