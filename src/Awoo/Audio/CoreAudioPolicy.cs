using System.Runtime.InteropServices;

namespace Awoo.Audio;

// The audio-policy half of Windows' sound stack: which device one
// application's sound goes to, as against CoreAudio.cs's endpoints and
// CoreAudioSessions.cs's streams. Interop only; AppRouting is what gives
// these meaning.

/// <summary>
/// The Windows Runtime entry points the policy interface is reached through.
/// Separate from <see cref="Awoo.Interop.NativeMethods"/> because .NET has no
/// built-in WinRT interop any more: an HSTRING here is a handle awoo creates,
/// reads and frees by hand rather than something the marshaller knows about.
/// </summary>
internal static class WindowsRuntime
{
    /// <summary>
    /// The activation factory for a Windows Runtime class, as the given
    /// interface. <paramref name="activatableClassId"/> is an HSTRING.
    /// </summary>
    [DllImport("combase.dll")]
    internal static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        ref Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object? factory);

    /// <summary>Makes an HSTRING the caller owns and must delete.</summary>
    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    internal static extern int WindowsCreateString(string source, uint length, out IntPtr value);

    /// <summary>Frees an HSTRING. A null handle is the empty string, and deleting it is a no-op.</summary>
    [DllImport("combase.dll")]
    internal static extern int WindowsDeleteString(IntPtr value);

    /// <summary>
    /// An HSTRING's characters, which belong to the string rather than to the
    /// caller — copy them out before deleting it.
    /// </summary>
    [DllImport("combase.dll")]
    internal static extern IntPtr WindowsGetStringRawBuffer(IntPtr value, out int length);

    /// <summary>
    /// Copies an HSTRING out as a managed string, or null for the empty
    /// string — which is what a null handle means and what Windows answers
    /// with when an application has no route of its own.
    /// </summary>
    internal static string? ReadString(IntPtr value)
    {
        if (value == IntPtr.Zero)
        {
            return null;
        }

        var buffer = WindowsGetStringRawBuffer(value, out var length);
        return buffer == IntPtr.Zero || length <= 0 ? null : Marshal.PtrToStringUni(buffer, length);
    }
}

/// <summary>
/// IAudioPolicyConfig — the activation factory behind Windows' own
/// Settings → System → Sound → "App volume and device preferences", which is
/// the only way to send one application's audio to a device that is not the
/// default. Undocumented, and the route EarTrumpet takes as well.
/// </summary>
/// <remarks>
/// <para>
/// A Windows Runtime interface rather than a plain COM one, so it derives
/// from IInspectable rather than from IUnknown, and its own methods begin
/// after IInspectable's three. Those three are declared here by hand: .NET's
/// built-in WinRT interop is gone, and this is an <see cref="InterfaceTypeAttribute"/>
/// of <see cref="ComInterfaceType.InterfaceIsIUnknown"/> as far as the
/// runtime is concerned, so nothing else would account for them.
/// </para>
/// <para>
/// Only the last three methods are ever called. The nineteen before them are
/// declared because a COM interface is its vtable order: drop one and every
/// slot after it points at the wrong function. They take no arguments and
/// return nothing useful, since nothing here invokes them — only their
/// position matters.
/// </para>
/// <para>
/// This GUID is the shape Windows 11 21H2 and later expose. Windows 10 1803
/// to 21H1 have the same layout under a different IID
/// (<c>2a59116d-6c4f-45e0-a74f-707e3fef9258</c>), which awoo does not declare:
/// its minimum platform is 10.0.22000, which is 21H2.
/// </para>
/// </remarks>
[ComImport]
[Guid("ab3d4648-e242-459f-b02f-541c70306324")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioPolicyConfig
{
    [PreserveSig]
    int GetIids(out int count, out IntPtr iids);

    [PreserveSig]
    int GetRuntimeClassName(out IntPtr name);

    [PreserveSig]
    int GetTrustLevel(out int level);

    [PreserveSig]
    int Unused_AddCtxVolumeChange();

    [PreserveSig]
    int Unused_RemoveCtxVolumeChanged();

    [PreserveSig]
    int Unused_AddRingerVibrateStateChanged();

    [PreserveSig]
    int Unused_RemoveRingerVibrateStateChange();

    [PreserveSig]
    int Unused_SetVolumeGroupGainForId();

    [PreserveSig]
    int Unused_GetVolumeGroupGainForId();

    [PreserveSig]
    int Unused_GetActiveVolumeGroupForEndpointId();

    [PreserveSig]
    int Unused_GetVolumeGroupsForEndpoint();

    [PreserveSig]
    int Unused_GetCurrentVolumeContext();

    [PreserveSig]
    int Unused_SetVolumeGroupMuteForId();

    [PreserveSig]
    int Unused_GetVolumeGroupMuteForId();

    [PreserveSig]
    int Unused_SetRingerVibrateState();

    [PreserveSig]
    int Unused_GetRingerVibrateState();

    [PreserveSig]
    int Unused_SetPreferredChatApplication();

    [PreserveSig]
    int Unused_ResetPreferredChatApplication();

    [PreserveSig]
    int Unused_GetPreferredChatApplication();

    [PreserveSig]
    int Unused_GetCurrentChatApplications();

    [PreserveSig]
    int Unused_AddChatContextChanged();

    [PreserveSig]
    int Unused_RemoveChatContextChanged();

    /// <summary>
    /// Sends one process's audio to one endpoint, for one role, and persists
    /// the choice against the application rather than against the process —
    /// so it survives the application being restarted. A null
    /// <paramref name="deviceId"/> clears the route, which puts the
    /// application back on whatever the default device is.
    /// </summary>
    /// <param name="deviceId">
    /// An HSTRING holding a device interface path rather than a Core Audio
    /// endpoint id; <see cref="AppRouting"/> is what converts between them.
    /// </param>
    [PreserveSig]
    int SetPersistedDefaultAudioEndpoint(uint processId, DataFlow flow, Role role, IntPtr deviceId);

    /// <summary>
    /// The endpoint one process's audio has been sent to, as an HSTRING the
    /// caller must delete. Empty where the application has no route of its
    /// own and follows the default device.
    /// </summary>
    [PreserveSig]
    int GetPersistedDefaultAudioEndpoint(uint processId, DataFlow flow, Role role, out IntPtr deviceId);

    [PreserveSig]
    int ClearAllPersistedApplicationDefaultEndpoints();
}
