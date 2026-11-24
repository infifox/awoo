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
    private AppSettings _stored;

    internal SettingsService() => _stored = SettingsStore.Load();

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
