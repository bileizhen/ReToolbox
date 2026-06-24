using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Models;
using ReToolbox.Services;

namespace ReToolbox.ViewModels
{
    public partial class SoftwarePageViewModel : ObservableObject
    {
        private readonly SoftwareInstallService _installService;

        public ObservableCollection<SoftwareItem> SoftwareItems { get; }

        // Lines streamed into the install progress dialog.
        public ObservableCollection<string> InstallLogs { get; } = new();

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private string _currentItemText = string.Empty;

        [ObservableProperty]
        private int _overallProgress;

        [ObservableProperty]
        private int _downloadProgress;

        [ObservableProperty]
        private bool _isDownloading;

        public SoftwarePageViewModel(SoftwareInstallService installService)
        {
            _installService = installService;
            SoftwareItems = new ObservableCollection<SoftwareItem>(_installService.GetSoftwareList());
        }

        public List<SoftwareItem> GetSoftwareByCategory(string category)
        {
            return SoftwareItems.Where(s => s.Category == category).ToList();
        }

        public HashSet<string> GetCategories()
        {
            return SoftwareItems.Select(s => s.Category).ToHashSet();
        }

        [RelayCommand]
        private async Task InstallSelectedAsync()
        {
            var selected = SoftwareItems.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            IsInstalling = true;
            InstallLogs.Clear();
            OverallProgress = 0;
            DownloadProgress = 0;
            IsDownloading = false;

            int completed = 0;
            int total = selected.Count;

            foreach (var item in selected)
            {
                CurrentItemText = $"{item.Name}  ({completed + 1}/{total})";
                // The download progress bar is meaningful both for winget items
                // (winget reports download progress) and direct-download items (mpv).
                IsDownloading = string.IsNullOrWhiteSpace(item.WingetId) &&
                                !string.IsNullOrWhiteSpace(item.DownloadUrl);
                DownloadProgress = 0;

                var log = new Progress<string>(message =>
                    InstallLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}"));

                var download = new Progress<int>(percent =>
                {
                    DownloadProgress = percent;
                    // Show the bar while a download is in progress for any item type.
                    IsDownloading = percent > 0 && percent < 100;
                });

                await _installService.InstallSoftwareAsync(item, log, download);

                completed++;
                OverallProgress = completed * 100 / total;
                IsDownloading = false;
            }

            CurrentItemText = $"全部完成（共 {total} 项）";
            IsInstalling = false;
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in SoftwareItems)
                item.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var item in SoftwareItems)
                item.IsSelected = false;
        }
    }
}
