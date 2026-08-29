using System;

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
        AwaitingVerification,
        Failed
    }

    public sealed record ActivationResult(
        ActivationOutcome Outcome,
        string Message);

    public static class ActivationWorkflow
    {
        public static ActivationScriptRelease CurrentRelease { get; } = new ActivationScriptRelease(
            "3.11",
            "b9906472628468de9f6e53b00cf5b06c318e8b96",
            "MAS_AIO.cmd",
            new Uri("https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/b9906472628468de9f6e53b00cf5b06c318e8b96/MAS/All-In-One-Version-KL/MAS_AIO.cmd"),
            "a0a6f670c9eb25468e9d41c9c2fc511b310250b31b43d02ef7c5694532dbba95",
            762_453,
            "/HWID-NoEditionChange");
    }
}
