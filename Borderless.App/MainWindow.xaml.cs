using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Borderless.App.Localization;
using Borderless.App.Models;
using Borderless.App.ViewModels;
using Borderless.App.Views;
using Wpf.Ui.Controls;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Borderless.App;

public partial class MainWindow : FluentWindow
{
    private const double SidebarWidth = 520;
    private const double OpenDurationMs = 220;
    private const double CloseDurationMs = 180;
    private const double NavToggleTopGap = 8;

    private static readonly Uri AppIconUri = new("pack://application:,,,/Resources/Iconx24.png");

    private NotifyIcon? _trayIcon;
    private bool _forceClose;
    private BitmapImage? _toggleBrandIcon;

    public MainWindow()
    {
        DataContext = App.MainViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(RulesPage));
        EnsureTrayIcon();
        Dispatcher.BeginInvoke(ApplyToggleBrandContent, System.Windows.Threading.DispatcherPriority.Loaded);
        _ = ViewModel.Settings.CheckForUpdatesOnStartupAsync();
    }

    private void OnNavigationLoaded(object sender, RoutedEventArgs e)
    {
        ApplyToggleBrandContent();
    }

    private void OnPaneOpened(NavigationView sender, RoutedEventArgs args)
    {
        // Wait for pane open visual state, then brand the toggle content.
        Dispatcher.BeginInvoke(ApplyToggleBrandContent, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnPaneClosed(NavigationView sender, RoutedEventArgs args)
    {
        // Collapse must show hamburger only — clear branded content immediately.
        Dispatcher.BeginInvoke(ApplyToggleBrandContent, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Expanded: hamburger + app icon + title.
    /// Collapsed: hamburger only (Content must be null for BasePaneButtonStyle).
    /// </summary>
    private void ApplyToggleBrandContent()
    {
        if (RootNavigation.Template?.FindName("PART_ToggleButton", RootNavigation) is not Button toggle)
        {
            return;
        }

        EnsureHamburgerIcon(toggle);
        LockToggleButtonSize(toggle, RootNavigation.IsPaneOpen);
        toggle.SetCurrentValue(
            FrameworkElement.MarginProperty,
            new Thickness(0, NavToggleTopGap, 0, 5));

        if (!RootNavigation.IsPaneOpen)
        {
            // Local null so template PaneTitle TextBlock cannot come back and steal the row.
            toggle.SetCurrentValue(ContentControl.ContentProperty, null);
            return;
        }

        _toggleBrandIcon ??= new BitmapImage(AppIconUri);

        var brand = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        brand.Children.Add(new Image
        {
            Source = _toggleBrandIcon,
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        });
        brand.Children.Add(new TextBlock
        {
            Text = Loc.Get("AppTitle"),
            FontSize = 14,
            FontWeight = FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });

        toggle.SetCurrentValue(ContentControl.ContentProperty, brand);
    }

    private static void EnsureHamburgerIcon(Button toggle)
    {
        if (toggle.Icon is SymbolIcon symbol && symbol.Symbol == SymbolRegular.LineHorizontal320)
        {
            return;
        }

        toggle.SetCurrentValue(
            Button.IconProperty,
            new SymbolIcon { Symbol = SymbolRegular.LineHorizontal320 });
    }

    private static void LockToggleButtonSize(Button toggle, bool isPaneOpen)
    {
        toggle.SetCurrentValue(FrameworkElement.HeightProperty, 40.0);
        toggle.SetCurrentValue(FrameworkElement.MinHeightProperty, 40.0);
        toggle.SetCurrentValue(FrameworkElement.MaxHeightProperty, 40.0);

        if (isPaneOpen)
        {
            toggle.ClearValue(FrameworkElement.WidthProperty);
            toggle.ClearValue(FrameworkElement.MaxWidthProperty);
            toggle.ClearValue(FrameworkElement.MinWidthProperty);
            toggle.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Stretch);
        }
        else
        {
            toggle.SetCurrentValue(FrameworkElement.MinWidthProperty, 40.0);
            toggle.SetCurrentValue(FrameworkElement.WidthProperty, 40.0);
            toggle.SetCurrentValue(FrameworkElement.MaxWidthProperty, 40.0);
            toggle.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        if (args.Page is FrameworkElement page)
        {
            page.DataContext = ViewModel;
        }

        ViewModel.Navigate(args.Page switch
        {
            DefaultsPage => AppSection.Defaults,
            SettingsPage => AppSection.Settings,
            _ => AppSection.Rules
        });
    }

    private void OnEditorScrimClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel.CloseEditorCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsEditorOpen))
        {
            return;
        }

        if (ViewModel.IsEditorOpen)
        {
            OpenSidebar();
        }
        else
        {
            CloseSidebar();
        }
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        if (_forceClose || !ViewModel.Settings.CloseToTray)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        EnsureTrayIcon();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = true;
        }
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _trayIcon = new NotifyIcon
        {
            Text = Loc.Get("AppTitle"),
            Icon = LoadAppIcon(),
            Visible = false
        };
        _trayIcon.MouseClick += OnTrayMouseClick;

        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.Get("TrayOpen"), null, (_, _) => RestoreFromTray());
        menu.Items.Add(Loc.Get("TrayExit"), null, (_, _) => ExitFromTray());
        _trayIcon.ContextMenuStrip = menu;
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var associated = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (associated is not null)
                {
                    return associated;
                }
            }
        }
        catch
        {
            // Fall through to embedded resource.
        }

        try
        {
            var uri = new Uri("pack://application:,,,/Resources/app.ico");
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo?.Stream is not null)
            {
                return new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch
        {
            // Fall through to default.
        }

        return SystemIcons.Application;
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            RestoreFromTray();
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
        }
    }

    private void ExitFromTray()
    {
        _forceClose = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
        }

        Application.Current.Shutdown();
    }

    private void OpenSidebar()
    {
        EditorOverlay.Visibility = Visibility.Visible;
        EditorSidebarTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        EditorSidebarTransform.X = SidebarWidth;

        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(OpenDurationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        EditorSidebarTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }

    private void CloseSidebar()
    {
        var animation = new DoubleAnimation
        {
            To = SidebarWidth,
            Duration = TimeSpan.FromMilliseconds(CloseDurationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) =>
        {
            if (!ViewModel.IsEditorOpen)
            {
                EditorOverlay.Visibility = Visibility.Collapsed;
                ViewModel.ClearEditor();
            }
        };
        EditorSidebarTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }
}
