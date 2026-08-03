using System.IO;
using System.Text.Json;
using Borderless.App.Helpers;

namespace Borderless.App.Services.Migrations;

/// <summary>
/// Runs pending <see cref="IAppMigration"/> steps once per machine after install/update.
/// Progress is stored in %LocalAppData%\Borderless\migrations.json.
/// </summary>
public sealed class AppMigrationRunner
{
    private readonly string _statePath;
    private readonly IReadOnlyList<IAppMigration> _migrations;

    public AppMigrationRunner(IEnumerable<IAppMigration>? migrations = null)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Borderless");
        Directory.CreateDirectory(folder);
        _statePath = Path.Combine(folder, "migrations.json");
        _migrations = (migrations ?? CreateDefaultMigrations())
            .OrderBy(m => m.Id)
            .ToArray();
    }

    public void Run(AppMigrationContext context)
    {
        var state = LoadState();
        var pending = _migrations.Where(m => m.Id > state.LastAppliedId).ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        foreach (var migration in pending)
        {
            migration.Execute(context);
            state.LastAppliedId = migration.Id;
            SaveState(state);
        }
    }

    private AppMigrationState LoadState()
    {
        if (!File.Exists(_statePath))
        {
            return new AppMigrationState();
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<AppMigrationState>(json, AppJson.IndentedCamelCase)
                   ?? new AppMigrationState();
        }
        catch
        {
            return new AppMigrationState();
        }
    }

    private void SaveState(AppMigrationState state)
    {
        var json = JsonSerializer.Serialize(state, AppJson.IndentedCamelCase);
        File.WriteAllText(_statePath, json);
    }

    private static IEnumerable<IAppMigration> CreateDefaultMigrations()
    {
        yield return new MigrateStartupToTaskScheduler();
        yield return new MigrateRemoveLegacyStartupRunKey();
    }
}
