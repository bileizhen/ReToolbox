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
            "Updater",
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
    public void StartingDiagnosticsRemovesOnlyOwnedLogsOlderThanSevenDays()
    {
        Directory.CreateDirectory(_sandbox);
        string expiredLog = Path.Combine(
            _sandbox,
            "ReToolbox-20200101-000000-100.log");
        string recentLog = Path.Combine(
            _sandbox,
            "ReToolbox-20200102-000000-101.log");
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
    public async Task FeedbackArchiveContainsOnlySanitizedApplicationLogsAndMetadata()
    {
        var service = new DiagnosticLogService(_sandbox);
        service.WriteError(
            "Application",
            $"启动失败：{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}",
            new InvalidOperationException("测试错误"));
        File.WriteAllText(
            Path.Combine(_sandbox, "activation-20260101.log"),
            "activation output");
        File.WriteAllText(
            Path.Combine(_sandbox, "notes.txt"),
            "unrelated");
        File.WriteAllText(
            Path.Combine(_sandbox, "ReToolbox-secret.log"),
            "lookalike");
        string archivePath = Path.Combine(_sandbox, "feedback.zip");

        await service.CreateFeedbackArchiveAsync(archivePath);

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        ZipArchiveEntry logEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName.StartsWith(
                "logs/",
                StringComparison.Ordinal));
        Assert.Equal(
            $"logs/{Path.GetFileName(service.CurrentLogPath)}",
            logEntry.FullName);
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName.Contains("activation", StringComparison.OrdinalIgnoreCase));
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

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }
}
