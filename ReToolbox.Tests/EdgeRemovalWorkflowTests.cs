using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public class EdgeRemovalWorkflowTests
{
    [Fact]
    public void PinsReviewedEdgeRemoverRelease()
    {
        EdgeRemovalScriptRelease release = EdgeRemovalWorkflow.CurrentRelease;

        Assert.Equal("v1.9.5", release.Tag);
        Assert.Equal("17220aca63d55d0d210d98004a504cbbcf25cb63", release.Commit);
        Assert.Equal("RemoveEdge.ps1", release.FileName);
        Assert.Equal(22_818, release.Size);
        Assert.Equal(
            "ca33fe16a9c6baf54b27d18864928fcb62b41886164606bd8dc6cda008d4b168",
            release.Sha256,
            ignoreCase: true);
        Assert.Equal("raw.githubusercontent.com", release.DownloadUri.Host);
        Assert.Equal(new[] { "-UninstallEdge", "-NonInteractive" }, release.Arguments);
    }

    [Fact]
    public void InstalledEdgeCanUseTheVerifiedRemovalWorkflow()
    {
        string service = File.ReadAllText(RepoFile("ReToolbox", "Services", "EdgeRemoverService.cs"));
        string page = File.ReadAllText(RepoFile("ReToolbox", "Views", "EdgeRemoverPage.xaml.cs"));

        Assert.Contains("EdgeRemovalWorkflow.CurrentRelease", service, StringComparison.Ordinal);
        Assert.Contains("VerifiedArtifactDownloader.DownloadAndOpenAsync", service, StringComparison.Ordinal);
        Assert.Contains("SecureStagingDirectory.Create", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowUnverifiedAdministratorTools", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-WebRequest", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh.llkk.cc", service, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UninstallEdgeCommand.ExecuteAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("卸载 Edge（已禁用）", page, StringComparison.Ordinal);
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
