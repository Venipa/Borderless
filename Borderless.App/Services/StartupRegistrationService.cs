using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace Borderless.App.Services;

/// <summary>
/// Registers / unregisters a logon Task Scheduler entry so Borderless can start elevated
/// (HKCU Run cannot elevate at startup when the app requests highestAvailable).
/// </summary>
public sealed class StartupRegistrationService
{
    private const string TaskName = "Borderless";
    private const string LegacyRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyRunValueName = "Borderless";

    public void Apply(bool enabled)
    {
        // Old Run-key entries never elevate at logon; clear them either way.
        RemoveLegacyRunRegistration();

        if (enabled)
        {
            RegisterLogonTask();
        }
        else
        {
            UnregisterLogonTask();
        }
    }

    /// <summary>
    /// Deletes the legacy HKCU Run value used before Task Scheduler startup.
    /// Safe to call repeatedly.
    /// </summary>
    public void RemoveLegacyRunRegistration()
    {
        using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKeyPath, writable: true);
        if (key?.GetValue(LegacyRunValueName) is not null)
        {
            key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
        }
    }

    private static void RegisterLogonTask()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return;
        }

        using var service = new TaskService();
        var definition = service.NewTask();
        definition.RegistrationInfo.Description =
            "Starts Borderless at user logon with the highest available privileges.";
        definition.Principal.UserId = WindowsIdentity.GetCurrent().Name;
        definition.Principal.LogonType = TaskLogonType.InteractiveToken;
        definition.Principal.RunLevel = TaskRunLevel.Highest;
        definition.Settings.DisallowStartIfOnBatteries = false;
        definition.Settings.StopIfGoingOnBatteries = false;
        definition.Settings.AllowDemandStart = true;
        definition.Settings.StartWhenAvailable = true;
        definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        definition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
        definition.Triggers.Add(new LogonTrigger
        {
            UserId = WindowsIdentity.GetCurrent().Name
        });
        definition.Actions.Add(new ExecAction(exe));

        service.RootFolder.RegisterTaskDefinition(
            TaskName,
            definition,
            TaskCreation.CreateOrUpdate,
            null,
            null,
            TaskLogonType.InteractiveToken);
    }

    private static void UnregisterLogonTask()
    {
        using var service = new TaskService();
        service.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
    }
}
