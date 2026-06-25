using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Controls;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class HomePage : Page
    {
        public HomePageViewModel ViewModel { get; }

        public HomePage()
        {
            ViewModel = App.Services.GetService<HomePageViewModel>()
                ?? new HomePageViewModel(
                    App.Services.GetService<Services.SystemInfoService>()!,
                    App.Services.GetService<Services.ActivationService>()!);

            InitializeComponent();
            TileGalleryControl.NavigationRequested += TileGallery_NavigationRequested;
        }

        private void Navigate(string pageName) =>
            (App.MainWindow as MainWindow)?.NavigateTo($"ReToolbox.Views.{pageName}");

        private void TileGallery_NavigationRequested(object? sender, string pageTag) => Navigate(pageTag);

        private void NavigateToSoftware(object sender, RoutedEventArgs e) => Navigate("SoftwarePage");

        private void NavigateToActivation(object sender, RoutedEventArgs e) => Navigate("ActivationPage");

        private void NavigateToWindowsUpdate(object sender, RoutedEventArgs e) => Navigate("WindowsUpdatePage");

        private void NavigateToEdgeRemover(object sender, RoutedEventArgs e) => Navigate("EdgeRemoverPage");

        private void NavigateToSystemInfo(object sender, RoutedEventArgs e) => Navigate("SystemInfoPage");
    }
}
