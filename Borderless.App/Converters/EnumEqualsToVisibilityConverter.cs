using System.Globalization;
using System.Windows.Data;
using Borderless.App.Models;

namespace Borderless.App.Converters;

public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return System.Windows.Visibility.Collapsed;
        }

        return Equals(value, parameter)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
