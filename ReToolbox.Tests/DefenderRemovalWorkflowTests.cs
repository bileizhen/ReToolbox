using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public class DefenderRemovalWorkflowTests
{
    private const string DefenderRemoverSha256 = "64ea442286170f73a9083b52ded50a2edf8b0f3708fdcbc8b5855c4183b2ce2f";

    [Theory]
    [InlineData(DefenderRemovalMode.Full, "y", false)]
    [InlineData(DefenderRemovalMode.AntivirusOnly, "a", true)]
    public void ModesMapToUpstreamSelections(
        DefenderRemovalMode mode,
        string expectedSelection,
        bool keepsWindowsSecurity)
    {
        DefenderRemovalProfile profile = DefenderRemovalWorkflow.GetProfile(mode);

        Assert.Equal(expectedSelection, profile.UpstreamSelection);
        Assert.Equal(keepsWindowsSecurity, profile.KeepsWindowsSecurity);
    }

    [Fact]
    public void ModesExplainTheirSecurityScope()
    {
        DefenderRemovalProfile full = DefenderRemovalWorkflow.GetProfile(DefenderRemovalMode.Full);
        DefenderRemovalProfile antivirusOnly = DefenderRemovalWorkflow.GetProfile(DefenderRemovalMode.AntivirusOnly);

        Assert.Equal("完整移除", full.DisplayName);
        Assert.Contains("Windows 安全中心", full.Description, StringComparison.Ordinal);
        Assert.Equal("仅移除杀毒引擎", antivirusOnly.DisplayName);
        Assert.Contains("保留 Windows 安全中心", antivirusOnly.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void PinsTheReviewedUpstreamRelease()
    {
        DefenderRemoverRelease release = DefenderRemovalWorkflow.CurrentRelease;

        Assert.Equal("release13-rev1", release.Tag);
        Assert.Equal("Defender.Remover.13.exe", release.FileName);
        Assert.Equal(1_348_608, release.Size);
        Assert.Equal(DefenderRemoverSha256, release.Sha256, ignoreCase: true);
        Assert.Equal("https", release.DownloadUri.Scheme);
        Assert.Equal("github.com", release.DownloadUri.Host);
    }

    [Fact]
    public void FeatureIsEnabledOnlyThroughThePinnedVerificationPath()
    {
        string service = File.ReadAllText(RepoFile("ReToolbox", "Services", "DefenderService.cs"));
        string page = File.ReadAllText(RepoFile("ReToolbox", "Views", "DefenderPage.xaml.cs"));

        Assert.Contains("ArtifactIntegrity.HasExpectedSha256Async", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowUnverifiedAdministratorTools", service, StringComparison.Ordinal);
        Assert.Contains("RemoveDefenderCommand.ExecuteAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("移除 Defender（已禁用）", page, StringComparison.Ordinal);
        Assert.DoesNotContain("下载与执行已禁用", page, StringComparison.Ordinal);
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
