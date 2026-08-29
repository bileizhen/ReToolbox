using System;

namespace ReToolbox.Services
{
    public enum DefenderRemovalMode
    {
        Full,
        AntivirusOnly
    }

    public sealed record DefenderRemovalProfile(
        string DisplayName,
        string Description,
        string UpstreamSelection,
        bool KeepsWindowsSecurity);

    public sealed record DefenderRemoverRelease(
        string Tag,
        string FileName,
        Uri DownloadUri,
        string Sha256,
        long Size);

    public static class DefenderRemovalWorkflow
    {
        public static DefenderRemoverRelease CurrentRelease { get; } = new DefenderRemoverRelease(
            "release13-rev1",
            "Defender.Remover.13.exe",
            new Uri("https://github.com/ionuttbara/windows-defender-remover/releases/download/release13-rev1/Defender.Remover.13.exe"),
            "64ea442286170f73a9083b52ded50a2edf8b0f3708fdcbc8b5855c4183b2ce2f",
            1_348_608);

        public static DefenderRemovalProfile GetProfile(DefenderRemovalMode mode)
        {
            return mode switch
            {
                DefenderRemovalMode.Full => new DefenderRemovalProfile(
                    "完整移除",
                    "移除 Defender 杀毒引擎和 Windows 安全中心，并关闭相关安全组件。",
                    "y",
                    false),
                DefenderRemovalMode.AntivirusOnly => new DefenderRemovalProfile(
                    "仅移除杀毒引擎",
                    "仅移除 Defender 杀毒引擎，保留 Windows 安全中心界面；Windows 更新可能恢复杀毒引擎。",
                    "a",
                    true),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }
    }
}
