using System.Runtime.InteropServices;
using Awoo.Interop;

namespace Awoo.Tray;

/// <summary>
/// The scroll wheel over the tray icon: one notch of the wheel, one step of
/// the volume, the way the Plasma applet works.
/// </summary>
/// <remarks>
/// <para>
/// Windows does not forward the wheel to a notification area icon. The shell
/// tells an icon's window about clicks and about the pointer moving over it,
/// but there is no wheel among those notifications — so the only way to see
/// one is to ask for raw mouse input, which arrives whoever has the
/// foreground, and then work out for ourselves whether the pointer was on our
/// icon at the time. That is the route EarTrumpet takes for the same gesture.
/// </para>
/// <para>
/// Raw mouse input is every mouse movement on the machine, so it is asked for
/// only while the pointer is actually on the icon: the shell's own move
/// notification over the icon arms it, and the first raw event that finds the
/// pointer outside the icon's rectangle takes it away again. The shell
/// reports the pointer arriving on the icon but never leaving, which is why
/// the leaving has to be noticed this way.
/// </para>
/// </remarks>
internal sealed class TrayWheel : IDisposable
{
    private readonly IntPtr _window;
    private readonly Guid _iconId;
    private readonly Action<int> _onNotches;

    /// <summary>Held in a field because Windows is holding a pointer to it.</summary>
    private readonly NativeMethods.WindowProc _proc;

    private IntPtr _previousProc;
    private NativeMethods.RECT _iconRect;
    private bool _listening;
    private int _accumulated;
    private bool _disposed;

