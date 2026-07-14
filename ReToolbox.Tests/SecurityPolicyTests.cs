using ReToolbox.Utils;
using Xunit;

namespace ReToolbox.Tests;

public class SecurityPolicyTests
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
}
