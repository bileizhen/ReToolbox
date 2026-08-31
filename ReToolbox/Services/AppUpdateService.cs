using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using ReToolbox.Utils;

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

    public sealed record DownloadedUpdate(
        UpdateRelease Release,
        string InstallerPath);

    public sealed class AppUpdateService : IDisposable
    {
        private const string RegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string StartupCheckValue = "CheckUpdatesOnStartup";
        private const string AutomaticDownloadValue = "AutomaticUpdateDownload";
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/bileizhen/ReToolbox/releases/latest";

        private readonly HttpClient _httpClient;

        public AppUpdateService()
        {
            CleanupStaleUpdateDirectories();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ReToolbox-Updater/1.0");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
                "application/vnd.github+json");
            _httpClient.DefaultRequestHeaders.Add(
                "X-GitHub-Api-Version",
                "2022-11-28");
        }

        public bool CheckOnStartupEnabled
        {
            get => ReadEnabledByDefault(StartupCheckValue);
            set => RegistryHelper.SetValue(
                RegistryPath,
                StartupCheckValue,
                value ? 1 : 0,
                RegistryValueKind.DWord);
        }

        public bool AutomaticDownloadEnabled
        {
            get => ReadEnabledByDefault(AutomaticDownloadValue);
            set => RegistryHelper.SetValue(
                RegistryPath,
                AutomaticDownloadValue,
                value ? 1 : 0,
                RegistryValueKind.DWord);
        }

        public Version CurrentVersion
        {
            get
            {
                Version assemblyVersion =
                    Assembly.GetExecutingAssembly().GetName().Version ??
                    new Version(0, 0, 0);
                return new Version(
                    assemblyVersion.Major,
                    assemblyVersion.Minor,
                    Math.Max(assemblyVersion.Build, 0));
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Get,
                    LatestReleaseUrl);
                using HttpResponseMessage response = await _httpClient.SendAsync(
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

                if (!UpdateWorkflow.IsNewerRelease(
                        release.TagName,
                        CurrentVersion))
                {
                    return new UpdateCheckResult(
                        UpdateCheckState.UpToDate,
                        $"当前已是最新版本 v{CurrentVersion}");
                }

                return new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"发现新版本 {release.TagName}",
                    release);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                return new UpdateCheckResult(
                    UpdateCheckState.Failed,
                    $"检查更新失败：{ex.Message}");
            }
        }

        public async Task<DownloadedUpdate> DownloadUpdateAsync(
            UpdateRelease release,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string stagingDirectory = SecureStagingDirectory.CreateUpdateDirectory();
            string installerPath = Path.Combine(
                stagingDirectory,
                UpdateWorkflow.InstallerAssetName);

            try
            {
                progress?.Report($"正在下载 {release.TagName}...");
                using HttpResponseMessage response = await GitHubMirrorHelper.GetAsync(
                    _httpClient,
                    release.InstallerUri.AbsoluteUri,
                    mirror => progress?.Report(
                        mirror is null
                            ? "正在从 GitHub 下载更新..."
                            : $"正在通过 {mirror} 下载更新..."),
                    cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using (Stream remote = await response.Content.ReadAsStreamAsync(
                    cancellationToken).ConfigureAwait(false))
                await using (FileStream local = new FileStream(
                    installerPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await remote.CopyToAsync(local, cancellationToken)
                        .ConfigureAwait(false);
                }

                await VerifyInstallerAsync(
                    installerPath,
                    release,
                    cancellationToken).ConfigureAwait(false);
                progress?.Report("更新已下载并通过 SHA-256 校验");
                return new DownloadedUpdate(release, installerPath);
            }
            catch
            {
                TryDeleteStagingDirectory(stagingDirectory);
                throw;
            }
        }

        public async Task LaunchInstallerAsync(
            DownloadedUpdate update,
            CancellationToken cancellationToken = default)
        {
            await VerifyInstallerAsync(
                update.InstallerPath,
                update.Release,
                cancellationToken).ConfigureAwait(false);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = update.InstallerPath,
                UseShellExecute = true,
                Verb = "runas"
            };
            if (Process.Start(startInfo) is null)
            {
                throw new InvalidOperationException("无法启动更新安装器");
            }
        }

        public void DiscardDownloadedUpdate(DownloadedUpdate update)
        {
            string? directory = Path.GetDirectoryName(update.InstallerPath);
            if (directory is null ||
                !Path.GetFileName(update.InstallerPath).Equals(
                    UpdateWorkflow.InstallerAssetName,
                    StringComparison.Ordinal) ||
                !UpdateWorkflow.IsOwnedUpdateDirectory(
                    directory,
                    GetCommonApplicationData()))
            {
                return;
            }

            TryDeleteUpdateDirectory(directory);
        }

        public void OpenReleasePage(UpdateRelease release)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = release.ReleasePageUri.AbsoluteUri,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private static bool ReadEnabledByDefault(string valueName)
        {
            object? value = RegistryHelper.GetValue(RegistryPath, valueName);
            return value is null || value is int enabled && enabled != 0;
        }

        private static async Task VerifyInstallerAsync(
            string installerPath,
            UpdateRelease release,
            CancellationToken cancellationToken)
        {
            await using FileStream installer = new FileStream(
                installerPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);
            if (installer.Length != release.InstallerSize ||
                !await ArtifactIntegrity.HasExpectedSha256Async(
                    installer,
                    release.InstallerSha256,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException(
                    "更新安装器的文件大小或 SHA-256 校验失败，已阻止执行");
            }
        }

        private static string GetCommonApplicationData()
        {
            return Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
        }

        private static void CleanupStaleUpdateDirectories()
        {
            string root = GetCommonApplicationData();
            try
            {
                foreach (string directory in Directory.EnumerateDirectories(
                             root,
                             "ReToolbox-Update-*",
                             SearchOption.TopDirectoryOnly))
                {
                    if (UpdateWorkflow.IsOwnedUpdateDirectory(directory, root))
                    {
                        TryDeleteUpdateDirectory(directory);
                    }
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteStagingDirectory(string stagingDirectory)
        {
            if (UpdateWorkflow.IsOwnedUpdateDirectory(
                    stagingDirectory,
                    GetCommonApplicationData()))
            {
                TryDeleteUpdateDirectory(stagingDirectory);
            }
        }

        private static void TryDeleteUpdateDirectory(string directory)
        {
            try
            {
                bool isReparsePoint =
                    (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
                Directory.Delete(directory, recursive: !isReparsePoint);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
