using System.Windows;
using System.Windows.Threading;

namespace Borderless.App.Helpers;

/// <summary>
/// Marshals work onto the WPF UI dispatcher without blocking callers.
/// </summary>
public static class UiDispatch
{
    public static Dispatcher Dispatcher =>
        Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

    public static bool CheckAccess() => Dispatcher.CheckAccess();

    public static void Post(Action action, DispatcherPriority priority = DispatcherPriority.Background)
    {
        if (CheckAccess() && priority >= DispatcherPriority.DataBind)
        {
            action();
            return;
        }

        _ = Dispatcher.BeginInvoke(action, priority);
    }

    public static Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(action, priority).Task;
    }

    public static Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (CheckAccess())
        {
            return Task.FromResult(func());
        }

        return Dispatcher.InvokeAsync(func, priority).Task;
    }
}
