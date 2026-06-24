using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace ReToolbox.Controls
{
    public sealed partial class HeaderTile : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(HeaderTile), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(HeaderTile), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(object), typeof(HeaderTile), new PropertyMetadata(null));

        public static readonly DependencyProperty TargetPageProperty =
            DependencyProperty.Register(nameof(TargetPage), typeof(string), typeof(HeaderTile), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ExternalLinkProperty =
            DependencyProperty.Register(nameof(ExternalLink), typeof(string), typeof(HeaderTile), new PropertyMetadata(string.Empty));

        public event EventHandler<string>? NavigationRequested;

        public HeaderTile()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object? Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public string TargetPage
        {
            get => (string)GetValue(TargetPageProperty);
            set => SetValue(TargetPageProperty, value);
        }

        public string ExternalLink
        {
            get => (string)GetValue(ExternalLinkProperty);
            set => SetValue(ExternalLinkProperty, value);
        }

        private async void TileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ExternalLink) &&
                Uri.TryCreate(ExternalLink, UriKind.Absolute, out Uri? uri))
            {
                await Launcher.LaunchUriAsync(uri);
                return;
            }

            if (!string.IsNullOrWhiteSpace(TargetPage))
            {
                NavigationRequested?.Invoke(this, TargetPage);
            }
        }
    }
}
