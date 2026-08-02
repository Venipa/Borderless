namespace Borderless.App.Models;

/// <summary>
/// How window title and executable criteria combine when matching a process.
/// </summary>
public enum MatchCondition
{
    /// <summary>All filled fields must match (default).</summary>
    Both = 0,

    /// <summary>Title and executable must both be set and both match.</summary>
    And = 1,

    /// <summary>Any filled field may match.</summary>
    Or = 2
}
