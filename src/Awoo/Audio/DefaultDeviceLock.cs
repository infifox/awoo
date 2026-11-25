using Awoo.Settings;

namespace Awoo.Audio;

/// <summary>
/// Holds Windows to the devices the user selected, while the
/// "prevent Windows from automatically switching my audio device" setting is
/// on: any default moved from outside awoo is put back the moment Windows
/// announces it.
/// </summary>
/// <remarks>
/// <para>
/// Notification-driven and nothing else — there is no timer. The audio service
/// already hears every default moving, whether from Windows auto-switching to
/// a device as it appears, from a driver, from another application, or from
/// Windows' own Sound settings, and publishes a device list in which the roles
/// have moved. This watches that list.
/// </para>
/// <para>
/// Which makes awoo's flyout the place devices get switched while the lock is
/// on: a pick there tells <see cref="SettingsService.RememberLockedDefault"/>
/// what was chosen before it asks for the switch, so the switch is the
/// selection moving rather than something to put back. That Windows' own Sound
/// settings page is overridden too is the agreed behaviour.
/// </para>
/// <para>
/// Lives on the audio service's one thread, like everything else that touches
/// it: the service dispatches <see cref="IAudioService.DevicesChanged"/>
/// there, and the setting is only ever moved from the settings window, on the
/// same thread.
/// </para>
/// </remarks>
internal sealed class DefaultDeviceLock : IDisposable
{
    /// <summary>
    /// The two roles the lock holds apart. Together they are all three of
    /// Windows' — <see cref="DefaultRole.Audio"/> is console and multimedia —
    /// and they are held apart whether or not the split setting is drawing
    /// them apart, so turning that setting over changes nothing here.
    /// </summary>
    private static readonly DefaultRole[] Roles = [DefaultRole.Audio, DefaultRole.Communications];

    private static readonly DeviceKind[] Kinds = [DeviceKind.Output, DeviceKind.Input];

    private readonly IAudioService _audio;
    private readonly SettingsService _settings;

    /// <summary>
    /// Set while a revert is going through.
    /// <see cref="IAudioService.SetDefaultDevice"/> publishes before it
    /// returns, so the assertion would otherwise land back here — and a switch
    /// Windows refused would land back here for ever. This is what makes it
    /// one assertion per announcement.
    /// </summary>
    private bool _asserting;

    private bool _disposed;

    internal DefaultDeviceLock(IAudioService audio, SettingsService settings)
    {
        _audio = audio;
        _settings = settings;
        _audio.DevicesChanged += OnDevicesChanged;
        _settings.LockDefaultDevicesChanged += OnLockChanged;

        // awoo was not running to hear what moved while it was off, so the
        // first thing a locked awoo does is take its devices back.
        Hold(_audio.Devices);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _audio.DevicesChanged -= OnDevicesChanged;
        _settings.LockDefaultDevicesChanged -= OnLockChanged;
    }

    private void OnDevicesChanged(object? sender, IReadOnlyList<AudioDeviceState> devices) => Hold(devices);

    /// <summary>
    /// The lock turned on or off. On is when the selection to hold is taken
    /// down: whatever is default at that moment is what the user has, and so
    /// is what they are asking to keep. Off takes nothing back and puts
    /// nothing anywhere.
    /// </summary>
    private void OnLockChanged(object? sender, EventArgs args)
    {
        if (!_disposed && _settings.LockDefaultDevices)
        {
            _settings.RememberLockedDefaults(Current(_audio.Devices));
        }
    }

    /// <summary>
    /// Puts every role back on the device remembered for it, where something
    /// has moved it. Four independent questions — output and input, audio and
    /// calls — so a revert of one is read against a list the revert of another
    /// has not changed.
    /// </summary>
    private void Hold(IReadOnlyList<AudioDeviceState> devices)
    {
        if (_disposed || _asserting || !_settings.LockDefaultDevices)
        {
            return;
        }

        var locked = _settings.LockedDefaults;

        _asserting = true;
        try
        {
            foreach (var kind in Kinds)
            {
                foreach (var role in Roles)
                {
                    Assert(devices, locked, kind, role);
                }
            }
        }
        finally
        {
            _asserting = false;
        }
    }

    /// <summary>
    /// One role of one kind: asks for the remembered device back unless it is
    /// already holding the role, or is not here to hold it.
    /// </summary>
    /// <remarks>
    /// A remembered device that is unplugged leaves Windows' fallback exactly
    /// where Windows put it — reverting to a device that is away would be
    /// asking for silence. The id is kept through the absence, so plugging it
    /// back in publishes a list it is in again, and this takes it back with no
    /// user action.
    /// </remarks>
    private void Assert(
        IReadOnlyList<AudioDeviceState> devices,
        LockedDefaults locked,
        DeviceKind kind,
        DefaultRole role)
    {
        if (locked.For(kind, role) is not { } wanted || Find(devices, kind, wanted) is not { } device)
        {
            return;
        }

        if (Holds(device, role))
        {
            // A change that landed on the remembered device, which is the
            // ordinary case: every pick through awoo's flyout comes through
            // here too.
            return;
        }

        // The endpoint's own id rather than the remembered string, which a
        // hand-edit may have in another case.
        _audio.SetDefaultDevice(device.Id, role);
    }

    /// <summary>Where the defaults stand right now, as a selection to remember.</summary>
    private static LockedDefaults Current(IReadOnlyList<AudioDeviceState> devices) => new(
        Holder(devices, DeviceKind.Output, DefaultRole.Audio),
        Holder(devices, DeviceKind.Input, DefaultRole.Audio),
        Holder(devices, DeviceKind.Output, DefaultRole.Communications),
        Holder(devices, DeviceKind.Input, DefaultRole.Communications));

    /// <summary>
    /// The device holding one role, or null where nothing does — a machine
    /// with no device of that kind at all.
    /// </summary>
    private static string? Holder(IReadOnlyList<AudioDeviceState> devices, DeviceKind kind, DefaultRole role)
    {
        foreach (var device in devices)
        {
            if (device.Kind == kind && Holds(device, role))
            {
                return device.Id;
            }
        }

        return null;
    }

    /// <summary>Whether one device holds one of the two roles the lock keeps.</summary>
    private static bool Holds(AudioDeviceState device, DefaultRole role) =>
        role == DefaultRole.Communications ? device.IsDefaultCommunications : device.IsDefault;

    /// <summary>
    /// One named endpoint of one kind in the published list, or null where it
    /// is not there — which is what an unplugged device looks like, the list
    /// being the active endpoints.
    /// </summary>
    private static AudioDeviceState? Find(IReadOnlyList<AudioDeviceState> devices, DeviceKind kind, string id)
    {
        foreach (var device in devices)
        {
            if (device.Kind == kind && AppSettings.DeviceIdComparer.Equals(device.Id, id))
            {
                return device;
            }
        }

        return null;
    }
}
