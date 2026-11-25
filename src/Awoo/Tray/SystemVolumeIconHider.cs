using System.Diagnostics;
using System.Runtime.InteropServices;
using Awoo.Interop;
using Microsoft.Win32;

namespace Awoo.Tray;

/// <summary>
/// Hides the stock Windows 11 volume icon from the taskbar while awoo is
/// running, and restores it.
/// </summary>
/// <remarks>
/// Windows 11 build 26300 offers no setting, policy or registry value that
/// hides the system tray's volume icon, so this reaches it the only way left:
/// the documented XAML diagnostics entry point
/// (<c>InitializeXamlDiagnosticsEx</c>, the same one Visual Studio's Live
/// Visual Tree rides on) loads awoo's native TAP DLL into Explorer, and the
/// DLL walks the taskbar's visual tree and collapses the icon. This class is
/// the awoo-side half: it injects the DLL and then drives it through a
/// message-only window the DLL puts up inside Explorer — see
/// <c>src/AwooTap/AwooTapContract.h</c>, whose constants are mirrored here.
///
/// Every step can fail — the API can refuse, the DLL can be missing, a Windows
/// update can move the icon out of reach — and every failure is swallowed into
/// an <see cref="HideOutcome"/>. awoo carries on fully working whatever
/// happens here; nothing in this class throws to its caller.
/// </remarks>
internal sealed class SystemVolumeIconHider
{
    /// <summary>What an attempt to hide came to.</summary>
    internal enum HideOutcome
    {
        /// <summary>Not tried yet.</summary>
        NotAttempted,

        /// <summary>The icon was found and is now hidden.</summary>
        Hidden,

        /// <summary>
        /// The engine ran but found no volume icon to hide — the shape awoo
        /// expects has moved, most likely after a Windows update. awoo behaves
        /// as if the setting were off, and the settings window says so in a
        /// note under the switch.
        /// </summary>
        NotFound,

        /// <summary>
        /// The mechanism itself did not come up: the DLL was missing, the API
        /// refused, or the injected DLL never reported in.
        /// </summary>
        Failed,
    }

    // Mirrors AwooTapContract.h. Kept in lockstep with it by hand.
    private const int ProtocolVersion = 1;
    private const string WindowClass = "Awoo.TrayTap.Control.3117367e";
    private const string Endpoint = "VisualDiagConnection1";
    private const string TapDllName = "AwooTap.dll";

    // {E6038BAE-6773-4567-BACF-9D407E974033}
    private static readonly Guid TapClsid = new("E6038BAE-6773-4567-BACF-9D407E974033");

    private const uint WM_APP = 0x8000;
    private const uint WM_AWOO_TAP_PROTOCOL = WM_APP + 1;
    private const uint WM_AWOO_TAP_HIDE = WM_APP + 2;
    private const uint WM_AWOO_TAP_RESTORE = WM_APP + 3;
    private const uint WM_AWOO_TAP_QUERY = WM_APP + 4;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InitializeXamlDiagnosticsExFn(
        [MarshalAs(UnmanagedType.LPWStr)] string endpointName,
        uint pid,
        [MarshalAs(UnmanagedType.LPWStr)] string? dllXamlDiagnostics,
        [MarshalAs(UnmanagedType.LPWStr)] string tapDllName,
        Guid tapClsid,
        [MarshalAs(UnmanagedType.LPWStr)] string? initializationData);

    private readonly bool _pretendNothingFound;

    private IntPtr _window;

    /// <param name="pretendNothingFound">
    /// Answers every <see cref="Hide"/> with <see cref="HideOutcome.NotFound"/>
    /// without touching Explorer. The dev-only
    /// <c>--volume-icon-hide-not-found</c> switch passes this: the settings
    /// window's "hiding isn't working" note is for the day a Windows update
    /// moves the icon out of reach, and that day cannot be arranged on demand.
    /// </param>
    internal SystemVolumeIconHider(bool pretendNothingFound = false) =>
        _pretendNothingFound = pretendNothingFound;

    /// <summary>The outcome of the last <see cref="Hide"/>.</summary>
    internal HideOutcome LastOutcome { get; private set; } = HideOutcome.NotAttempted;

    /// <summary>
    /// Injects the TAP into Explorer if it is not already there and asks it to
    /// hide the volume icon. Safe to call more than once: a second call just
    /// re-sends the hide to the window already standing.
    /// </summary>
    internal HideOutcome Hide()
    {
        try
        {
            LastOutcome = HideCore();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[awoo] volume-icon hide threw: {ex}");
            LastOutcome = HideOutcome.Failed;
        }

        Debug.WriteLine($"[awoo] volume-icon hide: {LastOutcome}");
        return LastOutcome;
    }

