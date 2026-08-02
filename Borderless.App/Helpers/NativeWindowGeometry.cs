using Borderless.App.Native;

namespace Borderless.App.Helpers;

/// <summary>
/// Shared Win32 window geometry helpers.
/// </summary>
internal static class NativeWindowGeometry
{
    public static bool TryGetClientScreenRect(nint hwnd, out NativeMethods.Rect screenRect)
    {
        if (!NativeMethods.GetClientRect(hwnd, out var client))
        {
            screenRect = default;
            return false;
        }

        var topLeft = new NativeMethods.Point { X = client.Left, Y = client.Top };
        var bottomRight = new NativeMethods.Point { X = client.Right, Y = client.Bottom };
        if (!NativeMethods.ClientToScreen(hwnd, ref topLeft)
            || !NativeMethods.ClientToScreen(hwnd, ref bottomRight))
        {
            screenRect = default;
            return false;
        }

        screenRect = new NativeMethods.Rect
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Right = bottomRight.X,
            Bottom = bottomRight.Y
        };
        return true;
    }
}
