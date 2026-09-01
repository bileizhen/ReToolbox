using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ReToolbox.Services
{
    public sealed record UpdateRelease(
        Version Version,
        string TagName,
        Uri ReleasePageUri,
        Uri InstallerUri,
        long InstallerSize,
        string InstallerSha256);

    public static class UpdateWorkflow
    {
        public const string InstallerAssetName = "ReToolbox-Setup.exe";
        private const string LaunchCopyPrefix = "ReToolbox-Update-";

        public static bool IsNewerRelease(string tagName, Version currentVersion)
        {
            string normalized = tagName.Trim().TrimStart('v', 'V');
            return Version.TryParse(normalized, out Version? releaseVersion) &&
                   releaseVersion > currentVersion;
        }

        public static bool IsOwnedUpdateDirectory(
            string directoryPath,
            string commonApplicationData)
        {
            const string prefix = "ReToolbox-Update-";
            try
            {
                string root = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(commonApplicationData));
                string directory = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(directoryPath));
                string? parent = Path.GetDirectoryName(directory);
                string name = Path.GetFileName(directory);

                return parent is not null &&
                       parent.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                       name.StartsWith(prefix, StringComparison.Ordinal) &&
                       Guid.TryParseExact(
                           name[prefix.Length..],
                           "N",
                           out _);
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public static string CreatePolicyCompatibleLaunchPath(
            string applicationDirectory,
            Guid launchId)
        {
            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(applicationDirectory));
            return Path.Combine(
                root,
                $"{LaunchCopyPrefix}{launchId:N}.exe");
        }

        public static bool IsOwnedLaunchCopy(
            string filePath,
            string applicationDirectory)
        {
            try
            {
                string root = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(applicationDirectory));
                string file = Path.GetFullPath(filePath);
                string? parent = Path.GetDirectoryName(file);
                string name = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file);

                return parent is not null &&
                       parent.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                       extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                       name.StartsWith(LaunchCopyPrefix, StringComparison.Ordinal) &&
                       Guid.TryParseExact(
                           name[LaunchCopyPrefix.Length..],
                           "N",
                           out _);
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public static bool TryReadLatestRelease(
            string json,
            out UpdateRelease? release)
        {
            release = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                string tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
                string normalizedVersion = tagName.Trim().TrimStart('v', 'V');
                if (!Version.TryParse(normalizedVersion, out Version? version) ||
                    !TryReadTrustedUri(root, "html_url", out Uri releasePageUri))
                {
                    return false;
                }

                foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
                {
                    if (asset.GetProperty("name").GetString() != InstallerAssetName ||
                        asset.GetProperty("state").GetString() != "uploaded" ||
                        !asset.TryGetProperty("size", out JsonElement sizeElement) ||
                        !sizeElement.TryGetInt64(out long size) ||
                        size <= 0 ||
                        !TryReadSha256(asset, out string sha256) ||
                        !TryReadTrustedUri(
                            asset,
                            "browser_download_url",
                            out Uri installerUri))
                    {
                        continue;
                    }

                    string expectedReleasePath =
                        $"/bileizhen/ReToolbox/releases/tag/{tagName}";
                    string expectedInstallerPath =
                        $"/bileizhen/ReToolbox/releases/download/{tagName}/ReToolbox-Setup.exe";
                    if (!releasePageUri.AbsolutePath.Equals(
                            expectedReleasePath,
                            StringComparison.Ordinal) ||
                        !installerUri.AbsolutePath.Equals(
                            expectedInstallerPath,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    release = new UpdateRelease(
                        version,
                        tagName,
                        releasePageUri,
                        installerUri,
                        size,
                        sha256);
                    return true;
                }
            }
            catch (JsonException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return false;
        }

        private static bool TryReadTrustedUri(
            JsonElement element,
            string propertyName,
            out Uri uri)
        {
            uri = null!;
            if (!element.TryGetProperty(propertyName, out JsonElement value) ||
                !Uri.TryCreate(
                    value.GetString(),
                    UriKind.Absolute,
                    out Uri? parsedUri) ||
                parsedUri.Scheme != Uri.UriSchemeHttps ||
                !parsedUri.Host.Equals(
                    "github.com",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            uri = parsedUri;
            return true;
        }

        private static bool TryReadSha256(
            JsonElement asset,
            out string sha256)
        {
            sha256 = string.Empty;
            if (!asset.TryGetProperty("digest", out JsonElement digestElement))
            {
                return false;
            }

            string digest = digestElement.GetString() ?? string.Empty;
            const string prefix = "sha256:";
            if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string candidate = digest[prefix.Length..];
            if (candidate.Length != 64)
            {
                return false;
            }

            try
            {
                Convert.FromHexString(candidate);
                sha256 = candidate.ToLowerInvariant();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
