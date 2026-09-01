using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ReToolbox.Services
{
    public enum DiagnosticLogSource
    {
        Application,
        Updater,
        Software,
        Diagnostics
    }

    public sealed class DiagnosticLogService
    {
        private readonly object _writeGate = new object();

        public DiagnosticLogService()
            : this(GetDefaultLogDirectory(), GetFallbackLogDirectory())
        {
        }

        internal DiagnosticLogService(string logDirectory)
            : this(logDirectory, GetFallbackLogDirectory())
        {
        }

        internal DiagnosticLogService(
            string preferredLogDirectory,
            string fallbackLogDirectory)
        {
            if (!TryPrepareLogDirectory(
                    preferredLogDirectory,
                    out string logDirectory) &&
                !TryPrepareLogDirectory(
                    fallbackLogDirectory,
                    out logDirectory))
            {
                LogDirectory = string.Empty;
                CurrentLogPath = string.Empty;
                return;
            }

            LogDirectory = logDirectory;
            CurrentLogPath = Path.Combine(
                LogDirectory,
                $"ReToolbox-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
            CleanupExpiredLogs();
        }

        public string LogDirectory { get; }

        public string CurrentLogPath { get; }

        public bool IsAvailable => CurrentLogPath.Length > 0;

        public void WriteInformation(
            DiagnosticLogSource source,
            string message)
        {
            WriteLine("INFO", source, message);
        }

        public void WriteError(
            DiagnosticLogSource source,
            string message,
            Exception exception)
        {
            WriteLine("ERROR", source, $"{message}{Environment.NewLine}{exception}");
        }

        public async Task CreateFeedbackArchiveAsync(
            string archivePath,
            CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException(
                    "诊断日志目录不可用，无法创建诊断包");
            }

            string destination = Path.GetFullPath(archivePath);
            string? destinationDirectory = Path.GetDirectoryName(destination);
            if (destinationDirectory is null)
            {
                throw new ArgumentException(
                    "诊断包路径必须包含有效目录",
                    nameof(archivePath));
            }

            Directory.CreateDirectory(destinationDirectory);
            string temporaryPath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.partial");
            try
            {
                await using (FileStream output = new FileStream(
                                 temporaryPath,
                                 FileMode.CreateNew,
                                 FileAccess.ReadWrite,
                                 FileShare.None,
                                 81920,
                                 useAsync: true))
                using (ZipArchive archive = new ZipArchive(
                           output,
                           ZipArchiveMode.Create,
                           leaveOpen: false))
                {
                    foreach (string logPath in Directory.EnumerateFiles(
                                 LogDirectory,
                                 "ReToolbox-*.log",
                                 SearchOption.TopDirectoryOnly))
                    {
                        if (!IsOwnedLogPath(logPath) || IsReparsePoint(logPath))
                        {
                            continue;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        archive.CreateEntryFromFile(
                            logPath,
                            $"logs/{Path.GetFileName(logPath)}",
                            CompressionLevel.Optimal);
                    }

                    ZipArchiveEntry metadataEntry = archive.CreateEntry(
                        "diagnostics.json",
                        CompressionLevel.Optimal);
                    await using Stream metadata = metadataEntry.Open();
                    await JsonSerializer.SerializeAsync(
                        metadata,
                        new
                        {
                            appVersion = Assembly.GetExecutingAssembly()
                                .GetName()
                                .Version?
                                .ToString(3) ?? "unknown",
                            operatingSystem = RuntimeInformation.OSDescription,
                            architecture = RuntimeInformation.OSArchitecture.ToString(),
                            exportedAt = DateTimeOffset.UtcNow
                        },
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryPath, destination, overwrite: true);
            }
            catch
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                }

                throw;
            }
        }

        private void WriteLine(
            string level,
            DiagnosticLogSource source,
            string message)
        {
            if (!IsAvailable)
            {
                return;
            }

            string line =
                $"[{DateTimeOffset.Now:O}] [{level}] [{source}] " +
                $"{Redact(message)}{Environment.NewLine}";
            try
            {
                lock (_writeGate)
                {
                    File.AppendAllText(CurrentLogPath, line, Encoding.UTF8);
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                // Diagnostics must never interrupt the operation being recorded.
            }
        }

        private static string Redact(string value)
        {
            string redacted = value;
            string profile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                redacted = redacted.Replace(
                    profile,
                    "%USERPROFILE%",
                    StringComparison.OrdinalIgnoreCase);
            }

            string userName = Environment.UserName;
            if (!string.IsNullOrWhiteSpace(userName))
            {
                redacted = redacted.Replace(
                    userName,
                    "%USERNAME%",
                    StringComparison.OrdinalIgnoreCase);
            }

            return redacted;
        }

        private static string GetDefaultLogDirectory()
        {
            try
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "ReToolbox",
                    "Logs");
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or SecurityException)
            {
                return string.Empty;
            }
        }

        private static string GetFallbackLogDirectory()
        {
            try
            {
                return Path.Combine(
                    Path.GetTempPath(),
                    "ReToolbox",
                    "Logs");
            }
            catch (Exception ex) when (
                ex is IOException or ArgumentException or
                NotSupportedException or SecurityException)
            {
                return string.Empty;
            }
        }

        private static bool TryPrepareLogDirectory(
            string path,
            out string logDirectory)
        {
            logDirectory = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                logDirectory = Path.GetFullPath(path);
                Directory.CreateDirectory(logDirectory);
                return true;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or
                PathTooLongException or SecurityException)
            {
                logDirectory = string.Empty;
                return false;
            }
        }

        private void CleanupExpiredLogs()
        {
            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-7);
            try
            {
                foreach (string path in Directory.EnumerateFiles(
                             LogDirectory,
                             "ReToolbox-*.log",
                             SearchOption.TopDirectoryOnly))
                {
                    if (IsOwnedLogPath(path) &&
                        !IsReparsePoint(path) &&
                        File.GetLastWriteTimeUtc(path) < cutoffUtc)
                    {
                        File.Delete(path);
                    }
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                // Retention cleanup is best effort.
            }
        }

        private bool IsOwnedLogPath(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string? parent = Path.GetDirectoryName(fullPath);
                string fileName = Path.GetFileNameWithoutExtension(fullPath);
                const string prefix = "ReToolbox-";
                if (parent is null ||
                    !parent.Equals(
                        LogDirectory,
                        StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetExtension(fullPath).Equals(
                        ".log",
                        StringComparison.OrdinalIgnoreCase) ||
                    !fileName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }

                string identity = fileName[prefix.Length..];
                return identity.Length >= 17 &&
                       identity[15] == '-' &&
                       DateTime.TryParseExact(
                           identity[..15],
                           "yyyyMMdd-HHmmss",
                           CultureInfo.InvariantCulture,
                           DateTimeStyles.None,
                           out _) &&
                       int.TryParse(
                           identity[16..],
                           NumberStyles.None,
                           CultureInfo.InvariantCulture,
                           out int processId) &&
                       processId > 0;
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }
    }
}
