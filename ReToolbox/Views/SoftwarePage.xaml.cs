using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ReToolbox.Models;
using ReToolbox.Utils;
using ReToolbox.ViewModels;

namespace ReToolbox.Views
{
    public sealed partial class SoftwarePage : Page
    {
        private readonly Dictionary<SoftwareItem, CheckBox> _itemCheckBoxes = new();
        private readonly Dictionary<SoftwareItem, TextBlock> _itemStatusLabels = new();
        private bool _subscriptionsActive;

        public SoftwarePageViewModel ViewModel { get; }

        public SoftwarePage()
        {
            ViewModel = App.Services.GetService<SoftwarePageViewModel>()
                ?? new SoftwarePageViewModel(App.Services.GetService<Services.SoftwareInstallService>()!);

            InitializeComponent();

            AttachSubscriptions();
            BuildSoftwareList();
            UpdateSelectionState();
            Loaded += SoftwarePage_Loaded;
            Unloaded += SoftwarePage_Unloaded;
        }

        private void SoftwarePage_Loaded(object sender, RoutedEventArgs e)
        {
            AttachSubscriptions();
            BuildSoftwareList();
            PageAnimations.StaggerIn(this);
        }

        private void SoftwarePage_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachSubscriptions();
        }

        private void AttachSubscriptions()
        {
            if (_subscriptionsActive)
            {
                return;
            }

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.InstallLogs.CollectionChanged -= InstallLogs_CollectionChanged;
            ViewModel.InstallLogs.CollectionChanged += InstallLogs_CollectionChanged;
            foreach (SoftwareItem item in ViewModel.SoftwareItems)
            {
                item.PropertyChanged -= SoftwareItem_PropertyChanged;
                item.PropertyChanged += SoftwareItem_PropertyChanged;
            }

            _subscriptionsActive = true;
        }

        private void DetachSubscriptions()
        {
            if (!_subscriptionsActive)
            {
                return;
            }

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.InstallLogs.CollectionChanged -= InstallLogs_CollectionChanged;
            foreach (SoftwareItem item in ViewModel.SoftwareItems)
            {
                item.PropertyChanged -= SoftwareItem_PropertyChanged;
            }

            _subscriptionsActive = false;
        }

        private void BuildSoftwareList()
        {
            Style? cardStyle = TryGetAppStyle("ConfigurationSettingsCardTemplate");
            Style? categoryTitleStyle = TryGetAppStyle("CategoryTitleStyle");
            IReadOnlyList<SoftwareItem> visibleItems = ViewModel.GetVisibleSoftware();

            SoftwareListPanel.Children.Clear();
            _itemCheckBoxes.Clear();
            _itemStatusLabels.Clear();

            foreach (IGrouping<string, SoftwareItem> group in visibleItems.GroupBy(item => item.Category))
            {
                SoftwareListPanel.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    Style = categoryTitleStyle
                });

