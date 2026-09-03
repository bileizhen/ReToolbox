using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public class SoftwareCatalogTests
{
    [Fact]
    public void CataloguedSoftwareUsesSupportedDistributionChannels()
    {
        SoftwareCatalogEntry huorong = SoftwareCatalog.Entries.Single(entry => entry.Name == "火绒安全");
        Assert.Equal("XPDNH1FMW7NB40", huorong.WingetId);
        Assert.Equal("msstore", huorong.WingetSource);
        Assert.Null(huorong.OfficialPageUri);

        SoftwareCatalogEntry kaspersky = SoftwareCatalog.Entries.Single(entry => entry.Name == "卡巴斯基免费版");
        Assert.Equal(string.Empty, kaspersky.WingetId);
        Assert.Equal(
            new Uri("https://www.kaspersky.com.cn/downloads/free-antivirus"),
            kaspersky.OfficialPageUri);

        SoftwareCatalogEntry geek = SoftwareCatalog.Entries.Single(entry => entry.Name == "Geek Uninstaller");
        Assert.Equal("GeekUninstaller.GeekUninstaller", geek.WingetId);
        Assert.Equal("winget", geek.WingetSource);
        Assert.Null(geek.OfficialPageUri);

        SoftwareCatalogEntry steam = SoftwareCatalog.Entries.Single(entry => entry.Name == "Steam");
        Assert.Equal("Valve.Steam", steam.WingetId);
        Assert.Equal("winget", steam.WingetSource);
        Assert.Null(steam.OfficialPageUri);

        SoftwareCatalogEntry officeTool = SoftwareCatalog.Entries.Single(entry => entry.Name == "Office Tool Plus");
        Assert.Equal(string.Empty, officeTool.WingetId);
        Assert.Equal(
            new Uri("https://github.com/YerongAI/Office-Tool"),
            officeTool.OfficialPageUri);
        Assert.Equal(
            @"^Office_Tool_with_runtime_.*_x64\.zip$",
            officeTool.GitHubRelease!.AssetNamePattern);
        Assert.Equal(
            @"^Office Tool Plus\.exe$",
            officeTool.GitHubRelease.ExecutableNamePattern);

        SoftwareCatalogEntry uuRemote = SoftwareCatalog.Entries.Single(entry => entry.Name == "UU远程");
        Assert.Equal("NetEase.UURemote", uuRemote.WingetId);
        Assert.Equal("winget", uuRemote.WingetSource);
        Assert.Null(uuRemote.OfficialPageUri);

        SoftwareCatalogEntry foliaMajor = SoftwareCatalog.Entries.Single(entry => entry.Name == "Folia Major");
        Assert.Equal(string.Empty, foliaMajor.WingetId);
        Assert.Equal(
            new Uri("https://github.com/chthollyphile/folia-major"),
            foliaMajor.OfficialPageUri);
        Assert.Equal(
            @"^Folia-Setup-.*\.exe$",
            foliaMajor.GitHubRelease!.AssetNamePattern);
        Assert.Equal(
            @"^Folia-Setup-.*\.exe$",
            foliaMajor.GitHubRelease.ExecutableNamePattern);
    }

    [Fact]
    public void GitHubReleaseAssetMustMatchTheCataloguedRepositoryAndSha256()
    {
        SoftwareCatalogEntry officeTool = SoftwareCatalog.Entries.Single(entry => entry.Name == "Office Tool Plus");
        const string sha256 =
            "68a9ebf8b569fa56a55a1b3601ec9260aeb2a3d9acfed1def039c4cc9e092c9f";
        string json = $$"""
            {
              "tag_name": "v11.6.6.0",
              "assets": [
                {
                  "name": "Office_Tool_with_runtime_v11.6.6.0_x64.zip",
                  "state": "uploaded",
                  "size": 81322475,
                  "digest": "sha256:{{sha256}}",
                  "browser_download_url": "https://github.com/YerongAI/Office-Tool/releases/download/v11.6.6.0/Office_Tool_with_runtime_v11.6.6.0_x64.zip"
                }
              ]
            }
            """;

        Assert.True(GitHubReleaseWorkflow.TryReadLatestAsset(
            json,
            officeTool.GitHubRelease!,
            out GitHubReleaseAsset? asset));
        Assert.NotNull(asset);
        Assert.Equal("Office_Tool_with_runtime_v11.6.6.0_x64.zip", asset!.FileName);
        Assert.Equal(sha256, asset.Sha256);
    }

    [Fact]
    public void WingetInstallArgumentsPinTheSelectedSource()
    {
        SoftwareCatalogEntry huorong = SoftwareCatalog.Entries.Single(entry => entry.Name == "火绒安全");
        SoftwareCatalogEntry geek = SoftwareCatalog.Entries.Single(entry => entry.Name == "Geek Uninstaller");
        SoftwareCatalogEntry steam = SoftwareCatalog.Entries.Single(entry => entry.Name == "Steam");
        SoftwareCatalogEntry uuRemote = SoftwareCatalog.Entries.Single(entry => entry.Name == "UU远程");

        Assert.Equal(
            new[]
            {
                "install", "--id", "XPDNH1FMW7NB40", "--exact",
                "--source", "msstore", "--accept-package-agreements",
                "--accept-source-agreements", "--silent", "--disable-interactivity"
            },
            SoftwareCatalog.BuildWingetInstallArguments(huorong));
        Assert.Contains("winget", SoftwareCatalog.BuildWingetInstallArguments(geek));
        Assert.Equal("Valve.Steam", SoftwareCatalog.BuildWingetInstallArguments(steam)[2]);
        Assert.Equal("NetEase.UURemote", SoftwareCatalog.BuildWingetInstallArguments(uuRemote)[2]);
    }

    [Fact]
    public void OnlyCataloguedOfficialInstallPagesAreAllowed()
    {
        Assert.True(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("https://www.kaspersky.com.cn/downloads/free-antivirus")));
        Assert.True(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("https://github.com/YerongAI/Office-Tool")));
        Assert.True(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("https://github.com/chthollyphile/folia-major")));
        Assert.False(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("http://www.kaspersky.com.cn/downloads/free-antivirus")));
        Assert.False(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("https://example.com/download.exe")));
    }

    [Fact]
    public void OnlyCataloguedGitHubInstallersCanBeAutoStarted()
    {
        GitHubReleaseDownload officeTool = SoftwareCatalog.Entries
            .Single(entry => entry.Name == "Office Tool Plus")
            .GitHubRelease!;

        Assert.True(SoftwareCatalog.IsApprovedGitHubRelease(officeTool));
        Assert.False(SoftwareCatalog.IsApprovedGitHubRelease(
            new GitHubReleaseDownload(
                "unknown/repository",
                @"^setup\.exe$",
                @"^setup\.exe$")));
    }
}
