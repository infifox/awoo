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
    /// Sends one application's sound to one endpoint, or back to following
    /// the default device when <paramref name="deviceId"/> is null. Windows
    /// remembers the choice against the application, so it outlives the
    /// application being closed and reopened. Ignored for a row that
    /// <see cref="AudioAppState.CanRoute"/> says cannot be routed, and for
    /// unknown ids.
    /// </summary>
    void SetAppDevice(string appId, string? deviceId);

    /// <summary>
    /// Re-reads where every application's sound is going. Nothing announces a
    /// route changing — not awoo's own write and not one made in Windows'
    /// Settings — so this is what brings <see cref="Apps"/> up to date, and
    /// the flyout calls it as it opens.
    /// </summary>
    void RefreshAppRoutes();

    /// <summary>
    /// Makes one endpoint Windows' default for its kind, over the roles
    /// <paramref name="role"/> names — all three together, the audio pair, or
    /// communications alone. Unknown ids are ignored, and so is a device that
    /// already holds those roles.
    /// </summary>
    void SetDefaultDevice(string deviceId, DefaultRole role);

    /// <summary>
    /// Puts one output into a spatial sound format, or turns spatial sound
    /// off there when <paramref name="formatId"/> is null. Ignored for a
    /// device whose <see cref="AudioDeviceState.Spatial"/> has nothing to
    /// offer, and for unknown ids.
    /// </summary>
    /// <returns>
    /// A task that completes once Windows has answered and the endpoint has
    /// been read back — so a caller that awaits it is looking at what the
    /// device took, which is not always what it was asked for. The answer
    /// itself is in <see cref="Devices"/> rather than in the task: a format
    /// Windows declined leaves the endpoint where it was.
    /// </returns>
    Task SetDeviceSpatialFormat(string deviceId, Guid? formatId);

    /// <summary>
    /// Re-reads every output's spatial sound. Windows does announce this one
    /// changing, unlike an application's route, so this is belt and braces
    /// for the moment a stale menu would be seen: the flyout calls it as it
    /// opens, beside <see cref="RefreshAppRoutes"/>.
    /// </summary>
    void RefreshSpatialFormats();
}
