using System.Windows;
using System.Windows.Input;
using Borderless.App.Localization;
using Borderless.App.Models;
using Borderless.App.Services;
using Wpf.Ui.Controls;

namespace Borderless.App.Views;

public partial class UpdateAvailableWindow : FluentWindow
{
    public UpdatePromptResult Result { get; private set; } = UpdatePromptResult.Cancel;

    public UpdateAvailableWindow(UpdateCheckResult update)
    {
        ArgumentNullException.ThrowIfNull(update);

        InitializeComponent();

        Title = Loc.Get("UpdateWindowTitle");
        HeadlineText.Text = Loc.Get("UpdateWindowHeadline");
        VersionText.Text = string.Format(
            Loc.Get("UpdateWindowVersionFormat"),
            update.RemoteVersion,
            update.LocalVersion);

        var releaseTitle = string.IsNullOrWhiteSpace(update.ReleaseName)
            ? update.TagName
            : update.ReleaseName;
        ReleaseTitleText.Text = releaseTitle ?? string.Empty;
        ReleaseTitleText.Visibility = string.IsNullOrWhiteSpace(releaseTitle)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var notes = update.ReleaseBody?.Trim();
        NotesText.Text = string.IsNullOrWhiteSpace(notes)
            ? Loc.Get("UpdateWindowNoNotes")
            : notes;

        DownloadButton.Content = Loc.Get("UpdateWindowDownload");
        InstallAfterExitButton.Content = Loc.Get("UpdateWindowInstallAfterExit");
        CancelButton.Content = Loc.Get("UpdateWindowCancel");
        NotesHeader.Text = Loc.Get("UpdateWindowNotesHeader");
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        Result = UpdatePromptResult.DownloadNow;
        DialogResult = true;
        Close();
    }

    private void OnInstallAfterExitClick(object sender, RoutedEventArgs e)
    {
        Result = UpdatePromptResult.InstallAfterExit;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = UpdatePromptResult.Cancel;
        DialogResult = false;
        Close();
    }

    private void OnTitleBarCloseClicked(TitleBar sender, RoutedEventArgs args)
    {
        Result = UpdatePromptResult.Cancel;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = UpdatePromptResult.Cancel;
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
