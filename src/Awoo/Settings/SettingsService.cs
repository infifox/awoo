using Awoo.Audio;

namespace Awoo.Settings;

/// <summary>
/// awoo's settings as the rest of the app uses them: the values kept on disk
/// and the Run key behind "start with Windows", behind one object, so nothing
/// else has to know which of the two a setting lives in.
/// </summary>
/// <remarks>
/// Every write goes straight through to where the setting lives, so there is
/// no save step and nothing to lose by quitting.
/// </remarks>
internal sealed class SettingsService
{
    /// <summary>
    /// The hidden endpoints as something to ask about one id at a time, which
    /// is how every reader uses them — the list in
    /// <see cref="AppSettings.HiddenDeviceIds"/> is what gets written.
    /// </summary>
    private readonly HashSet<string> _hidden;

    private AppSettings _stored;

    internal SettingsService()
    {
        _stored = SettingsStore.Load();
        _hidden = new HashSet<string>(_stored.HiddenDeviceIds, AppSettings.DeviceIdComparer);
    }

    /// <summary>
    /// Raised when a device has been hidden or shown again. What the flyout's
    /// two lists listen to: hiding takes effect where the volume control can
    /// be seen, not at the next start.
    /// </summary>
    internal event EventHandler? HiddenDevicesChanged;

    /// <summary>
    /// Raised when the default audio device and the default communications
    /// device have been split apart, or put back together. The Devices tab's
    /// rows listen to it for the same reason they listen to
    /// <see cref="HiddenDevicesChanged"/>: an open flyout redraws rather than
    /// waiting for awoo to be restarted.
    /// </summary>
    internal event EventHandler? SplitDefaultRolesChanged;

    /// <summary>
    /// Raised when the default-device lock has been turned on or off. What
    /// <see cref="DefaultDeviceLock"/> listens to: turning it on is also when
    /// the devices to hold are taken down, and that is the lock's reading of
    /// the machine rather than this object's.
    /// </summary>
    internal event EventHandler? LockDefaultDevicesChanged;

    /// <summary>
    /// How far one notch of the wheel over the tray icon moves the volume.
    /// Clamped rather than refused: the setting has a range, and a value
    /// outside it is a value at the end of it.
    /// </summary>
    internal int VolumeStep
    {
        get => _stored.VolumeStep;
        set => Store(_stored with
        {
            VolumeStep = Math.Clamp(value, AppSettings.MinVolumeStep, AppSettings.MaxVolumeStep),
        });
    }

    /// <summary>
    /// Whether the Devices tab sets the default audio device and the default
    /// communications device apart, a marker each, rather than moving all
    /// three of Windows' roles with one. Turning it off moves no defaults:
    /// the two markers collapse back into one, and the next click on one sets
    /// all three roles again.
    /// </summary>
    internal bool SplitDefaultRoles
    {
        get => _stored.SplitDefaultRoles;
        set
        {
            if (value == _stored.SplitDefaultRoles)
            {
                return;
            }

            Store(_stored with { SplitDefaultRoles = value });
            SplitDefaultRolesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Whether awoo holds Windows to the devices the user selected. Turning it
    /// on is what captures them — <see cref="DefaultDeviceLock"/> answers the
    /// event with the defaults as they stand. Turning it off leaves every
    /// default where it is and reverts nothing afterwards; the remembered ids
    /// stay in the file, and the next time it is turned on they are taken
    /// again.
    /// </summary>
    internal bool LockDefaultDevices
    {
        get => _stored.LockDefaultDevices;
        set
        {
            if (value == _stored.LockDefaultDevices)
            {
                return;
            }

            Store(_stored with { LockDefaultDevices = value });
            LockDefaultDevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The devices the lock is holding Windows to.</summary>
    internal LockedDefaults LockedDefaults => _stored.LockedDefaults;

    /// <summary>Whether Windows starts awoo at sign-in. Read from, and written to, the Run key.</summary>
    internal bool StartWithWindows
    {
        get => Autostart.IsEnabled;
        set => Autostart.TrySet(value);
    }

    /// <summary>
    /// Whether this endpoint has been hidden from the volume control. Says
    /// nothing about whether it is plugged in, and nothing about the device
    /// itself: hiding is awoo's own, and Windows goes on using the device.
    /// </summary>
    internal bool IsDeviceHidden(string deviceId) => _hidden.Contains(deviceId);

    /// <summary>
    /// Hides one endpoint from the volume control, or puts it back. Raises
    /// <see cref="HiddenDevicesChanged"/> only where something moved, so the
    /// lists are not rebuilt for a toggle set to what it already was.
    /// </summary>
    internal void SetDeviceHidden(string deviceId, bool hidden)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || hidden == _hidden.Contains(deviceId))
        {
            return;
        }

        if (hidden)
        {
            _hidden.Add(deviceId);
        }
        else
        {
            _hidden.Remove(deviceId);
        }

        Store(_stored with { HiddenDeviceIds = [.. _hidden] });
        HiddenDevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Takes down the whole selection the lock is to hold. What turning the
    /// lock on does, once the lock has read where the defaults stand.
    /// </summary>
    internal void RememberLockedDefaults(LockedDefaults defaults) => Store(_stored.With(defaults));

    /// <summary>
    /// Remembers one device as the lock's choice for every role
    /// <paramref name="role"/> covers — what a pick in awoo's flyout does, so
    /// that the guard lets the pick stand instead of putting the old device
    /// back.
    /// </summary>
    /// <remarks>
    /// Nothing at all while the lock is off: there is no selection being held
    /// then, and remembering one would have the file fill with devices nobody
    /// asked awoo to keep. Turning the lock on captures the defaults as they
    /// are at that moment instead.
    /// </remarks>
    internal void RememberLockedDefault(DeviceKind kind, DefaultRole role, string deviceId)
    {
        if (!_stored.LockDefaultDevices || string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        Store(_stored.With(_stored.LockedDefaults.With(kind, role, deviceId)));
    }

    /// <summary>
    /// The on-by-default decision, made once and recorded as made. Called at
    /// startup, before anything reads <see cref="StartWithWindows"/>.
    /// </summary>
    internal void ApplyFirstRunDefaults()
    {
        if (_stored.AutostartRegistered)
        {
            // Not a first run — but this copy of awoo may not be the one the
            // entry names any more.
            Autostart.Refresh();
            return;
        }

        Autostart.TrySet(true);
        Store(_stored with { AutostartRegistered = true });
    }

    private void Store(AppSettings settings)
    {
        if (settings == _stored)
        {
            return;
        }

        _stored = settings;
        SettingsStore.Save(settings);
    }
}
