using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

namespace Borderless.App.Controls;

/// <summary>
/// Clickable settings row: title + muted subtitle, optional info tooltip, toggle on the right.
/// Clicking the row (outside the switch / info icon) toggles <see cref="IsChecked"/>.
/// </summary>
public class SwitchOptionRow : Control
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(SwitchOptionRow),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description),
            typeof(string),
            typeof(SwitchOptionRow),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(
            nameof(IsChecked),
            typeof(bool),
            typeof(SwitchOptionRow),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty InfoTooltipEnabledProperty =
        DependencyProperty.Register(
            nameof(InfoTooltipEnabled),
            typeof(bool),
            typeof(SwitchOptionRow),
            new PropertyMetadata(false));

    public static readonly DependencyProperty InfoTooltipContentProperty =
        DependencyProperty.Register(
            nameof(InfoTooltipContent),
            typeof(string),
            typeof(SwitchOptionRow),
            new PropertyMetadata(string.Empty));

    static SwitchOptionRow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SwitchOptionRow),
            new FrameworkPropertyMetadata(typeof(SwitchOptionRow)));
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Muted subtitle under the title. Hidden when null/empty.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public bool InfoTooltipEnabled
    {
        get => (bool)GetValue(InfoTooltipEnabledProperty);
        set => SetValue(InfoTooltipEnabledProperty, value);
    }

    public string InfoTooltipContent
    {
        get => (string)GetValue(InfoTooltipContentProperty);
        set => SetValue(InfoTooltipContentProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_Root") is UIElement root)
        {
            root.MouseLeftButtonUp -= OnRootMouseLeftButtonUp;
            root.MouseLeftButtonUp += OnRootMouseLeftButtonUp;
        }
    }

    private void OnRootMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled || e.Handled)
        {
            return;
        }

        if (IsExcludedClickSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        IsChecked = !IsChecked;
        e.Handled = true;
    }

    private static bool IsExcludedClickSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ToggleSwitch or Button)
            {
                return true;
            }

            if (source is FrameworkElement { Name: "PART_InfoHit" })
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
