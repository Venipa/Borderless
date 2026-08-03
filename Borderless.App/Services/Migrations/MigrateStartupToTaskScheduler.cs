namespace Borderless.App.Services.Migrations;

/// <summary>
/// Initial startup path: apply current start-on-startup setting via Task Scheduler.
/// </summary>
public sealed class MigrateStartupToTaskScheduler : IAppMigration
{
    public int Id => 1;

    public string Name => "StartupToTaskScheduler";

    public void Execute(AppMigrationContext context)
    {
        context.Startup.Apply(context.Settings.StartOnStartup);
    }
}
