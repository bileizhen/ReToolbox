using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public sealed record DownloadedGitHubRelease(
        GitHubReleaseAsset Release,
        string FilePath);

    public sealed class GitHubReleaseDownloadService : IDisposable
    {
        private readonly HttpClient _httpClient;

        public GitHubReleaseDownloadService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ReToolbox-SoftwareDownloader/1.0");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
                "application/vnd.github+json");
            _httpClient.DefaultRequestHeaders.Add(
                "X-GitHub-Api-Version",
                "2022-11-28");
        }

        public async Task<DownloadedGitHubRelease> DownloadLatestAsync(
            GitHubReleaseDownload source,
            IProgress<string>? progress = null,
            IProgress<int>? downloadProgress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report($"正在获取 {source.Repository} 的最新 Release...");
            GitHubReleaseAsset release = await GetLatestReleaseAssetAsync(
                source,
                cancellationToken).ConfigureAwait(false);

            string downloadDirectory = GetDownloadDirectory();
            Directory.CreateDirectory(downloadDirectory);
            string destinationPath = GetUniqueDestinationPath(
                downloadDirectory,
                release.FileName);
            string temporaryPath = Path.Combine(
                downloadDirectory,
                $".{Guid.NewGuid():N}.download");

            try
            {
                await GitHubMirrorHelper.DownloadFileAsync(
                    _httpClient,
                    release.DownloadUri.AbsoluteUri,
                    temporaryPath,
                    release.Size,
                    (path, token) => VerifyAssetAsync(path, release, token),
                    mirror => progress?.Report(
                        mirror is null
                            ? "镜像不可用，正在从 GitHub 直连下载..."
                            : $"正在通过 {mirror} 下载..."),
                    preferMirrors: true,
                    onProgress: percent => downloadProgress?.Report(percent),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                File.Move(temporaryPath, destinationPath);
                progress?.Report(
                    $"已下载 {release.FileName}（{release.TagName}）并完成 SHA-256 校验");
                return new DownloadedGitHubRelease(release, destinationPath);
            }
            catch
            {
                TryDeleteTemporaryFile(temporaryPath);
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private async Task<GitHubReleaseAsset> GetLatestReleaseAssetAsync(
            GitHubReleaseDownload source,
            CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{source.Repository}/releases/latest");
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(
                cancellationToken).ConfigureAwait(false);

            if (!GitHubReleaseWorkflow.TryReadLatestAsset(
                    json,
                    source,
                    out GitHubReleaseAsset? release) ||
                release is null)
            {
                throw new InvalidDataException(
                    "GitHub 最新 Release 未提供受支持且带 SHA-256 摘要的 Windows 安装文件");
            }

            return release;
        }

        private static async Task VerifyAssetAsync(
            string path,
            GitHubReleaseAsset release,
            CancellationToken cancellationToken)
        {
            await using FileStream asset = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);
            if (asset.Length != release.Size ||
                !await ArtifactIntegrity.HasExpectedSha256Async(
                    asset,
                    release.Sha256,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException(
                    "下载文件的大小或 SHA-256 校验失败，已阻止使用该文件");
            }
        }

        private static string GetDownloadDirectory()
        {
            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Downloads", "ReToolbox");
        }

        private static string GetUniqueDestinationPath(
            string downloadDirectory,
            string fileName)
        {
            string safeFileName = Path.GetFileName(fileName);
            if (!safeFileName.Equals(fileName, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(safeFileName))
            {
                throw new InvalidDataException("Release 文件名不安全");
            }

            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(downloadDirectory));
            string candidate = Path.Combine(root, safeFileName);
            int suffix = 1;
            while (File.Exists(candidate))
            {
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(safeFileName);
                string extension = Path.GetExtension(safeFileName);
                candidate = Path.Combine(
                    root,
                    $"{nameWithoutExtension} ({suffix++}){extension}");
            }

            if (!Path.GetDirectoryName(candidate)!.Equals(
                    root,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Release 文件路径不安全");
            }

            return candidate;
        }

        private static void TryDeleteTemporaryFile(string path)
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
