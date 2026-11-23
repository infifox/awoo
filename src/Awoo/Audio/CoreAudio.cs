using System.Runtime.InteropServices;

namespace Awoo.Audio;

/// <summary>
/// The slice of Windows Core Audio awoo talks to. Interop only — no policy
/// and no state; <see cref="AudioService"/> is what gives these meaning.
/// </summary>
/// <remarks>
/// Hand-written rather than taken from a library: later tasks need the
/// undocumented audio-policy interfaces alongside these, which no package
/// offers, so one interop surface for all of it beats two.
/// </remarks>
internal static class CoreAudio
{
    internal const int S_OK = 0;

    /// <summary>E_NOTFOUND — what GetDefaultAudioEndpoint says when there is no output device at all.</summary>
    internal const int E_NOTFOUND = unchecked((int)0x80070490);

    /// <summary>DEVICE_STATE_ACTIVE</summary>
    internal const int DeviceStateActive = 0x1;

    /// <summary>STGM_READ</summary>
    internal const int StgmRead = 0x0;

    /// <summary>VT_LPWSTR</summary>
    internal const ushort VtLpwstr = 31;

    /// <summary>CLSCTX_ALL</summary>
    internal const int ClsCtxAll = 0x17;

    /// <summary>PKEY_Device_FriendlyName — the name Windows shows for an endpoint.</summary>
    internal static readonly PropertyKey DeviceFriendlyName =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    private static readonly Guid ClsidMMDeviceEnumerator =
        new("bcde0395-e52f-467c-8e3d-c4579291692e");

    /// <summary>Creates the device enumerator. Call it on the thread that will use it.</summary>
    internal static IMMDeviceEnumerator CreateEnumerator()
    {
        var type = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator)
            ?? throw new InvalidOperationException("MMDeviceEnumerator is not registered on this machine.");

        return (IMMDeviceEnumerator)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("MMDeviceEnumerator could not be created."));
    }

    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref PropVariant value);
}

internal enum DataFlow
{
    Render = 0,
    Capture = 1,
    All = 2,
}

internal enum Role
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly struct PropertyKey(Guid formatId, int propertyId)
{
    public readonly Guid FormatId = formatId;
    public readonly int PropertyId = propertyId;

    public bool Matches(PropertyKey other) => FormatId == other.FormatId && PropertyId == other.PropertyId;
}

/// <summary>
/// Enough of PROPVARIANT to read a string out of a property store. The union
/// is wider than this, but every arm of it starts at the same offset, so a
/// pointer-sized pair covers the one case we read.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort Type;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public IntPtr Value;
    public IntPtr Value2;
}

/// <summary>
/// What <see cref="IAudioEndpointVolumeCallback"/> is handed when something
/// changes an endpoint's volume. The real struct ends in a variable-length
/// array of per-channel volumes; the master volume and mute sit before it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AudioVolumeNotificationData
{
    public Guid EventContext;
    public int Muted;
    public float MasterVolume;
    public int ChannelCount;
}

[ComImport]
[Guid("a95664d2-9614-4f35-a746-de8db63617e6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(DataFlow flow, int stateMask, out IMMDeviceCollection? devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(DataFlow flow, Role role, out IMMDevice? device);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice? device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IMMNotificationClient client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport]
[Guid("0bd7a1be-7a1a-44db-8397-cc5392387b5e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out int count);

    [PreserveSig]
    int Item(int index, out IMMDevice? device);
}

[ComImport]
[Guid("d666063f-1587-4e43-81f1-b948e807363f")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int classContext, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object? instance);

    [PreserveSig]
    int OpenPropertyStore(int access, out IPropertyStore? store);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string? id);

    [PreserveSig]
    int GetState(out int state);
}

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out int count);

    [PreserveSig]
    int GetAt(int index, out PropertyKey key);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant value);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant value);

    [PreserveSig]
    int Commit();
}

[ComImport]
[Guid("5cdf2c82-841e-4546-9722-0cf74078229a")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);

    [PreserveSig]
    int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);

    [PreserveSig]
    int GetChannelCount(out int count);

    [PreserveSig]
    int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

    [PreserveSig]
    int GetMasterVolumeLevel(out float levelDb);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig]
    int SetChannelVolumeLevel(int channel, float levelDb, ref Guid eventContext);

    [PreserveSig]
    int SetChannelVolumeLevelScalar(int channel, float level, ref Guid eventContext);

    [PreserveSig]
    int GetChannelVolumeLevel(int channel, out float levelDb);

    [PreserveSig]
    int GetChannelVolumeLevelScalar(int channel, out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);

    [PreserveSig]
    int GetVolumeStepInfo(out int step, out int stepCount);

    [PreserveSig]
    int VolumeStepUp(ref Guid eventContext);

    [PreserveSig]
    int VolumeStepDown(ref Guid eventContext);

    [PreserveSig]
    int QueryHardwareSupport(out int mask);

    [PreserveSig]
    int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
}

/// <summary>
/// Implemented by awoo and handed to an endpoint, so a volume change made
/// anywhere on the machine arrives here instead of being polled for.
/// </summary>
[ComImport]
[Guid("657804fa-d6ad-4496-8a60-352752af4f89")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig]
    int OnNotify(IntPtr notificationData);
}

/// <summary>
/// Implemented by awoo and handed to the enumerator: how the default device
/// changing, or a device coming and going, reaches us.
/// </summary>
[ComImport]
[Guid("7991eec9-7e89-4d85-8390-6c703cec60c0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    [PreserveSig]
    int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);

    [PreserveSig]
    int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [PreserveSig]
    int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [PreserveSig]
    int OnDefaultDeviceChanged(DataFlow flow, Role role, [MarshalAs(UnmanagedType.LPWStr)] string? deviceId);

    [PreserveSig]
    int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}
