using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using ReToolbox.Services;
using System;
using System.Diagnostics;
using System.Linq;

namespace ReToolbox
{
    public sealed partial class MainWindow : Window
    {
        private bool _startupUpdateCheckStarted;

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
            RootGrid.Loaded += RootGrid_Loaded;

            // 默认选中第一项并导航到主页
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First();
            NavigateTo("ReToolbox.Views.HomePage");
        }

        private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (_startupUpdateCheckStarted)
            {
                return;
            }

            _startupUpdateCheckStarted = true;
            AppUpdateService updateService =
                App.Services.GetRequiredService<AppUpdateService>();
            if (!updateService.CheckOnStartupEnabled)
            {
                return;
            }

            try
            {
                UpdateCheckResult check = await updateService.CheckForUpdatesAsync();
                if (check.State != UpdateCheckState.UpdateAvailable ||
                    check.Release is null)
                {
                    return;
                }

                if (!updateService.AutomaticDownloadEnabled)
                {
                    await ShowAvailableUpdateAsync(updateService, check.Release);
                    return;
                }

                DownloadedUpdate downloaded;
                try
                {
                    downloaded = await updateService.DownloadUpdateAsync(check.Release);
                }
                catch (Exception ex)
                {
                    await ShowAvailableUpdateAsync(
                        updateService,
                        check.Release,
                        $"自动下载失败：{ex.Message}");
                    return;
                }

                ContentDialog installDialog = new ContentDialog
                {
                    XamlRoot = RootGrid.XamlRoot,
                    Title = $"{downloaded.Release.TagName} 已准备就绪",
                    Content = "更新已下载并通过文件大小与 SHA-256 校验。是否关闭 ReToolbox 并启动安装程序？",
                    PrimaryButtonText = "立即安装",
                    CloseButtonText = "稍后",
                    DefaultButton = ContentDialogButton.Primary
                };
                if (await installDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        await updateService.LaunchInstallerAsync(downloaded);
                        Close();
                    }
                    catch (Exception ex)
                    {
                        await ShowUpdateErrorAsync($"无法启动更新安装器：{ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup update check failed: {ex}");
            }
        }

        private async System.Threading.Tasks.Task ShowAvailableUpdateAsync(
            AppUpdateService updateService,
            UpdateRelease release,
            string? detail = null)
        {
            string message = $"发现新版本 {release.TagName}。";
            if (!string.IsNullOrWhiteSpace(detail))
            {
                message += $"\n\n{detail}";
            }

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "发现 ReToolbox 更新",
                Content = message,
                PrimaryButtonText = "查看发布页",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                updateService.OpenReleasePage(release);
            }
        }

        private async System.Threading.Tasks.Task ShowUpdateErrorAsync(string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "更新失败",
                Content = message,
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close
            };
            await dialog.ShowAsync();
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
                string? navItemTag = args.SelectedItemContainer.Tag?.ToString();
                Type? pageType = navItemTag is null ? null : Type.GetType(navItemTag);
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
            Type? pageType = Type.GetType(pageTag);
            if (pageType != null)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void ThemeToggle_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleTheme();
        }

        private void ThemeToggle_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
            {
                ToggleTheme();
                e.Handled = true;
            }
        }

        private void ToggleTheme()
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
