namespace Awoo.Audio;

/// <summary>
/// awoo's view of the machine's audio. The UI binds to this and never touches
/// Core Audio itself, which is also what lets the flyout and the tray icon
/// show the same state without knowing about each other.
/// </summary>
internal interface IAudioService : IDisposable
{
    /// <summary>The default output device as it stands right now.</summary>
    VolumeState State { get; }

    /// <summary>
    /// Every active endpoint, outputs before inputs and alphabetical within
    /// each. A fresh list on every change rather than a live collection, so a
    /// reader is never iterating one that is being rewritten.
    /// </summary>
    IReadOnlyList<AudioDeviceState> Devices { get; }

    /// <summary>
    /// Every application playing or recording right now, playback before
    /// recording and alphabetical within each. One row per application, not
    /// per stream. A fresh list on every change, like <see cref="Devices"/>.
    /// </summary>
    IReadOnlyList<AudioAppState> Apps { get; }

    /// <summary>Whether this build of Windows let us at the default-device switch.</summary>
    bool CanSetDefaultDevice { get; }

    /// <summary>
    /// Raised whenever <see cref="State"/> changes — by awoo, by Windows, by
    /// a hardware key or by the default device moving. Raised on the thread
    /// the service was given to dispatch onto, so a handler may touch the UI.
    /// </summary>
    event EventHandler<VolumeState>? StateChanged;

    /// <summary>
    /// Raised whenever <see cref="Devices"/> changes: a device plugged in or
    /// removed, one renamed, one's volume or mute moved, or the default
    /// moving. Dispatched like <see cref="StateChanged"/>.
    /// </summary>
    event EventHandler<IReadOnlyList<AudioDeviceState>>? DevicesChanged;

    /// <summary>
    /// Raised whenever <see cref="Apps"/> changes: an application starting or
    /// stopping, or one's volume or mute moving — including from Windows' own
    /// volume mixer. Dispatched like <see cref="StateChanged"/>.
    /// </summary>
    event EventHandler<IReadOnlyList<AudioAppState>>? AppsChanged;

    /// <summary>Sets the default output device's volume, 0–100.</summary>
    void SetPercent(int percent);

    /// <summary>Mutes or unmutes the default output device.</summary>
    void SetMute(bool muted);

    /// <summary>Sets one named endpoint's volume, 0–100. Unknown ids are ignored.</summary>
    void SetDevicePercent(string deviceId, int percent);

    /// <summary>Mutes or unmutes one named endpoint. Unknown ids are ignored.</summary>
    void SetDeviceMute(string deviceId, bool muted);

    /// <summary>
    /// Sets one application's own volume, 0–100 — what the Windows mixer's
    /// per-app slider moves, under the device's volume rather than instead of
    /// it. Applies to every stream the app has. Unknown ids are ignored.
    /// </summary>
    void SetAppPercent(string appId, int percent);

    /// <summary>Mutes or unmutes every stream of one application. Unknown ids are ignored.</summary>
    void SetAppMute(string appId, bool muted);

    /// <summary>
    /// Makes one endpoint Windows' default for its kind, across all three
    /// roles. Unknown ids are ignored, and so is a device that is already the
    /// default.
    /// </summary>
    void SetDefaultDevice(string deviceId);
}
