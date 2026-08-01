using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Borderless.App.Controls;

/// <summary>
/// TextBlock that highlights case-insensitive matches of <see cref="Highlight"/> inside <see cref="SourceText"/>.
/// </summary>
public sealed class HighlightTextBlock : TextBlock
{
    private static readonly Brush HighlightBrush = CreateHighlightBrush();

    public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
        nameof(SourceText),
        typeof(string),
        typeof(HighlightTextBlock),
        new PropertyMetadata(string.Empty, OnHighlightInputsChanged));

    public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(
        nameof(Highlight),
        typeof(string),
        typeof(HighlightTextBlock),
        new PropertyMetadata(string.Empty, OnHighlightInputsChanged));

    public string SourceText
    {
        get => (string)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public string Highlight
    {
        get => (string)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    private static void OnHighlightInputsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HighlightTextBlock block)
        {
            block.RebuildInlines();
        }
    }

    private void RebuildInlines()
    {
        Inlines.Clear();

        var text = SourceText ?? string.Empty;
        var highlight = Highlight ?? string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(highlight))
        {
            Inlines.Add(new Run(text));
            return;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        var index = 0;

        while (index < text.Length)
        {
            var matchIndex = text.IndexOf(highlight, index, comparison);
            if (matchIndex < 0)
            {
                Inlines.Add(new Run(text[index..]));
                break;
            }

            if (matchIndex > index)
            {
                Inlines.Add(new Run(text[index..matchIndex]));
            }

            Inlines.Add(new Run(text.Substring(matchIndex, highlight.Length))
            {
                Foreground = HighlightBrush,
                FontWeight = FontWeights.SemiBold
            });

            index = matchIndex + highlight.Length;
        }
    }

    private static SolidColorBrush CreateHighlightBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xF2, 0xC9, 0x4C));
        brush.Freeze();
        return brush;
    }
}
