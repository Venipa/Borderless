using Borderless.App.Models;

namespace Borderless.App.Services.Migrations;

/// <summary>Services and state available to a migration step.</summary>
public sealed class AppMigrationContext
{
    public required AppSettings Settings { get; init; }

    public required SettingsStore SettingsStore { get; init; }

    public required StartupRegistrationService Startup { get; init; }
}
