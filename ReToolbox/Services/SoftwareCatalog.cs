using System;
using System.Collections.Generic;
using System.Linq;

namespace ReToolbox.Services
{
    public sealed record SoftwareCatalogEntry(
        string Name,
        string WingetId,
        string WingetSource,
        Uri? OfficialPageUri,
        string Category,
        string Description,
        string IconGlyph,
        GitHubReleaseDownload? GitHubRelease = null);

    public sealed record GitHubReleaseDownload(
        string Repository,
        string AssetNamePattern);

    public enum SoftwareInstallOutcome
    {
        Installed,
        ManualActionRequired,
        Failed
    }

    public sealed record SoftwareInstallResult(
        SoftwareInstallOutcome Outcome,
        string Message);

    public static class SoftwareCatalog
    {
        public static IReadOnlyList<SoftwareCatalogEntry> Entries { get; } =
            new SoftwareCatalogEntry[]
            {
                new(
                    "火绒安全",
                    "XPDNH1FMW7NB40",
                    "msstore",
                    null,
                    "安全",
                    "火绒安全软件（Microsoft Store 免费版）",
                    "\uEA18"),
                new(
                    "卡巴斯基免费版",
                    string.Empty,
                    string.Empty,
                    new Uri("https://www.kaspersky.com.cn/downloads/free-antivirus"),
                    "安全",
                    "打开卡巴斯基官方免费版页面；产品是否可用取决于所在地区",
                    "\uEA18"),
                new(
                    "Geek Uninstaller",
                    "GeekUninstaller.GeekUninstaller",
                    "winget",
                    null,
                    "系统工具",
                    "轻量级软件卸载与残留清理工具",
                    "\uE74D"),
                new(
                    "Steam",
                    "Valve.Steam",
                    "winget",
                    null,
                    "游戏",
                    "Steam 游戏与软件平台",
                    "\uE7FC"),
                new(
                    "Office Tool Plus",
                    string.Empty,
                    string.Empty,
                    new Uri("https://github.com/YerongAI/Office-Tool"),
                    "办公",
                    "自动下载最新 x64 完整版 Release（GitHub 镜像优先）",
                    "\uE8A5",
                    new GitHubReleaseDownload(
                        "YerongAI/Office-Tool",
                        @"^Office_Tool_with_runtime_.*_x64\.zip$")),
                new(
                    "UU远程",
                    "NetEase.UURemote",
                    "winget",
                    null,
                    "远程工具",
                    "网易 UU 远程控制客户端",
                    "\uE7EF"),
                new(
                    "Folia Major",
                    string.Empty,
                    string.Empty,
                    new Uri("https://github.com/chthollyphile/folia-major"),
                    "媒体",
                    "自动下载最新 Windows x64 Release（GitHub 镜像优先）",
                    "\uEC4F",
                    new GitHubReleaseDownload(
                        "chthollyphile/folia-major",
                        @"^Folia-Setup-.*\.exe$"))
            };

        public static IReadOnlyList<string> BuildWingetInstallArguments(
            SoftwareCatalogEntry entry)
        {
            return BuildWingetInstallArguments(entry.WingetId, entry.WingetSource);
        }

        public static IReadOnlyList<string> BuildWingetInstallArguments(
            string wingetId,
            string wingetSource)
        {
            if (string.IsNullOrWhiteSpace(wingetId) ||
                wingetSource is not ("winget" or "msstore"))
            {
                throw new ArgumentException(
                    "The package does not define a supported winget distribution.");
            }

            return new[]
            {
                "install",
                "--id",
                wingetId,
                "--exact",
                "--source",
                wingetSource,
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--silent",
                "--disable-interactivity"
            };
        }

        public static bool IsApprovedOfficialPage(Uri pageUri)
        {
            return pageUri.Scheme == Uri.UriSchemeHttps &&
                   Entries.Any(entry => entry.OfficialPageUri == pageUri);
        }
    }
}
