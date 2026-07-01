using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Controls;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            TileGalleryControl.NavigationRequested += TileGallery_NavigationRequested;
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }

        private void TileGallery_NavigationRequested(object? sender, string pageTag)
        {
            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateTo($"ReToolbox.Views.{pageTag}");
            }
        }

        private void NavigateToSoftware(object sender, RoutedEventArgs e)
        {
            (App.MainWindow as MainWindow)?.NavigateTo("ReToolbox.Views.SoftwarePage");
        }

        private void NavigateToActivation(object sender, RoutedEventArgs e)
        {
            (App.MainWindow as MainWindow)?.NavigateTo("ReToolbox.Views.ActivationPage");
        }

        private void NavigateToWindowsUpdate(object sender, RoutedEventArgs e)
        {
            (App.MainWindow as MainWindow)?.NavigateTo("ReToolbox.Views.WindowsUpdatePage");
        }

        private void NavigateToEdgeRemover(object sender, RoutedEventArgs e)
        {
            (App.MainWindow as MainWindow)?.NavigateTo("ReToolbox.Views.EdgeRemoverPage");
        }
    }
}
