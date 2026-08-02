using System.IO;
using System.Text.Json;
using Borderless.App.Helpers;
using Borderless.App.Models;

namespace Borderless.App.Services;

public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Borderless");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, AppJson.IndentedCamelCase) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, AppJson.IndentedCamelCase);
        File.WriteAllText(_filePath, json);
    }
}
