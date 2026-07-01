using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using ReToolbox.Services;
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

            // Apply the stored theme (dark by default) before anything is shown.
            ThemeService.Init(RootGrid);
            UpdateTitleBarColors(ThemeService.Current);
            UpdateThemeIcon();

            // 默认选中第一项并导航到主页
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First();
            NavigateTo("ReToolbox.Views.HomePage");
        }

        private void NavigationViewControl_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // Use a slide-in entrance transition so pages animate in rather than
            // hard-cutting. The RecommendedTransitionInfo from the NavigationView
            // is a plain slide; we keep that for consistency.
            var transition = args.RecommendedNavigationTransitionInfo
                ?? new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };

            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(Views.SettingsPage), null, transition);
            }
            else if (args.SelectedItemContainer != null)
            {
                var navItemTag = args.SelectedItemContainer.Tag.ToString();
                Type pageType = Type.GetType(navItemTag);
                if (pageType != null)
                {
                    ContentFrame.Navigate(pageType, null, transition);
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

        private void ThemeToggle_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Flip between dark and light; Default falls back to whatever is current.
            ElementTheme next = ThemeService.Current == ElementTheme.Dark
                ? ElementTheme.Light
                : ElementTheme.Dark;
            ThemeService.Apply(RootGrid, next);
            UpdateTitleBarColors(next);
            bool dark = next == ElementTheme.Dark;
            AnimateThemeSwitch(dark);
            // Keep the footer item's label in sync with the active theme so the
            // expanded pane reads "深色模式" / "浅色模式" correctly.
            ThemeToggle.Content = dark ? "深色模式" : "浅色模式";
        }

        // Keeps the window caption buttons (minimize/maximize/close) legible by
        // syncing their foreground with the active theme. Without this their glyphs
        // can become invisible when the app's theme diverges from the system theme.
        private void UpdateTitleBarColors(ElementTheme theme)
        {
            if (AppWindow?.TitleBar == null) return;

            bool dark = theme != ElementTheme.Light;
            Windows.UI.Color caption = dark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;

            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonForegroundColor = caption;
            titleBar.ButtonHoverForegroundColor = caption;
            titleBar.ButtonHoverBackgroundColor = dark
                ? Microsoft.UI.ColorHelper.FromArgb(0x14, 0xFF, 0xFF, 0xFF)
                : Microsoft.UI.ColorHelper.FromArgb(0x14, 0x00, 0x00, 0x00);
            titleBar.ButtonPressedForegroundColor = caption;
            titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        }

        // Spin the glyph 180° and swap sun/moon at the halfway point — the only
        // flourish, kept minimal so the button reads like a normal title-bar icon.
        private void AnimateThemeSwitch(bool dark)
        {
            // Keep spinning the same direction on each toggle.
            double toAngle = ThemeIconRotate.Angle + 180;
            AnimateDouble(ThemeIconRotate, "Angle", toAngle, 360);

            // Swap the glyph half-way through the spin for a clean reveal.
            var swapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            swapTimer.Tick += (_, _) =>
            {
                swapTimer.Stop();
                ThemeIcon.Glyph = dark ? "\uE708" : "\uE793"; // moon / sun
            };
            swapTimer.Start();
        }

        private static void AnimateDouble(DependencyObject target, string property, double to, int durationMs)
        {
            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, property);
            sb.Children.Add(anim);
            sb.Begin();
        }

        // Set the icon and label to match the stored theme without animating (startup).
        private void UpdateThemeIcon()
        {
            bool dark = ThemeService.Current == ElementTheme.Dark;
            ThemeIcon.Glyph = dark ? "\uE708" : "\uE793";
            ThemeIconRotate.Angle = dark ? 180 : 0;
            ThemeToggle.Content = dark ? "深色模式" : "浅色模式";
        }
    }
}
