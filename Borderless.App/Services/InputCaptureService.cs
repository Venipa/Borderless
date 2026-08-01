using Borderless.App.Models;
using Borderless.App.Native;

namespace Borderless.App.Services;

/// <summary>
/// Cursor clip / hide and window menu removal for matched game windows.
/// </summary>
public sealed class InputCaptureService : IDisposable
{
    private readonly Dictionary<nint, nint> _menuBackups = new();
    private readonly HashSet<nint> _menusRemoved = [];
    private nint _clipHwnd;
    private bool _cursorHidden;
    private bool _disposed;

    public void Apply(nint hwnd, ProcessRule rule, bool isForeground)
    {
        if (_disposed || !NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        ApplyMenu(hwnd, rule.RemoveGameMenus);

        if (!isForeground)
        {
            return;
        }

        if (rule.LockCursor)
        {
            ClipToWindow(hwnd);
        }
        else if (_clipHwnd == hwnd)
        {
            ClearClip();
        }

        if (rule.HideCursor)
        {
            EnsureCursorHidden();
        }
        else
        {
            EnsureCursorVisible();
        }
    }

    /// <summary>
    /// Restore input state for windows that no longer match, and clear global
    /// cursor lock/hide when the foreground is not an active capture target.
    /// </summary>
    public void SyncActiveWindows(HashSet<nint> activeHwnds, nint foregroundHwnd, bool foregroundWantsClip, bool foregroundWantsHide)
    {
        foreach (var hwnd in _menusRemoved.ToList())
        {
            if (!activeHwnds.Contains(hwnd) || !NativeMethods.IsWindow(hwnd))
            {
                RestoreMenu(hwnd);
            }
        }

        if (!foregroundWantsClip || foregroundHwnd == 0 || !activeHwnds.Contains(foregroundHwnd))
        {
            ClearClip();
        }

        if (!foregroundWantsHide || foregroundHwnd == 0 || !activeHwnds.Contains(foregroundHwnd))
        {
            EnsureCursorVisible();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearClip();
        EnsureCursorVisible();

        foreach (var hwnd in _menusRemoved.ToList())
        {
            RestoreMenu(hwnd);
        }

        _menuBackups.Clear();
        _menusRemoved.Clear();
    }

    private void ApplyMenu(nint hwnd, bool remove)
    {
        if (remove)
        {
            if (_menusRemoved.Contains(hwnd))
            {
                return;
            }

            var menu = NativeMethods.GetMenu(hwnd);
            if (menu != IntPtr.Zero)
            {
                _menuBackups[hwnd] = menu;
                _ = NativeMethods.SetMenu(hwnd, IntPtr.Zero);
                _ = NativeMethods.DrawMenuBar(hwnd);
            }

            _menusRemoved.Add(hwnd);
            return;
        }

        if (_menusRemoved.Contains(hwnd))
        {
            RestoreMenu(hwnd);
        }
    }

    private void RestoreMenu(nint hwnd)
    {
        if (_menuBackups.TryGetValue(hwnd, out var menu) && NativeMethods.IsWindow(hwnd))
        {
            _ = NativeMethods.SetMenu(hwnd, menu);
            _ = NativeMethods.DrawMenuBar(hwnd);
        }

        _menuBackups.Remove(hwnd);
        _menusRemoved.Remove(hwnd);
    }

    private void ClipToWindow(nint hwnd)
    {
        if (!NativeMethods.GetClientRect(hwnd, out var client))
        {
            return;
        }

        var topLeft = new NativeMethods.Point { X = client.Left, Y = client.Top };
        var bottomRight = new NativeMethods.Point { X = client.Right, Y = client.Bottom };
        if (!NativeMethods.ClientToScreen(hwnd, ref topLeft)
            || !NativeMethods.ClientToScreen(hwnd, ref bottomRight))
        {
            return;
        }

        var clip = new NativeMethods.Rect
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Right = bottomRight.X,
            Bottom = bottomRight.Y
        };

        if (clip.Right <= clip.Left || clip.Bottom <= clip.Top)
        {
            return;
        }

        _ = NativeMethods.ClipCursor(ref clip);
        _clipHwnd = hwnd;
    }

    private void ClearClip()
    {
        if (_clipHwnd == 0)
        {
            return;
        }

        _ = NativeMethods.ClipCursor(IntPtr.Zero);
        _clipHwnd = 0;
    }

    private void EnsureCursorHidden()
    {
        if (_cursorHidden)
        {
            return;
        }

        while (NativeMethods.ShowCursor(false) >= 0)
        {
            // Drive display count negative.
        }

        _cursorHidden = true;
    }

    private void EnsureCursorVisible()
    {
        if (!_cursorHidden)
        {
            return;
        }

        while (NativeMethods.ShowCursor(true) < 0)
        {
            // Drive display count non-negative.
        }

        _cursorHidden = false;
    }
}
