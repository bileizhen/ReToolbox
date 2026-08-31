using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public sealed class DiskCleanupWorkflowTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(
        Path.GetTempPath(),
        $"ReToolbox-DiskCleanup-Test-{Guid.NewGuid():N}");

    [Fact]
    public async Task ScanReportsOnlyFilesInsideAnAllowedCleanupTarget()
    {
        string allowedRoot = Path.Combine(_sandbox, "allowed");
        string cacheRoot = Path.Combine(allowedRoot, "cache");
        Directory.CreateDirectory(Path.Combine(cacheRoot, "nested"));
        await File.WriteAllBytesAsync(
            Path.Combine(cacheRoot, "first.tmp"),
            new byte[] { 1, 2, 3, 4, 5 });
        await File.WriteAllBytesAsync(
            Path.Combine(cacheRoot, "nested", "second.tmp"),
            new byte[] { 6, 7, 8 });

        var service = new DiskCleanupService(
            new[]
            {
                new DiskCleanupRule(
                    "test.cache",
                    "测试",
                    "测试缓存",
                    "可重新生成的测试内容",
                    DiskCleanupRisk.Safe,
                    isRecommended: true,
                    new[] { new DiskCleanupTarget(cacheRoot, TimeSpan.Zero) })
            },
            new[] { allowedRoot });

        IReadOnlyList<DiskCleanupScanItem> result =
            await service.ScanAsync();

        DiskCleanupScanItem item = Assert.Single(result);
        Assert.Equal("test.cache", item.Id);
        Assert.Equal(2, item.FileCount);
        Assert.Equal(8, item.SizeBytes);
        Assert.True(item.IsRecommended);
    }

    [Fact]
    public async Task CleanDeletesOnlySelectedRuleContentsAndPreservesRoots()
    {
        string allowedRoot = Path.Combine(_sandbox, "allowed");
        string selectedRoot = Path.Combine(allowedRoot, "selected-cache");
        string retainedRoot = Path.Combine(allowedRoot, "retained-cache");
        Directory.CreateDirectory(selectedRoot);
        Directory.CreateDirectory(retainedRoot);
        await File.WriteAllBytesAsync(
            Path.Combine(selectedRoot, "remove.tmp"),
            new byte[] { 1, 2, 3, 4 });
        await File.WriteAllBytesAsync(
            Path.Combine(retainedRoot, "keep.tmp"),
            new byte[] { 5, 6 });

        var service = new DiskCleanupService(
            new[]
            {
                Rule("selected", selectedRoot),
                Rule("retained", retainedRoot)
            },
            new[] { allowedRoot });

        DiskCleanupRunResult result = await service.CleanAsync(
            new[] { "selected" });

        Assert.Equal(4, result.FreedBytes);
        Assert.Equal(1, result.DeletedFiles);
        Assert.Equal(0, result.FailedFiles);
        Assert.True(Directory.Exists(selectedRoot));
        Assert.Empty(Directory.EnumerateFileSystemEntries(selectedRoot));
        Assert.True(File.Exists(Path.Combine(retainedRoot, "keep.tmp")));
    }

    [Fact]
    public void CatalogRejectsTargetsOutsideItsExplicitAllowedRoots()
    {
        string allowedRoot = Path.Combine(_sandbox, "allowed");
        string outsideRoot = Path.Combine(_sandbox, "outside-cache");

        Assert.Throws<ArgumentException>(() => new DiskCleanupService(
            new[] { Rule("outside", outsideRoot) },
            new[] { allowedRoot }));
    }

    [Fact]
    public void DefaultCatalogNeverRecommendsRecoverableContent()
    {
        IReadOnlyList<DiskCleanupRule> rules =
            DiskCleanupCatalog.CreateDefaultRules();

        Assert.NotEmpty(rules);
        Assert.Equal(
            rules.Count,
            rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            rules.Where(rule => rule.IsRecommended),
            rule => Assert.Equal(DiskCleanupRisk.Safe, rule.Risk));
    }

    [Fact]
    public async Task MinimumAgeKeepsRecentlyModifiedFiles()
    {
        string allowedRoot = Path.Combine(_sandbox, "allowed");
        string cacheRoot = Path.Combine(allowedRoot, "cache");
        Directory.CreateDirectory(cacheRoot);
        string recent = Path.Combine(cacheRoot, "recent.tmp");
        string old = Path.Combine(cacheRoot, "old.tmp");
        await File.WriteAllBytesAsync(recent, new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(old, new byte[] { 4, 5 });
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-2));

        var service = new DiskCleanupService(
            new[]
            {
                new DiskCleanupRule(
                    "aged",
                    "测试",
                    "旧缓存",
                    "仅清理旧文件",
                    DiskCleanupRisk.Safe,
                    isRecommended: true,
                    new[]
                    {
                        new DiskCleanupTarget(
                            cacheRoot,
                            TimeSpan.FromDays(1))
                    })
            },
            new[] { allowedRoot });

        DiskCleanupScanItem item = Assert.Single(await service.ScanAsync());

        Assert.Equal(1, item.FileCount);
        Assert.Equal(2, item.SizeBytes);
    }

    private static DiskCleanupRule Rule(string id, string path)
    {
        return new DiskCleanupRule(
            id,
            "测试",
            id,
            "测试缓存",
            DiskCleanupRisk.Safe,
            isRecommended: true,
            new[] { new DiskCleanupTarget(path, TimeSpan.Zero) });
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }
}
