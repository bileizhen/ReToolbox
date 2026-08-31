using System;
using System.Collections.Generic;
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

        // GETs <paramref name="url"/> directly first, then through each mirror when
        // enabled. onMirror reports the serving host, or null when GitHub serves it.
        public static async Task<HttpResponseMessage> GetAsync(
            HttpClient client,
            string url,
            Action<string?> onMirror,
            CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || !IsGitHubUrl(url))
            {
                onMirror(null);
                using var directRequest = new HttpRequestMessage(HttpMethod.Get, url);
                return await client.SendAsync(
                    directRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
            }

            using (CancellationTokenSource directTimeout =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                directTimeout.CancelAfter(MirrorTimeout);
                try
                {
                    using var probeRequest = new HttpRequestMessage(HttpMethod.Get, url);
                    HttpResponseMessage response = await client.SendAsync(
                        probeRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        directTimeout.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        onMirror(null);
                        return response;
                    }

                    response.Dispose();
                }
                catch (OperationCanceledException)
                    when (!cancellationToken.IsCancellationRequested)
                {
                }
                catch (HttpRequestException)
                {
                }
            }

            if (IsGitHubUrl(url))
            {
                // Selected mirror first, then the rest of the presets, de-duplicated so
                // a custom URL or picked preset is preferred but never blocks fail-over.
                var candidates = new List<string>();
                string selected = SelectedMirror;
                if (selected.Length > 0)
                {
                    candidates.Add(selected);
                }
                foreach (string m in Mirrors)
                {
                    if (!candidates.Contains(m))
                    {
                        candidates.Add(m);
                    }
                }

                foreach (string mirror in candidates)
                {
                    using CancellationTokenSource cts =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(MirrorTimeout);

                    Uri mirroredUri = GitHubUrlRouting.BuildMirroredUri(
                        mirror,
                        new Uri(url));
                    using var mirrored = new HttpRequestMessage(HttpMethod.Get, mirroredUri);

                    HttpResponseMessage response;
                    try
                    {
                        response = await client.SendAsync(mirrored, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                        when (!cancellationToken.IsCancellationRequested)
                    {
                        // Per-mirror timeout — try the next candidate.
                        continue;
                    }
                    catch (HttpRequestException)
                    {
                        // DNS failure, timeout, connection refused — try the next mirror.
                        continue;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        onMirror(mirror);
                        return response;
                    }

                    // 4xx/5xx from the mirror: move on, but free this response.
                    response.Dispose();
                }
            }

            // All fallbacks failed. Retry GitHub without the short probe timeout so
            // the caller receives the authoritative response/error.
            onMirror(null);
            using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
            return await client.SendAsync(retryRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
