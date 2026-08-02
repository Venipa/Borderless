using System.IO;
using System.Text.Json;
using Borderless.App.Helpers;
using Borderless.App.Models;

namespace Borderless.App.Services;

/// <summary>
/// Persists process rules as JSON under LocalApplicationData.
/// </summary>
public sealed class RuleStore
{
    private readonly string _filePath;

    public RuleStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Borderless");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "rules.json");
    }

    public IReadOnlyList<ProcessRule> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var rules = JsonSerializer.Deserialize<List<ProcessRule>>(json, AppJson.IndentedCamelCase);
            return rules ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<ProcessRule> rules)
    {
        var json = JsonSerializer.Serialize(rules.ToList(), AppJson.IndentedCamelCase);
        File.WriteAllText(_filePath, json);
    }
}
