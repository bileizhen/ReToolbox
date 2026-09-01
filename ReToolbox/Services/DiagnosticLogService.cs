using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ReToolbox.Services
{
    public sealed class DiagnosticLogService
    {
        private readonly object _writeGate = new object();

        public DiagnosticLogService()
            : this(Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ReToolbox",
                "Logs"))
        {
        }

        internal DiagnosticLogService(string logDirectory)
        {
            LogDirectory = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(LogDirectory);
            CurrentLogPath = Path.Combine(
                LogDirectory,
                $"ReToolbox-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
            CleanupExpiredLogs();
        }

        public string LogDirectory { get; }

        public string CurrentLogPath { get; }

        public void WriteInformation(string source, string message)
        {
            WriteLine("INFO", source, message);
        }

        public void WriteError(
            string source,
            string message,
            Exception exception)
        {
            WriteLine("ERROR", source, $"{message}{Environment.NewLine}{exception}");
        }

        public async Task CreateFeedbackArchiveAsync(
            string archivePath,
            CancellationToken cancellationToken = default)
        {
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

        private void WriteLine(string level, string source, string message)
        {
            string line =
                $"[{DateTimeOffset.Now:O}] [{level}] [{Redact(source)}] " +
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
