using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace ReToolbox.Services
{
    public enum DiskCleanupRisk
    {
        Safe,
        Recoverable
    }

    public sealed class DiskCleanupTarget
    {
        public DiskCleanupTarget(string path, TimeSpan minimumAge)
        {
            Path = path;
            MinimumAge = minimumAge;
        }

        public string Path { get; }
        public TimeSpan MinimumAge { get; }
    }

    public sealed class DiskCleanupRule
    {
        public DiskCleanupRule(
            string id,
            string category,
            string name,
            string description,
            DiskCleanupRisk risk,
            bool isRecommended,
            IReadOnlyList<DiskCleanupTarget> targets)
        {
            Id = id;
            Category = category;
            Name = name;
            Description = description;
            Risk = risk;
            IsRecommended = isRecommended;
            Targets = targets;
        }

        public string Id { get; }
        public string Category { get; }
        public string Name { get; }
        public string Description { get; }
        public DiskCleanupRisk Risk { get; }
        public bool IsRecommended { get; }
        public IReadOnlyList<DiskCleanupTarget> Targets { get; }
    }

    public sealed record DiskCleanupScanItem(
        string Id,
        string Category,
        string Name,
        string Description,
        DiskCleanupRisk Risk,
        bool IsRecommended,
        long SizeBytes,
        int FileCount);

    public sealed record DiskCleanupRunResult(
        long FreedBytes,
        int DeletedFiles,
        int FailedFiles);

    internal sealed record DiskCleanupConfiguration(
        IReadOnlyList<DiskCleanupRule> Rules,
        IReadOnlyList<string> AllowedRoots);

    public static class DiskCleanupCatalog
    {
        public static IReadOnlyList<DiskCleanupRule> CreateDefaultRules()
        {
            string local = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            string windows = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            string temporary = Path.TrimEndingDirectorySeparator(
                Path.GetTempPath());

            var rules = new List<DiskCleanupRule>
            {
                Rule(
                    "system.user-temp",
                    "系统缓存",
                    "用户临时文件",
                    "超过 24 小时未修改的临时内容；正在使用或无法访问的文件会自动跳过。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    Target(temporary, TimeSpan.FromDays(1))),
                Rule(
                    "system.windows-temp",
                    "系统缓存",
                    "Windows 临时文件",
                    "超过 7 天未修改的系统临时内容，不包含 Windows Update 数据库。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    Target(Path.Combine(windows, "Temp"), TimeSpan.FromDays(7))),
                Rule(
                    "system.shader-cache",
                    "系统缓存",
                    "DirectX 着色器缓存",
                    "显卡驱动和应用可按需重新生成的着色器缓存。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    Target(Path.Combine(local, "D3DSCache"), TimeSpan.Zero)),
                Rule(
                    "system.crash-reports",
                    "诊断数据",
                    "崩溃报告与转储",
                    "Windows 错误报告队列和应用崩溃转储，不包含个人文档。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    Targets(
                        Path.Combine(local, "CrashDumps"),
                        Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"),
                        Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue"),
                        Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
                        Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"))),
                Rule(
                    "browser.edge-cache",
                    "浏览器缓存",
                    "Microsoft Edge 缓存",
                    "网页资源、脚本和 GPU 缓存；不会删除书签、密码、Cookie 或浏览历史。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    ChromiumTargets(Path.Combine(
                        local,
                        "Microsoft",
                        "Edge",
                        "User Data"))),
                Rule(
                    "browser.chrome-cache",
                    "浏览器缓存",
                    "Google Chrome 缓存",
                    "网页资源、脚本和 GPU 缓存；不会删除书签、密码、Cookie 或浏览历史。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    ChromiumTargets(Path.Combine(
                        local,
                        "Google",
                        "Chrome",
                        "User Data"))),
                Rule(
                    "browser.firefox-cache",
                    "浏览器缓存",
                    "Mozilla Firefox 缓存",
                    "各 Firefox 配置的 cache2 网页缓存，不触碰配置和登录数据。",
                    DiskCleanupRisk.Safe,
                    recommended: true,
                    FirefoxTargets(Path.Combine(
                        local,
                        "Mozilla",
                        "Firefox",
                        "Profiles"))),
                Rule(
                    "app.discord-cache",
                    "应用缓存",
                    "Discord 渲染缓存",
                    "Electron 网页、脚本和 GPU 缓存；建议关闭 Discord 后清理。",
                    DiskCleanupRisk.Safe,
                    recommended: false,
                    Targets(
                        Path.Combine(roaming, "discord", "Cache"),
                        Path.Combine(roaming, "discord", "Code Cache"),
                        Path.Combine(roaming, "discord", "GPUCache"))),
                Rule(
                    "dev.nuget-cache",
                    "开发工具",
                    "NuGet 下载缓存",
                    "仅清理 HTTP 与插件缓存；不会删除全局包目录，但之后可能需要重新下载。",
                    DiskCleanupRisk.Recoverable,
                    recommended: false,
                    Targets(
                        Path.Combine(local, "NuGet", "v3-cache"),
                        Path.Combine(local, "NuGet", "plugins-cache"))),
                Rule(
                    "dev.npm-cache",
                    "开发工具",
                    "npm 内容缓存",
                    "npm 可重新下载的内容寻址缓存，不触碰任何项目中的 node_modules。",
                    DiskCleanupRisk.Recoverable,
                    recommended: false,
                    Target(
                        Path.Combine(local, "npm-cache", "_cacache"),
                        TimeSpan.Zero)),
                Rule(
                    "dev.pip-cache",
                    "开发工具",
                    "Python pip 缓存",
                    "pip 下载和构建缓存，不触碰虚拟环境或项目文件。",
                    DiskCleanupRisk.Recoverable,
                    recommended: false,
                    Target(
                        Path.Combine(local, "pip", "Cache"),
                        TimeSpan.Zero))
            };

            return rules
                .Where(rule => rule.Targets.Count > 0)
                .ToArray();
        }

        internal static DiskCleanupConfiguration CreateDefaultConfiguration()
        {
            IReadOnlyList<DiskCleanupRule> rules = CreateDefaultRules();
            string[] allowedRoots = rules
                .SelectMany(rule => rule.Targets)
                .Select(target => Path.GetDirectoryName(
                    Path.GetFullPath(target.Path)))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new DiskCleanupConfiguration(rules, allowedRoots);
        }

        private static DiskCleanupRule Rule(
            string id,
            string category,
            string name,
            string description,
            DiskCleanupRisk risk,
            bool recommended,
            params DiskCleanupTarget[] targets)
        {
            return new DiskCleanupRule(
                id,
                category,
                name,
                description,
                risk,
                recommended,
                targets
                    .Where(target => !string.IsNullOrWhiteSpace(target.Path))
                    .ToArray());
        }

        private static DiskCleanupTarget Target(
            string path,
            TimeSpan minimumAge)
        {
            return new DiskCleanupTarget(path, minimumAge);
        }

        private static DiskCleanupTarget[] Targets(params string[] paths)
        {
            return paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Target(path, TimeSpan.Zero))
                .ToArray();
        }

        private static DiskCleanupTarget[] ChromiumTargets(string userDataRoot)
        {
            var profiles = new List<string>
            {
                Path.Combine(userDataRoot, "Default")
            };
            TryAddDirectories(userDataRoot, "Profile *", profiles);

            return profiles
                .SelectMany(profile => new[]
                {
                    Path.Combine(profile, "Cache"),
                    Path.Combine(profile, "Code Cache"),
                    Path.Combine(profile, "GPUCache")
                })
                .Select(path => Target(path, TimeSpan.Zero))
                .ToArray();
        }

        private static DiskCleanupTarget[] FirefoxTargets(string profilesRoot)
        {
            var profiles = new List<string>();
            TryAddDirectories(profilesRoot, "*", profiles);
            return profiles
                .Select(profile => Target(
                    Path.Combine(profile, "cache2"),
                    TimeSpan.Zero))
                .ToArray();
        }

        private static void TryAddDirectories(
            string root,
            string pattern,
            ICollection<string> destination)
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    return;
                }

                foreach (string path in Directory.EnumerateDirectories(
                             root,
                             pattern,
                             SearchOption.TopDirectoryOnly))
                {
                    destination.Add(path);
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or SecurityException)
            {
            }
        }
    }
}
