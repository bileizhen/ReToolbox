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
                DownloadProgress = 0;
                // Keep the download bar visible during each item. Winget can report
                // download progress later than the process start, so hiding it until
                // the first non-zero value makes the UI look stuck.
                IsDownloading = !string.IsNullOrWhiteSpace(item.WingetId) ||
                                !string.IsNullOrWhiteSpace(item.DownloadUrl);

                var log = new Progress<LogEntry>(entry =>
                {
                    string line = $"[{DateTime.Now:HH:mm:ss}] {entry.Text}";
                    // A Progress entry (winget bar redraw / download chunk) refreshes
                    // the last line in place instead of scrolling a new row in.
                    if (entry.Kind == LogEntryKind.Progress && InstallLogs.Count > 0)
                    {
                        InstallLogs[InstallLogs.Count - 1] = line;
                    }
                    else
                    {
                        InstallLogs.Add(line);
                    }
                });

                var download = new Progress<int>(percent =>
                {
                    DownloadProgress = Math.Clamp(percent, 0, 100);
                    IsDownloading = true;
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