    /// <param name="window">
    /// The window the tray icon is registered against — H.NotifyIcon's
    /// message window. Subclassed, because raw input arrives as a window
    /// message and that library's own procedure has no case for it.
    /// </param>
    /// <param name="iconId">
    /// The GUID the icon was registered under, where it was registered under
    /// one.
    /// </param>
    /// <param name="onNotches">
    /// Called with how many notches the wheel moved: positive away from the
    /// user, which is louder.
    /// </param>
    internal TrayWheel(IntPtr window, Guid iconId, Action<int> onNotches)
    {
        _window = window;
        _iconId = iconId;
        _onNotches = onNotches;

        _proc = OnMessage;
        _previousProc = NativeMethods.SetWindowLongPtr(
            _window,
            NativeMethods.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_proc));
    }

    /// <summary>
    /// The pointer is on the icon, so from here the wheel is ours until it
    /// leaves. Called for every move the shell reports over the icon, which is
    /// also what keeps the icon's rectangle current as the taskbar reflows.
    /// </summary>
    /// <param name="x">Where the shell says the pointer was, in screen pixels.</param>
    /// <param name="y">The same.</param>
    internal void PointerOverIcon(int x, int y)
    {
        if (_disposed || _previousProc == IntPtr.Zero)
        {
            return;
        }

        _iconRect = TryGetIconRect(out var rect) ? rect : Around(x, y);
        StartListening();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopListening();

        if (_previousProc != IntPtr.Zero)
        {
            _ = NativeMethods.SetWindowLongPtr(_window, NativeMethods.GWLP_WNDPROC, _previousProc);
            _previousProc = IntPtr.Zero;
        }
    }

    private IntPtr OnMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WM_INPUT)
        {
            OnRawInput(lParam);
        }

        // Passed on either way: the library's procedure is what makes the
        // icon work, and raw input still has to reach DefWindowProc to be
        // cleaned up.
        return NativeMethods.CallWindowProc(_previousProc, hwnd, message, wParam, lParam);
    }

    private unsafe void OnRawInput(IntPtr input)
    {
        if (!_listening)
        {
            return;
        }

        NativeMethods.RAWINPUTMOUSE raw;
        var size = (uint)sizeof(NativeMethods.RAWINPUTMOUSE);
        var copied = NativeMethods.GetRawInputData(
            input,
            NativeMethods.RID_INPUT,
            &raw,
            ref size,
            (uint)sizeof(NativeMethods.RAWINPUTHEADER));

        if (copied == unchecked((uint)-1) || raw.Header.Type != NativeMethods.RIM_TYPEMOUSE)
        {
            return;
        }

        // Any raw event at all is the chance to notice the pointer has gone.
        if (!IsPointerOnIcon())
        {
            StopListening();
            return;
        }

        if ((raw.Mouse.ButtonFlags & NativeMethods.RI_MOUSE_WHEEL) != 0)
        {
            Accumulate(raw.Mouse.ButtonData);
        }
    }

    /// <summary>
    /// Turns wheel movement into whole notches. A high-resolution wheel sends
    /// less than a notch at a time, so what is left over is kept for the next
    /// event rather than rounded away — and a change of direction starts
    /// again, so a flick back does not first have to undo what was banked.
    /// </summary>
    private void Accumulate(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        if (Math.Sign(delta) != Math.Sign(_accumulated))
        {
            _accumulated = 0;
        }

        _accumulated += delta;

        var notches = _accumulated / NativeMethods.WHEEL_DELTA;
        if (notches == 0)
        {
            return;
        }

        _accumulated -= notches * NativeMethods.WHEEL_DELTA;
        _onNotches(notches);
    }

    private bool IsPointerOnIcon() =>
        NativeMethods.GetCursorPos(out var point) &&
        point.X >= _iconRect.Left && point.X < _iconRect.Right &&
        point.Y >= _iconRect.Top && point.Y < _iconRect.Bottom;

    /// <summary>
    /// One small icon's worth of room around a point the shell has just said
    /// the pointer was over our icon at.
    /// </summary>
    /// <remarks>
    /// This is what stands in for the icon's rectangle where the shell will
    /// not give one up — which on Windows 11 is every icon tucked away under
    /// "show hidden icons", and that is where a newly registered icon starts.
    /// Scrolling it there should work rather than silently do nothing.
    /// <para>
    /// It is anchored on a point the shell itself vouched for and moved on by
    /// every further move it reports, so the pointer is never more than a
    /// stale notification away from where this says it is — and the tolerance
    /// is half an icon, which is less than the gap to the next one.
    /// </para>
    /// </remarks>
    private static NativeMethods.RECT Around(int x, int y)
    {
        var width = Math.Max(NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON), 2) / 2;
        var height = Math.Max(NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSMICON), 2) / 2;

        return new NativeMethods.RECT
        {
            Left = x - width,
            Top = y - height,
            Right = x + width,
            Bottom = y + height,
        };
    }

    /// <summary>
    /// Where the shell is drawing our icon. Both halves of the identifier are
    /// filled in: the shell goes by the GUID where the icon was registered
    /// with one and by the window and id where it was not, and which of those
    /// H.NotifyIcon used is its business rather than ours.
    /// </summary>
    private bool TryGetIconRect(out NativeMethods.RECT rect)
    {
        var identifier = new NativeMethods.NOTIFYICONIDENTIFIER
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONIDENTIFIER>(),
            Window = _window,
            Id = 0,
            Item = _iconId,
        };

        // Only S_OK: Windows 11 refuses outright for an icon in the overflow,
        // which <see cref="Around"/> is the answer to.
        return NativeMethods.ShellNotifyIconGetRect(ref identifier, out rect) == 0;
    }

    private void StartListening()
    {
        if (_listening)
        {
            return;
        }

        _listening = Register(true);
        _accumulated = 0;
    }

    private void StopListening()
    {
        if (!_listening)
        {
            return;
        }

        _ = Register(false);
        _listening = false;
        _accumulated = 0;
    }

    private bool Register(bool wanted)
    {
        var device = new NativeMethods.RAWINPUTDEVICE
        {
            UsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
            Usage = NativeMethods.HID_USAGE_GENERIC_MOUSE,
            Flags = wanted ? NativeMethods.RIDEV_INPUTSINK : NativeMethods.RIDEV_REMOVE,

            // Giving it up takes no window, and is refused with one.
            Target = wanted ? _window : IntPtr.Zero,
        };

        return NativeMethods.RegisterRawInputDevices(
            ref device,
            1,
            (uint)Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>());
    }
}
