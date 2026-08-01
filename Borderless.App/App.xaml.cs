using System.Globalization;
using System.Windows;
using Borderless.App.Services;
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
        ApplyUiCulture();

        ProcessCatalog = new ProcessCatalogService();
        var ruleStore = new RuleStore();
        var settingsStore = new SettingsStore();
        var startupService = new StartupRegistrationService();
        var updateService = new UpdateService();
        var settings = new AppSettingsViewModel(settingsStore, startupService, updateService);
        var windowStyleService = new WindowStyleService();
        var audioMuteService = new AudioMuteService();
        _ruleEngine = new RuleEngineService(windowStyleService, audioMuteService);
        MainViewModel = new MainViewModel(ruleStore, _ruleEngine, ProcessCatalog, settings);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        MainViewModel.Settings.FlushSave();
        MainViewModel.Settings.Dispose();
        _ruleEngine?.Dispose();
        base.OnExit(e);
    }

    private static void ApplyUiCulture()
    {
        var culture = CultureInfo.CurrentUICulture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
