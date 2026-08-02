using System.Text.Json;

namespace Borderless.App.Helpers;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for on-disk app state.
/// </summary>
public static class AppJson
{
    public static JsonSerializerOptions IndentedCamelCase { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
