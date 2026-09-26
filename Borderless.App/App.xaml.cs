using System.Windows;
using Borderless.App.Localization;
using Borderless.App.Services;
using Borderless.App.Services.Migrations;
using Borderless.App.ViewModels;
using Wpf.Ui.Appearance;

namespace Borderless.App;

public partial class App : Application
{
    public static MainViewModel MainViewModel { get; private set; } = null!;

    public static bool IsWindowsStartupLaunch { get; private set; }

    public static ProcessCatalogService ProcessCatalog { get; private set; } = null!;

    private const string InstanceMutexName =
        @"Local\Borderless.8F3C2A91-6B4E-4D7A-9C1F-2E5B8A0D4F73";

    private const string ActivationEventName =
        @"Local\Borderless.8F3C2A91-6B4E-4D7A-9C1F-2E5B8A0D4F73.Activate";

    private RuleEngineService? _ruleEngine;
    private System.Threading.Mutex? _instanceMutex;
    private System.Threading.EventWaitHandle? _activationEvent;
    private System.Threading.CancellationTokenSource? _activationCancellation;
    private System.Threading.Tasks.Task? _activationListenerTask;
    private bool _ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        IsWindowsStartupLaunch = System.Array.Exists(
            e.Args,
            arg => string.Equals(
                arg,
                StartupRegistrationService.StartupArgument,
                System.StringComparison.OrdinalIgnoreCase));

        if (!TryBecomePrimaryInstance())
        {
            // A normal user launch should bring the existing Borderless
            // instance forward. A duplicate Windows-startup invocation
            // should simply exit without disturbing the user.
            if (!IsWindowsStartupLaunch)
            {
                SignalPrimaryInstance();
            }

            Shutdown();
            return;
        }

        var settingsStore = new SettingsStore();
        var appSettings = settingsStore.Load();
        LanguageManager.Apply(appSettings.UiLanguage);

        ProcessCatalog = new ProcessCatalogService();
        var ruleStore = new RuleStore();
        var startupService = new StartupRegistrationService();
        var updateService = new UpdateService();

        try
        {
            new AppMigrationRunner().Run(new AppMigrationContext
            {
                Settings = appSettings,
                SettingsStore = settingsStore,
                Startup = startupService
            });
        }
        catch
        {
            // Migrations must not block app launch.
        }

        var settings = new AppSettingsViewModel(settingsStore, startupService, updateService);
        var windowStyleService = new WindowStyleService();
        var audioMuteService = new AudioMuteService();
        var inputCaptureService = new InputCaptureService();
        _ruleEngine = new RuleEngineService(windowStyleService, audioMuteService, inputCaptureService);
        MainViewModel = new MainViewModel(ruleStore, _ruleEngine, ProcessCatalog, settings);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private bool TryBecomePrimaryInstance()
    {
        _instanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            InstanceMutexName,
            out _ownsInstanceMutex);

        if (!_ownsInstanceMutex)
        {
            return false;
        }

        _activationEvent = new System.Threading.EventWaitHandle(
            false,
            System.Threading.EventResetMode.AutoReset,
            ActivationEventName);

        _activationCancellation = new System.Threading.CancellationTokenSource();
        _activationListenerTask = System.Threading.Tasks.Task.Run(
            () => ListenForActivationRequestsAsync(_activationCancellation.Token));

        return true;
    }

    private static void SignalPrimaryInstance()
    {
        // The primary may have acquired the mutex only milliseconds ago,
        // so briefly retry while it creates the activation event.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var activationEvent =
                    System.Threading.EventWaitHandle.OpenExisting(ActivationEventName);

                activationEvent.Set();
                return;
            }
            catch (System.Threading.WaitHandleCannotBeOpenedException)
            {
                System.Threading.Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private async System.Threading.Tasks.Task ListenForActivationRequestsAsync(
        System.Threading.CancellationToken token)
    {
        if (_activationEvent is null)
        {
            return;
        }

        var handles = new System.Threading.WaitHandle[]
        {
            _activationEvent,
            token.WaitHandle
        };

        while (!token.IsCancellationRequested)
        {
            var signaled = System.Threading.WaitHandle.WaitAny(handles);

            if (signaled != 0 || token.IsCancellationRequested)
            {
                return;
            }

            await RestoreExistingWindowAsync(token).ConfigureAwait(false);
        }
    }

    private async System.Threading.Tasks.Task RestoreExistingWindowAsync(
        System.Threading.CancellationToken token)
    {
        // Usually the existing window is already loaded. The short retry
        // also handles a second launch during the primary's cold startup.
        for (var attempt = 0; attempt < 30 && !token.IsCancellationRequested; attempt++)
        {
            var restored = await Dispatcher.InvokeAsync(() =>
            {
                if (MainWindow is not Borderless.App.MainWindow window || !window.IsLoaded)
                {
                    return false;
                }

                window.RestoreFromExternalActivation();
                return true;
            });

            if (restored)
            {
                return;
            }

            try
            {
                await System.Threading.Tasks.Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void StopSingleInstanceListener()
    {
        _activationCancellation?.Cancel();

        // Wake WaitAny immediately during shutdown.
        _activationEvent?.Set();

        if (_ownsInstanceMutex && _instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Already released during shutdown.
            }
        }

        _instanceMutex?.Dispose();
        _instanceMutex = null;
        _ownsInstanceMutex = false;
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (!_ownsInstanceMutex)
        {
            _instanceMutex?.Dispose();
            base.OnExit(e);
            return;
        }

        MainViewModel.Settings.FlushSave();
        try
        {
            MainViewModel.Settings.LaunchPendingUpdateInstaller();
        }
        catch
        {
            // Never block process exit on installer launch failure.
        }

        MainViewModel.Settings.Dispose();
        _ruleEngine?.Dispose();
        StopSingleInstanceListener();
        base.OnExit(e);
    }
}
