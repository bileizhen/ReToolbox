using System;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ReToolbox.Services
{
    public enum UpdateCheckState
    {
        UpToDate,
        UpdateAvailable,
        Failed
    }

    public sealed record UpdateCheckResult(
        UpdateCheckState State,
        string Message,
        UpdateRelease? Release = null);

    public static class AppUpdateCheckWorkflow
    {
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/bileizhen/ReToolbox/releases/latest";
        private static readonly TimeSpan MaximumCacheAge = TimeSpan.FromHours(24);

        public static async Task<UpdateCheckResult> CheckAsync(
            HttpClient httpClient,
            Version currentVersion,
            string cachePath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Get,
                    LatestReleaseUrl);
                using HttpResponseMessage response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(
                    cancellationToken).ConfigureAwait(false);

                if (!UpdateWorkflow.TryReadLatestRelease(
                        json,
                        out UpdateRelease? release) ||
                    release is null)
                {
                    return new UpdateCheckResult(
                        UpdateCheckState.Failed,
                        "GitHub Release 未提供可验证的 ReToolbox 安装器");
                }

                TryWriteCache(cachePath, json);
                return Evaluate(release, currentVersion, usedCache: false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                if (TryReadCache(cachePath, out UpdateRelease? cachedRelease) &&
                    cachedRelease is not null)
                {
                    return Evaluate(cachedRelease, currentVersion, usedCache: true);
                }

                return new UpdateCheckResult(
                    UpdateCheckState.Failed,
                    $"检查更新失败：{ex.Message}");
            }
        }

        private static UpdateCheckResult Evaluate(
            UpdateRelease release,
            Version currentVersion,
            bool usedCache)
        {
            string cacheSuffix = usedCache
                ? "（GitHub 暂不可用，已使用最近一次验证缓存）"
                : string.Empty;
            if (!UpdateWorkflow.IsNewerRelease(release.TagName, currentVersion))
            {
                return new UpdateCheckResult(
                    UpdateCheckState.UpToDate,
                    $"当前已是最新版本 v{currentVersion}{cacheSuffix}",
                    release);
            }

            return new UpdateCheckResult(
                UpdateCheckState.UpdateAvailable,
                $"发现新版本 {release.TagName}{cacheSuffix}",
                release);
        }

        private static bool TryReadCache(
            string cachePath,
            out UpdateRelease? release)
        {
            release = null;
            try
            {
                if (!File.Exists(cachePath))
                {
                    return false;
                }

                TimeSpan age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
                if (age < TimeSpan.Zero || age > MaximumCacheAge)
                {
                    return false;
                }

                string json = File.ReadAllText(cachePath);
                return UpdateWorkflow.TryReadLatestRelease(json, out release) &&
                       release is not null;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or PathTooLongException or
                SecurityException)
            {
                return false;
            }
        }

        private static void TryWriteCache(string cachePath, string json)
        {
            string? temporaryPath = null;
            try
            {
                string destination = Path.GetFullPath(cachePath);
                string? directory = Path.GetDirectoryName(destination);
                if (directory is null)
                {
                    return;
                }

                Directory.CreateDirectory(directory);
                temporaryPath = Path.Combine(
                    directory,
                    $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.partial");
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, destination, overwrite: true);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or PathTooLongException or
                SecurityException)
            {
                if (temporaryPath is not null)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception cleanupException) when (
                        cleanupException is IOException or UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }
}
