using System.IO;
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
    private const int PidCacheTtlMs = 10_000;
    private const int MuteSessionRefreshTicks = 5;

    private readonly WindowStyleService _windowStyleService;
    private readonly AudioMuteService _audioMuteService;
    private readonly DispatcherTimer _timer;
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly object _rulesGate = new();
    private readonly Dictionary<int, CachedExe> _pidExeCache = new();
    private readonly StringBuilder _titleBuffer = new(512);
    private readonly StringBuilder _pathBuffer = new(1024);
    private IReadOnlyList<ProcessRule> _rules = [];
    private readonly HashSet<int> _muteTrackedPids = [];
    private int _tickRunning;
    private int _muteRefreshCountdown;
    private bool _disposed;

    /// <summary>
    /// Fired on the UI thread after each apply pass with live statuses for matched rule ids.
    /// Missing ids are idle (no matching process this tick).
    /// </summary>
    public event Action<IReadOnlyDictionary<Guid, RuleLiveStatus>>? RuleStatusesChanged;

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
        var posted = false;
        try
        {
            IReadOnlyList<ProcessRule> rules;
            lock (_rulesGate)
            {
                rules = _rules;
            }

            if (rules.Count == 0)
            {
                UiDispatch.Post(() =>
                {
                    try
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        _windowStyleService.SyncActiveWindows([]);
                        ClearMuteTracking();
                        RuleStatusesChanged?.Invoke(new Dictionary<Guid, RuleLiveStatus>());
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _tickRunning, 0);
                    }
                });
                posted = true;
                return;
            }

            var foreground = NativeMethods.GetForegroundWindow();
            var matches = new List<WindowMatch>();
            var matchedMutePids = new HashSet<int>();
            var now = Environment.TickCount64;
            PrunePidCache(now);

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

                var executableName = TryGetExecutableName((int)pid, now);
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

            var refreshMuteSessions = false;
            if (matchedMutePids.Count > 0)
            {
                _muteRefreshCountdown--;
                if (_muteRefreshCountdown <= 0)
                {
                    _muteRefreshCountdown = MuteSessionRefreshTicks;
                    refreshMuteSessions = true;
                }
            }
            else
            {
                _muteRefreshCountdown = 0;
            }

            // Win32 style + Core Audio COM — marshal to UI/STA dispatcher.
            UiDispatch.Post(() =>
            {
                try
                {
                    ApplyMatches(matches, matchedMutePids, refreshMuteSessions);
                }
                finally
                {
                    Interlocked.Exchange(ref _tickRunning, 0);
                }
            }, DispatcherPriority.Background);
            posted = true;
        }
        finally
        {
            if (!posted)
            {
                Interlocked.Exchange(ref _tickRunning, 0);
            }
        }
    }

    private void ApplyMatches(List<WindowMatch> matches, HashSet<int> matchedMutePids, bool refreshMuteSessions)
    {
        if (_disposed)
        {
            return;
        }

        var activeHwnds = new HashSet<nint>(matches.Count);
        foreach (var match in matches)
        {
            activeHwnds.Add(match.Hwnd);
        }

        _windowStyleService.SyncActiveWindows(activeHwnds);

        var statuses = new Dictionary<Guid, RuleLiveStatus>();
        foreach (var match in matches)
        {
            try
            {
                _windowStyleService.ApplyVideo(match.Hwnd, match.Rule);

                if (match.Rule.MuteInBackground)
                {
                    _audioMuteService.SetMuteDesired(match.ProcessId, !match.IsForeground);
                }

                if (!statuses.TryGetValue(match.Rule.Id, out var existing) || existing != RuleLiveStatus.Error)
                {
                    statuses[match.Rule.Id] = RuleLiveStatus.Active;
                }
            }
            catch
            {
                statuses[match.Rule.Id] = RuleLiveStatus.Error;
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

        if (refreshMuteSessions)
        {
            _audioMuteService.Refresh();
        }

        RuleStatusesChanged?.Invoke(statuses);
    }

    private void ClearMuteTracking()
    {
        foreach (var pid in _muteTrackedPids.ToList())
        {
            _audioMuteService.Clear(pid);
        }

        _muteTrackedPids.Clear();
    }

    private string GetWindowTitle(nint hwnd)
    {
        _titleBuffer.Clear();
        _titleBuffer.EnsureCapacity(512);
        _ = NativeMethods.GetWindowText(hwnd, _titleBuffer, _titleBuffer.Capacity);
        return _titleBuffer.ToString();
    }

    private string? TryGetExecutableName(int processId, long now)
    {
        if (_pidExeCache.TryGetValue(processId, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Name;
        }

        var name = QueryExecutableName(processId);
        _pidExeCache[processId] = new CachedExe(name, now + PidCacheTtlMs);
        return name;
    }

    private string? QueryExecutableName(int processId)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            _pathBuffer.Clear();
            _pathBuffer.EnsureCapacity(1024);
            var size = _pathBuffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, _pathBuffer, ref size) || size <= 0)
            {
                return null;
            }

            var fileName = Path.GetFileName(_pathBuffer.ToString());
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private void PrunePidCache(long now)
    {
        if (_pidExeCache.Count < 64)
        {
            return;
        }

        foreach (var pid in _pidExeCache.Where(kv => kv.Value.ExpiresAt <= now).Select(kv => kv.Key).ToList())
        {
            _pidExeCache.Remove(pid);
        }
    }

    private readonly record struct WindowMatch(nint Hwnd, int ProcessId, ProcessRule Rule, bool IsForeground);

    private readonly record struct CachedExe(string? Name, long ExpiresAt);
}
