namespace Borderless.App.Services.Migrations;

/// <summary>One-shot post-update / post-install system change.</summary>
public interface IAppMigration
{
    /// <summary>Monotonic id. Migrations run in ascending order; each id applies at most once.</summary>
    int Id { get; }

    string Name { get; }

    void Execute(AppMigrationContext context);
}
