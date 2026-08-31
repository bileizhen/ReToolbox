using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ReToolbox.Utils
{
    // Routes GitHub file downloads through a chain of public mirror proxies when a
    // direct request fails. Each mirror is a plain
    // prefix: https://{mirror}/{original-full-url-including-https://}. We try them in
    // order after trying GitHub itself. Release metadata is intentionally fetched
    // directly by AppUpdateService so its digest remains independent of the proxy.
    public static class GitHubMirrorHelper
    {
        private const string RegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string EnabledValue = "GitHubMirrorEnabled";
        private const string SelectedValue = "GitHubMirror";

        // Preset mirrors, tried in order. Each is the host with scheme and no trailing /.
        public static readonly string[] Mirrors =
        {
            GitHubUrlRouting.RecommendedProxy,
            "https://gh-proxy.com",
            "https://github.dpik.top",
            "https://ghfast.top",
            "https://gh.llk.cc"
        };

        // Per-mirror probe budget: a dead/slow node must not stall the download for
        // long before we move on to the next candidate.
        private static readonly TimeSpan MirrorTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan TransferStallTimeout =
            TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TransferTimeout =
            TimeSpan.FromMinutes(15);

        // Whether mirror acceleration is on. Defaults to enabled (1) so a fresh
        // install in a restricted network benefits immediately; persisted in the
        // registry like the rest of the app's state.
        public static bool IsEnabled
        {
            get
            {
                object? v = RegistryHelper.GetValue(RegistryPath, EnabledValue);
                // Absent value (first run) means enabled.
                return v is null || Convert.ToInt32(v) != 0;
            }
            set => RegistryHelper.SetValue(RegistryPath, EnabledValue, value ? 1 : 0, RegistryValueKind.DWord);
        }

        // The mirror to try first. Empty/whitespace means "auto" (use the preset
        // order as-is). A preset host or any custom URL the user typed is tried first,
        // and on failure we keep walking the rest of the candidates. Stored as the
        // full URL (scheme + host, no trailing slash).
        public static string SelectedMirror
        {
            get => NormalizeMirror(RegistryHelper.GetValue(RegistryPath, SelectedValue) as string);
            set => RegistryHelper.SetValue(RegistryPath, SelectedValue, NormalizeMirror(value), RegistryValueKind.String);
        }

        public static bool TryNormalizeMirror(string? value, out string normalized)
        {
            return InputValidation.TryNormalizeHttpsOrigin(value, out normalized);
        }

        private static string NormalizeMirror(string? value)
        {
            return TryNormalizeMirror(value, out string normalized) ? normalized : string.Empty;
        }

        public static bool IsGitHubUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
                   uri.Scheme == Uri.UriSchemeHttps &&
                   GitHubUrlRouting.IsSupportedGitHubHost(uri.Host);
        }

        // Downloads and validates a complete file directly first, then retries the
        // entire transfer through each enabled mirror. A partial or invalid file is
        // removed before the next source is attempted.
        public static async Task DownloadFileAsync(
            HttpClient client,
            string url,
            string destinationPath,
            long expectedSize,
            Func<string, CancellationToken, Task> validate,
            Action<string?> onMirror,
            CancellationToken cancellationToken = default)
        {
            if (!IsGitHubUrl(url))
            {
                throw new ArgumentException(
                    "Only HTTPS GitHub file URLs can use this downloader.",
                    nameof(url));
            }
            if (expectedSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedSize));
            }

            var candidates = new List<(Uri Uri, string? Mirror)>
            {
                (new Uri(url), null)
            };
            if (IsEnabled)
            {
                var mirrors = new List<string>();
                string selected = SelectedMirror;
                if (selected.Length > 0)
                {
                    mirrors.Add(selected);
                }

                foreach (string mirror in Mirrors)
                {
                    if (!mirrors.Contains(mirror))
                    {
                        mirrors.Add(mirror);
                    }
                }

                foreach (string mirror in mirrors)
                {
                    candidates.Add((
                        GitHubUrlRouting.BuildMirroredUri(
                            mirror,
                            new Uri(url)),
                        mirror));
                }
            }

            Exception? lastFailure = null;
            foreach ((Uri candidateUri, string? mirror) in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDeletePartialFile(destinationPath);
                try
                {
                    using CancellationTokenSource headerTimeout =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                    headerTimeout.CancelAfter(MirrorTimeout);
                    using var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        candidateUri);
                    using HttpResponseMessage response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        headerTimeout.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        lastFailure = new HttpRequestException(
                            $"下载源返回 HTTP {(int)response.StatusCode}");
                        continue;
                    }
                    if (response.Content.Headers.ContentLength is long contentLength &&
                        contentLength != expectedSize)
                    {
                        lastFailure = new InvalidDataException(
                            $"下载源声明的文件大小不正确：{contentLength}");
                        continue;
                    }

                    onMirror(mirror);
                    using CancellationTokenSource transferTimeout =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                    transferTimeout.CancelAfter(TransferTimeout);
                    await CopyResponseToFileAsync(
                        response,
                        destinationPath,
                        expectedSize,
                        transferTimeout.Token).ConfigureAwait(false);
                    await validate(destinationPath, transferTimeout.Token)
                        .ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException ex)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    lastFailure = new IOException(
                        "下载源连接或传输超时",
                        ex);
                }
                catch (Exception ex) when (
                    ex is HttpRequestException or IOException or InvalidDataException)
                {
                    lastFailure = ex;
                }
            }

            TryDeletePartialFile(destinationPath);
            throw new HttpRequestException(
                "GitHub 及已配置的下载代理均未能提供完整、有效的更新文件",
                lastFailure);
        }

        private static async Task CopyResponseToFileAsync(
            HttpResponseMessage response,
            string destinationPath,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            await using Stream remote = await response.Content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);
            await using FileStream local = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

            byte[] buffer = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                using CancellationTokenSource stallTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                stallTimeout.CancelAfter(TransferStallTimeout);
                int maximumRead = (int)Math.Min(
                    buffer.Length,
                    expectedSize - totalBytes + 1);
                int bytesRead = await remote.ReadAsync(
                    buffer.AsMemory(0, maximumRead),
                    stallTimeout.Token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > expectedSize)
                {
                    throw new InvalidDataException(
                        "下载源返回的数据超过官方 Release 声明的文件大小");
                }

                await local.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken).ConfigureAwait(false);
            }

            await local.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void TryDeletePartialFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
