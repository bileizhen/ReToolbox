using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReToolbox.Services
{
    public sealed record PreparedSoftwareInstaller(
        string FilePath,
        string WorkingDirectory);

    public static class SoftwareInstallerWorkflow
    {
        private const int MaximumArchiveEntries = 5000;
        private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
        private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

        public static bool IsSuccessfulWingetOutcome(
            int exitCode,
            bool isInstalled)
        {
            return exitCode == 0 || isInstalled;
        }

        public static PreparedSoftwareInstaller PrepareInstaller(
            string artifactPath,
            string releaseFileName,
            GitHubReleaseDownload source)
        {
            if (!File.Exists(artifactPath))
            {
                throw new FileNotFoundException("下载的安装文件不存在", artifactPath);
            }

            string safeReleaseFileName = Path.GetFileName(releaseFileName);
            if (string.IsNullOrWhiteSpace(safeReleaseFileName) ||
                !safeReleaseFileName.Equals(releaseFileName, StringComparison.Ordinal) ||
                !Regex.IsMatch(
                    safeReleaseFileName,
                    source.AssetNamePattern,
                    RegexOptions.CultureInvariant,
                    PatternTimeout))
            {
                throw new InvalidDataException("下载文件与受信任的 Release 规则不匹配");
            }

            string extension = Path.GetExtension(safeReleaseFileName);
            if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsApprovedExecutableName(safeReleaseFileName, source))
                {
                    throw new InvalidDataException("Release 安装程序名称未通过白名单校验");
                }

                string fullPath = Path.GetFullPath(artifactPath);
                return new PreparedSoftwareInstaller(
                    fullPath,
                    Path.GetDirectoryName(fullPath)!);
            }

            if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("仅支持自动运行经过校验的 EXE 或 ZIP Release");
            }

            string extractionDirectory = CreateUniqueExtractionDirectory(artifactPath);
            try
            {
                ExtractArchiveSafely(artifactPath, extractionDirectory);
                string[] candidates = Directory
                    .EnumerateFiles(extractionDirectory, "*.exe", SearchOption.AllDirectories)
                    .Where(path => IsApprovedExecutableName(Path.GetFileName(path), source))
                    .ToArray();

                if (candidates.Length != 1)
                {
                    throw new InvalidDataException(
                        candidates.Length == 0
                            ? "压缩包中没有找到受信任的启动程序"
                            : "压缩包中存在多个同名启动程序，已阻止自动运行");
                }

                string installerPath = Path.GetFullPath(candidates[0]);
                EnsurePathInsideRoot(installerPath, extractionDirectory);
                return new PreparedSoftwareInstaller(
                    installerPath,
                    Path.GetDirectoryName(installerPath)!);
            }
            catch
            {
                TryDeleteOwnedDirectory(extractionDirectory);
                throw;
            }
        }

        private static bool IsApprovedExecutableName(
            string fileName,
            GitHubReleaseDownload source)
        {
            return Regex.IsMatch(
                fileName,
                source.ExecutableNamePattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                PatternTimeout);
        }

        private static string CreateUniqueExtractionDirectory(string artifactPath)
        {
            string artifactFullPath = Path.GetFullPath(artifactPath);
            string parent = Path.GetDirectoryName(artifactFullPath)!;
            string baseName = Path.GetFileNameWithoutExtension(artifactFullPath);
            string candidate = Path.Combine(parent, $"{baseName}-files");
            int suffix = 1;
            while (Directory.Exists(candidate) || File.Exists(candidate))
            {
                candidate = Path.Combine(parent, $"{baseName}-files ({suffix++})");
            }

            Directory.CreateDirectory(candidate);
            return Path.GetFullPath(candidate);
        }

        private static void ExtractArchiveSafely(
            string archivePath,
            string extractionDirectory)
        {
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > MaximumArchiveEntries)
            {
                throw new InvalidDataException("压缩包文件数量超过安全限制");
            }

            long expandedBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (IsSymbolicLink(entry))
                {
                    throw new InvalidDataException("压缩包包含不受支持的符号链接");
                }

                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaximumExpandedBytes)
                {
                    throw new InvalidDataException("压缩包解压大小超过安全限制");
                }

                string relativePath = entry.FullName.Replace(
                    '/',
                    Path.DirectorySeparatorChar);
                string destinationPath = Path.GetFullPath(
                    Path.Combine(extractionDirectory, relativePath));
                EnsurePathInsideRoot(destinationPath, extractionDirectory);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? destinationParent = Path.GetDirectoryName(destinationPath);
                if (destinationParent is null)
                {
                    throw new InvalidDataException("压缩包条目路径无效");
                }

                Directory.CreateDirectory(destinationParent);
                using Stream source = entry.Open();
                using FileStream destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                source.CopyTo(destination);
            }
        }

        private static bool IsSymbolicLink(ZipArchiveEntry entry)
        {
            const int UnixFileTypeMask = 0xF000;
            const int UnixSymbolicLink = 0xA000;
            int unixMode = (entry.ExternalAttributes >> 16) & UnixFileTypeMask;
            return unixMode == UnixSymbolicLink;
        }

        private static void EnsurePathInsideRoot(string path, string root)
        {
            string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            string fullPath = Path.GetFullPath(path);
            string rootPrefix = fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("压缩包包含越界路径，已阻止解压");
            }
        }

        private static void TryDeleteOwnedDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
