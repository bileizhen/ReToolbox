using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace ReToolbox.Utils
{
    // Reusable entrance + hover animations for WinUI pages. Keeps every page
    // feeling consistent without hand-writing a Storyboard per page.
    //
    // Usage from a page:
    //   Loaded += (s, e) => PageAnimations.StaggerIn(this);
    public static class PageAnimations
    {
        // Fades and slides up the visible children of the page's content root,
        // staggered so cards appear one after another. Identifies SettingsCards
        // and generic FrameworkElements with x:Name to animate, and skips items
        // that are collapsed.
        public static void StaggerIn(Page page)
        {
            FrameworkElement root = FindAnimatableRoot(page);
            if (root == null) return;

            var targets = CollectAnimatableChildren(root);
            if (targets.Count == 0) return;

            int index = 0;
            foreach (var child in targets)
            {
                AnimateIn(child, index * 60, 420);
                index++;
            }
        }

        // Walks the visual tree to find the first meaningful layout container
        // (ScrollViewer's content, a StackPanel, a Grid with multiple children).
        private static FrameworkElement FindAnimatableRoot(Page page)
        {
            // The page's content is usually a Grid or ScrollViewer.
            if (page.Content is ScrollViewer sv && sv.Content is FrameworkElement svContent)
                return svContent;

            if (page.Content is FrameworkElement direct)
                return direct;

            return null;
        }

        // Collects direct children that should animate. For a Grid/StackPanel we
        // take the top-level children; for nested layouts we go one level deep.
        private static List<FrameworkElement> CollectAnimatableChildren(FrameworkElement root)
        {
            var result = new List<FrameworkElement>();

            if (root is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is FrameworkElement fe && fe.Visibility == Visibility.Visible)
                        result.Add(fe);
                }
            }
            else if (root is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is FrameworkElement fe && fe.Visibility == Visibility.Visible)
                        result.Add(fe);
                }
            }

            return result;
        }

        // Single-element fade + slide-up entrance.
        public static void AnimateIn(FrameworkElement element, int delayMs = 0, int durationMs = 400)
        {
            if (element == null) return;

            // Set the initial transform so it starts translated + transparent.
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new TranslateTransform { Y = 24 };
            element.Opacity = 0;

            var sb = new Storyboard();
            var beginTime = TimeSpan.FromMilliseconds(delayMs);
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Opacity 0 -> 1
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = beginTime,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(fade, element);
            Storyboard.SetTargetProperty(fade, "Opacity");
            sb.Children.Add(fade);

            // Translate Y 24 -> 0
            var slide = new DoubleAnimation
            {
                From = 24,
                To = 0,
                BeginTime = beginTime,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(slide, element);
            Storyboard.SetTargetProperty(slide, "(UIElement.RenderTransform).(TranslateTransform.Y)");
            sb.Children.Add(slide);

            sb.Begin();
        }

        // Adds a subtle scale-up on pointer enter / scale-down on exit. Use on
        // individual cards so the whole page feels alive without a per-page
        // Storyboard.
        public static void AddHoverScale(FrameworkElement element, double scale = 1.015)
        {
            if (element == null) return;

            element.RenderTransformOrigin = new Point(0.5, 0.5);
            if (element.RenderTransform is not ScaleTransform)
            {
                element.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
            }

            element.PointerEntered += (s, e) => AnimateScale(element, scale, 120);
            element.PointerExited += (s, e) => AnimateScale(element, 1.0, 180);
        }

        private static void AnimateScale(FrameworkElement element, double to, int durationMs)
        {
            var sb = new Storyboard();
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(durationMs);

            var sx = new DoubleAnimation { To = to, Duration = duration, EasingFunction = ease };
            Storyboard.SetTarget(sx, element);
            Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
            sb.Children.Add(sx);

            var sy = new DoubleAnimation { To = to, Duration = duration, EasingFunction = ease };
            Storyboard.SetTarget(sy, element);
            Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
            sb.Children.Add(sy);

            sb.Begin();
        }
    }
}
