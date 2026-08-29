using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Services;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class DefenderPage : Page
    {
        public DefenderPageViewModel ViewModel { get; }

        public DefenderPage()
        {
            ViewModel = App.Services.GetService<DefenderPageViewModel>()
                ?? new DefenderPageViewModel(App.Services.GetService<DefenderService>()!);

            InitializeComponent();
            UpdatePrimaryActionButton();
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }

        private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
        {
            DefenderRemovalProfile fullProfile = DefenderRemovalWorkflow.GetProfile(DefenderRemovalMode.Full);
            DefenderRemovalProfile antivirusOnlyProfile = DefenderRemovalWorkflow.GetProfile(DefenderRemovalMode.AntivirusOnly);

            RadioButton fullOption = CreateModeOption(fullProfile, isChecked: true);
            RadioButton antivirusOnlyOption = CreateModeOption(antivirusOnlyProfile, isChecked: false);
            StackPanel options = new StackPanel { Spacing = 12 };
            options.Children.Add(fullOption);
            options.Children.Add(antivirusOnlyOption);

            ContentDialog confirmation = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "选择 Defender 移除范围",
                Content = options,
                PrimaryButtonText = "确认并继续",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            DefenderRemovalMode mode = antivirusOnlyOption.IsChecked == true
                ? DefenderRemovalMode.AntivirusOnly
                : DefenderRemovalMode.Full;
            DefenderRemovalProfile selectedProfile = DefenderRemovalWorkflow.GetProfile(mode);

            ContentDialog finalConfirmation = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"确认{selectedProfile.DisplayName}？",
                Content = $"{selectedProfile.Description}\n\n此操作可能不可逆并会降低系统防护能力，且上游工具会安排系统重启。建议先创建系统还原点。",
                PrimaryButtonText = "继续执行",
                CloseButtonText = "返回",
                DefaultButton = ContentDialogButton.Close
            };

            if (await finalConfirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            PrimaryActionButton.IsEnabled = false;
            RemoveProgress.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = $"正在准备：{selectedProfile.DisplayName}...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            try
            {
                await ViewModel.RemoveDefenderCommand.ExecuteAsync(mode);
                StatusInfoBar.Message = ViewModel.StatusMessage;
                StatusInfoBar.Severity = ViewModel.StatusMessage.Contains("流程已完成")
                    ? InfoBarSeverity.Success
                    : InfoBarSeverity.Warning;
            }
            catch (Exception ex)
            {
                StatusInfoBar.Message = $"移除失败：{ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                RemoveProgress.Visibility = Visibility.Collapsed;
                UpdatePrimaryActionButton();
            }
        }

        private static RadioButton CreateModeOption(
            DefenderRemovalProfile profile,
            bool isChecked)
        {
            StackPanel content = new StackPanel { Spacing = 3 };
            content.Children.Add(new TextBlock
            {
                Text = profile.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            content.Children.Add(new TextBlock
            {
                Text = profile.Description,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            return new RadioButton
            {
                GroupName = "DefenderRemovalMode",
                Content = content,
                IsChecked = isChecked
            };
        }

        private async void OpenSecurity_Click(object sender, RoutedEventArgs e)
        {
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Message = "正在打开 Windows 安全中心...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;

            await Task.Run(() => App.Services.GetService<DefenderService>()!.OpenWindowsSecurity());

            StatusInfoBar.Message = "已打开 Windows 安全中心";
            StatusInfoBar.Severity = InfoBarSeverity.Success;
        }

        private void UpdatePrimaryActionButton()
        {
            PrimaryActionButton.Content = "选择移除模式";
            PrimaryActionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            PrimaryActionButton.IsEnabled = !ViewModel.IsRemoving;
        }
    }
}
