using System.Windows.Controls;
using Borderless.App.Helpers;

namespace Borderless.App.Views;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ScrollViewerWheel.Attach(this, SettingsScrollViewer);
            ScrollViewerWheel.Attach(SettingsScrollViewer, SettingsScrollViewer);
        };
    }
}
