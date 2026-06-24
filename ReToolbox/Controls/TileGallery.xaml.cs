using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ReToolbox.Controls
{
    public sealed partial class TileGallery : UserControl
    {
        public event EventHandler<string>? NavigationRequested;

        public TileGallery()
        {
            InitializeComponent();
        }

        private void HeaderTile_NavigationRequested(object sender, string pageTag)
        {
            NavigationRequested?.Invoke(this, pageTag);
        }

        private void scroller_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (e.FinalView.HorizontalOffset < 1)
            {
                ScrollBackBtn.Visibility = Visibility.Collapsed;
            }
            else if (e.FinalView.HorizontalOffset > 1)
            {
                ScrollBackBtn.Visibility = Visibility.Visible;
            }

            if (e.FinalView.HorizontalOffset > scroller.ScrollableWidth - 1)
            {
                ScrollForwardBtn.Visibility = Visibility.Collapsed;
            }
            else if (e.FinalView.HorizontalOffset < scroller.ScrollableWidth - 1)
            {
                ScrollForwardBtn.Visibility = Visibility.Visible;
            }
        }

        private void ScrollBackBtn_Click(object sender, RoutedEventArgs e)
        {
            scroller.ChangeView(scroller.HorizontalOffset - scroller.ViewportWidth, null, null);
            ScrollForwardBtn.Focus(FocusState.Programmatic);
        }

        private void ScrollForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            scroller.ChangeView(scroller.HorizontalOffset + scroller.ViewportWidth, null, null);
            ScrollBackBtn.Focus(FocusState.Programmatic);
        }

        private void scroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollButtonsVisibility();
        }

        private void UpdateScrollButtonsVisibility()
        {
            if (scroller.ScrollableWidth > 0)
            {
                ScrollForwardBtn.Visibility = Visibility.Visible;
            }
            else
            {
                ScrollForwardBtn.Visibility = Visibility.Collapsed;
                ScrollBackBtn.Visibility = Visibility.Collapsed;
            }
        }
    }
}
