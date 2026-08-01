using System.Windows.Controls;
using Borderless.App.Helpers;

namespace Borderless.App.Views;

public partial class DefaultsPage : Page
{
    public DefaultsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ScrollViewerWheel.Attach(this, DefaultsScrollViewer);
            ScrollViewerWheel.Attach(DefaultsScrollViewer, DefaultsScrollViewer);
        };
    }
}
