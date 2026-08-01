namespace Borderless.App.Models;

public sealed class ProcessSuggestion
{
    public required string WindowTitle { get; init; }

    public required string ExecutableName { get; init; }

    public string DisplayText => $"{WindowTitle}, {ExecutableName}";

    public override string ToString() => DisplayText;
}
