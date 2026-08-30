using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public class SoftwareCatalogTests
{
    [Fact]
    public void SecurityAndRemovalToolsUseTheirSupportedDistributionChannels()
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
    }

    [Fact]
    public void WingetInstallArgumentsPinTheSelectedSource()
    {
        SoftwareCatalogEntry huorong = SoftwareCatalog.Entries.Single(entry => entry.Name == "火绒安全");
        SoftwareCatalogEntry geek = SoftwareCatalog.Entries.Single(entry => entry.Name == "Geek Uninstaller");

        Assert.Equal(
            new[]
            {
                "install", "--id", "XPDNH1FMW7NB40", "--exact",
                "--source", "msstore", "--accept-package-agreements",
                "--accept-source-agreements", "--silent", "--disable-interactivity"
            },
            SoftwareCatalog.BuildWingetInstallArguments(huorong));
        Assert.Contains("winget", SoftwareCatalog.BuildWingetInstallArguments(geek));
    }

    [Fact]
    public void OnlyCataloguedOfficialInstallPagesAreAllowed()
    {
        Assert.True(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("https://www.kaspersky.com.cn/downloads/free-antivirus")));
        Assert.False(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("http://www.kaspersky.com.cn/downloads/free-antivirus")));
        Assert.False(SoftwareCatalog.IsApprovedOfficialPage(
            new Uri("https://example.com/download.exe")));
    }
}
