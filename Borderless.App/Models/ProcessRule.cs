using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Borderless.App.Models;

/// <summary>
/// Rule that matches a process by window title and/or executable name and applies window options.
/// </summary>
public sealed partial class ProcessRule : ObservableObject
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(150);

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Window title match (exact or regex). Empty skips title matching.</summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>When true, <see cref="WindowTitle"/> is treated as a regular expression.</summary>
    public bool UseTitleRegex { get; set; }

    /// <summary>Executable file name, e.g. game.exe. Empty skips executable matching.</summary>
    public string ExecutableName { get; set; } = string.Empty;

    /// <summary>How title and executable criteria combine. Default: <see cref="MatchCondition.Both"/>.</summary>
    public MatchCondition MatchCondition { get; set; } = MatchCondition.Both;

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

    /// <summary>Clip the cursor to the matched window while it is focused. Alt+Tab releases.</summary>
    public bool LockCursor { get; set; }

    /// <summary>Hide the mouse cursor while the matched window is focused. Alt+Tab restores.</summary>
    public bool HideCursor { get; set; }

    /// <summary>Remove the window menu bar (HMENU) while the rule is active.</summary>
    public bool RemoveGameMenus { get; set; }

    /// <summary>Runtime indicator; not persisted.</summary>
    [JsonIgnore]
    [ObservableProperty]
    private RuleLiveStatus _liveStatus = RuleLiveStatus.Idle;

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

        if (MatchCondition == MatchCondition.And && (!hasTitle || !hasExe))
        {
            return false;
        }

        var titleMatched = hasTitle && MatchTitle(windowTitle);
        var exeMatched = hasExe && MatchExecutable(executableName);

        return MatchCondition switch
        {
            MatchCondition.Or => titleMatched || exeMatched,
            MatchCondition.And => titleMatched && exeMatched,
            _ => (!hasTitle || titleMatched) && (!hasExe || exeMatched)
        };
    }

    public bool HasSameMatchKey(ProcessRule other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(WindowTitle.Trim(), other.WindowTitle.Trim(), StringComparison.Ordinal)
            && UseTitleRegex == other.UseTitleRegex
            && MatchCondition == other.MatchCondition
            && string.Equals(
                Path.GetFileName(ExecutableName)?.Trim() ?? string.Empty,
                Path.GetFileName(other.ExecutableName)?.Trim() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchTitle(string? windowTitle)
    {
        var actual = windowTitle?.Trim() ?? string.Empty;
        var pattern = WindowTitle.Trim();

        if (!UseTitleRegex)
        {
            return string.Equals(actual, pattern, StringComparison.Ordinal);
        }

        try
        {
            return Regex.IsMatch(
                actual,
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                RegexMatchTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private bool MatchExecutable(string? executableName) =>
        string.Equals(
            Path.GetFileName(executableName)?.Trim(),
            Path.GetFileName(ExecutableName)?.Trim(),
            StringComparison.OrdinalIgnoreCase);
}
