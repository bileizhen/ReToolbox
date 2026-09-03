using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public class ActivationWorkflowTests
{
    private const string WindowsApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    [Fact]
    public void PinsReviewedMasHwidScript()
    {
        ActivationScriptRelease release = ActivationWorkflow.CurrentRelease;

        Assert.Equal("3.11", release.Tag);
        Assert.Equal("b9906472628468de9f6e53b00cf5b06c318e8b96", release.Commit);
        Assert.Equal("MAS_AIO.cmd", release.FileName);
        Assert.Equal(762_453, release.Size);
        Assert.Equal(
            "a0a6f670c9eb25468e9d41c9c2fc511b310250b31b43d02ef7c5694532dbba95",
            release.Sha256,
            ignoreCase: true);
        Assert.Equal("raw.githubusercontent.com", release.DownloadUri.Host);
        Assert.Equal("/HWID-NoEditionChange", release.ActivationSwitch);
    }

    [Fact]
    public void ActivationEntryUsesTheVerifiedWorkflowAndIsEnabled()
    {
        string service = File.ReadAllText(RepoFile("ReToolbox", "Services", "ActivationService.cs"));
        string workflow = File.ReadAllText(RepoFile("ReToolbox", "Services", "ActivationWorkflow.cs"));
        string downloader = File.ReadAllText(RepoFile("ReToolbox", "Utils", "VerifiedArtifactDownloader.cs"));
        string page = File.ReadAllText(RepoFile("ReToolbox", "Views", "ActivationPage.xaml"));
        string pageCode = File.ReadAllText(RepoFile("ReToolbox", "Views", "ActivationPage.xaml.cs"));

        Assert.Contains("ActivationWorkflow.CurrentRelease", service, StringComparison.Ordinal);
        Assert.Contains("VerifiedArtifactDownloader.DownloadAndOpenAsync", service, StringComparison.Ordinal);
        Assert.Contains("ArtifactIntegrity.HasExpectedSha256Async", downloader, StringComparison.Ordinal);
        Assert.Contains("SecureStagingDirectory.Create", service, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = true", service, StringComparison.Ordinal);
        Assert.Contains("run-mas.cmd", service, StringComparison.Ordinal);
        Assert.DoesNotContain("RedirectStandardOutput = true", service, StringComparison.Ordinal);
        Assert.Contains("DiagnosticLogPath", workflow, StringComparison.Ordinal);
        Assert.Contains("固定校验源下载失败", service, StringComparison.Ordinal);
        Assert.DoesNotContain("GETMASCN.ps1", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-RestMethod", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ActivationOutcome.AwaitingVerification", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowRemoteActivationScripts", service, StringComparison.Ordinal);
        Assert.DoesNotContain("（已禁用）", page, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnabled=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("ActivateCommand.ExecuteAsync", pageCode, StringComparison.Ordinal);
        Assert.Contains("OpenDiagnosticLog_Click", pageCode, StringComparison.Ordinal);
        Assert.Contains("notepad.exe", pageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("FileName = logPath", pageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("远程激活脚本执行已禁用", pageCode, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsMarkUsesThemeAwareForeground()
    {
        string page = File.ReadAllText(RepoFile("ReToolbox", "Views", "ActivationPage.xaml"));

        Assert.DoesNotContain("Fill=\"#F3F3F3\"", page, StringComparison.Ordinal);
        Assert.Equal(4, page.Split("Fill=\"{ThemeResource TextFillColorPrimaryBrush}\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void OnlyLicensedPrimaryWindowsProductsCountAsActivated()
    {
        WindowsLicenseSnapshot[] licenses =
        {
            new(WindowsApplicationId, null, "WINDOWS-KEY", 3),
            new("0ff1ce15-a989-479d-af46-f275c6370663", null, "OFFICE-KEY", 1),
            new(WindowsApplicationId, "parent-license", "ADDON-KEY", 1)
        };

        Assert.False(ActivationWorkflow.IsWindowsActivated(licenses));

        licenses = licenses.Append(
            new WindowsLicenseSnapshot(WindowsApplicationId, null, "WINDOWS-KEY", 1)).ToArray();

        Assert.True(ActivationWorkflow.IsWindowsActivated(licenses));
    }

    private static string RepoFile(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ReToolbox.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(segments).ToArray());
    }
}
