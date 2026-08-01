using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Borderless.App.Localization;

/// <summary>
/// Looks up localized UI strings from satellite .resx resources.
/// </summary>
public static class Loc
{
    private static readonly ResourceManager ResourceManager =
        new("Borderless.App.Resources.Strings", typeof(Loc).Assembly);

    /// <summary>Binding source for XAML; raises when UI culture changes.</summary>
    public static LocBindingSource Source { get; } = new();

    public static string Get(string key)
    {
        var culture = CultureInfo.CurrentUICulture;
        return ResourceManager.GetString(key, culture)
            ?? ResourceManager.GetString(key, culture.Parent)
            ?? ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentUICulture, Get(key), args);
    }

    public static void NotifyChanged() => Source.Notify();
}

/// <summary>
/// Indexer source so <c>{loc:Loc Key}</c> can re-evaluate after culture changes.
/// </summary>
public sealed class LocBindingSource : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Loc.Get(key);

    public void Notify() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}