    /// <summary>
    /// Restores the stock icon the TAP hid. A no-op if nothing was hidden, and
    /// safe to call on any shutdown path.
    /// </summary>
    internal void Restore()
    {
        try
        {
            var window = ResolveWindow();
            if (window != IntPtr.Zero)
            {
                NativeMethods.SendMessage(window, WM_AWOO_TAP_RESTORE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[awoo] volume-icon restore threw: {ex}");
        }
    }

    private HideOutcome HideCore()
    {
        if (_pretendNothingFound)
        {
            return HideOutcome.NotFound;
        }

        // If the TAP is already standing — a second hide, or awoo never tore a
        // previous one down — just drive it.
        var existing = ResolveWindow();
        if (existing != IntPtr.Zero)
        {
            return DriveHide(existing);
        }

        var explorerPid = FindTrayProcessId();
        if (explorerPid == 0)
        {
            return HideOutcome.Failed;
        }

        var dllPath = Path.Combine(AppContext.BaseDirectory, TapDllName);
        if (!File.Exists(dllPath))
        {
            Debug.WriteLine($"[awoo] TAP DLL not found at {dllPath}");
            return HideOutcome.Failed;
        }

        var initialize = ResolveInitializeXamlDiagnostics();
        if (initialize is null)
        {
            return HideOutcome.Failed;
        }

        // The injector creates our object by CLSID inside Explorer, so the
        // mapping from CLSID to this DLL has to be resolvable there. HKCU is
        // enough — Explorer runs as the same user — and needs no admin. The
        // key comes out again as soon as the object exists.
        RegisterClsid(dllPath);
        try
        {
            int hr = initialize(Endpoint, explorerPid, null, dllPath, TapClsid, null);
            if (hr < 0)
            {
                Debug.WriteLine($"[awoo] InitializeXamlDiagnosticsEx failed: 0x{hr:X8}");
                return HideOutcome.Failed;
            }

            var window = WaitForWindow(TimeSpan.FromSeconds(5));
            if (window == IntPtr.Zero)
            {
                Debug.WriteLine("[awoo] TAP never put up its window");
                return HideOutcome.Failed;
            }

            return DriveHide(window);
        }
        finally
        {
            UnregisterClsid();
        }
    }

    private HideOutcome DriveHide(IntPtr window)
    {
        var protocol = NativeMethods.SendMessage(window, WM_AWOO_TAP_PROTOCOL, IntPtr.Zero, IntPtr.Zero);
        if (protocol.ToInt64() != ProtocolVersion)
        {
            Debug.WriteLine($"[awoo] TAP protocol mismatch: got {protocol.ToInt64()}, want {ProtocolVersion}");
            return HideOutcome.Failed;
        }

        _window = window;
        var hidden = NativeMethods.SendMessage(window, WM_AWOO_TAP_HIDE, IntPtr.Zero, IntPtr.Zero);
        if (hidden.ToInt64() == 1)
        {
            return HideOutcome.Hidden;
        }

        // The tree is replayed to the DLL synchronously, so a miss here is
        // almost always a real not-found. But should a Windows build deliver
        // the replay late, the icon arrives after HIDE and the DLL collapses
        // it then; give that a brief moment to settle before calling it.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
            if (NativeMethods.SendMessage(window, WM_AWOO_TAP_QUERY, IntPtr.Zero, IntPtr.Zero).ToInt64() == 1)
            {
                return HideOutcome.Hidden;
            }
        }

        return HideOutcome.NotFound;
    }

    /// <summary>
    /// The window we last saw, if it is still a window, or a fresh lookup —
    /// Explorer restarting takes the old one with it.
    /// </summary>
    private IntPtr ResolveWindow()
    {
        if (_window != IntPtr.Zero && NativeMethods.IsWindow(_window))
        {
            return _window;
        }

        _window = NativeMethods.FindWindow(WindowClass, null);
        return _window;
    }

    private IntPtr WaitForWindow(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var window = NativeMethods.FindWindow(WindowClass, null);
            if (window != IntPtr.Zero)
            {
                return window;
            }
            Thread.Sleep(50);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// The process that owns the taskbar. The tray's visual tree lives in
    /// whatever process hosts <c>Shell_TrayWnd</c>, which is the Explorer we
    /// inject into.
    /// </summary>
    private static uint FindTrayProcessId()
    {
        var tray = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero)
        {
            return 0;
        }
        NativeMethods.GetWindowThreadProcessId(tray, out var pid);
        return pid;
    }

    private static InitializeXamlDiagnosticsExFn? ResolveInitializeXamlDiagnostics()
    {
        // InitializeXamlDiagnosticsEx is exported by the system (UWP) XAML
        // framework, which the WinUI-3 app does not otherwise load, so it is
        // taken by full path rather than left to the loader's search.
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var xaml = Path.Combine(system, "Windows.UI.Xaml.dll");
        try
        {
            if (!NativeLibrary.TryLoad(xaml, out var module))
            {
                Debug.WriteLine($"[awoo] could not load {xaml}");
                return null;
            }
            if (!NativeLibrary.TryGetExport(module, "InitializeXamlDiagnosticsEx", out var export))
            {
                Debug.WriteLine("[awoo] no InitializeXamlDiagnosticsEx export");
                return null;
            }
            return Marshal.GetDelegateForFunctionPointer<InitializeXamlDiagnosticsExFn>(export);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[awoo] resolving InitializeXamlDiagnosticsEx threw: {ex}");
            return null;
        }
    }

    private static string ClsidKeyPath => $@"Software\Classes\CLSID\{TapClsid.ToString("B").ToUpperInvariant()}\InprocServer32";

    private static void RegisterClsid(string dllPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ClsidKeyPath);
        key.SetValue(null, dllPath);
        key.SetValue("ThreadingModel", "Both");
    }

    private static void UnregisterClsid()
    {
        try
        {
            // Delete the InprocServer32 key and the empty CLSID key above it.
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\CLSID\{TapClsid.ToString("B").ToUpperInvariant()}", throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[awoo] unregistering TAP CLSID threw: {ex}");
        }
    }
}
