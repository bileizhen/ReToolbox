using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.UI.ViewManagement;

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
        private static bool AnimationsEnabled
        {
            get
            {
                try
                {
                    return new UISettings().AnimationsEnabled;
                }
                catch
                {
                    return true;
                }
            }
        }

        public static void StaggerIn(Page page)
        {
            FrameworkElement? root = FindAnimatableRoot(page);
            if (root == null) return;

            var targets = CollectAnimatableChildren(root);
            if (targets.Count == 0) return;

            if (!AnimationsEnabled)
            {
                foreach (var child in targets)
                {
                    child.Opacity = 1;
                    child.RenderTransform = null;
                }
                return;
            }

            int index = 0;
            foreach (var child in targets)
            {
                AnimateIn(child, index * 60, 420);
                index++;
            }
        }

        // Walks the visual tree to find the first meaningful layout container
        // (ScrollViewer's content, a StackPanel, a Grid with multiple children).
        private static FrameworkElement? FindAnimatableRoot(Page page)
        {
            // The page's content is usually a Grid or ScrollViewer.
            if (page.Content is ScrollViewer sv && sv.Content is FrameworkElement svContent)
                return svContent;

            if (page.Content is FrameworkElement direct)
                return direct;

            return null;
        }

        // Collects the elements that should stagger in. Unlike a flat walk, this
        // drills into ScrollViewer/Border wrappers so the *cards inside* become
        // the staggered targets — otherwise the whole scroll area slides in as
        // one block and you never see the "one-by-one" effect.
        private static List<FrameworkElement> CollectAnimatableChildren(FrameworkElement root)
        {
            var result = new List<FrameworkElement>();
            AppendChildren(root, result);
            return result;
        }

        // Recursively collects animatable children, unpacking layout wrappers so
        // the real cards (SettingsCard, grids with content, etc.) are what we
        // animate. Skips InfoBars and named overlay elements.
        private static void AppendChildren(FrameworkElement element, List<FrameworkElement> result)
        {
            foreach (var child in GetLogicalChildren(element))
            {
                if (child is not FrameworkElement fe) continue;
                if (fe.Visibility != Visibility.Visible) continue;
                // InfoBar is an overlay/toast; it manages its own transforms
                // (e.g. WindowsUpdatePage animates it on status changes), so we
                // must not hijack it here.
                if (fe is InfoBar) continue;

                if (IsLayoutWrapper(fe))
                {
                    // Drill into wrappers (ScrollViewer, Border, single-child
                    // panels) to reach the actual content below.
                    AppendChildren(fe, result);
                }
                else
                {
                    result.Add(fe);
                }
            }
        }

        // True for containers whose only job is to host other content (so we
        // should look *through* them rather than animate the wrapper itself).
        private static bool IsLayoutWrapper(FrameworkElement element)
        {
            return element is ScrollViewer
                || element is Border
                || element is ContentControl
                || element is ContentPresenter;
        }

        // Yields the children of a panel/grid in declaration order. Returns an
        // empty enumerable for non-container elements.
        private static System.Collections.IEnumerable GetLogicalChildren(DependencyObject parent)
        {
            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                    yield return child;
            }
            else if (parent is Grid grid)
            {
                foreach (var child in grid.Children)
                    yield return child;
            }
            else if (parent is Border border && border.Child is not null)
            {
                yield return border.Child;
            }
            else if (parent is ContentControl cc && cc.Content is DependencyObject content)
            {
                yield return content;
            }
            else if (parent is ContentPresenter cp && cp.Content is DependencyObject cpContent)
            {
                yield return cpContent;
            }
            else if (parent is ScrollViewer sv && sv.Content is not null)
            {
                yield return sv.Content;
            }
        }

        // Single-element fade + slide-up entrance.
        public static void AnimateIn(FrameworkElement element, int delayMs = 0, int durationMs = 400)
        {
            if (element == null) return;
            if (!AnimationsEnabled)
            {
                element.Opacity = 1;
                element.RenderTransform = null;
                return;
            }

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
