using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public class ActivationWorkflowTests
{
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
        string page = File.ReadAllText(RepoFile("ReToolbox", "Views", "ActivationPage.xaml"));
        string pageCode = File.ReadAllText(RepoFile("ReToolbox", "Views", "ActivationPage.xaml.cs"));

        Assert.Contains("ActivationWorkflow.CurrentRelease", service, StringComparison.Ordinal);
        Assert.Contains("ArtifactIntegrity.HasExpectedSha256Async", service, StringComparison.Ordinal);
        Assert.Contains("SecureStagingDirectory.Create", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowRemoteActivationScripts", service, StringComparison.Ordinal);
        Assert.DoesNotContain("（已禁用）", page, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnabled=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("ActivateCommand.ExecuteAsync", pageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("远程激活脚本执行已禁用", pageCode, StringComparison.Ordinal);
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
