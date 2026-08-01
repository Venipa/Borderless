using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Borderless.App.Localization;

/// <summary>
/// XAML markup extension: Text="{loc:Loc ProcessRulesTitle}"
/// Returns a live binding so culture switches refresh the UI.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Source,
            Mode = BindingMode.OneWay
        };

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget
            {
                TargetObject: DependencyObject
            })
        {
            return binding.ProvideValue(serviceProvider);
        }

        return Loc.Get(Key);
    }
}
