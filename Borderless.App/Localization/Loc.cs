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

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentUICulture, Get(key), args);
    }
}
