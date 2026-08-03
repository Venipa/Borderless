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

    public static ProcessCatalogService ProcessCatalog { get; private set; } = null!;

    private RuleEngineService? _ruleEngine;

    protected override void OnStartup(StartupEventArgs e)
    {
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
        base.OnExit(e);
    }
}
