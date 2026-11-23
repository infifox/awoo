using Awoo.Interop;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Awoo.Flyout;

/// <summary>
/// Works out where a tray flyout of a given size belongs: tucked against the
/// taskbar edge, anchored to the notification area, on whichever monitor the
/// pointer is on.
/// </summary>
internal static class FlyoutPlacement
{
    /// <summary>Gap between the flyout and both the taskbar and the screen edge, in logical pixels.</summary>
    private const int GapDips = 12;

    /// <summary>
    /// Top-left corner, in physical screen pixels, for a flyout of
    /// <paramref name="size"/> physical pixels.
    /// </summary>
    internal static PointInt32 For(SizeInt32 size, double scale)
    {
        var gap = (int)Math.Round(GapDips * scale);
        var cursor = CursorPosition();
        var display = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Nearest);
        var work = display.WorkArea;
        var outer = display.OuterBounds;

        var edge = TaskbarEdge(work, outer);
        var notify = NotificationAreaRect();

        int x, y;
        switch (edge)
        {
            case Edge.Top:
                y = work.Y + gap;
                x = HorizontalAnchor(size, work, notify, gap);
                break;
            case Edge.Left:
                x = work.X + gap;
                y = VerticalAnchor(size, work, notify, gap);
                break;
            case Edge.Right:
                x = work.X + work.Width - size.Width - gap;
                y = VerticalAnchor(size, work, notify, gap);
                break;
            default: // Edge.Bottom — the only one Windows 11 offers.
                y = work.Y + work.Height - size.Height - gap;
                x = HorizontalAnchor(size, work, notify, gap);
                break;
        }

        return new PointInt32(
            Clamp(x, work.X + gap, work.X + work.Width - size.Width - gap),
            Clamp(y, work.Y + gap, work.Y + work.Height - size.Height - gap));
    }

    /// <summary>
    /// Right edge of the flyout lines up with the right edge of the
    /// notification area, the way Windows' own quick settings does; without a
    /// notification area to go by, the corner of the work area.
    /// </summary>
    private static int HorizontalAnchor(SizeInt32 size, RectInt32 work, NativeMethods.RECT? notify, int gap)
    {
        if (notify is { } rect)
        {
            return rect.Right - size.Width;
        }

        return work.X + work.Width - size.Width - gap;
    }

    private static int VerticalAnchor(SizeInt32 size, RectInt32 work, NativeMethods.RECT? notify, int gap)
    {
        if (notify is { } rect)
        {
            return rect.Bottom - size.Height;
        }

        return work.Y + work.Height - size.Height - gap;
    }

    private enum Edge
    {
        Bottom,
        Top,
        Left,
        Right,
    }

    /// <summary>
    /// Which side the taskbar is on, read off the inset between the monitor's
    /// full bounds and its work area.
    /// </summary>
    private static Edge TaskbarEdge(RectInt32 work, RectInt32 outer)
    {
        var top = work.Y - outer.Y;
        var bottom = (outer.Y + outer.Height) - (work.Y + work.Height);
        var left = work.X - outer.X;
        var right = (outer.X + outer.Width) - (work.X + work.Width);

        var widest = Math.Max(Math.Max(top, bottom), Math.Max(left, right));
        if (widest <= 0)
        {
            return Edge.Bottom;
        }

        if (widest == bottom)
        {
            return Edge.Bottom;
        }

        if (widest == top)
        {
            return Edge.Top;
        }

        return widest == left ? Edge.Left : Edge.Right;
    }

    /// <summary>
    /// The notification area ("TrayNotifyWnd" inside the taskbar), or null
    /// when the shell does not give it up — a different shell, or a future
    /// taskbar that renames it.
    /// </summary>
    private static NativeMethods.RECT? NotificationAreaRect()
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero)
        {
            return null;
        }

        var notify = NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify == IntPtr.Zero)
        {
            return null;
        }

        return NativeMethods.GetWindowRect(notify, out var rect) ? rect : null;
    }

    private static PointInt32 CursorPosition()
    {
        return NativeMethods.GetCursorPos(out var point)
            ? new PointInt32(point.X, point.Y)
            : new PointInt32(0, 0);
    }

    private static int Clamp(int value, int min, int max)
    {
        // A flyout taller or wider than the work area would invert the bounds.
        return max < min ? min : Math.Clamp(value, min, max);
    }
}
