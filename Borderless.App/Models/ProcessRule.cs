using System.IO;

namespace Borderless.App.Models;

/// <summary>
/// Rule that matches a process by window title and/or executable name and applies window options.
/// </summary>
public sealed class ProcessRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Exact window title match. Empty skips title matching.</summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>Executable file name, e.g. game.exe. Empty skips executable matching.</summary>
    public string ExecutableName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool IsBorderless { get; set; } = true;

    public bool IsAlwaysOnTop { get; set; }

    /// <summary>Expand / max-size window to the target rect (screen or custom).</summary>
    public bool IsExpandToScreen { get; set; } = true;

    /// <summary>When true, use CustomX/Y/Width/Height instead of full monitor.</summary>
    public bool UseCustomDimension { get; set; }

    /// <summary>Custom window X. Used when <see cref="UseCustomDimension"/> is true.</summary>
    public int CustomX { get; set; }

    /// <summary>Custom window Y. Used when <see cref="UseCustomDimension"/> is true.</summary>
    public int CustomY { get; set; }

    /// <summary>Custom width. 0 = use monitor width at apply time.</summary>
    public int CustomWidth { get; set; }

    /// <summary>Custom height. 0 = use monitor height at apply time.</summary>
    public int CustomHeight { get; set; }

    public bool MuteInBackground { get; set; }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WindowTitle))
            {
                return WindowTitle;
            }

            if (!string.IsNullOrWhiteSpace(ExecutableName))
            {
                return ExecutableName;
            }

            return Localization.Loc.Get("UntitledRule");
        }
    }

    public bool Matches(string? windowTitle, string? executableName)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(WindowTitle);
        var hasExe = !string.IsNullOrWhiteSpace(ExecutableName);

        if (!hasTitle && !hasExe)
        {
            return false;
        }

        var titleOk = !hasTitle
            || string.Equals(windowTitle?.Trim(), WindowTitle.Trim(), StringComparison.Ordinal);

        var exeOk = !hasExe
            || string.Equals(
                Path.GetFileName(executableName)?.Trim(),
                Path.GetFileName(ExecutableName)?.Trim(),
                StringComparison.OrdinalIgnoreCase);

        return titleOk && exeOk;
    }
}
