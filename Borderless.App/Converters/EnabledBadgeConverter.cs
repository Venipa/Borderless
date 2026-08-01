using System.Globalization;
using System.Windows.Data;
using Borderless.App.Localization;

namespace Borderless.App.Converters;

public sealed class EnabledBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var enabled = value is true;
        return Loc.Format("EnabledFormat", enabled);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
