using System.Net;
using System.Net.Http;
using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public sealed class AppUpdateCheckWorkflowTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(
        Path.GetTempPath(),
        $"ReToolbox-UpdateCheck-{Guid.NewGuid():N}");

    [Fact]
    public async Task FreshVerifiedMetadataIsUsedWhenGitHubRateLimitsNextCheck()
    {
        Directory.CreateDirectory(_sandbox);
        string cachePath = Path.Combine(_sandbox, "latest-release.json");
        const string releaseJson = """
            {
              "tag_name": "v1.6.9",
              "html_url": "https://github.com/bileizhen/ReToolbox/releases/tag/v1.6.9",
              "assets": [
                {
                  "name": "ReToolbox-Setup.exe",
                  "state": "uploaded",
                  "size": 46827198,
                  "digest": "sha256:b02f361b6df653427b333b2ec6fa4453093b75d7d62548bcd757fdbdcb450428",
                  "browser_download_url": "https://github.com/bileizhen/ReToolbox/releases/download/v1.6.9/ReToolbox-Setup.exe"
                }
              ]
            }
            """;

        using var onlineClient = new HttpClient(
            new StubHandler(HttpStatusCode.OK, releaseJson));
        UpdateCheckResult online = await AppUpdateCheckWorkflow.CheckAsync(
            onlineClient,
            new Version(1, 6, 9),
            cachePath);
        Assert.Equal(UpdateCheckState.UpToDate, online.State);

        using var limitedClient = new HttpClient(
            new StubHandler(HttpStatusCode.Forbidden, "rate limit exceeded"));
        UpdateCheckResult cached = await AppUpdateCheckWorkflow.CheckAsync(
            limitedClient,
            new Version(1, 6, 9),
            cachePath);

        Assert.Equal(UpdateCheckState.UpToDate, cached.State);
        Assert.Contains("缓存", cached.Message);
        Assert.NotNull(cached.Release);
    }

    [Fact]
    public async Task RateLimitWithoutVerifiedCacheStillReportsFailure()
    {
        Directory.CreateDirectory(_sandbox);
        using var client = new HttpClient(
            new StubHandler(HttpStatusCode.Forbidden, "rate limit exceeded"));

        UpdateCheckResult result = await AppUpdateCheckWorkflow.CheckAsync(
            client,
            new Version(1, 6, 9),
            Path.Combine(_sandbox, "missing.json"));

        Assert.Equal(UpdateCheckState.Failed, result.State);
        Assert.Contains("403", result.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public StubHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(_content)
            });
        }
    }
}
