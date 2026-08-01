using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using Borderless.App.Helpers;
using Borderless.App.Models;
using Borderless.App.Native;

namespace Borderless.App.Services;

/// <summary>
/// Polls top-level windows off the UI thread and applies matching process rules.
/// </summary>
public sealed class RuleEngineService : IDisposable
{
    private readonly WindowStyleService _windowStyleService;
    private readonly AudioMuteService _audioMuteService;
    private readonly DispatcherTimer _timer;
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly object _rulesGate = new();
    private IReadOnlyList<ProcessRule> _rules = [];
    private readonly HashSet<int> _muteTrackedPids = [];
    private int _tickRunning;
    private bool _disposed;

    public RuleEngineService(WindowStyleService windowStyleService, AudioMuteService audioMuteService)
    {
        _windowStyleService = windowStyleService;
        _audioMuteService = audioMuteService;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _timer.Tick += (_, _) => QueueTick();
    }

    public void UpdateRules(IEnumerable<ProcessRule> rules)
    {
        var enabled = rules.Where(r => r.IsEnabled).ToList();
        lock (_rulesGate)
        {
            _rules = enabled;
        }
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _audioMuteService.Dispose();
    }

    private void QueueTick()
    {
        if (Interlocked.Exchange(ref _tickRunning, 1) == 1)
        {
            return;
        }

        _ = Task.Run(TickWorker);
    }

    private void TickWorker()
    {
        try
        {
            IReadOnlyList<ProcessRule> rules;
            lock (_rulesGate)
            {
                rules = _rules;
            }

            if (rules.Count == 0)
            {
                return;
            }

            var foreground = NativeMethods.GetForegroundWindow();
            var matches = new List<WindowMatch>();
            var matchedMutePids = new HashSet<int>();

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd))
                {
                    return true;
                }

                var title = GetWindowTitle(hwnd);
                if (string.IsNullOrWhiteSpace(title))
                {
                    return true;
                }

                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
                if ((int)pid == _ownProcessId)
                {
                    return true;
                }

                var executableName = TryGetExecutableName((int)pid);
                var rule = rules.FirstOrDefault(r => r.Matches(title, executableName));
                if (rule is null)
                {
                    return true;
                }

                matches.Add(new WindowMatch(hwnd, (int)pid, rule, hwnd == foreground));
                if (rule.MuteInBackground)
                {
                    matchedMutePids.Add((int)pid);
                }

                return true;
            }, IntPtr.Zero);

            // Win32 style + Core Audio COM — marshal to UI/STA dispatcher.
            UiDispatch.Post(() => ApplyMatches(matches, matchedMutePids), DispatcherPriority.Background);
        }
        finally
        {
            Interlocked.Exchange(ref _tickRunning, 0);
        }
    }

    private void ApplyMatches(List<WindowMatch> matches, HashSet<int> matchedMutePids)
    {
        if (_disposed)
        {
            return;
        }

        _windowStyleService.PruneClosed();

        foreach (var match in matches)
        {
            _windowStyleService.ApplyVideo(match.Hwnd, match.Rule);

            if (match.Rule.MuteInBackground)
            {
                _audioMuteService.SetMuteDesired(match.ProcessId, !match.IsForeground);
            }
        }

        foreach (var stalePid in _muteTrackedPids.Except(matchedMutePids).ToList())
        {
            _audioMuteService.Clear(stalePid);
        }

        _muteTrackedPids.Clear();
        foreach (var pid in matchedMutePids)
        {
            _muteTrackedPids.Add(pid);
        }

        _audioMuteService.Refresh();
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var buffer = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string? TryGetExecutableName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct WindowMatch(nint Hwnd, int ProcessId, ProcessRule Rule, bool IsForeground);
}
