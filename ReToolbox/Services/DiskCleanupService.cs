using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ReToolbox.Services
{
    public sealed class DiskCleanupService
    {
        private static readonly EnumerationOptions ScanOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        private readonly IReadOnlyList<DiskCleanupRule> _rules;
        private readonly IReadOnlyList<string> _allowedRoots;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);

        public DiskCleanupService()
            : this(DiskCleanupCatalog.CreateDefaultConfiguration())
        {
        }

        private DiskCleanupService(DiskCleanupConfiguration configuration)
            : this(configuration.Rules, configuration.AllowedRoots)
        {
        }

        public DiskCleanupService(
            IReadOnlyList<DiskCleanupRule> rules,
            IReadOnlyList<string> allowedRoots)
        {
            _rules = rules;
            _allowedRoots = allowedRoots
                .Select(Path.GetFullPath)
                .ToArray();

            if (_allowedRoots.Count == 0)
            {
                throw new ArgumentException(
                    "At least one allowed cleanup root is required.",
                    nameof(allowedRoots));
            }
            if (_rules.GroupBy(rule => rule.Id, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Cleanup rule identifiers must be unique.",
                    nameof(rules));
            }

            foreach (DiskCleanupRule rule in _rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Id) ||
                    rule.IsRecommended && rule.Risk != DiskCleanupRisk.Safe ||
                    rule.Targets.Count == 0 ||
                    rule.Targets.Any(target => !IsAllowedTarget(target.Path)))
                {
                    throw new ArgumentException(
                        $"Cleanup rule '{rule.Id}' contains an unsafe target path.",
                        nameof(rules));
                }
            }
        }

        public async Task<IReadOnlyList<DiskCleanupScanItem>> ScanAsync(
            CancellationToken cancellationToken = default)
        {
            await _operationGate.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return await Task.Run<IReadOnlyList<DiskCleanupScanItem>>(
                    () => Scan(cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<DiskCleanupRunResult> CleanAsync(
            IEnumerable<string> selectedRuleIds,
            CancellationToken cancellationToken = default)
        {
            string[] selected = selectedRuleIds
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            await _operationGate.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return await Task.Run(
                    () => Clean(selected, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private IReadOnlyList<DiskCleanupScanItem> Scan(
            CancellationToken cancellationToken)
        {
            var items = new List<DiskCleanupScanItem>();
            foreach (DiskCleanupRule rule in _rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long size = 0;
                int count = 0;
                foreach (DiskCleanupTarget target in rule.Targets)
                {
                    if (!IsAllowedTarget(target.Path) ||
                        !Directory.Exists(target.Path) ||
                        IsReparsePoint(target.Path))
                    {
                        continue;
                    }

                    DateTime cutoff = DateTime.UtcNow - target.MinimumAge;
                    foreach (string path in EnumerateFilesSafely(target.Path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var file = new FileInfo(path);
                            if (!file.Exists ||
                                file.LastWriteTimeUtc > cutoff ||
                                (file.Attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                continue;
                            }

                            size += file.Length;
                            count++;
                        }
                        catch (Exception ex) when (
                            ex is IOException or UnauthorizedAccessException or SecurityException)
                        {
                        }
                    }
                }

                if (count > 0)
                {
                    items.Add(new DiskCleanupScanItem(
                        rule.Id,
                        rule.Category,
                        rule.Name,
                        rule.Description,
                        rule.Risk,
                        rule.IsRecommended,
                        size,
                        count));
                }
            }

            return items;
        }

        private DiskCleanupRunResult Clean(
            IReadOnlyCollection<string> selectedRuleIds,
            CancellationToken cancellationToken)
        {
            long freedBytes = 0;
            int deletedFiles = 0;
            int failedFiles = 0;
            var selected = new HashSet<string>(
                selectedRuleIds,
                StringComparer.Ordinal);

            foreach (DiskCleanupRule rule in _rules)
            {
                if (!selected.Contains(rule.Id))
                {
                    continue;
                }

                foreach (DiskCleanupTarget target in rule.Targets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsAllowedTarget(target.Path) ||
                        !Directory.Exists(target.Path) ||
                        IsReparsePoint(target.Path))
                    {
                        continue;
                    }

                    string targetRoot = Path.GetFullPath(target.Path);
                    DateTime cutoff = DateTime.UtcNow - target.MinimumAge;
                    foreach (string path in EnumerateFilesSafely(targetRoot))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            string fullPath = Path.GetFullPath(path);
                            var file = new FileInfo(fullPath);
                            if (!IsPathSafeForDeletion(fullPath, targetRoot) ||
                                !file.Exists ||
                                file.LastWriteTimeUtc > cutoff ||
                                (file.Attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                continue;
                            }

                            long length = file.Length;
                            file.Delete();
                            freedBytes += length;
                            deletedFiles++;
                        }
                        catch (Exception ex) when (
                            ex is IOException or UnauthorizedAccessException or SecurityException)
                        {
                            failedFiles++;
                        }
                    }

                    DeleteEmptyDirectories(targetRoot, cancellationToken);
                }
            }

            return new DiskCleanupRunResult(
                freedBytes,
                deletedFiles,
                failedFiles);
        }

        private bool IsAllowedTarget(string path)
        {
            string target = Path.GetFullPath(path);
            return _allowedRoots.Any(root =>
                target.StartsWith(
                    Path.TrimEndingDirectorySeparator(root) +
                    Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDescendantOf(string path, string root)
        {
            return Path.GetFullPath(path).StartsWith(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathSafeForDeletion(
            string path,
            string targetRoot)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(targetRoot);
            if (!IsDescendantOf(fullPath, fullRoot) || IsReparsePoint(fullRoot))
            {
                return false;
            }

            string? current = Path.GetDirectoryName(fullPath);
            while (current is not null &&
                   !current.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (IsReparsePoint(current))
                {
                    return false;
                }

                current = Path.GetDirectoryName(current);
            }

            return current is not null;
        }

        private static void DeleteEmptyDirectories(
            string targetRoot,
            CancellationToken cancellationToken)
        {
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(
                        targetRoot,
                        "*",
                        ScanOptions)
                    .OrderByDescending(path => path.Length)
                    .ToArray();
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                return;
            }

            foreach (string directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!IsReparsePoint(directory) &&
                        !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory, recursive: false);
                    }
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException or SecurityException)
                {
                }
            }
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                return true;
            }
        }

        private static IEnumerable<string> EnumerateFilesSafely(string root)
        {
            IEnumerator<string>? enumerator = null;
            try
            {
                enumerator = Directory.EnumerateFiles(
                        root,
                        "*",
                        ScanOptions)
                    .GetEnumerator();
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = enumerator.MoveNext();
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException or SecurityException)
                    {
                        yield break;
                    }

                    if (!hasNext)
                    {
                        yield break;
                    }

                    yield return enumerator.Current;
                }
            }
            finally
            {
                enumerator?.Dispose();
            }
        }
    }
}
