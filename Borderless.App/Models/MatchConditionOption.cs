using Borderless.App.Localization;

namespace Borderless.App.Models;

/// <summary>Localized ComboBox entry for <see cref="MatchCondition"/>.</summary>
public sealed class MatchConditionOption
{
    public MatchCondition Value { get; }

    public string DisplayName { get; }

    public string Summary { get; }

    /// <summary>Display name, newline, then summary — for ToolTip.</summary>
    public string FormattedToolTip => $"{DisplayName}{Environment.NewLine}{Summary}";

    private MatchConditionOption(MatchCondition value, string displayName, string summary)
    {
        Value = value;
        DisplayName = displayName;
        Summary = summary;
    }

    public static IReadOnlyList<MatchConditionOption> CreateAll() =>
    [
        new(
            MatchCondition.Both,
            Loc.Get("MatchConditionBoth"),
            Loc.Get("MatchConditionBothSummary")),
        new(
            MatchCondition.And,
            Loc.Get("MatchConditionAnd"),
            Loc.Get("MatchConditionAndSummary")),
        new(
            MatchCondition.Or,
            Loc.Get("MatchConditionOr"),
            Loc.Get("MatchConditionOrSummary"))
    ];

    public static MatchConditionOption Find(IEnumerable<MatchConditionOption> options, MatchCondition value) =>
        options.FirstOrDefault(o => o.Value == value) ?? options.First();
}
