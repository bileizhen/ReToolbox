using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ReToolbox.Services;

namespace ReToolbox.Models
{
    public partial class SoftwareItem : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string WingetId { get; set; } = string.Empty;
        public string WingetSource { get; set; } = "winget";
        public Uri? OfficialPageUri { get; set; }
        public GitHubReleaseDownload? GitHubRelease { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE8A7";

        public string DistributionLabel => GitHubRelease is not null
            ? "GitHub Release（镜像优先）"
            : OfficialPageUri is not null
            ? "官方网站"
            : WingetSource == "msstore" ? "Microsoft Store" : "winget";

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isInstalled;

        [ObservableProperty]
        private string _installStatus = string.Empty;
    }
}
