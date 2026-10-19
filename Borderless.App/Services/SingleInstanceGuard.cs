using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using Borderless.App.Helpers;
using Borderless.App.Native;

namespace Borderless.App.Services;

/// <summary>
/// One process per user session. A later launch pulses the first to show its window.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\Venipa.Borderless.SingleInstance";
    private const string ShowEventName = @"Local\Venipa.Borderless.Show";
    private const int ShowEventRetries = 50;
    private const int ShowEventRetryMs = 20;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly Action _onShowRequested;
    private readonly RegisteredWaitHandle _wait;
    private int _disposed;

    private SingleInstanceGuard(Mutex mutex, Action onShowRequested)
    {
        _mutex = mutex;
        _onShowRequested = onShowRequested;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _wait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            static (state, _) => ((SingleInstanceGuard)state!).OnShowEvent(),
            this,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public static SingleInstanceGuard? ClaimPrimary(Action onShowRequested)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                createdNew = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                createdNew = true;
            }
        }

        if (!createdNew)
        {
            mutex.Dispose();
            SignalShow();
            return null;
        }

        return new SingleInstanceGuard(mutex, onShowRequested);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _wait.Unregister(null);
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
        _showEvent.Dispose();
    }

    private void OnShowEvent()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        UiDispatch.Post(_onShowRequested, DispatcherPriority.Normal);
    }

    private static void SignalShow()
    {
        YieldForegroundToOtherInstances();
        for (var i = 0; i < ShowEventRetries; i++)
        {
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
                showEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(ShowEventRetryMs);
            }
        }
    }

    private static void YieldForegroundToOtherInstances()
    {
        var currentId = Environment.ProcessId;
        var name = Process.GetCurrentProcess().ProcessName;
        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                if (process.Id != currentId)
                {
                    NativeMethods.AllowSetForegroundWindow(process.Id);
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
