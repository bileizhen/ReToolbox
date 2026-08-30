using System;

namespace ReToolbox.Services
{
    public sealed record EdgeRemovalScriptRelease(
        string Tag,
        string Commit,
        string FileName,
        Uri DownloadUri,
        long Size,
        string Sha256,
        string[] Arguments);

    public static class EdgeRemovalWorkflow
    {
        public static EdgeRemovalScriptRelease CurrentRelease { get; } = new(
            "v1.9.5",
            "17220aca63d55d0d210d98004a504cbbcf25cb63",
            "RemoveEdge.ps1",
            new Uri(
                "https://raw.githubusercontent.com/he3als/EdgeRemover/" +
                "17220aca63d55d0d210d98004a504cbbcf25cb63/RemoveEdge.ps1"),
            22_818,
            "ca33fe16a9c6baf54b27d18864928fcb62b41886164606bd8dc6cda008d4b168",
            new[] { "-UninstallEdge", "-NonInteractive" });
    }
}
