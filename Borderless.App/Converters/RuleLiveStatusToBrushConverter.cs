using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Borderless.App.Models;

namespace Borderless.App.Converters;

/// <summary>
/// Maps <see cref="RuleLiveStatus"/> to the left status-pill brush (idle gray / active green / error red).
/// </summary>
public sealed class RuleLiveStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush IdleBrush = CreateFrozen(0xFF, 0x8A, 0x8A, 0x8A);
    private static readonly SolidColorBrush ActiveBrush = CreateFrozen(0xFF, 0x16, 0xC6, 0x0A);
    private static readonly SolidColorBrush ErrorBrush = CreateFrozen(0xFF, 0xE8, 0x11, 0x23);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            RuleLiveStatus.Active => ActiveBrush,
            RuleLiveStatus.Error => ErrorBrush,
            _ => IdleBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush CreateFrozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
