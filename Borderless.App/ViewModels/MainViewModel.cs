using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using Borderless.App.Localization;
using Borderless.App.Models;
using Borderless.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Borderless.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly RuleStore _ruleStore;
    private readonly RuleEngineService _ruleEngine;
    private readonly ProcessCatalogService _processCatalog;

    public ObservableCollection<ProcessRule> Rules { get; } = [];

    public ICollectionView FilteredRules { get; }

    public AppSettingsViewModel Settings { get; }

    [ObservableProperty]
    private ProcessRule? _selectedRule;

    [ObservableProperty]
    private bool _hasRules;

    [ObservableProperty]
    private bool _hasVisibleRules;

    [ObservableProperty]
    private string _rulesSearchText = string.Empty;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private RuleEditorViewModel? _editor;

    [ObservableProperty]
    private AppSection _currentSection = AppSection.Rules;

    public bool IsRulesSection => CurrentSection == AppSection.Rules;
    public bool IsDefaultsSection => CurrentSection == AppSection.Defaults;
    public bool IsSettingsSection => CurrentSection == AppSection.Settings;
    public bool ShowAddButton => IsRulesSection && !IsEditorOpen;
    public bool ShowRulesEmpty => !HasRules;
    public bool ShowSearchEmpty => HasRules && !HasVisibleRules;

    public MainViewModel(
        RuleStore ruleStore,
        RuleEngineService ruleEngine,
        ProcessCatalogService processCatalog,
        AppSettingsViewModel settings)
    {
        _ruleStore = ruleStore;
        _ruleEngine = ruleEngine;
        _processCatalog = processCatalog;
        Settings = settings;

        FilteredRules = CollectionViewSource.GetDefaultView(Rules);
        FilteredRules.Filter = MatchesRulesSearch;
        Rules.CollectionChanged += OnRulesCollectionChanged;
        _ruleEngine.RuleStatusesChanged += OnRuleStatusesChanged;

        foreach (var rule in _ruleStore.Load())
        {
            Rules.Add(rule);
        }

        RefreshRuleVisibility();
        PushRulesToEngine();
        _ruleEngine.Start();
    }

    private void OnRuleStatusesChanged(IReadOnlyDictionary<Guid, RuleLiveStatus> live)
    {
        foreach (var rule in Rules)
        {
            if (!rule.IsEnabled)
            {
                if (rule.LiveStatus != RuleLiveStatus.Idle)
                {
                    rule.LiveStatus = RuleLiveStatus.Idle;
                }

                continue;
            }

            var next = live.TryGetValue(rule.Id, out var status) ? status : RuleLiveStatus.Idle;
            if (rule.LiveStatus != next)
            {
                rule.LiveStatus = next;
            }
        }
    }

    partial void OnCurrentSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsRulesSection));
        OnPropertyChanged(nameof(IsDefaultsSection));
        OnPropertyChanged(nameof(IsSettingsSection));
        OnPropertyChanged(nameof(ShowAddButton));
    }

    partial void OnIsEditorOpenChanged(bool value) => OnPropertyChanged(nameof(ShowAddButton));

    partial void OnHasRulesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRulesEmpty));
        OnPropertyChanged(nameof(ShowSearchEmpty));
    }

    partial void OnHasVisibleRulesChanged(bool value) => OnPropertyChanged(nameof(ShowSearchEmpty));

    partial void OnRulesSearchTextChanged(string value)
    {
        FilteredRules.Refresh();
        RefreshRuleVisibility();
    }

    public void Navigate(AppSection section) => CurrentSection = section;

    public void OpenAddEditor()
    {
        OpenEditor(new RuleEditorViewModel(_processCatalog, Settings.CreateRuleDefaults()));
    }

    public void OpenEditEditor(ProcessRule rule)
    {
        OpenEditor(new RuleEditorViewModel(_processCatalog, rule));
    }

    public void OpenEditor(RuleEditorViewModel editor)
    {
        Editor = editor;
        IsEditorOpen = true;
        _ = editor.RefreshProcessSnapshotAsync();
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
    }

    public void ClearEditor()
    {
        Editor = null;
    }

    [RelayCommand]
    private void SaveEditor()
    {
        if (Editor is null || !Editor.TryBuild(out var rule) || rule is null)
        {
            return;
        }

        if (Rules.Any(r => r.Id != rule.Id && r.HasSameMatchKey(rule)))
        {
            Editor.ValidationMessage = Loc.Get("ValidationDuplicateRule");
            return;
        }

        AddOrUpdateRule(rule);
        CloseEditor();
    }

    public void AddOrUpdateRule(ProcessRule rule)
    {
        var duplicate = Rules.FirstOrDefault(r => r.Id != rule.Id && r.HasSameMatchKey(rule));
        if (duplicate is not null)
        {
            return;
        }

        var existing = Rules.FirstOrDefault(r => r.Id == rule.Id);
        if (existing is null)
        {
            Rules.Add(rule);
        }
        else
        {
            var index = Rules.IndexOf(existing);
            rule.LiveStatus = existing.LiveStatus;
            Rules[index] = rule;
        }

        PersistAsync();
    }

    [RelayCommand]
    private void DeleteRule(ProcessRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        Rules.Remove(rule);
        if (SelectedRule?.Id == rule.Id)
        {
            SelectedRule = null;
        }

        PersistAsync();
    }

    [RelayCommand]
    private void ClearRulesSearch()
    {
        RulesSearchText = string.Empty;
    }

    private void OnRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRuleVisibility();
    }

    private void PersistAsync()
    {
        RefreshRuleVisibility();
        PushRulesToEngine();

        var snapshot = Rules.ToList();
        _ = Task.Run(() => _ruleStore.Save(snapshot));
    }

    private void PushRulesToEngine() => _ruleEngine.UpdateRules(Rules);

    private void RefreshRuleVisibility()
    {
        HasRules = Rules.Count > 0;
        HasVisibleRules = Rules.Any(MatchesRulesSearch);
    }

    private bool MatchesRulesSearch(object obj) =>
        obj is ProcessRule rule && MatchesRulesSearch(rule);

    private bool MatchesRulesSearch(ProcessRule rule)
    {
        if (string.IsNullOrWhiteSpace(RulesSearchText))
        {
            return true;
        }

        var query = RulesSearchText.Trim();
        return ContainsIgnoreCase(rule.DisplayName, query)
            || ContainsIgnoreCase(rule.ExecutableName, query)
            || ContainsIgnoreCase(rule.WindowTitle, query);
    }

    private static bool ContainsIgnoreCase(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
