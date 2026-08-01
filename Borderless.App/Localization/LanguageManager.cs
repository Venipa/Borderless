using System.ComponentModel;
using System.Globalization;

namespace Borderless.App.Localization;

/// <summary>
/// Applies and lists UI languages. Empty code = follow OS (system default).
/// </summary>
public static class LanguageManager
{
    /// <summary>Stored value meaning "use OS UI language".</summary>
    public const string SystemCode = "";

    private static readonly CultureInfo SystemUiCulture = CultureInfo.InstalledUICulture;

    public static IReadOnlyList<LanguageOption> Options { get; } = BuildOptions();

    public static void Apply(string? languageCode)
    {
        var culture = Resolve(languageCode);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Loc.NotifyChanged();
        foreach (var option in Options)
        {
            option.RefreshDisplayName();
        }
    }

    public static CultureInfo Resolve(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return SystemUiCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageCode.Trim());
        }
        catch (CultureNotFoundException)
        {
            return SystemUiCulture;
        }
    }

    public static LanguageOption FindOption(string? languageCode)
    {
        var code = languageCode?.Trim() ?? SystemCode;
        return Options.FirstOrDefault(o =>
                   string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase))
               ?? Options[0];
    }

    private static IReadOnlyList<LanguageOption> BuildOptions()
    {
        var list = new List<LanguageOption>
        {
            new(SystemCode)
        };

        foreach (var code in new[]
                 {
                     "en",
                     "de",
                     "es-ES",
                     "fr-FR",
                     "it-IT",
                     "pt-PT",
                     "ja-JP",
                     "ko-KR",
                     "pl-PL",
                     "uk-UA",
                     "zh-CN",
                     "zh-TW"
                 })
        {
            list.Add(new LanguageOption(code));
        }

        return list;
    }
}

/// <summary>
/// One entry in the language picker. <see cref="Code"/> empty = system.
/// </summary>
public sealed class LanguageOption : INotifyPropertyChanged
{
    public LanguageOption(string code)
    {
        Code = code ?? LanguageManager.SystemCode;
    }

    public string Code { get; }

    public bool IsSystem => string.IsNullOrEmpty(Code);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Native culture name, or localized "System" for the default entry.</summary>
    public string DisplayName
    {
        get
        {
            if (IsSystem)
            {
                return Loc.Get("SettingsLanguageSystem");
            }

            try
            {
                return CultureInfo.GetCultureInfo(Code).NativeName;
            }
            catch (CultureNotFoundException)
            {
                return Code;
            }
        }
    }

    public void RefreshDisplayName() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
}
