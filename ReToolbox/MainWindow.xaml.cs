using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace ReToolbox
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            if (AppWindow != null && AppWindow.TitleBar != null)
            {
                AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            }

            // 默认选中第一项并导航到主页
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First();
            NavigateTo("ReToolbox.Views.HomePage");
        }

        private void NavigationViewControl_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(Views.SettingsPage), null, args.RecommendedNavigationTransitionInfo);
            }
            else if (args.SelectedItemContainer != null)
            {
                var navItemTag = args.SelectedItemContainer.Tag.ToString();
                Type pageType = Type.GetType(navItemTag);
                if (pageType != null)
                {
                    ContentFrame.Navigate(pageType, null, args.RecommendedNavigationTransitionInfo);
                }
            }
        }

        private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            NavigationViewControl.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType == typeof(Views.SettingsPage))
            {
                NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.SettingsItem;
            }
            else if (ContentFrame.SourcePageType != null)
            {
                var item = NavigationViewControl.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(n => n.Tag.ToString() == ContentFrame.SourcePageType.FullName);

                if (item != null)
                {
                    NavigationViewControl.SelectedItem = item;
                }
            }
        }
        public void NavigateTo(string pageTag)
        {
            Type pageType = Type.GetType(pageTag);
            if (pageType != null)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}
