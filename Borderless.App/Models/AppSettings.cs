namespace Borderless.App.Models;

public sealed class RuleDefaults
{
    public bool IsBorderless { get; set; } = true;

    public bool IsAlwaysOnTop { get; set; }

    public bool IsExpandToScreen { get; set; } = true;

    public bool UseCustomDimension { get; set; }

    public int CustomX { get; set; }

    public int CustomY { get; set; }

    public int CustomWidth { get; set; }

    public int CustomHeight { get; set; }

    public bool MuteInBackground { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public sealed class AppSettings
{
    public RuleDefaults Defaults { get; set; } = new();

    public bool StartOnStartup { get; set; }

    public bool CloseToTray { get; set; }

    public bool UpdaterEnabled { get; set; }

    /// <summary>When true, apply updates immediately without asking.</summary>
    public bool AutoUpdateWithoutConfirmation { get; set; }
}

public enum AppSection
{
    Rules,
    Defaults,
    Settings
}
