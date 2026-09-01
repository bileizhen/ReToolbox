using ReToolbox.Services;
using ReToolbox.Utils;
using Xunit;

namespace ReToolbox.Tests;

public class UpdateWorkflowTests
{
    [Fact]
    public void NewerSemanticReleaseIsDetected()
    {
        Assert.True(UpdateWorkflow.IsNewerRelease(
            "v1.10.0",
            new Version(1, 9, 9)));
    }

    [Fact]
    public void LatestReleaseRequiresVerifiedInstallerAsset()
    {
        const string sha256 = "b02f361b6df653427b333b2ec6fa4453093b75d7d62548bcd757fdbdcb450428";
        string json = $$"""
            {
              "tag_name": "v1.7.0",
              "html_url": "https://github.com/bileizhen/ReToolbox/releases/tag/v1.7.0",
              "assets": [
                {
                  "name": "ReToolbox-Setup.exe.sha256",
                  "size": 86,
                  "digest": "sha256:unused",
                  "browser_download_url": "https://github.com/bileizhen/ReToolbox/releases/download/v1.7.0/ReToolbox-Setup.exe.sha256"
                },
                {
                  "name": "ReToolbox-Setup.exe",
                  "state": "uploaded",
                  "size": 46827198,
                  "digest": "sha256:{{sha256}}",
                  "browser_download_url": "https://github.com/bileizhen/ReToolbox/releases/download/v1.7.0/ReToolbox-Setup.exe"
                }
              ]
            }
            """;

        Assert.True(UpdateWorkflow.TryReadLatestRelease(
            json,
            out UpdateRelease? release));
        Assert.NotNull(release);
        Assert.Equal(new Version(1, 7, 0), release.Version);
        Assert.Equal(46_827_198, release.InstallerSize);
        Assert.Equal(sha256, release.InstallerSha256);
    }

    [Fact]
    public void RecommendedGitHubProxyPreservesTheFullSourceUrl()
    {
        Uri source = new Uri(
            "https://github.com/bileizhen/ReToolbox/releases/download/v1.7.0/ReToolbox-Setup.exe");

        Uri mirrored = GitHubUrlRouting.BuildMirroredUri(
            "https://ghfile.geekertao.top",
            source);

        Assert.Equal(
            "https://ghfile.geekertao.top/https://github.com/bileizhen/ReToolbox/releases/download/v1.7.0/ReToolbox-Setup.exe",
            mirrored.AbsoluteUri);
    }

    [Fact]
    public void InstallerWithoutGitHubDigestIsRejected()
    {
        const string json = """
            {
              "tag_name": "v1.7.0",
              "html_url": "https://github.com/bileizhen/ReToolbox/releases/tag/v1.7.0",
              "assets": [
                {
                  "name": "ReToolbox-Setup.exe",
                  "state": "uploaded",
                  "size": 46827198,
                  "browser_download_url": "https://github.com/bileizhen/ReToolbox/releases/download/v1.7.0/ReToolbox-Setup.exe"
                }
              ]
            }
            """;

        Assert.False(UpdateWorkflow.TryReadLatestRelease(
            json,
            out UpdateRelease? release));
        Assert.Null(release);
    }

    [Fact]
    public void InstallerOutsideTheReToolboxReleasePathIsRejected()
    {
        const string json = """
            {
              "tag_name": "v1.7.0",
              "html_url": "https://github.com/bileizhen/ReToolbox/releases/tag/v1.7.0",
              "assets": [
                {
                  "name": "ReToolbox-Setup.exe",
                  "state": "uploaded",
                  "size": 46827198,
                  "digest": "sha256:b02f361b6df653427b333b2ec6fa4453093b75d7d62548bcd757fdbdcb450428",
                  "browser_download_url": "https://github.com/example/other/releases/download/v1.7.0/ReToolbox-Setup.exe"
                }
              ]
            }
            """;

        Assert.False(UpdateWorkflow.TryReadLatestRelease(
            json,
            out UpdateRelease? release));
        Assert.Null(release);
    }

    [Fact]
    public void UpdaterOwnsOnlyItsImmediateGuidStagingDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "ProgramData");
        string owned = Path.Combine(
            root,
            "ReToolbox-Update-0123456789abcdef0123456789abcdef");

        Assert.True(UpdateWorkflow.IsOwnedUpdateDirectory(owned, root));
        Assert.False(UpdateWorkflow.IsOwnedUpdateDirectory(
            Path.Combine(root, "ReToolbox-Update-not-a-guid"),
            root));
        Assert.False(UpdateWorkflow.IsOwnedUpdateDirectory(
            Path.Combine(root, "Other", Path.GetFileName(owned)),
            root));
    }

    [Fact]
    public void InstallerLaunchCopyIsPlacedBesideTheRunningApplication()
    {
        string applicationDirectory = Path.Combine(
            Path.GetTempPath(),
            "ReToolbox-Installed");
        Guid launchId = Guid.ParseExact(
            "0123456789abcdef0123456789abcdef",
            "N");

        string launchPath = UpdateWorkflow.CreatePolicyCompatibleLaunchPath(
            applicationDirectory,
            launchId);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(applicationDirectory),
                "ReToolbox-Update-0123456789abcdef0123456789abcdef.exe"),
            launchPath);
        Assert.True(UpdateWorkflow.IsOwnedLaunchCopy(
            launchPath,
            applicationDirectory));
        Assert.False(UpdateWorkflow.IsOwnedLaunchCopy(
            Path.Combine(
                Path.GetTempPath(),
                Path.GetFileName(launchPath)),
            applicationDirectory));
    }
}
