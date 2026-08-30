using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ReToolbox.Utils
{
    public static class VerifiedArtifactDownloader
    {
        public static async Task<FileStream> DownloadAndOpenAsync(
            Uri downloadUri,
            string localPath,
            long expectedSize,
            string expectedSha256,
            IProgress<string>? progress = null)
        {
            using HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ReToolbox/1.4");
            await DownloadWithRetryAsync(client, downloadUri, localPath, progress);

            FileStream downloaded = new FileStream(
                localPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            try
            {
                if (downloaded.Length != expectedSize ||
                    !await ArtifactIntegrity.HasExpectedSha256Async(
                        downloaded,
                        expectedSha256))
                {
                    throw new InvalidDataException(
                        "下载文件的大小或 SHA-256 与固定版本不匹配，已阻止执行");
                }

                downloaded.Position = 0;
                return downloaded;
            }
            catch
            {
                await downloaded.DisposeAsync();
                throw;
            }
        }

        private static async Task DownloadWithRetryAsync(
            HttpClient client,
            Uri downloadUri,
            string localPath,
            IProgress<string>? progress)
        {
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(
                        downloadUri,
                        HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using Stream remote = await response.Content.ReadAsStreamAsync();
                    await using FileStream local = new FileStream(
                        localPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        true);
                    await remote.CopyToAsync(local);
                    return;
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    progress?.Report($"下载失败，正在重试（{attempt}/{maxAttempts}）...");
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
        }
    }
}
