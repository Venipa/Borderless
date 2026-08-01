namespace Borderless.App.Models;

/// <summary>
/// Live apply state for a rule in the rules list indicator.
/// </summary>
public enum RuleLiveStatus
{
    /// <summary>Enabled but no matching process right now.</summary>
    Idle = 0,

    /// <summary>Matching process found and styles are being applied.</summary>
    Active = 1,

    /// <summary>Apply failed for a matching window.</summary>
    Error = 2
}
