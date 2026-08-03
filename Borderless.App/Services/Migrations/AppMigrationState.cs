namespace Borderless.App.Services.Migrations;

public sealed class AppMigrationState
{
    /// <summary>Highest migration id that has successfully completed on this machine.</summary>
    public int LastAppliedId { get; set; }
}
