namespace Borderless.App.Services.Migrations;

/// <summary>
/// Removes HKCU Run\Borderless and, if start-on-startup is enabled, registers the
/// Task Scheduler logon task (highest privileges) instead.
/// </summary>
public sealed class MigrateRemoveLegacyStartupRunKey : IAppMigration
{
    public int Id => 2;

    public string Name => "RemoveLegacyStartupRunKey";

    public void Execute(AppMigrationContext context)
    {
        context.Startup.RemoveLegacyRunRegistration();

        if (context.Settings.StartOnStartup)
        {
            context.Startup.Apply(enabled: true);
        }
    }
}
