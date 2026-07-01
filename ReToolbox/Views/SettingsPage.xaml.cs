using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPageViewModel ViewModel { get; }

        public SettingsPage()
        {
            ViewModel = App.Services.GetService<SettingsPageViewModel>()
                ?? new SettingsPageViewModel();

            InitializeComponent();
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }
    }
}
