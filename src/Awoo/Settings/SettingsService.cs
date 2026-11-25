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
    /// Raised when <see cref="HideSystemVolumeIcon"/> has been turned on or
    /// off. What <see cref="Tray.SystemVolumeIconService"/> listens to: the
    /// switch hides or restores the stock icon as it is flipped, not at the
    /// next start.
    /// </summary>
    internal event EventHandler? HideSystemVolumeIconChanged;

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

    /// <summary>Whether Windows starts awoo at sign-in. Read from, and written to, the Run key.</summary>
    internal bool StartWithWindows
    {
        get => Autostart.IsEnabled;
        set => Autostart.TrySet(value);
    }

    /// <summary>
    /// Whether awoo hides Windows' own volume icon while it runs. Off by
    /// default; see <see cref="AppSettings.HideSystemVolumeIcon"/>.
    /// </summary>
    /// <remarks>
    /// Stored here and acted on by <see cref="Tray.SystemVolumeIconService"/>,
    /// which listens for the change: a setting that reaches into Explorer
    /// cannot be applied from the property that records it.
    /// </remarks>
    internal bool HideSystemVolumeIcon
    {
        get => _stored.HideSystemVolumeIcon;
        set
        {
            if (value == _stored.HideSystemVolumeIcon)
            {
                return;
            }

            Store(_stored with { HideSystemVolumeIcon = value });
            HideSystemVolumeIconChanged?.Invoke(this, EventArgs.Empty);
        }
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
