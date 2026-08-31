using ReToolbox.Utils;
using Xunit;

namespace ReToolbox.Tests;

public class SafetyWorkflowTests
{
    [Theory]
    [InlineData("https://gh-proxy.com", "https://gh-proxy.com")]
    [InlineData("gh-proxy.com/", "https://gh-proxy.com")]
    [InlineData("", "")]
    public void MirrorNormalizationAcceptsHttpsOnly(string input, string expected)
    {
        Assert.True(InputValidation.TryNormalizeHttpsOrigin(input, out string actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://user@example.com")]
    [InlineData("https://example.com?asset=1")]
    public void MirrorNormalizationRejectsUnsafeValues(string input)
    {
        Assert.False(InputValidation.TryNormalizeHttpsOrigin(input, out _));
    }

    [Fact]
    public void RetiredMirrorIsNotIncludedInBuiltInFallbacks()
    {
        string source = File.ReadAllText(
            RepoFile("ReToolbox", "Utils", "GitHubMirrorHelper.cs"));

        Assert.DoesNotContain("https://gh.llk.cc", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallerBuildRestoresBeforePublishing()
    {
        string script = File.ReadAllText(
            RepoFile("scripts", "build-installer.ps1"));

        Assert.DoesNotContain("-t:Restore,Publish", script, StringComparison.Ordinal);
        Assert.Contains("-t:Restore", script, StringComparison.Ordinal);
        Assert.Contains("-t:Publish", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Microsoft.PowerToys")]
    [InlineData("7zip.7zip")]
    [InlineData("Vendor.Package-Preview")]
    public void WingetIdsAcceptExpectedFormat(string id)
    {
        Assert.True(InputValidation.IsValidWingetId(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("package & calc")]
    [InlineData("--source evil")]
    [InlineData("a")]
    public void WingetIdsRejectUnsafeFormat(string id)
    {
        Assert.False(InputValidation.IsValidWingetId(id));
    }

    [Fact]
    public void ActivationServiceDoesNotPipeRemoteContentIntoPowerShell()
    {
        string source = File.ReadAllText(RepoFile("ReToolbox", "Services", "ActivationService.cs"));

        Assert.DoesNotContain("| iex", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-Expression", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DownloadString(", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoftwareCatalogUsesSupportedWingetPackagesOnly()
    {
        string source = File.ReadAllText(RepoFile("ReToolbox", "Services", "SoftwareInstallService.cs"));

        Assert.Contains("shinchiro.mpv", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadUrl =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowUnverifiedDirectInstallers", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallFromUrlAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadAndRunAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserFacingSourcesDoNotMarkFeaturesAsDisabled()
    {
        string root = Path.GetDirectoryName(RepoFile("ReToolbox", "ReToolbox.csproj"))!;
        string[] userFacingDirectories = { "Views", "ViewModels", "Services" };

        foreach (string directory in userFacingDirectories)
        {
            foreach (string path in Directory.EnumerateFiles(
                Path.Combine(root, directory),
                "*.*",
                SearchOption.AllDirectories))
            {
                if (Path.GetExtension(path) is not (".cs" or ".xaml"))
                {
                    continue;
                }

                Assert.DoesNotContain("禁用", File.ReadAllText(path), StringComparison.Ordinal);
            }
        }
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
