using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Services;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class SystemInfoPage : Page
    {
        public SystemInfoPageViewModel ViewModel { get; }

        public SystemInfoPage()
        {
            ViewModel = App.Services.GetService<SystemInfoPageViewModel>()
                ?? new SystemInfoPageViewModel(App.Services.GetService<SystemInfoService>()!);

            InitializeComponent();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshCommand.Execute(null);
        }
    }
}
