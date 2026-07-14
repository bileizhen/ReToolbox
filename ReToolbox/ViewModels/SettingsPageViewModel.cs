using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using ReToolbox.Utils;

namespace ReToolbox.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
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

        public SettingsPageViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is not null)
            {
                Version = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }
    }
}
