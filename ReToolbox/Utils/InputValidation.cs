using System;
using System.Text.RegularExpressions;

namespace ReToolbox.Utils
{
    public static class InputValidation
    {
        private static readonly Regex WingetIdPattern =
            new(@"^[A-Za-z0-9][A-Za-z0-9._+-]{1,127}$", RegexOptions.Compiled);

        public static bool TryNormalizeHttpsOrigin(string? value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string candidate = value.Trim().TrimEnd('/');
            if (!candidate.Contains("://", StringComparison.Ordinal))
            {
                candidate = "https://" + candidate;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                return false;
            }

            normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            return true;
        }

        public static bool IsValidWingetId(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && WingetIdPattern.IsMatch(value);
        }
    }
}
