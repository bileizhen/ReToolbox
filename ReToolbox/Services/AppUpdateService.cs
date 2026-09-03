using System;
using System.ComponentModel;
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
    public sealed record DownloadedUpdate(
        UpdateRelease Release,
        string InstallerPath);

    public sealed class AppUpdateService : IDisposable
    {
        private const string RegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string StartupCheckValue = "CheckUpdatesOnStartup";
        private const string AutomaticDownloadValue = "AutomaticUpdateDownload";
        private readonly HttpClient _httpClient;

        public AppUpdateService()
        {
            CleanupStaleUpdateDirectories();
            CleanupStaleLaunchCopies();
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
            return await AppUpdateCheckWorkflow.CheckAsync(
                _httpClient,
                CurrentVersion,
                GetUpdateMetadataCachePath(),
                cancellationToken).ConfigureAwait(false);
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
                await GitHubMirrorHelper.DownloadFileAsync(
                    _httpClient,
                    release.InstallerUri.AbsoluteUri,
                    installerPath,
                    release.InstallerSize,
                    (path, token) => VerifyInstallerAsync(
                        path,
                        release,
                        token),
                    mirror => progress?.Report(
                        mirror is null
                            ? "正在从 GitHub 下载更新..."
                            : $"正在通过 {mirror} 下载更新..."),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
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

            string launchCopyPath =
                await PreparePolicyCompatibleLaunchCopyAsync(
                    update,
                    cancellationToken).ConfigureAwait(false);
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = launchCopyPath,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                if (Process.Start(startInfo) is null)
                {
                    throw new InvalidOperationException("无法启动更新安装器");
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1260)
            {
                TryDeleteLaunchCopy(launchCopyPath);
                throw new InvalidOperationException(
                    "Windows 应用程序控制策略仍阻止更新安装器。当前安装包未数字签名，请从 GitHub 发布页手动下载，或由管理员将该版本加入允许策略。",
                    ex);
            }
            catch
            {
                TryDeleteLaunchCopy(launchCopyPath);
                throw;
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

        private static async Task<string> PreparePolicyCompatibleLaunchCopyAsync(
            DownloadedUpdate update,
            CancellationToken cancellationToken)
        {
            string launchCopyPath =
                UpdateWorkflow.CreatePolicyCompatibleLaunchPath(
                    AppContext.BaseDirectory,
                    Guid.NewGuid());
            try
            {
                await using (FileStream source = new FileStream(
                                 update.InstallerPath,
                                 FileMode.Open,
                                 FileAccess.Read,
                                 FileShare.Read,
                                 81920,
                                 useAsync: true))
                await using (FileStream destination = new FileStream(
                                 launchCopyPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 useAsync: true))
                {
                    await source.CopyToAsync(
                        destination,
                        81920,
                        cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                await VerifyInstallerAsync(
                    launchCopyPath,
                    update.Release,
                    cancellationToken).ConfigureAwait(false);
                return launchCopyPath;
            }
            catch
            {
                TryDeleteLaunchCopy(launchCopyPath);
                throw;
            }
        }

        private static string GetCommonApplicationData()
        {
            return Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
        }

        private static string GetUpdateMetadataCachePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ReToolbox",
                "Update",
                "latest-release.json");
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

        private static void CleanupStaleLaunchCopies()
        {
            string applicationDirectory = AppContext.BaseDirectory;
            try
            {
                foreach (string file in Directory.EnumerateFiles(
                             applicationDirectory,
                             "ReToolbox-Update-*.exe",
                             SearchOption.TopDirectoryOnly))
                {
                    TryDeleteLaunchCopy(file);
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

        private static void TryDeleteLaunchCopy(string path)
        {
            if (!UpdateWorkflow.IsOwnedLaunchCopy(
                    path,
                    AppContext.BaseDirectory))
            {
                return;
            }

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
