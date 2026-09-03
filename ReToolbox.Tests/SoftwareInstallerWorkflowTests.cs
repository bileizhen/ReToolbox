using System.IO.Compression;
using ReToolbox.Services;
using Xunit;

namespace ReToolbox.Tests;

public sealed class SoftwareInstallerWorkflowTests
{
    [Fact]
    public void WingetNoOpIsSuccessfulWhenPackageIsAlreadyInstalled()
    {
        Assert.True(SoftwareInstallerWorkflow.IsSuccessfulWingetOutcome(
            exitCode: unchecked((int)0x8A15002B),
            isInstalled: true));
        Assert.False(SoftwareInstallerWorkflow.IsSuccessfulWingetOutcome(
            exitCode: unchecked((int)0x8A15002B),
            isInstalled: false));
    }

    [Fact]
    public void DirectExeMustMatchBothReleaseAndExecutableRules()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string installerPath = Path.Combine(root, "Folia-Setup-0.7.1.exe");
            File.WriteAllBytes(installerPath, new byte[] { 0x4D, 0x5A });
            var source = new GitHubReleaseDownload(
                "chthollyphile/folia-major",
                @"^Folia-Setup-.*\.exe$",
                @"^Folia-Setup-.*\.exe$");

            PreparedSoftwareInstaller installer =
                SoftwareInstallerWorkflow.PrepareInstaller(
                    installerPath,
                    "Folia-Setup-0.7.1.exe",
                    source);

            Assert.Equal(Path.GetFullPath(installerPath), installer.FilePath);
            Assert.Equal(Path.GetFullPath(root), installer.WorkingDirectory);
            Assert.Throws<InvalidDataException>(() =>
                SoftwareInstallerWorkflow.PrepareInstaller(
                    installerPath,
                    "untrusted.exe",
                    source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZipReleaseExtractsAndSelectsOnlyTheApprovedExecutable()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string archivePath = Path.Combine(
                root,
                "Office_Tool_with_runtime_v11.6.6.0_x64.zip");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "Office Tool/Office Tool Plus.exe", "program");
                WriteEntry(archive, "Office Tool/Office Tool Plus.Console.exe", "console");
            }

            var source = new GitHubReleaseDownload(
                "YerongAI/Office-Tool",
                @"^Office_Tool_with_runtime_.*_x64\.zip$",
                @"^Office Tool Plus\.exe$");
            PreparedSoftwareInstaller installer =
                SoftwareInstallerWorkflow.PrepareInstaller(
                    archivePath,
                    Path.GetFileName(archivePath),
                    source);

            Assert.Equal("Office Tool Plus.exe", Path.GetFileName(installer.FilePath));
            Assert.True(File.Exists(installer.FilePath));
            Assert.StartsWith(
                Path.GetDirectoryName(archivePath)!,
                installer.FilePath,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZipReleaseRejectsPathTraversal()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string archivePath = Path.Combine(root, "trusted.zip");
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "../Trusted.exe", "program");
            }

            var source = new GitHubReleaseDownload(
                "owner/repository",
                @"^trusted\.zip$",
                @"^Trusted\.exe$");

            Assert.Throws<InvalidDataException>(() =>
                SoftwareInstallerWorkflow.PrepareInstaller(
                    archivePath,
                    "trusted.zip",
                    source));
            Assert.False(File.Exists(Path.Combine(root, "Trusted.exe")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ReToolbox-SoftwareInstaller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using StreamWriter writer = new(entry.Open());
        writer.Write(content);
    }
}
