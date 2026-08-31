using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReToolbox.Services;
using ReToolbox.Utils;

namespace ReToolbox.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly AppUpdateService _updateService;

        [ObservableProperty]
        private string _appName = "ReToolbox";

        [ObservableProperty]
        private string _version = "v0.0.0";

        [ObservableProperty]
        private string _description = "Windows 重装后一键配置工具";

        [ObservableProperty]
        private string _githubLink = "https://github.com";

        // Mirror acceleration is persisted in the registry (HKLM\SOFTWARE\ReToolbox);
        // the getter re-reads it so the toggle reflects whatever the service layer sees.
        private bool _isGitHubMirrorEnabled = GitHubMirrorHelper.IsEnabled;

        public bool IsGitHubMirrorEnabled
        {
            get => _isGitHubMirrorEnabled;
            set
            {
                if (SetProperty(ref _isGitHubMirrorEnabled, value))
                {
                    GitHubMirrorHelper.IsEnabled = value;
                }
            }
        }

        // Which mirror to try first; empty = auto (preset order). The user can pick a
        // preset from the dropdown or type a custom mirror URL. Persisted in the
        // registry alongside the enable flag, and normalized there too.
        private string _selectedMirror = GitHubMirrorHelper.SelectedMirror;

        public string SelectedMirror
        {
            get => _selectedMirror;
            set
            {
                if (SetProperty(ref _selectedMirror, value))
                {
                    GitHubMirrorHelper.SelectedMirror = value;
                }
            }
        }

        // Preset mirror URLs offered in the settings dropdown.
        public List<string> MirrorPresets => GitHubMirrorHelper.Mirrors.ToList();

        private bool _checkUpdatesOnStartup;

        public bool CheckUpdatesOnStartup
        {
            get => _checkUpdatesOnStartup;
            set
            {
                if (SetProperty(ref _checkUpdatesOnStartup, value))
                {
                    _updateService.CheckOnStartupEnabled = value;
                }
            }
        }

        private bool _automaticUpdateEnabled;

        public bool AutomaticUpdateEnabled
        {
            get => _automaticUpdateEnabled;
            set
            {
                if (SetProperty(ref _automaticUpdateEnabled, value))
                {
                    _updateService.AutomaticDownloadEnabled = value;
                }
            }
        }

        private bool _isCheckingForUpdates;

        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            private set => SetProperty(ref _isCheckingForUpdates, value);
        }

        private string _updateStatusText = "可立即检查 GitHub 上的最新正式版本";

        public string UpdateStatusText
        {
            get => _updateStatusText;
            private set => SetProperty(ref _updateStatusText, value);
        }

        public SettingsPageViewModel(AppUpdateService updateService)
        {
            _updateService = updateService;
            _checkUpdatesOnStartup = updateService.CheckOnStartupEnabled;
            _automaticUpdateEnabled = updateService.AutomaticDownloadEnabled;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is not null)
            {
                Version = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            if (IsCheckingForUpdates)
            {
                return;
            }

            IsCheckingForUpdates = true;
            UpdateStatusText = "正在检查更新...";
            try
            {
                UpdateCheckResult result = await _updateService.CheckForUpdatesAsync();
                UpdateStatusText = result.Message;
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }
    }
}
