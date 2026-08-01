using Borderless.App.Models;
using Borderless.App.Native;

namespace Borderless.App.Services;

/// <summary>
/// Applies borderless, expand-to-screen, and always-on-top styles to native windows.
/// </summary>
public sealed class WindowStyleService
{
    private const int StandardChromeStyle =
        NativeMethods.WsCaption
        | NativeMethods.WsThickFrame
        | NativeMethods.WsMinimizeBox
        | NativeMethods.WsMaximizeBox
        | NativeMethods.WsSysMenu
        | NativeMethods.WsBorder;

    private readonly Dictionary<nint, ChromeBackup> _chromeBackups = new();
    private readonly HashSet<nint> _managedHwnds = [];
    private readonly Dictionary<nint, bool> _topMostState = new();
    private readonly Dictionary<nint, string> _lastLayoutSignature = new();

    public void ApplyVideo(nint hwnd, ProcessRule rule)
    {
        if (!NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        // Chrome is asserted every tick so games that re-strip styles still get undone.
        if (rule.IsBorderless)
        {
            ApplyBorderlessChrome(hwnd);
        }
        else if (_managedHwnds.Contains(hwnd))
        {
            RestoreBorderlessChrome(hwnd);
            _managedHwnds.Remove(hwnd);
            _chromeBackups.Remove(hwnd);
        }

        var layoutSignature = BuildLayoutSignature(rule);
        if (_lastLayoutSignature.TryGetValue(hwnd, out var previous) && previous == layoutSignature)
        {
            ApplyAlwaysOnTop(hwnd, rule.IsAlwaysOnTop);
            return;
        }

        if (rule.IsExpandToScreen || rule.UseCustomDimension)
        {
            ApplyBounds(hwnd, rule);
        }

        ApplyAlwaysOnTop(hwnd, rule.IsAlwaysOnTop);
        _lastLayoutSignature[hwnd] = layoutSignature;
    }

    public void ApplyAlwaysOnTop(nint hwnd, bool enabled)
    {
        if (!NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        if (_topMostState.TryGetValue(hwnd, out var current) && current == enabled)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            enabled ? NativeMethods.HwndTopMost : NativeMethods.HwndNoTopMost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);

        _topMostState[hwnd] = enabled;
    }

    /// <summary>
    /// Drop tracking for HWNDs that no longer match a rule. Restores chrome / topmost when still alive.
    /// </summary>
    public void SyncActiveWindows(HashSet<nint> activeHwnds)
    {
        foreach (var hwnd in CollectTrackedHwnds())
        {
            if (activeHwnds.Contains(hwnd))
            {
                continue;
            }

            Release(hwnd);
        }
    }

    public void Forget(nint hwnd)
    {
        _chromeBackups.Remove(hwnd);
        _managedHwnds.Remove(hwnd);
        _topMostState.Remove(hwnd);
        _lastLayoutSignature.Remove(hwnd);
    }

    public void PruneClosed()
    {
        foreach (var hwnd in CollectTrackedHwnds())
        {
            if (!NativeMethods.IsWindow(hwnd))
            {
                Forget(hwnd);
            }
        }
    }

    public static (int Width, int Height) GetPrimaryMonitorSize()
    {
        if (!TryGetMonitorRect(IntPtr.Zero, out var rect))
        {
            return (1920, 1080);
        }

        return (rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private void Release(nint hwnd)
    {
        if (NativeMethods.IsWindow(hwnd))
        {
            if (_managedHwnds.Contains(hwnd))
            {
                RestoreBorderlessChrome(hwnd);
            }

            if (_topMostState.TryGetValue(hwnd, out var wasTopMost) && wasTopMost)
            {
                NativeMethods.SetWindowPos(
                    hwnd,
                    NativeMethods.HwndNoTopMost,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
            }
        }

        Forget(hwnd);
    }

    private List<nint> CollectTrackedHwnds()
    {
        var set = new HashSet<nint>(_managedHwnds);
        foreach (var hwnd in _chromeBackups.Keys)
        {
            set.Add(hwnd);
        }

        foreach (var hwnd in _topMostState.Keys)
        {
            set.Add(hwnd);
        }

        foreach (var hwnd in _lastLayoutSignature.Keys)
        {
            set.Add(hwnd);
        }

        return set.ToList();
    }

    private void ApplyBorderlessChrome(nint hwnd)
    {
        var currentStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt32();
        var currentExStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt32();

        if (!_chromeBackups.ContainsKey(hwnd))
        {
            // Keep chrome bits in the backup even if the window was already borderless.
            var restoreStyle = currentStyle | StandardChromeStyle;
            restoreStyle &= ~NativeMethods.WsPopup;
            var restoreExStyle = currentExStyle | NativeMethods.WsExWindowEdge;

            _chromeBackups[hwnd] = new ChromeBackup(restoreStyle, restoreExStyle);
        }

        _managedHwnds.Add(hwnd);

        if (!IsMissingChrome(currentStyle))
        {
            var style = currentStyle;
            style &= ~StandardChromeStyle;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, new IntPtr(style));

            var exStyle = currentExStyle;
            exStyle &= ~(
                NativeMethods.WsExDlgModalFrame
                | NativeMethods.WsExClientEdge
                | NativeMethods.WsExStaticEdge
                | NativeMethods.WsExWindowEdge);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(exStyle));
            NotifyFrameChanged(hwnd);
        }
    }

    private void RestoreBorderlessChrome(nint hwnd)
    {
        if (_chromeBackups.TryGetValue(hwnd, out var backup))
        {
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, new IntPtr(backup.Style));
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(backup.ExStyle));
        }
        else
        {
            ForceStandardChrome(hwnd);
        }

        NotifyFrameChanged(hwnd);
    }

