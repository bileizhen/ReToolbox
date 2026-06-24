using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReToolbox.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _appName = "ReToolbox";

        [ObservableProperty]
        private string _version = "1.0.0";

        [ObservableProperty]
        private string _description = "Windows 重装后一键配置工具";

        [ObservableProperty]
        private string _githubLink = "https://github.com";

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
