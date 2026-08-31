using System;

namespace ReToolbox.Utils
{
    public static class GitHubUrlRouting
    {
        public const string RecommendedProxy = "https://ghfile.geekertao.top";

        public static Uri BuildMirroredUri(string mirrorOrigin, Uri sourceUri)
        {
            if (!InputValidation.TryNormalizeHttpsOrigin(
                    mirrorOrigin,
                    out string normalizedMirror) ||
                sourceUri.Scheme != Uri.UriSchemeHttps ||
                !IsSupportedGitHubHost(sourceUri.Host))
            {
                throw new ArgumentException("A trusted HTTPS GitHub URL and proxy origin are required.");
            }

            return new Uri($"{normalizedMirror}/{sourceUri.AbsoluteUri}");
        }

        public static bool IsSupportedGitHubHost(string host)
        {
            return host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}
