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

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private int _progressValue;

        [ObservableProperty]
        private string _currentInstalling = string.Empty;

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
            if (selected.Count == 0) return;

            IsInstalling = true;
            StatusText = $"正在安装 {selected.Count} 个软件...";
            ProgressValue = 0;

            int completed = 0;
            int total = selected.Count;

            foreach (var item in selected)
            {
                CurrentInstalling = $"正在安装 {item.Name}... ({completed + 1}/{total})";
                await _installService.InstallSoftwareAsync(item);
                completed++;
                ProgressValue = completed * 100 / total;
            }

            StatusText = "安装完成";
            CurrentInstalling = string.Empty;
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
