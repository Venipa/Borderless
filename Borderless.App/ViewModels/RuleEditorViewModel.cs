using System.Collections.ObjectModel;
using System.Windows.Threading;
using Borderless.App.Helpers;
using Borderless.App.Localization;
using Borderless.App.Models;
using Borderless.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Borderless.App.ViewModels;

public sealed partial class RuleEditorViewModel : ObservableObject
{
    private readonly ProcessCatalogService _processCatalog;
    private CancellationTokenSource? _refreshCts;
    private int _refreshVersion;
    private bool _dimensionDialogFromToggle;
    private bool _syncingExclusive;

    public Guid RuleId { get; }

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _executableName = string.Empty;

    [ObservableProperty]
    private bool _isBorderless = true;

    [ObservableProperty]
    private bool _isAlwaysOnTop;

    [ObservableProperty]
    private bool _isExpandToScreen = true;

    [ObservableProperty]
    private bool _useCustomDimension;

    [ObservableProperty]
    private int _customX;

    [ObservableProperty]
    private int _customY;

    [ObservableProperty]
    private int _customWidth;

    [ObservableProperty]
    private int _customHeight;

    [ObservableProperty]
    private bool _muteInBackground;

    [ObservableProperty]
    private bool _lockCursor;

    [ObservableProperty]
    private bool _hideCursor;

    [ObservableProperty]
    private bool _removeGameMenus;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isLoadingProcesses;

    [ObservableProperty]
    private bool _isDimensionDialogOpen;

    [ObservableProperty]
    private double _draftX;

    [ObservableProperty]
    private double _draftY;

    [ObservableProperty]
    private double _draftWidth;

    [ObservableProperty]
    private double _draftHeight;

    public ObservableCollection<ProcessSuggestion> AllProcesses { get; } = [];

    public ObservableCollection<ProcessSuggestion> FilteredProcesses { get; } = [];

    [ObservableProperty]
    private bool _isProcessPickerOpen;

    public string DialogTitle { get; }

    public string EnabledToggleLabel =>
        IsEnabled ? Loc.Get("ToggleRuleEnabled") : Loc.Get("RuleStatusDisabled");

    public string CustomDimensionSummary
    {
        get
        {
            var (screenW, screenH) = WindowStyleService.GetPrimaryMonitorSize();
            var width = CustomWidth > 0 ? CustomWidth : screenW;
            var height = CustomHeight > 0 ? CustomHeight : screenH;
            return string.Format(Loc.Get("CustomDimensionSummaryFormat"), CustomX, CustomY, width, height);
        }
    }

    public RuleEditorViewModel(ProcessCatalogService processCatalog, RuleDefaults defaults)
    {
        _processCatalog = processCatalog;
        RuleId = Guid.NewGuid();
        DialogTitle = Loc.Get("AddRuleTitle");
        IsBorderless = defaults.IsBorderless;
        IsAlwaysOnTop = defaults.IsAlwaysOnTop;
        MuteInBackground = defaults.MuteInBackground;
        LockCursor = defaults.LockCursor;
        HideCursor = defaults.HideCursor;
        RemoveGameMenus = defaults.RemoveGameMenus;
        IsEnabled = defaults.IsEnabled;
        CustomX = defaults.CustomX;
        CustomY = defaults.CustomY;
        CustomWidth = defaults.CustomWidth;
        CustomHeight = defaults.CustomHeight;
        ApplyExclusiveSizeMode(defaults.UseCustomDimension, defaults.IsExpandToScreen);
    }

    public RuleEditorViewModel(ProcessCatalogService processCatalog, ProcessRule rule)
    {
        _processCatalog = processCatalog;
        RuleId = rule.Id;
        WindowTitle = rule.WindowTitle;
        ExecutableName = rule.ExecutableName;
        IsBorderless = rule.IsBorderless;
        IsAlwaysOnTop = rule.IsAlwaysOnTop;
        MuteInBackground = rule.MuteInBackground;
        LockCursor = rule.LockCursor;
        HideCursor = rule.HideCursor;
        RemoveGameMenus = rule.RemoveGameMenus;
        IsEnabled = rule.IsEnabled;
        CustomX = rule.CustomX;
        CustomY = rule.CustomY;
        CustomWidth = rule.CustomWidth;
        CustomHeight = rule.CustomHeight;
        DialogTitle = Loc.Get("EditRuleTitle");
        ApplyExclusiveSizeMode(rule.UseCustomDimension, rule.IsExpandToScreen);
    }

    private void ApplyExclusiveSizeMode(bool useCustom, bool expandToScreen)
    {
        _syncingExclusive = true;
        if (useCustom)
        {
            UseCustomDimension = true;
            IsExpandToScreen = false;
        }
        else
        {
            UseCustomDimension = false;
            IsExpandToScreen = expandToScreen;
        }

        _syncingExclusive = false;
    }

    public Task RefreshProcessSnapshotAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        return RefreshProcessSnapshotCoreAsync(_refreshCts.Token);
    }

    public void SelectProcess(ProcessSuggestion suggestion)
    {
        WindowTitle = suggestion.WindowTitle ?? string.Empty;
        ExecutableName = suggestion.ExecutableName ?? string.Empty;
        IsProcessPickerOpen = false;
        RefreshFilteredProcesses();
    }

    public void OpenProcessPicker()
    {
        RefreshFilteredProcesses();
        IsProcessPickerOpen = FilteredProcesses.Count > 0;
    }

    public void CloseProcessPicker()
    {
        IsProcessPickerOpen = false;
    }

    partial void OnExecutableNameChanged(string value) => RefreshFilteredProcesses();

    public void RefreshFilteredProcesses()
    {
        var filter = ExecutableName?.Trim() ?? string.Empty;
        IEnumerable<ProcessSuggestion> query = AllProcesses;
        if (!string.IsNullOrEmpty(filter))
        {
            query = AllProcesses.Where(p =>
                p.ExecutableName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || p.WindowTitle.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || p.DisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var matches = query.Take(80).ToList();
        FilteredProcesses.Clear();
        foreach (var match in matches)
        {
            FilteredProcesses.Add(match);
        }

        if (IsProcessPickerOpen && FilteredProcesses.Count == 0)
        {
            IsProcessPickerOpen = false;
        }
    }

    public bool TryBuild(out ProcessRule? rule)
    {
        ValidationMessage = null;

        if (string.IsNullOrWhiteSpace(WindowTitle) && string.IsNullOrWhiteSpace(ExecutableName))
        {
            ValidationMessage = Loc.Get("ValidationMatchRequired");
            rule = null;
            return false;
        }

        rule = new ProcessRule
        {
            Id = RuleId,
            WindowTitle = WindowTitle.Trim(),
            ExecutableName = ExecutableName.Trim(),
            IsBorderless = IsBorderless,
            IsAlwaysOnTop = IsAlwaysOnTop,
            IsExpandToScreen = IsExpandToScreen,
            UseCustomDimension = UseCustomDimension,
            CustomX = CustomX,
            CustomY = CustomY,
            CustomWidth = CustomWidth,
            CustomHeight = CustomHeight,
            MuteInBackground = MuteInBackground,
            LockCursor = LockCursor,
            HideCursor = HideCursor,
            RemoveGameMenus = RemoveGameMenus,
            IsEnabled = IsEnabled
        };
        return true;
    }

    partial void OnIsEnabledChanged(bool value) =>
        OnPropertyChanged(nameof(EnabledToggleLabel));

    partial void OnIsExpandToScreenChanged(bool value)
    {
        if (_syncingExclusive || !value || !UseCustomDimension)
        {
            return;
        }

        _syncingExclusive = true;
        UseCustomDimension = false;
        _syncingExclusive = false;
    }

    partial void OnUseCustomDimensionChanged(bool value)
    {
        if (_syncingExclusive)
        {
            OnPropertyChanged(nameof(CustomDimensionSummary));
            return;
        }

        OnPropertyChanged(nameof(CustomDimensionSummary));

        if (!value)
        {
            return;
        }

        if (IsExpandToScreen)
        {
            _syncingExclusive = true;
            IsExpandToScreen = false;
            _syncingExclusive = false;
        }

        if (!IsDimensionDialogOpen)
        {
            _dimensionDialogFromToggle = true;
            OpenDimensionDialog();
        }
    }

    partial void OnCustomXChanged(int value) => OnPropertyChanged(nameof(CustomDimensionSummary));
    partial void OnCustomYChanged(int value) => OnPropertyChanged(nameof(CustomDimensionSummary));
    partial void OnCustomWidthChanged(int value) => OnPropertyChanged(nameof(CustomDimensionSummary));
    partial void OnCustomHeightChanged(int value) => OnPropertyChanged(nameof(CustomDimensionSummary));

    [RelayCommand]
    private void OpenDimensionDialog()
    {
        var (screenW, screenH) = WindowStyleService.GetPrimaryMonitorSize();
        DraftX = CustomX;
        DraftY = CustomY;
        DraftWidth = CustomWidth > 0 ? CustomWidth : screenW;
        DraftHeight = CustomHeight > 0 ? CustomHeight : screenH;
        IsDimensionDialogOpen = true;
    }

    [RelayCommand]
    private void EditDimensionDialog()
    {
        _dimensionDialogFromToggle = false;
        OpenDimensionDialog();
    }

    [RelayCommand]
    private void SaveDimensionDialog()
    {
        CustomX = (int)Math.Round(DraftX);
        CustomY = (int)Math.Round(DraftY);
        CustomWidth = Math.Max(0, (int)Math.Round(DraftWidth));
        CustomHeight = Math.Max(0, (int)Math.Round(DraftHeight));
        _dimensionDialogFromToggle = false;
        UseCustomDimension = true;
        IsDimensionDialogOpen = false;
        OnPropertyChanged(nameof(CustomDimensionSummary));
    }

    [RelayCommand]
    private void CancelDimensionDialog()
    {
        IsDimensionDialogOpen = false;
        if (_dimensionDialogFromToggle)
        {
            _dimensionDialogFromToggle = false;
            UseCustomDimension = false;
        }
    }

    private async Task RefreshProcessSnapshotCoreAsync(CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref _refreshVersion);

        await UiDispatch.InvokeAsync(() => IsLoadingProcesses = true, DispatcherPriority.Background)
            .ConfigureAwait(false);

        IReadOnlyList<ProcessSuggestion> processes;
        try
        {
            processes = await _processCatalog.GetRunningProcessesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            processes = [];
        }

        if (cancellationToken.IsCancellationRequested || version != _refreshVersion)
        {
            return;
        }

        await UiDispatch.InvokeAsync(() =>
        {
            if (version != _refreshVersion)
            {
                return;
            }

            AllProcesses.Clear();
            foreach (var process in processes)
            {
                AllProcesses.Add(process);
            }

            RefreshFilteredProcesses();
            if (IsProcessPickerOpen)
            {
                IsProcessPickerOpen = FilteredProcesses.Count > 0;
            }

            IsLoadingProcesses = false;
        }, DispatcherPriority.Background).ConfigureAwait(false);
    }
}
