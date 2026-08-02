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
    private bool _defaultLockCursor;

    [ObservableProperty]
    private bool _defaultHideCursor;

    [ObservableProperty]
    private bool _defaultRemoveGameMenus;

    [ObservableProperty]
    private bool _defaultIsEnabled = true;

    [ObservableProperty]
    private MatchCondition _defaultMatchCondition = MatchCondition.Both;

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

    /// <summary>Show modal update dialog when a new version is found (default on).</summary>
    [ObservableProperty]
    private bool _showUpdateDialog = true;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _showUpdateHint;

    [ObservableProperty]
    private string _updateHintLabel = string.Empty;

    [ObservableProperty]
    private string _updateHintToolTip = string.Empty;

    private UpdateCheckResult? _pendingUpdate;

    public string AppVersionText =>
        string.Format(Loc.Get("AboutVersionFormat"), AppMetadata.GetLocalVersionString());

    public string AppAuthorText =>
        string.Format(Loc.Get("AboutAuthorFormat"), AppMetadata.Author);

    public string RepositoryUrl => AppMetadata.RepositoryUrl;

    public IReadOnlyList<LanguageOption> LanguageOptions => LanguageManager.Options;

    public IReadOnlyList<MatchConditionOption> MatchConditionOptions { get; private set; } =
        MatchConditionOption.CreateAll();

    public string DefaultMatchConditionToolTip =>
        MatchConditionOption.Find(MatchConditionOptions, DefaultMatchCondition).FormattedToolTip;

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
        DefaultLockCursor = settings.Defaults.LockCursor;
        DefaultHideCursor = settings.Defaults.HideCursor;
        DefaultRemoveGameMenus = settings.Defaults.RemoveGameMenus;
        DefaultIsEnabled = settings.Defaults.IsEnabled;
        DefaultMatchCondition = settings.Defaults.MatchCondition;
        StartOnStartup = settings.StartOnStartup;
        CloseToTray = settings.CloseToTray;
        SelectedLanguage = LanguageManager.FindOption(settings.UiLanguage);
        UpdaterEnabled = settings.UpdaterEnabled;
        AutoUpdateWithoutConfirmation = settings.AutoUpdateWithoutConfirmation;
        ShowUpdateDialog = settings.ShowUpdateDialog ?? true;
        _suppressSave = false;

        _startup.Apply(StartOnStartup);
        UpdateStatusMessage = Loc.Get("UpdateStatusIdle");
        Loc.Source.PropertyChanged += OnLocChanged;
    }

    public RuleDefaults CreateRuleDefaults() => new()
    {
        MatchCondition = DefaultMatchCondition,
        IsBorderless = DefaultIsBorderless,
        IsAlwaysOnTop = DefaultIsAlwaysOnTop,
        IsExpandToScreen = DefaultIsExpandToScreen,
        MuteInBackground = DefaultMuteInBackground,
        LockCursor = DefaultLockCursor,
        HideCursor = DefaultHideCursor,
        RemoveGameMenus = DefaultRemoveGameMenus,
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
        MatchConditionOptions = MatchConditionOption.CreateAll();
        OnPropertyChanged(nameof(MatchConditionOptions));
        OnPropertyChanged(nameof(DefaultMatchConditionToolTip));
        OnPropertyChanged(nameof(AppVersionText));
        OnPropertyChanged(nameof(AppAuthorText));
        RefreshUpdateHintTexts();
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

    /// <summary>Opens the update dialog for a previously detected pending update.</summary>
    public Task ShowPendingUpdateDialogAsync()
    {
        if (_pendingUpdate is null || !_pendingUpdate.IsUpdateAvailable)
        {
            return Task.CompletedTask;
        }

        return PromptAndHandleUpdateAsync(_pendingUpdate, CancellationToken.None);
    }

    [RelayCommand]
    private Task OpenPendingUpdateDialogAsync() => ShowPendingUpdateDialogAsync();

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
            await UiDispatch.InvokeAsync(ClearPendingUpdate).ConfigureAwait(false);

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

        await UiDispatch.InvokeAsync(() => SetPendingUpdate(result)).ConfigureAwait(false);

        if (AutoUpdateWithoutConfirmation)
        {
            await HandleUpdatePromptAsync(result, UpdatePromptResult.DownloadNow, token).ConfigureAwait(false);
            return;
        }

        // Interactive check always opens the dialog; startup/background respects the setting.
        if (!interactive && !ShowUpdateDialog)
        {
            return;
        }

        await PromptAndHandleUpdateAsync(result, token).ConfigureAwait(false);
    }

    private async Task PromptAndHandleUpdateAsync(UpdateCheckResult result, CancellationToken cancellationToken)
    {
        var promptResult = await UiDispatch.InvokeAsync(() =>
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

        await HandleUpdatePromptAsync(result, promptResult, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleUpdatePromptAsync(
        UpdateCheckResult result,
        UpdatePromptResult promptResult,
        CancellationToken token)
    {
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
                await UiDispatch.InvokeAsync(ClearPendingUpdate).ConfigureAwait(false);
                await _updater.ApplyUpdateAsync(result, token).ConfigureAwait(false);
                return;
            }

            var path = await _updater.DownloadUpdateAsync(result, token).ConfigureAwait(false);
            _updater.ScheduleInstallOnExit(path);
            await UiDispatch.InvokeAsync(() =>
            {
                ClearPendingUpdate();
                UpdateStatusMessage = Loc.Get("UpdateQueuedForExit");
            }).ConfigureAwait(false);
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

    private void SetPendingUpdate(UpdateCheckResult result)
    {
        _pendingUpdate = result;
        ShowUpdateHint = true;
        RefreshUpdateHintTexts();
    }

    private void ClearPendingUpdate()
    {
        _pendingUpdate = null;
        ShowUpdateHint = false;
        UpdateHintLabel = string.Empty;
        UpdateHintToolTip = string.Empty;
    }

    private void RefreshUpdateHintTexts()
    {
        if (_pendingUpdate?.RemoteVersion is null)
        {
            return;
        }

        var remote = _pendingUpdate.RemoteVersion.ToString();
        var local = _pendingUpdate.LocalVersion.ToString();
        UpdateHintLabel = string.Format(Loc.Get("UpdateHintLabelFormat"), remote);
        UpdateHintToolTip = string.Format(Loc.Get("UpdateAvailableFormat"), remote, local);
    }

    partial void OnDefaultIsBorderlessChanged(bool value) => ScheduleSave();
    partial void OnDefaultIsAlwaysOnTopChanged(bool value) => ScheduleSave();
    partial void OnDefaultIsExpandToScreenChanged(bool value) => ScheduleSave();
    partial void OnDefaultMuteInBackgroundChanged(bool value) => ScheduleSave();
    partial void OnDefaultLockCursorChanged(bool value) => ScheduleSave();
    partial void OnDefaultHideCursorChanged(bool value) => ScheduleSave();
    partial void OnDefaultRemoveGameMenusChanged(bool value) => ScheduleSave();
    partial void OnDefaultIsEnabledChanged(bool value) => ScheduleSave();

    partial void OnDefaultMatchConditionChanged(MatchCondition value)
    {
        OnPropertyChanged(nameof(DefaultMatchConditionToolTip));
        ScheduleSave();
    }

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

        if (!value)
        {
            ClearPendingUpdate();
        }

        ScheduleSave();
    }

    partial void OnAutoUpdateWithoutConfirmationChanged(bool value) => ScheduleSave();

    partial void OnShowUpdateDialogChanged(bool value) => ScheduleSave();

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
        AutoUpdateWithoutConfirmation = AutoUpdateWithoutConfirmation,
        ShowUpdateDialog = ShowUpdateDialog
    };
}
