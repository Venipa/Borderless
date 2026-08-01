using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Borderless.App.Models;
using Borderless.App.ViewModels;

namespace Borderless.App.Views;

public partial class RuleEditorSidebar : UserControl
{
    private int _openSuggestionsVersion;

    public RuleEditorSidebar()
    {
        InitializeComponent();
    }

    private RuleEditorViewModel? ViewModel => DataContext as RuleEditorViewModel;

    private async void OnExecutableGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        await OpenProcessPickerAsync(refreshSnapshot: true);
    }

    private async void OnExecutablePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && !element.IsKeyboardFocusWithin)
        {
            element.Focus();
            e.Handled = true;
            await OpenProcessPickerAsync(refreshSnapshot: false);
        }
    }

    private void OnExecutableLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsFocusWithinProcessPicker(e.NewFocus as DependencyObject))
        {
            return;
        }

        ViewModel?.CloseProcessPicker();
    }

    private void OnProcessListPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var suggestion = FindProcessSuggestion(e.OriginalSource as DependencyObject);
        if (suggestion is null || ViewModel is null)
        {
            return;
        }

        e.Handled = true;
        ViewModel.SelectProcess(suggestion);
        ExecutableBox.Text = suggestion.ExecutableName;
        Keyboard.Focus(ExecutableBox);
    }

    private async Task OpenProcessPickerAsync(bool refreshSnapshot)
    {
        if (ViewModel is null)
        {
            return;
        }

        var version = Interlocked.Increment(ref _openSuggestionsVersion);

        if (refreshSnapshot || ViewModel.AllProcesses.Count == 0)
        {
            await ViewModel.RefreshProcessSnapshotAsync();
        }

        if (version != _openSuggestionsVersion)
        {
            return;
        }

        ViewModel.OpenProcessPicker();
    }

    private bool IsFocusWithinProcessPicker(DependencyObject? focus)
    {
        while (focus is not null)
        {
            if (ReferenceEquals(focus, ProcessList)
                || ReferenceEquals(focus, ProcessPopup)
                || ReferenceEquals(focus, ExecutableBox)
                || ReferenceEquals(focus, ExecutablePickerHost))
            {
                return true;
            }

            focus = GetParentObject(focus);
        }

        return false;
    }

    private static ProcessSuggestion? FindProcessSuggestion(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: ProcessSuggestion suggestion })
            {
                return suggestion;
            }

            if (source is FrameworkContentElement { DataContext: ProcessSuggestion contentSuggestion })
            {
                return contentSuggestion;
            }

            if (source is ListBoxItem { Content: ProcessSuggestion listSuggestion })
            {
                return listSuggestion;
            }

            source = GetParentObject(source);
        }

        return null;
    }

    private static DependencyObject? GetParentObject(DependencyObject? child)
    {
        if (child is null)
        {
            return null;
        }

        if (child is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(child) ?? LogicalTreeHelper.GetParent(child);
        }

        return LogicalTreeHelper.GetParent(child);
    }
}
