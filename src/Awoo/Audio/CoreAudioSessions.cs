using System.Runtime.InteropServices;

namespace Awoo.Audio;

// The audio-session half of Core Audio: what each application is playing or
// recording, as against CoreAudio.cs's whole endpoints. Interop only;
// AudioSessionIndex is what gives these meaning.

/// <summary>Core Audio's AudioSessionState.</summary>
internal enum AudioSessionState
{
    Inactive = 0,
    Active = 1,
    Expired = 2,
}

/// <summary>Core Audio's AudioSessionDisconnectReason.</summary>
internal enum AudioSessionDisconnectReason
{
    DeviceRemoval = 0,
    ServerShutdown = 1,
    FormatChanged = 2,
    SessionLogoff = 3,
    SessionDisconnected = 4,
    ExclusiveModeOverride = 5,
}

/// <summary>
/// IAudioSessionManager2 — an endpoint's sessions, and notice of new ones.
/// Activated off an <see cref="IMMDevice"/>.
/// </summary>
/// <remarks>
/// The first two methods come from IAudioSessionManager and are never called;
/// they are declared because a COM interface is its vtable order. Their
/// parameters are flattened to pointers for the same reason.
/// </remarks>
[ComImport]
[Guid("77aa99a0-1bd6-484f-8bc7-2c654c9a9b6f")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    [PreserveSig]
    int GetAudioSessionControl(IntPtr sessionGuid, int streamFlags, out IAudioSessionControl? control);

    [PreserveSig]
    int GetSimpleAudioVolume(IntPtr sessionGuid, int crossProcess, out ISimpleAudioVolume? volume);

    [PreserveSig]
    int GetSessionEnumerator(out IAudioSessionEnumerator? sessions);

    [PreserveSig]
    int RegisterSessionNotification(IAudioSessionNotification notification);

    [PreserveSig]
    int UnregisterSessionNotification(IAudioSessionNotification notification);

    [PreserveSig]
    int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string? sessionId, IntPtr notification);

    [PreserveSig]
    int UnregisterDuckNotification(IntPtr notification);
}

[ComImport]
[Guid("e2f5bb11-0570-40ca-acdd-3aa01277dee8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig]
    int GetCount(out int count);

    [PreserveSig]
    int GetSession(int index, out IAudioSessionControl? session);
}

/// <summary>
/// IAudioSessionControl — one session as the base interface sees it.
/// Declared because the enumerator and
/// <see cref="IAudioSessionNotification.OnSessionCreated"/> hand this over;
/// awoo casts it to <see cref="IAudioSessionControl2"/> straight away.
/// </summary>
[ComImport]
[Guid("f4b1a599-7266-4319-a8ca-e70acb11e8cd")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl
{
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? name);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr eventContext);

    [PreserveSig]
    int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? path);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string? path, IntPtr eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingParam);

    [PreserveSig]
    int SetGroupingParam(ref Guid groupingParam, IntPtr eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IAudioSessionEvents events);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IAudioSessionEvents events);
}

/// <summary>
/// IAudioSessionControl2 — the same session plus what owns it: the process,
/// a stable identifier, and whether this is Windows' own system-sounds
/// session.
/// </summary>
[ComImport]
[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? name);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr eventContext);

    [PreserveSig]
    int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? path);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string? path, IntPtr eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingParam);

    [PreserveSig]
    int SetGroupingParam(ref Guid groupingParam, IntPtr eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IAudioSessionEvents events);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IAudioSessionEvents events);

    [PreserveSig]
    int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);

    [PreserveSig]
    int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);

    [PreserveSig]
    int GetProcessId(out int processId);

    /// <summary>S_OK for Windows' system-sounds session, S_FALSE for anything else.</summary>
    [PreserveSig]
    int IsSystemSoundsSession();

    [PreserveSig]
    int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
}

/// <summary>
/// ISimpleAudioVolume — one session's volume and mute, which is what the
/// Windows mixer's per-app slider moves.
/// </summary>
[ComImport]
[Guid("87ce5498-68d6-44e5-9215-6da47ef883d8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    [PreserveSig]
    int SetMasterVolume(float level, ref Guid eventContext);

    [PreserveSig]
    int GetMasterVolume(out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
}

/// <summary>
/// Implemented by awoo and registered on a session: how a change made in
/// Windows' own mixer, or the session ending, reaches us.
/// </summary>
/// <remarks>
/// Every event context here is an LPCGUID, which Core Audio is free to pass
/// as NULL — so it is an <see cref="IntPtr"/> rather than a <c>ref Guid</c>
/// that would fault on the null.
/// </remarks>
[ComImport]
[Guid("24918acc-64b3-37c1-8ca9-74a66e9957a8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEvents
{
    [PreserveSig]
    int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr eventContext);

    [PreserveSig]
    int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string? path, IntPtr eventContext);

    [PreserveSig]
    int OnSimpleVolumeChanged(float level, [MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr eventContext);

    [PreserveSig]
    int OnChannelVolumeChanged(int channelCount, IntPtr volumes, int changedChannel, IntPtr eventContext);

    [PreserveSig]
    int OnGroupingParamChanged(IntPtr groupingParam, IntPtr eventContext);

    [PreserveSig]
    int OnStateChanged(AudioSessionState state);

    [PreserveSig]
    int OnSessionDisconnected(AudioSessionDisconnectReason reason);
}

/// <summary>
/// Implemented by awoo and registered on an endpoint's session manager: how
/// an application starting to play reaches us without being polled for.
/// </summary>
[ComImport]
[Guid("641dd20b-4d41-49cc-aba3-174b9477bb08")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionNotification
{
    [PreserveSig]
    int OnSessionCreated(IAudioSessionControl? session);
}
