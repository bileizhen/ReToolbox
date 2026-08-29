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
        string ExtractedRootDirectory,
        Uri DownloadUri,
        string Sha256,
        long Size);

    public sealed record DefenderRemovalResult(bool Success, string Message);

    public static class DefenderRemovalWorkflow
    {
        public static DefenderRemoverRelease CurrentRelease { get; } = new DefenderRemoverRelease(
            "release13-rev1",
            "windows-defender-remover-release13-rev1.zip",
            "windows-defender-remover-release13-rev1",
            new Uri("https://github.com/ionuttbara/windows-defender-remover/archive/refs/tags/release13-rev1.zip"),
            "c88881a0ebfe49fea282cf97f8409d8ba16501b4d74bb44d1ccf6df525190313",
            1_159_593);

        public static DefenderRemovalProfile GetProfile(DefenderRemovalMode mode)
        {
            return mode switch
            {
                DefenderRemovalMode.Full => new DefenderRemovalProfile(
                    "完整移除",
                    "移除 Defender 杀毒引擎和 Windows 安全中心，并调整 SmartScreen、VBS 与系统缓解等相关安全组件。",
                    "y",
                    false),
                DefenderRemovalMode.AntivirusOnly => new DefenderRemovalProfile(
                    "仅移除杀毒引擎",
                    "仅移除 Defender 杀毒引擎，保留 Windows 安全中心界面；Windows 更新可能恢复杀毒引擎，上游通用验证器会把保留的安全中心显示为未完全移除。",
                    "a",
                    true),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }
    }
}
