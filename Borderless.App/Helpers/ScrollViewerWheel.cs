using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Borderless.App.Helpers;

/// <summary>
/// Forces mouse-wheel scrolling when NavigationView / Fluent hosts swallow wheel events.
/// </summary>
public static class ScrollViewerWheel
{
    private static readonly DependencyProperty IsAttachedProperty =
        DependencyProperty.RegisterAttached(
            "IsAttached",
            typeof(bool),
            typeof(ScrollViewerWheel),
            new PropertyMetadata(false));

    public static void Attach(UIElement target, ScrollViewer? scrollViewer = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if ((bool)target.GetValue(IsAttachedProperty))
        {
            return;
        }

        target.SetValue(IsAttachedProperty, true);

        // handledEventsToo: NavigationView marks PreviewMouseWheel handled before the page.
        target.AddHandler(
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler((_, e) =>
            {
                var viewer = scrollViewer
                    ?? target as ScrollViewer
                    ?? FindDescendantScrollViewer(target);

                Handle(viewer, e);
            }),
            handledEventsToo: true);
    }

    private static void Handle(ScrollViewer? scrollViewer, MouseWheelEventArgs e)
    {
        if (scrollViewer is null || e.Delta == 0)
        {
            return;
        }

        var scrollable = Math.Max(
            scrollViewer.ScrollableHeight,
            scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        if (scrollable <= 0)
        {
            return;
        }

        var lines = SystemParameters.WheelScrollLines;
        if (lines <= 0)
        {
            lines = 3;
        }

        // ~one line (~16px) per WheelScrollLines notch.
        var offset = e.Delta > 0 ? -lines * 16.0 : lines * 16.0;
        var next = Math.Clamp(scrollViewer.VerticalOffset + offset, 0, scrollable);
        scrollViewer.ScrollToVerticalOffset(next);
        e.Handled = true;
    }

    public static ScrollViewer? FindDescendantScrollViewer(DependencyObject? root)
    {
        if (root is null)
        {
            return null;
        }

        if (root is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindDescendantScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
