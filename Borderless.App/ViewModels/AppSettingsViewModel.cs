using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Borderless.App.Helpers;
using Borderless.App.Localization;
using Borderless.App.Models;
using Borderless.App.Services;
using Borderless.App.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Borderless.App.ViewModels;

public sealed partial class AppSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(450);

    private readonly SettingsStore _store;
    private readonly StartupRegistrationService _startup;
    private readonly UpdateService _updater;
    private readonly object _saveGate = new();
    private CancellationTokenSource? _saveCts;
    private CancellationTokenSource? _updateCts;
    private bool _suppressSave;
    private bool _disposed;

    [ObservableProperty]
    private bool _defaultIsBorderless = true;

    [ObservableProperty]
    private bool _defaultIsAlwaysOnTop;

    [ObservableProperty]
    private bool _defaultIsExpandToScreen = true;

    [ObservableProperty]
    private bool _defaultMuteInBackground;

    [ObservableProperty]
    private bool _defaultIsEnabled = true;

    [ObservableProperty]
    private bool _startOnStartup;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private LanguageOption _selectedLanguage = LanguageManager.Options[0];

    [ObservableProperty]
    private bool _updaterEnabled;

    [ObservableProperty]
    private bool _autoUpdateWithoutConfirmation;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    public string AppVersionText =>
        string.Format(Loc.Get("AboutVersionFormat"), AppMetadata.GetLocalVersionString());

    public string AppAuthorText =>
        string.Format(Loc.Get("AboutAuthorFormat"), AppMetadata.Author);

    public string RepositoryUrl => AppMetadata.RepositoryUrl;

    public IReadOnlyList<LanguageOption> LanguageOptions => LanguageManager.Options;

    public AppSettingsViewModel(
        SettingsStore store,
        StartupRegistrationService startup,
        UpdateService updater)
    {
        _store = store;
        _startup = startup;
        _updater = updater;

        _suppressSave = true;
        var settings = store.Load();
        DefaultIsBorderless = settings.Defaults.IsBorderless;
        DefaultIsAlwaysOnTop = settings.Defaults.IsAlwaysOnTop;
        DefaultIsExpandToScreen = settings.Defaults.IsExpandToScreen;
        DefaultMuteInBackground = settings.Defaults.MuteInBackground;
        DefaultIsEnabled = settings.Defaults.IsEnabled;
        StartOnStartup = settings.StartOnStartup;
        CloseToTray = settings.CloseToTray;
        SelectedLanguage = LanguageManager.FindOption(settings.UiLanguage);
        UpdaterEnabled = settings.UpdaterEnabled;
        AutoUpdateWithoutConfirmation = settings.AutoUpdateWithoutConfirmation;
        _suppressSave = false;

        _startup.Apply(StartOnStartup);
        UpdateStatusMessage = Loc.Get("UpdateStatusIdle");
        Loc.Source.PropertyChanged += OnLocChanged;
    }

    public RuleDefaults CreateRuleDefaults() => new()
    {
        IsBorderless = DefaultIsBorderless,
        IsAlwaysOnTop = DefaultIsAlwaysOnTop,
        IsExpandToScreen = DefaultIsExpandToScreen,
        MuteInBackground = DefaultMuteInBackground,
        IsEnabled = DefaultIsEnabled
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loc.Source.PropertyChanged -= OnLocChanged;
        FlushSave();
        _saveCts?.Dispose();
        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updater.Dispose();
    }

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AppVersionText));
        OnPropertyChanged(nameof(AppAuthorText));
        if (!IsCheckingForUpdates)
        {
            UpdateStatusMessage = Loc.Get("UpdateStatusIdle");
        }
    }

    /// <summary>Writes pending settings immediately (e.g. app exit).</summary>
    public void FlushSave()
    {
        CancellationTokenSource? pending;
        lock (_saveGate)
        {
            pending = _saveCts;
            _saveCts = null;
        }

        pending?.Cancel();
        pending?.Dispose();
        SaveNow();
    }

    public void LaunchPendingUpdateInstaller()
    {
        if (_disposed)
        {
            return;
        }

        _updater.TryLaunchPendingInstaller();
    }

    public Task CheckForUpdatesOnStartupAsync()
    {
        if (!UpdaterEnabled)
        {
            return Task.CompletedTask;
        }

        return RunUpdateCheckAsync(interactive: false);
    }

    [RelayCommand]
    private Task CheckForUpdatesAsync() => RunUpdateCheckAsync(interactive: true);

    [RelayCommand]
    private void OpenRepository()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = RepositoryUrl,
            UseShellExecute = true
        });
    }

    private async Task RunUpdateCheckAsync(bool interactive)
    {
        if (_disposed || IsCheckingForUpdates)
        {
            return;
        }

        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;

        await UiDispatch.InvokeAsync(() =>
        {
            IsCheckingForUpdates = true;
            UpdateStatusMessage = Loc.Get("UpdateChecking");
        }).ConfigureAwait(false);

        UpdateCheckResult result;
        try
        {
            result = await _updater.CheckForUpdatesAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await UiDispatch.InvokeAsync(() =>
            {
                IsCheckingForUpdates = false;
                UpdateStatusMessage = Loc.Get("UpdateStatusIdle");
            }).ConfigureAwait(false);
            return;
        }

        await UiDispatch.InvokeAsync(() =>
        {
            IsCheckingForUpdates = false;
            UpdateStatusMessage = result.ErrorMessage
                ?? result.StatusMessage
                ?? Loc.Get("UpdateStatusIdle");
        }).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage) || !result.IsUpdateAvailable)
        {
            if (interactive && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                await UiDispatch.InvokeAsync(() =>
                    MessageBox.Show(
                        result.ErrorMessage,
                        AppMetadata.Product,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning)).ConfigureAwait(false);
            }

            return;
        }

        var shouldApply = AutoUpdateWithoutConfirmation;
        var promptResult = UpdatePromptResult.Cancel;

        if (shouldApply)
        {
            promptResult = UpdatePromptResult.DownloadNow;
        }
        else
        {
            promptResult = await UiDispatch.InvokeAsync(() =>
            {
                var owner = Application.Current.MainWindow;
                var window = new UpdateAvailableWindow(result);
                if (owner is { IsLoaded: true })
                {
                    window.Owner = owner;
                }

                _ = window.ShowDialog();
                return window.Result;
            }).ConfigureAwait(false);
        }

        if (promptResult == UpdatePromptResult.Cancel)
        {
            return;
        }

        try
        {
            await UiDispatch.InvokeAsync(() =>
                UpdateStatusMessage = Loc.Get("UpdateDownloading")).ConfigureAwait(false);

            if (promptResult == UpdatePromptResult.DownloadNow)
            {
                await _updater.ApplyUpdateAsync(result, token).ConfigureAwait(false);
                return;
            }

            var path = await _updater.DownloadUpdateAsync(result, token).ConfigureAwait(false);
            _updater.ScheduleInstallOnExit(path);
            await UiDispatch.InvokeAsync(() =>
                UpdateStatusMessage = Loc.Get("UpdateQueuedForExit")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await UiDispatch.InvokeAsync(() =>
            {
                UpdateStatusMessage = string.Format(Loc.Get("UpdateApplyErrorFormat"), ex.Message);
                MessageBox.Show(
                    UpdateStatusMessage,
                    AppMetadata.Product,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }).ConfigureAwait(false);
        }
    }

    partial void OnDefaultIsBorderlessChanged(bool value) => ScheduleSave();
    partial void OnDefaultIsAlwaysOnTopChanged(bool value) => ScheduleSave();
    partial void OnDefaultIsExpandToScreenChanged(bool value) => ScheduleSave();
    partial void OnDefaultMuteInBackgroundChanged(bool value) => ScheduleSave();
    partial void OnDefaultIsEnabledChanged(bool value) => ScheduleSave();

    partial void OnStartOnStartupChanged(bool value)
    {
        ScheduleSave();
        _ = Task.Run(() => _startup.Apply(value));
    }

    partial void OnCloseToTrayChanged(bool value) => ScheduleSave();

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value is null)
        {
            return;
        }

        LanguageManager.Apply(value.Code);
        ScheduleSave();
    }

    partial void OnUpdaterEnabledChanged(bool value)
    {
        if (!value && AutoUpdateWithoutConfirmation)
        {
            _suppressSave = true;
            AutoUpdateWithoutConfirmation = false;
            _suppressSave = false;
        }

        ScheduleSave();
    }

    partial void OnAutoUpdateWithoutConfirmationChanged(bool value) => ScheduleSave();

    private void ScheduleSave()
    {
        if (_suppressSave || _disposed)
        {
            return;
        }

        CancellationTokenSource cts;
        lock (_saveGate)
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            cts = _saveCts;
        }

        var token = cts.Token;
        _ = DebouncedSaveAsync(token);
    }

    private async Task DebouncedSaveAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaveDebounce, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            SaveNow();
        }
        catch (OperationCanceledException)
        {
            // Newer change restarted the debounce window.
        }
    }

    private void SaveNow()
    {
        if (_suppressSave)
        {
            return;
        }

        var snapshot = ToModel();
        _ = Task.Run(() =>
        {
            try
            {
                _store.Save(snapshot);
            }
            catch
            {
                // Ignore transient IO failures; next change will retry.
            }
        });
    }

    private AppSettings ToModel() => new()
    {
        Defaults = CreateRuleDefaults(),
        StartOnStartup = StartOnStartup,
        CloseToTray = CloseToTray,
        UiLanguage = SelectedLanguage?.Code ?? LanguageManager.SystemCode,
        UpdaterEnabled = UpdaterEnabled,
        AutoUpdateWithoutConfirmation = AutoUpdateWithoutConfirmation
    };
}
