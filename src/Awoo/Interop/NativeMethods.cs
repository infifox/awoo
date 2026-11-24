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
}
