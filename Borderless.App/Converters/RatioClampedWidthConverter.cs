using System.Globalization;
using System.Windows.Data;

namespace Borderless.App.Converters;

/// <summary>
/// Maps a width to <c>min(Max, value * Ratio)</c>. Used for pill toggles.
/// ConverterParameter optional: "ratio,max" e.g. "0.5,32".
/// </summary>
public sealed class RatioClampedWidthConverter : IValueConverter
{
    public double Ratio { get; set; } = 0.5;

    public double Max { get; set; } = 32;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ratio = Ratio;
        var max = Max;
        if (parameter is string raw && !string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
            {
                ratio = r;
            }

            if (parts.Length >= 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
            {
                max = m;
            }
        }

        if (value is not double width || double.IsNaN(width) || width <= 0)
        {
            return 0d;
        }

        return Math.Min(max, width * ratio);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
