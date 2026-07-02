using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using ReToolbox.Services;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class WindowsUpdatePage : Page
    {
        private readonly WindowsUpdateService _updateService;
        private readonly DispatcherQueueTimer _statusTimer;
        private bool _isStatusVisible;
        private int _statusVersion;
        private bool _isPageReady;

        public WindowsUpdatePageViewModel ViewModel { get; }

        public WindowsUpdatePage()
        {
            _updateService = App.Services.GetService<WindowsUpdateService>()!;
            ViewModel = App.Services.GetService<WindowsUpdatePageViewModel>()
                ?? new WindowsUpdatePageViewModel(_updateService);

            InitializeComponent();
            Loaded += WindowsUpdatePage_Loaded;
            Loaded += (s, e) => PageAnimations.StaggerIn(this);

            _statusTimer = DispatcherQueue.CreateTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(3);
            _statusTimer.IsRepeating = false;
            _statusTimer.Tick += async (_, _) => await HideStatusAsync(_statusVersion);
        }

        private void WindowsUpdatePage_Loaded(object sender, RoutedEventArgs e)
        {
            _isPageReady = true;
        }

        private void PauseToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isPageReady)
            {
                return;
            }

            if (sender is ToggleSwitch toggleSwitch)
            {
                ViewModel.SetUpdatePauseState(toggleSwitch.IsOn);
                ViewModel.RefreshState();
                ShowStatus(ViewModel.StatusMessage);
            }
        }

        private void DriverToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isPageReady)
            {
                return;
            }

            if (sender is not ToggleSwitch toggleSwitch)
            {
                return;
            }

            if (toggleSwitch.IsOn)
                _updateService.DisableDriverUpdates();
            else
                _updateService.EnableDriverUpdates();

            ViewModel.AreDriverUpdatesDisabled = toggleSwitch.IsOn;
            ViewModel.RefreshState();
            ShowStatus(toggleSwitch.IsOn ? "驱动更新已禁用" : "驱动更新已启用");
        }

        private void TenYearPauseToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isPageReady)
            {
                return;
            }

            if (sender is ToggleSwitch toggleSwitch)
            {
                ViewModel.SetTenYearPauseState(toggleSwitch.IsOn);
                ViewModel.RefreshState();
                ShowStatus(ViewModel.StatusMessage);
            }
        }

        private void SummaryActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsTenYearPauseEnabled)
            {
                ViewModel.SetTenYearPauseState(false);
                ViewModel.RefreshState();
                ShowStatus(ViewModel.StatusMessage);
                return;
            }

            ViewModel.RefreshState();
            ShowStatus("Windows 更新状态已刷新");
        }

        private void ResetPolicies_Click(object sender, RoutedEventArgs e)
        {
            _updateService.ResetToDefaultPolicies();
            ViewModel.RefreshState();
            ShowStatus("Windows Update 禁更策略已清理，请重新打开系统设置检查更新状态");
        }

        private async void ShowStatus(string message)
        {
            _statusTimer.Stop();
            int currentVersion = ++_statusVersion;
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = GetStatusSeverity(message);

            if (StatusInfoBar.RenderTransform is not TranslateTransform transform)
            {
                StatusInfoBar.RenderTransform = transform = new TranslateTransform();
            }

            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Opacity = 0;
            transform.Y = 18;

            await AnimateStatusAsync(show: true);
            if (currentVersion != _statusVersion)
            {
                return;
            }

            _isStatusVisible = true;
            _statusTimer.Start();
        }

        private static InfoBarSeverity GetStatusSeverity(string message)
        {
            if (message.Contains("失败", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("错误", StringComparison.OrdinalIgnoreCase))
            {
                return InfoBarSeverity.Error;
            }

            if (message.Contains("禁用", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("暂停 10 年", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("策略禁用", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("被限制", StringComparison.OrdinalIgnoreCase))
            {
                return InfoBarSeverity.Warning;
            }

            return InfoBarSeverity.Success;
        }

        private async Task HideStatusAsync(int version)
        {
            _statusTimer.Stop();
            if (!_isStatusVisible || version != _statusVersion)
            {
                return;
            }

            await AnimateStatusAsync(show: false);
            if (version != _statusVersion)
            {
                return;
            }

            StatusInfoBar.IsOpen = false;
            _isStatusVisible = false;
        }

        public Visibility GetFontBadgeVisibility(string badgeKind)
        {
            return string.Equals(badgeKind, "pause", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public Visibility GetPauseBadgeVisibility(string badgeKind)
        {
            return string.Equals(badgeKind, "pause", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private Task AnimateStatusAsync(bool show)
        {
            var tcs = new TaskCompletionSource();

            if (StatusInfoBar.RenderTransform is not TranslateTransform transform)
            {
                StatusInfoBar.RenderTransform = transform = new TranslateTransform();
            }

            var opacityAnimation = new DoubleAnimation
            {
                To = show ? 1 : 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(show ? 220 : 180)),
                EnableDependentAnimation = true
            };

            var translateAnimation = new DoubleAnimation
            {
                To = show ? 0 : 18,
                Duration = new Duration(TimeSpan.FromMilliseconds(show ? 220 : 180)),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn }
            };

            Storyboard.SetTarget(opacityAnimation, StatusInfoBar);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            Storyboard.SetTarget(translateAnimation, StatusInfoBar);
            Storyboard.SetTargetProperty(translateAnimation, "(UIElement.RenderTransform).(TranslateTransform.Y)");

            var storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(translateAnimation);
            storyboard.Completed += (_, _) => tcs.TrySetResult();
            storyboard.Begin();

            return tcs.Task;
        }
    }
}
