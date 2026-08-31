using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public sealed record GitHubReleaseAsset(
        string TagName,
        string FileName,
        Uri DownloadUri,
        long Size,
        string Sha256);

    public static class GitHubReleaseWorkflow
    {
        public static bool TryReadLatestAsset(
            string json,
            GitHubReleaseDownload source,
            out GitHubReleaseAsset? asset)
        {
            asset = null;
            if (!TrySplitRepository(source.Repository, out string owner, out string repository) ||
                string.IsNullOrWhiteSpace(source.AssetNamePattern))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                string tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return false;
                }

                Regex assetPattern = new Regex(
                    source.AssetNamePattern,
                    RegexOptions.CultureInvariant);
                foreach (JsonElement releaseAsset in root.GetProperty("assets").EnumerateArray())
                {
                    string fileName = releaseAsset.GetProperty("name").GetString() ?? string.Empty;
                    if (!assetPattern.IsMatch(fileName) ||
                        releaseAsset.GetProperty("state").GetString() != "uploaded" ||
                        !releaseAsset.TryGetProperty("size", out JsonElement sizeElement) ||
                        !sizeElement.TryGetInt64(out long size) ||
                        size <= 0 ||
                        !TryReadSha256(releaseAsset, out string sha256) ||
                        !TryReadTrustedDownloadUri(
                            releaseAsset,
                            owner,
                            repository,
                            tagName,
                            fileName,
                            out Uri downloadUri))
                    {
                        continue;
                    }

                    asset = new GitHubReleaseAsset(
                        tagName,
                        fileName,
                        downloadUri,
                        size,
                        sha256);
                    return true;
                }
            }
            catch (ArgumentException)
            {
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

        private static bool TrySplitRepository(
            string repository,
            out string owner,
            out string name)
        {
            owner = string.Empty;
            name = string.Empty;
            string[] parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !IsRepositorySegment(parts[0]) ||
                !IsRepositorySegment(parts[1]))
            {
                return false;
            }

            owner = parts[0];
            name = parts[1];
            return true;
        }

        private static bool IsRepositorySegment(string value)
        {
            return value.Length is > 0 and <= 100 &&
                   Regex.IsMatch(value, @"^[A-Za-z0-9_.-]+$");
        }

        private static bool TryReadTrustedDownloadUri(
            JsonElement releaseAsset,
            string owner,
            string repository,
            string tagName,
            string fileName,
            out Uri downloadUri)
        {
            downloadUri = null!;
            if (!releaseAsset.TryGetProperty(
                    "browser_download_url",
                    out JsonElement uriElement) ||
                !Uri.TryCreate(
                    uriElement.GetString(),
                    UriKind.Absolute,
                    out Uri? candidate) ||
                candidate.Scheme != Uri.UriSchemeHttps ||
                !candidate.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string expectedPath =
                $"/{owner}/{repository}/releases/download/{tagName}/{fileName}";
            if (!candidate.AbsolutePath.Equals(
                    expectedPath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            downloadUri = candidate;
            return true;
        }

        private static bool TryReadSha256(
            JsonElement releaseAsset,
            out string sha256)
        {
            sha256 = string.Empty;
            if (!releaseAsset.TryGetProperty("digest", out JsonElement digestElement))
            {
                return false;
            }

            const string prefix = "sha256:";
            string digest = digestElement.GetString() ?? string.Empty;
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
