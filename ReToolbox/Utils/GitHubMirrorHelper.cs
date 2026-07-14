using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ReToolbox.Utils
{
    // Routes GitHub downloads through a chain of public mirror proxies so a slow
    // or blocked github.com / api.github.com still completes. Each mirror is a plain
    // prefix: https://{mirror}/{original-full-url-including-https://}. We try them in
    // order and fall back to the original URL when none respond — some mirrors reject
    // api.github.com (ghfast.top → 403) or may be unreachable (DNS), which the
    // fail-over handles transparently.
    public static class GitHubMirrorHelper
    {
        private const string RegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string EnabledValue = "GitHubMirrorEnabled";
        private const string SelectedValue = "GitHubMirror";

        // Preset mirrors, tried in order. Each is the host with scheme and no trailing /.
        public static readonly string[] Mirrors =
        {
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
            return url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://api.github.com/", StringComparison.OrdinalIgnoreCase);
        }

        // GETs <paramref name="url"/> through each mirror in turn, returning the first
        // successful response. onMirror reports the serving host (e.g.
        // "https://gh-proxy.com"), or null when going direct. If acceleration is off, the
        // URL isn't GitHub, or every mirror fails, the request goes to the original URL.
        public static async Task<HttpResponseMessage> GetAsync(
            HttpClient client,
            string url,
            Action<string?> onMirror,
            CancellationToken cancellationToken = default)
        {
            if (IsEnabled && IsGitHubUrl(url))
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

                    using var mirrored = new HttpRequestMessage(HttpMethod.Get, $"{mirror}/{url}");

                    HttpResponseMessage response;
                    try
                    {
                        response = await client.SendAsync(mirrored, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                            .ConfigureAwait(false);
                    }
                    catch
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

            // No mirror available or none succeeded — go direct to the original URL.
            onMirror(null);
            using var direct = new HttpRequestMessage(HttpMethod.Get, url);
            return await client.SendAsync(direct, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
