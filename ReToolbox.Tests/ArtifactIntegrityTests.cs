using ReToolbox.Utils;
using Xunit;

namespace ReToolbox.Tests;

public class ArtifactIntegrityTests
{
    [Fact]
    public async Task AcceptsOnlyTheExpectedSha256()
    {
        const string helloSha256 = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

        await using MemoryStream expected = new("hello"u8.ToArray());
        Assert.True(await ArtifactIntegrity.HasExpectedSha256Async(expected, helloSha256));

        await using MemoryStream tampered = new("tampered"u8.ToArray());
        Assert.False(await ArtifactIntegrity.HasExpectedSha256Async(tampered, helloSha256));
    }
}
