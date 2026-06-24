using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Models;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class SoftwarePage : Page
    {
        public SoftwarePageViewModel ViewModel { get; }

        public SoftwarePage()
        {
            ViewModel = App.Services.GetService<SoftwarePageViewModel>()
                ?? new SoftwarePageViewModel(App.Services.GetService<Services.SoftwareInstallService>()!);

            InitializeComponent();
            BuildSoftwareList();
        }

        private void BuildSoftwareList()
        {
            Style? cardStyle = TryGetAppStyle("ConfigurationSettingsCardTemplate");
            SoftwareListPanel.Children.Clear();

            foreach (var item in ViewModel.SoftwareItems)
            {
                var card = new CommunityToolkit.WinUI.Controls.SettingsCard
                {
                    Header = item.Name,
                    DataContext = item,
                    HorizontalContentAlignment = HorizontalAlignment.Right
                };

                if (cardStyle is not null)
                {
                    card.Style = cardStyle;
                }

                card.HeaderIcon = CreateHeaderIcon(item);

                var checkBox = new CheckBox
                {
                    IsChecked = item.IsSelected,
                    DataContext = item,
                    Margin = new Thickness(0, 0, -75, 0)
                };
                checkBox.Checked += CheckBox_Checked;
                checkBox.Unchecked += CheckBox_Unchecked;

                card.Content = checkBox;
                SoftwareListPanel.Children.Add(card);
            }
        }

        private static Style? TryGetAppStyle(string key)
        {
            if (Application.Current?.Resources.TryGetValue(key, out object value) == true)
            {
                return value as Style;
            }

            return null;
        }

        private static IconElement CreateHeaderIcon(SoftwareItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.WingetId))
            {
                return new BitmapIcon
                {
                    UriSource = new Uri($"https://api.winstall.app/icons/next/{item.WingetId}.webp"),
                    ShowAsMonochrome = false
                };
            }

            return new FontIcon { Glyph = item.IconGlyph };
        }

        private void RefreshCheckBoxes()
        {
            foreach (var child in SoftwareListPanel.Children)
            {
                if (child is CommunityToolkit.WinUI.Controls.SettingsCard card &&
                    card.Content is CheckBox cb &&
                    cb.DataContext is SoftwareItem item)
                {
                    cb.IsChecked = item.IsSelected;
                }
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is SoftwareItem item)
            {
                item.IsSelected = true;
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is SoftwareItem item)
            {
                item.IsSelected = false;
            }
        }

        private async void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.SoftwareItems.Any(item => item.IsSelected))
            {
                return;
            }

            ProgressRingStackPanel.Visibility = Visibility.Visible;
            DownloadingProgressBar.Value = 0;

            await ViewModel.InstallSelectedCommand.ExecuteAsync(null);

            ProgressRingStackPanel.Visibility = Visibility.Collapsed;
            RefreshCheckBoxes();
        }
    }
}