                foreach (SoftwareItem item in group)
                {
                    SoftwareListPanel.Children.Add(CreateSoftwareCard(item, cardStyle));
                }
            }

            int categoryCount = visibleItems.Select(item => item.Category).Distinct().Count();
            VisibleCountText.Text = visibleItems.Count == 0
                ? "当前筛选条件下没有结果"
                : $"{visibleItems.Count} 款软件 · {categoryCount} 个分类";
            EmptyStatePanel.Visibility = visibleItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            SoftwareListPanel.Visibility = visibleItems.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            UpdateSelectionState();
        }

        private UIElement CreateSoftwareCard(SoftwareItem item, Style? cardStyle)
        {
            var card = new CommunityToolkit.WinUI.Controls.SettingsCard
            {
                Header = item.Name,
                Description = $"{item.Description} · 来源：{item.DistributionLabel}",
                DataContext = item,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                HeaderIcon = CreateHeaderIcon(item)
            };

            if (cardStyle is not null)
            {
                card.Style = cardStyle;
            }

            var statusLabel = new TextBlock
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Foreground = GetStatusBrush(item.InstallStatus),
                Text = item.InstallStatus,
                Visibility = string.IsNullOrWhiteSpace(item.InstallStatus)
                    ? Visibility.Collapsed
                    : Visibility.Visible
            };

            var checkBox = new CheckBox
            {
                IsChecked = item.IsSelected,
                IsEnabled = !ViewModel.IsInstalling,
                DataContext = item,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += CheckBox_Checked;
            checkBox.Unchecked += CheckBox_Unchecked;

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(statusLabel);
            content.Children.Add(checkBox);
            card.Content = content;

            _itemCheckBoxes[item] = checkBox;
            _itemStatusLabels[item] = statusLabel;
            return card;
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
            if (!string.IsNullOrWhiteSpace(item.WingetId) && item.WingetSource == "winget")
            {
                return new BitmapIcon
                {
                    UriSource = new Uri($"https://api.winstall.app/icons/next/{item.WingetId}.webp"),
                    ShowAsMonochrome = false
                };
            }

            return new FontIcon { Glyph = item.IconGlyph };
        }

        private static Brush GetStatusBrush(string status)
        {
            string resourceKey = status switch
            {
                "已安装" => "SystemFillColorSuccessBrush",
                "安装程序已启动" => "SystemFillColorSuccessBrush",
                "安装失败" => "SystemFillColorCriticalBrush",
                "已取消" => "SystemFillColorCautionBrush",
                "正在处理" => "AccentTextFillColorPrimaryBrush",
                _ => "TextFillColorSecondaryBrush"
            };

            if (Application.Current.Resources.TryGetValue(resourceKey, out object value) && value is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchText = SearchBox.Text;
            if (SoftwareListPanel is null)
            {
                return;
            }

            BuildSoftwareList();
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem is string category)
            {
                ViewModel.SelectedCategory = category;
                if (SoftwareListPanel is null)
                {
                    return;
                }

                BuildSoftwareList();
            }
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            CategoryComboBox.SelectedIndex = 0;
            ViewModel.SearchText = string.Empty;
            ViewModel.SelectedCategory = "全部分类";
            BuildSoftwareList();
        }

        private void SelectVisible_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectVisibleSoftware();
            RefreshCheckBoxes();
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeselectAllCommand.Execute(null);
            RefreshCheckBoxes();
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

        private void RefreshCheckBoxes()
        {
            foreach ((SoftwareItem item, CheckBox checkBox) in _itemCheckBoxes)
            {
                checkBox.IsChecked = item.IsSelected;
            }

            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            int selectedCount = ViewModel.SelectedCount;
            bool canChangeSelection = !ViewModel.IsInstalling;

            SelectionSummaryText.Text = selectedCount == 0
                ? "尚未选择软件"
                : $"已选择 {selectedCount} 款软件";
            SelectionHintText.Text = selectedCount == 0
                ? "勾选软件后即可开始批量安装"
                : "将按列表顺序依次处理；失败项目会保留选择";
            InstallButton.Content = ViewModel.IsInstalling ? "正在安装..." : ViewModel.InstallButtonText;
            InstallButton.IsEnabled = ViewModel.HasSelection && canChangeSelection;
            SelectVisibleButton.IsEnabled = canChangeSelection && ViewModel.GetVisibleSoftware().Count > 0;
            ClearSelectionButton.IsEnabled = canChangeSelection && ViewModel.HasSelection;

            foreach (CheckBox checkBox in _itemCheckBoxes.Values)
            {
                checkBox.IsEnabled = canChangeSelection;
            }
        }

        private async void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            await RunSelectedInstallationAsync();
        }

        private async Task RunSelectedInstallationAsync()
        {
            if (!ViewModel.HasSelection)
            {
                ShowStatus("请先选择至少一款软件", InfoBarSeverity.Warning, "未选择软件");
                return;
            }

            InstallActivitySection.Visibility = Visibility.Visible;
            InstallActivitySection.StartBringIntoView();
            ActivityTitleText.Text = "正在安装所选软件";
            CancelInstallButton.IsEnabled = true;
            CancelInstallButton.Visibility = Visibility.Visible;
            RetryFailedButton.Visibility = Visibility.Collapsed;
            CopyLogsButton.IsEnabled = false;
            StatusInfoBar.IsOpen = false;
            UpdateSelectionState();

            await ViewModel.InstallSelectedCommand.ExecuteAsync(null);

            ActivityTitleText.Text = ViewModel.BatchResultTitle;
            CancelInstallButton.Visibility = Visibility.Collapsed;
            RetryFailedButton.Visibility = ViewModel.HasFailedItems
                ? Visibility.Visible
                : Visibility.Collapsed;
            CopyLogsButton.IsEnabled = ViewModel.InstallLogs.Count > 0;
            RefreshCheckBoxes();
            RefreshStatusLabels();

            InfoBarSeverity severity = ViewModel.BatchResultTitle switch
            {
                "安装完成" => InfoBarSeverity.Success,
                "请继续完成手动安装" => InfoBarSeverity.Warning,
                "安装已取消" => InfoBarSeverity.Warning,
                _ => InfoBarSeverity.Error
            };
            ShowStatus(ViewModel.CurrentItemText, severity, ViewModel.BatchResultTitle);
        }

        private async void RetryFailed_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectFailedSoftware();
            RefreshCheckBoxes();
            await RunSelectedInstallationAsync();
        }

        private void CancelInstall_Click(object sender, RoutedEventArgs e)
        {
            CancelInstallButton.IsEnabled = false;
            ActivityTitleText.Text = "正在取消安装";
            ViewModel.CancelInstallCommand.Execute(null);
        }

        private void InstallLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            CopyLogsButton.IsEnabled = ViewModel.InstallLogs.Count > 0;
            if (ViewModel.InstallLogs.Count > 0)
            {
                InstallLogListView.ScrollIntoView(ViewModel.InstallLogs[^1]);
            }
        }

        private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.InstallLogs.Count == 0)
            {
                ShowStatus("当前还没有可复制的安装日志", InfoBarSeverity.Informational, "安装日志");
                return;
            }

            string text = string.Join(Environment.NewLine, ViewModel.InstallLogs);
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            ShowStatus("安装日志已复制到剪贴板", InfoBarSeverity.Success, "复制成功");
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwarePageViewModel.IsInstalling))
            {
                UpdateSelectionState();
            }
        }

        private void SoftwareItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not SoftwareItem item)
            {
                return;
            }

            if (e.PropertyName == nameof(SoftwareItem.IsSelected))
            {
                if (_itemCheckBoxes.TryGetValue(item, out CheckBox? checkBox) &&
                    checkBox.IsChecked != item.IsSelected)
                {
                    checkBox.IsChecked = item.IsSelected;
                }

                UpdateSelectionState();
            }
            else if (e.PropertyName == nameof(SoftwareItem.InstallStatus))
            {
                RefreshStatusLabel(item);
            }
        }

        private void RefreshStatusLabels()
        {
            foreach (SoftwareItem item in _itemStatusLabels.Keys.ToArray())
            {
                RefreshStatusLabel(item);
            }
        }

        private void RefreshStatusLabel(SoftwareItem item)
        {
            if (!_itemStatusLabels.TryGetValue(item, out TextBlock? label))
            {
                return;
            }

            label.Text = item.InstallStatus;
            label.Foreground = GetStatusBrush(item.InstallStatus);
            label.Visibility = string.IsNullOrWhiteSpace(item.InstallStatus)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ShowStatus(string message, InfoBarSeverity severity, string title)
        {
            StatusInfoBar.Title = title;
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}
