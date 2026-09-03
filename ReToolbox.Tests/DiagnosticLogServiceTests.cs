using System.IO.Compression;
using System.Text.Json;
using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public sealed class DiagnosticLogServiceTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(
        Path.GetTempPath(),
        $"ReToolbox-DiagnosticLog-Test-{Guid.NewGuid():N}");

    [Fact]
    public void WritingAnErrorRedactsTheUserProfileAndUserName()
    {
        string profile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        string userName = Environment.UserName;
        var service = new DiagnosticLogService(_sandbox);

        service.WriteError(
            DiagnosticLogSource.Updater,
            $"无法打开 {Path.Combine(profile, "Downloads", "setup.exe")}（用户 {userName}）",
            new InvalidOperationException(
                $"拒绝访问 {Path.Combine(profile, "AppData", "Local", "Temp")}。"));

        string content = File.ReadAllText(service.CurrentLogPath);
        Assert.Contains("[ERROR] [Updater]", content, StringComparison.Ordinal);
        Assert.Contains("%USERPROFILE%", content, StringComparison.Ordinal);
        Assert.Contains("%USERNAME%", content, StringComparison.Ordinal);
        Assert.DoesNotContain(profile, content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(userName, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartingDiagnosticsImmediatelyCreatesTheWritableSessionLog()
    {
        var service = new DiagnosticLogService(_sandbox);

        Assert.True(service.IsAvailable);
        Assert.True(File.Exists(service.CurrentLogPath));
        using FileStream writable = new FileStream(
            service.CurrentLogPath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite);
        Assert.True(writable.CanWrite);
    }

    [Fact]
    public void StartingDiagnosticsRemovesOnlyOwnedLogsOlderThanSevenDays()
    {
        Directory.CreateDirectory(_sandbox);
        string expiredLog = Path.Combine(
            _sandbox,
            "ReToolbox-20200101-000000-100-0123456789abcdef0123456789abcdef.log");
        string recentLog = Path.Combine(
            _sandbox,
            "ReToolbox-20200102-000000-101-fedcba9876543210fedcba9876543210.log");
        string unrelatedFile = Path.Combine(_sandbox, "support.log");
        File.WriteAllText(expiredLog, "expired");
        File.WriteAllText(recentLog, "recent");
        File.WriteAllText(unrelatedFile, "unrelated");
        File.SetLastWriteTimeUtc(expiredLog, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(recentLog, DateTime.UtcNow.AddDays(-6));
        File.SetLastWriteTimeUtc(unrelatedFile, DateTime.UtcNow.AddDays(-30));

        _ = new DiagnosticLogService(_sandbox);

        Assert.False(File.Exists(expiredLog));
        Assert.True(File.Exists(recentLog));
        Assert.True(File.Exists(unrelatedFile));
    }

    [Fact]
    public async Task FeedbackArchiveContainsOwnedApplicationAndActivationLogs()
    {
        var service = new DiagnosticLogService(_sandbox);
        service.WriteError(
            DiagnosticLogSource.Application,
            $"启动失败：{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}",
            new InvalidOperationException("测试错误"));
        string activationLog = Path.Combine(
            _sandbox,
            "activation-20260903-044205-0123456789abcdef0123456789abcdef.log");
        File.WriteAllText(
            activationLog,
            "activation output");
        File.WriteAllText(
            Path.Combine(_sandbox, "activation-20260101.log"),
            "activation lookalike");
        File.WriteAllText(
            Path.Combine(_sandbox, "notes.txt"),
            "unrelated");
        File.WriteAllText(
            Path.Combine(_sandbox, "ReToolbox-secret.log"),
            "lookalike");
        string archivePath = Path.Combine(_sandbox, "feedback.zip");

        await service.CreateFeedbackArchiveAsync(archivePath);

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        string[] logEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal))
            .Select(entry => entry.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, logEntries.Length);
        Assert.Contains(
            $"logs/{Path.GetFileName(service.CurrentLogPath)}",
            logEntries);
        Assert.Contains(
            $"logs/{Path.GetFileName(activationLog)}",
            logEntries);
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName == "logs/activation-20260101.log");
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName.Contains("notes", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName.Contains("secret", StringComparison.OrdinalIgnoreCase));

        ZipArchiveEntry metadataEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == "diagnostics.json");
        using Stream metadataStream = metadataEntry.Open();
        using JsonDocument metadata = await JsonDocument.ParseAsync(metadataStream);
        Assert.True(metadata.RootElement.TryGetProperty("appVersion", out _));
        Assert.True(metadata.RootElement.TryGetProperty("operatingSystem", out _));
        Assert.True(metadata.RootElement.TryGetProperty("architecture", out _));
        Assert.False(metadata.RootElement.TryGetProperty("userName", out _));
        Assert.False(metadata.RootElement.TryGetProperty("machineName", out _));
    }

    [Fact]
    public void DiagnosticsFallsBackWhenThePreferredLogPathIsUnavailable()
    {
        Directory.CreateDirectory(_sandbox);
        string unavailablePath = Path.Combine(_sandbox, "blocked");
        string fallbackPath = Path.Combine(_sandbox, "fallback");
        File.WriteAllText(unavailablePath, "this path is a file");

        var service = new DiagnosticLogService(
            unavailablePath,
            fallbackPath);
        service.WriteInformation(
            DiagnosticLogSource.Application,
            "fallback active");

        Assert.True(service.IsAvailable);
        Assert.Equal(Path.GetFullPath(fallbackPath), service.LogDirectory);
        Assert.True(File.Exists(service.CurrentLogPath));
        Assert.Contains(
            "fallback active",
            File.ReadAllText(service.CurrentLogPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsBecomesNoOpWhenAllLogPathsAreUnavailable()
    {
        Directory.CreateDirectory(_sandbox);
        string unavailablePrimary = Path.Combine(_sandbox, "blocked-primary");
        string unavailableFallback = Path.Combine(_sandbox, "blocked-fallback");
        File.WriteAllText(unavailablePrimary, "file");
        File.WriteAllText(unavailableFallback, "file");

        var service = new DiagnosticLogService(
            unavailablePrimary,
            unavailableFallback);
        Exception? writeFailure = Record.Exception(() =>
            service.WriteInformation(
                DiagnosticLogSource.Application,
                "must not interrupt startup"));

        Assert.False(service.IsAvailable);
        Assert.Null(writeFailure);
        Assert.Empty(service.LogDirectory);
        Assert.Empty(service.CurrentLogPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }
}