    private static void ForceStandardChrome(nint hwnd)
    {
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt32();
        style &= ~NativeMethods.WsPopup;
        style |= StandardChromeStyle;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, new IntPtr(style));

        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt32();
        exStyle |= NativeMethods.WsExWindowEdge;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(exStyle));
    }

    private static bool IsMissingChrome(int style) =>
        (style & NativeMethods.WsCaption) == 0
        && (style & NativeMethods.WsThickFrame) == 0;

    private static void NotifyFrameChanged(nint hwnd)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoZOrder
            | NativeMethods.SwpFrameChanged
            | NativeMethods.SwpNoActivate);

        NativeMethods.RedrawWindow(
            hwnd,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.RdwFrame
            | NativeMethods.RdwInvalidate
            | NativeMethods.RdwAllChildren
            | NativeMethods.RdwUpdateNow);
    }

    private static void ApplyBounds(nint hwnd, ProcessRule rule)
    {
        if (!TryGetMonitorRect(hwnd, out var monitor))
        {
            return;
        }

        var monitorWidth = monitor.Right - monitor.Left;
        var monitorHeight = monitor.Bottom - monitor.Top;

        int targetX;
        int targetY;
        int targetWidth;
        int targetHeight;

        if (rule.UseCustomDimension)
        {
            targetX = rule.CustomX;
            targetY = rule.CustomY;
            targetWidth = rule.CustomWidth > 0 ? rule.CustomWidth : monitorWidth;
            targetHeight = rule.CustomHeight > 0 ? rule.CustomHeight : monitorHeight;
        }
        else
        {
            targetX = monitor.Left;
            targetY = monitor.Top;
            targetWidth = monitorWidth;
            targetHeight = monitorHeight;
        }

        if (rule.IsBorderless)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                targetX,
                targetY,
                targetWidth,
                targetHeight,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
            return;
        }

        var clientRect = new NativeMethods.Rect
        {
            Left = targetX,
            Top = targetY,
            Right = targetX + targetWidth,
            Bottom = targetY + targetHeight
        };

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt32();
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt32();
        if (!NativeMethods.AdjustWindowRectEx(ref clientRect, style, false, exStyle))
        {
            NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                targetX,
                targetY,
                targetWidth,
                targetHeight,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            clientRect.Left,
            clientRect.Top,
            clientRect.Right - clientRect.Left,
            clientRect.Bottom - clientRect.Top,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
    }

    private static bool TryGetMonitorRect(nint hwnd, out NativeMethods.Rect monitorRect)
    {
        var monitor = hwnd == IntPtr.Zero
            ? NativeMethods.MonitorFromPoint(new NativeMethods.Point { X = 0, Y = 0 }, NativeMethods.MonitorDefaultToNearest)
            : NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);

        var info = new NativeMethods.MonitorInfoEx
        {
            Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            monitorRect = default;
            return false;
        }

        monitorRect = info.Monitor;
        return true;
    }

    private static string BuildLayoutSignature(ProcessRule rule) =>
        $"{rule.IsBorderless}|{rule.IsExpandToScreen}|{rule.UseCustomDimension}|{rule.CustomX}|{rule.CustomY}|{rule.CustomWidth}|{rule.CustomHeight}|{rule.IsAlwaysOnTop}";

    private readonly record struct ChromeBackup(int Style, int ExStyle);
}
