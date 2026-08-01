using System.Windows.Markup;

namespace Borderless.App.Localization;

/// <summary>
/// XAML markup extension: Text="{loc:Loc ProcessRulesTitle}"
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
