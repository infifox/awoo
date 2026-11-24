using System.Runtime.InteropServices;

namespace Awoo.Interop;

/// <summary>P/Invoke surface. Nothing here is awoo-specific.</summary>
internal static partial class NativeMethods
{
    internal const int SM_CXSMICON = 49;
    internal const int SM_CYSMICON = 50;

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE</summary>
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    /// <summary>DWMWCP_ROUND — the Windows 11 large corner radius.</summary>
    internal const int DWMWCP_ROUND = 2;

    /// <summary>PROCESS_QUERY_LIMITED_INFORMATION — enough to read another process's image path.</summary>
    internal const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(IntPtr hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr FindWindow(string? className, string? windowName);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(IntPtr icon);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr OpenProcess(int access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(IntPtr handle);

    /// <summary>
    /// A process's image path. Preferred over reading Process.MainModule,
    /// which needs rights on the process that another user's — or an
    /// elevated — process will not give up.
    /// </summary>
    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool QueryFullProcessImageName(IntPtr process, int flags, char* buffer, ref int size);

    /// <summary>
    /// Resolves an indirect string — an <c>@file,-id</c> reference to a
    /// resource — which is how Windows' own audio sessions name themselves.
    /// </summary>
    [LibraryImport("shlwapi.dll", EntryPoint = "SHLoadIndirectString", StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial int SHLoadIndirectString(string source, char* buffer, int bufferLength, IntPtr reserved);

    /// <summary>
    /// Pulls an icon of a given size out of a file: an executable, a DLL's
    /// resource, or an <c>.ico</c>. The caller owns both handles and must
    /// destroy them.
    /// </summary>
    [LibraryImport("shell32.dll", EntryPoint = "SHDefExtractIconW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SHDefExtractIcon(string file, int index, int flags,
        out IntPtr largeIcon, out IntPtr smallIcon, int sizes);

    /// <summary>GWLP_WNDPROC — a window's procedure, which is what subclassing one replaces.</summary>
    internal const int GWLP_WNDPROC = -4;

    /// <summary>WM_INPUT — raw input is waiting to be read with <see cref="GetRawInputData"/>.</summary>
    internal const uint WM_INPUT = 0x00FF;

    /// <summary>RID_INPUT — ask <see cref="GetRawInputData"/> for the input rather than its header.</summary>
    internal const uint RID_INPUT = 0x10000003;

    /// <summary>RIM_TYPEMOUSE — the raw input's union holds a <see cref="RAWMOUSE"/>.</summary>
    internal const uint RIM_TYPEMOUSE = 0;

    /// <summary>RIDEV_INPUTSINK — deliver raw input whoever has the foreground.</summary>
    internal const uint RIDEV_INPUTSINK = 0x00000100;

    /// <summary>RIDEV_REMOVE — stop delivering it.</summary>
    internal const uint RIDEV_REMOVE = 0x00000001;

    /// <summary>RI_MOUSE_WHEEL — this raw mouse event carries a wheel movement.</summary>
    internal const ushort RI_MOUSE_WHEEL = 0x0400;

    /// <summary>
    /// WHEEL_DELTA — one notch of a wheel. A high-resolution wheel sends
    /// fractions of it rather than whole notches.
    /// </summary>
    internal const int WHEEL_DELTA = 120;

    /// <summary>The HID generic desktop page, and the mouse on it.</summary>
    internal const ushort HID_USAGE_PAGE_GENERIC = 0x01;

    internal const ushort HID_USAGE_GENERIC_MOUSE = 0x02;

    /// <summary>A window procedure, as <see cref="SetWindowLongPtr"/> installs one.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    /// <summary>RAWINPUTDEVICE — one class of device to receive raw input from.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    /// <summary>RAWINPUTHEADER — what kind of device a raw input came from.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    /// <summary>
    /// RAWMOUSE. <c>Padding</c> is the gap C puts between <c>usFlags</c> and
    /// the union after it to align the union, and that C# would otherwise
    /// close up.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWMOUSE
    {
        public ushort Flags;
        public ushort Padding;
        public ushort ButtonFlags;

        /// <summary>Signed: a wheel turned towards the user is negative.</summary>
        public short ButtonData;

        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    /// <summary>A RAWINPUT whose union is known to hold a mouse.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTMOUSE
    {
        public RAWINPUTHEADER Header;
        public RAWMOUSE Mouse;
    }

    /// <summary>
    /// NOTIFYICONIDENTIFIER — which notification area icon is being asked
    /// about. The shell goes by <see cref="Item"/> where the icon was
    /// registered with a GUID and by <see cref="Window"/> and
    /// <see cref="Id"/> where it was not.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NOTIFYICONIDENTIFIER
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public Guid Item;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    internal static partial IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterRawInputDevices(ref RAWINPUTDEVICE devices, uint count, uint size);

    /// <summary>Returns the bytes copied, or <c>-1</c> as an unsigned value on failure.</summary>
    [LibraryImport("user32.dll")]
    internal static unsafe partial uint GetRawInputData(IntPtr input, uint command, void* data, ref uint size, uint headerSize);

    /// <summary>
    /// Where the shell is drawing one notification area icon. S_FALSE rather
    /// than S_OK means the icon is hidden in the overflow and the rectangle
    /// is the chevron's.
    /// </summary>
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    internal static partial int ShellNotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT location);
}
