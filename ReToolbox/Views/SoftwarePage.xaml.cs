using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReToolbox.Models;
using ReToolbox.Utils;
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
            Loaded += (s, e) => PageAnimations.StaggerIn(this);
        }

        private void BuildSoftwareList()
        {
            Style? cardStyle = TryGetAppStyle("ConfigurationSettingsCardTemplate");
            Style? categoryTitleStyle = TryGetAppStyle("CategoryTitleStyle");
            SoftwareListPanel.Children.Clear();

            // GroupBy preserves first-appearance order, matching the order defined in GetDefaultSoftwareList().
            foreach (var group in ViewModel.SoftwareItems.GroupBy(item => item.Category))
            {
                var categoryTitle = new TextBlock
                {
                    Text = group.Key,
                    Style = categoryTitleStyle
                };
                SoftwareListPanel.Children.Add(categoryTitle);

                foreach (var item in group)
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

            // Open a modal progress dialog for the whole batch.
            InstallProgressDialog.Title = "正在安装软件";
            InstallProgressDialog.CloseButtonText = null; // no dismiss while running
            InstallProgressDialog.XamlRoot = this.XamlRoot;

            ViewModel.InstallLogs.CollectionChanged += InstallLogs_CollectionChanged;

            // Show without awaiting; the dialog stays open while the install runs and
            // progress reports keep its bindings (progress bars + log list) updated.
            var showOperation = InstallProgressDialog.ShowAsync();

            try
            {
                await ViewModel.InstallSelectedCommand.ExecuteAsync(null);
                InstallProgressDialog.Title = "安装完成";
            }
            catch
            {
                InstallProgressDialog.Title = "安装出错";
            }
            finally
            {
                InstallProgressDialog.CloseButtonText = "关闭";
            }

            await showOperation;

            ViewModel.InstallLogs.CollectionChanged -= InstallLogs_CollectionChanged;
            RefreshCheckBoxes();
        }

        private void InstallLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (ViewModel.InstallLogs.Count > 0)
            {
                InstallLogListView.ScrollIntoView(ViewModel.InstallLogs[^1]);
            }
        }

        private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
        {
            string text = string.Join(Environment.NewLine, ViewModel.InstallLogs);
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
    }
}
