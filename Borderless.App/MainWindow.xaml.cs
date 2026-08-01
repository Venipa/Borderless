using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Borderless.App.Localization;
using Borderless.App.Models;
using Borderless.App.ViewModels;
using Borderless.App.Views;
using Wpf.Ui.Controls;
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

    private bool _forceClose;
    private BitmapImage? _toggleBrandIcon;

    public MainWindow()
    {
        DataContext = App.MainViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
        IsVisibleChanged += OnIsVisibleChanged;
        Loc.Source.PropertyChanged += OnLocChanged;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(RulesPage));
        RefreshTrayToggleHeader();
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
        Loc.Source.PropertyChanged -= OnLocChanged;
        AppTrayIcon.Dispose();
    }

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshTrayToggleHeader();
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
        if (_forceClose)
        {
            return;
        }

        // Close-to-tray: X only hides. Quit comes from the tray menu.
        if (ViewModel.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshTrayToggleHeader();
    }

    private void OnTrayMenuOpened(object sender, RoutedEventArgs e)
    {
        RefreshTrayToggleHeader();
    }

    private void OnTrayLeftClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void OnTrayToggleClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void OnTrayQuitClick(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        Application.Current.Shutdown();
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RefreshTrayToggleHeader()
    {
        if (TrayToggleMenuItem is null)
        {
            return;
        }

        TrayToggleMenuItem.Header = IsVisible ? Loc.Get("TrayHide") : Loc.Get("TrayShow");
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
