using System.Globalization;
using System.Windows.Data;
using Borderless.App.Localization;
using Borderless.App.Models;

namespace Borderless.App.Converters;

/// <summary>
/// Maps <see cref="RuleLiveStatus"/> to a localized tooltip.
/// </summary>
public sealed class RuleLiveStatusToTooltipConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            RuleLiveStatus.Active => Loc.Get("RuleStatusActive"),
            RuleLiveStatus.Error => Loc.Get("RuleStatusError"),
            _ => Loc.Get("RuleStatusIdle")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
