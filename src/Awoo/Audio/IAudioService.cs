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
    /// Raised whenever <see cref="State"/> changes — by awoo, by Windows, by
    /// a hardware key or by the default device moving. Raised on the thread
    /// the service was given to dispatch onto, so a handler may touch the UI.
    /// </summary>
    event EventHandler<VolumeState>? StateChanged;

    /// <summary>Sets the default output device's volume, 0–100.</summary>
    void SetPercent(int percent);

    /// <summary>Mutes or unmutes the default output device.</summary>
    void SetMute(bool muted);
}
