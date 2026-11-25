using Awoo.Settings;

namespace Awoo.Tray;

/// <summary>
/// Keeps the stock Windows volume icon in the state the
/// <see cref="AppSettings.HideSystemVolumeIcon"/> setting asks for: hidden
/// from the moment awoo starts, hidden or restored as the switch is flipped,
/// hidden again after Explorer has restarted, and restored when awoo exits.
/// </summary>
/// <remarks>
/// <para>
/// The engine underneath — <see cref="SystemVolumeIconHider"/> — injects into
/// Explorer and waits for the DLL to answer, which takes long enough that it
/// may not be done on the UI thread. So every change settles on one worker
/// instead: each request records that the state wants looking at, and the
/// worker applies whatever the setting says by the time it gets there. Two
/// flips in a row cannot land out of order, because neither of them carries a
/// state of its own.
/// </para>
/// <para>
/// Nothing here fails loudly. Hiding is a thing awoo does to somebody else's
/// process and it can stop working for reasons no release of awoo can fix, so
/// the whole of the failure story is <see cref="HideIsNotWorking"/> and the
/// quiet note the settings window puts under the switch.
/// </para>
/// </remarks>
internal sealed class SystemVolumeIconService : IDisposable
{
    /// <summary>
    /// How long to leave a restarted Explorer alone before hiding again. Its
    /// taskbar comes back before the XAML tree the engine has to walk is in
    /// it, and the tray icons arrive last of all — awoo's own among them.
    /// </summary>
    private static readonly TimeSpan TaskbarSettleDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long an exit waits for an attempt already inside Explorer, so the
    /// restore is not overtaken by the hide it was meant to undo.
    /// </summary>
    private static readonly TimeSpan ShutdownDrain = TimeSpan.FromSeconds(5);

    private readonly SettingsService _settings;
    private readonly SystemVolumeIconHider _hider;
    private readonly Action<Action> _dispatch;
    private readonly object _gate = new();

    private Task _work = Task.CompletedTask;
    private bool _pending;
    private bool _running;
    private bool _stopped;

    /// <summary>Read on the UI thread, written on the worker.</summary>
    private volatile bool _notWorking;

    /// <param name="settings">Where the setting lives, and what says when it changes.</param>
    /// <param name="hider">The engine that reaches into Explorer.</param>
    /// <param name="dispatch">
    /// Posts to the UI thread. <see cref="StateChanged"/> is raised from the
    /// worker and the settings window is listening to it.
    /// </param>
    internal SystemVolumeIconService(
        SettingsService settings,
        SystemVolumeIconHider hider,
        Action<Action> dispatch)
    {
        _settings = settings;
        _hider = hider;
        _dispatch = dispatch;
        _settings.HideSystemVolumeIconChanged += OnSettingChanged;
    }

    /// <summary>
    /// Raised on the UI thread when <see cref="HideIsNotWorking"/> has
    /// changed. What the settings window's note listens to.
    /// </summary>
    internal event EventHandler? StateChanged;

    /// <summary>
    /// Whether the setting is on and the last attempt did not get the icon
    /// hidden — nothing matched in the tray's visual tree, most likely after a
    /// Windows update, or the mechanism never came up at all. False until an
    /// attempt has come back, so the note never shows on hope alone.
    /// </summary>
    internal bool HideIsNotWorking => _settings.HideSystemVolumeIcon && _notWorking;

    /// <summary>
    /// Brings the stock icon in line with the setting at startup. Runs even
    /// with the setting off: an awoo that crashed left its DLL hiding the icon
    /// inside Explorer, and this is where that is put right.
    /// </summary>
    internal void Start() => Settle();

    /// <summary>
    /// Explorer has restarted, so the tray is new, the injected DLL died with
    /// the old one, and the stock icon is back whatever the setting says.
    /// Hides it again once the new taskbar has settled.
    /// </summary>
    internal void TaskbarRecreated()
    {
        if (!_settings.HideSystemVolumeIcon)
        {
            return;
        }

        _ = Task.Delay(TaskbarSettleDelay).ContinueWith(_ => Settle(), TaskScheduler.Default);
    }

    /// <summary>
    /// Restores the stock icon and stops attending to the setting. Called on
    /// awoo's orderly shutdown paths: the human chose that exiting awoo always
    /// brings the stock icon back, so no volume icon is ever lost with awoo
    /// gone.
    /// </summary>
    public void Dispose()
    {
        _settings.HideSystemVolumeIconChanged -= OnSettingChanged;

        Task work;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _pending = false;
            work = _work;
        }

        try
        {
            work.Wait(ShutdownDrain);
        }
        catch (AggregateException)
        {
            // The worker swallows its own failures; this is only the wait.
        }

        _hider.Restore();
    }

    private void OnSettingChanged(object? sender, EventArgs args)
    {
        // The verdict belongs to the attempt that earned it, and that attempt
        // is now the last switch position's. So the note goes before the new
        // attempt starts, rather than carrying over to it.
        SetNotWorking(false);
        Settle();
    }

    /// <summary>
    /// Says the stock icon's state wants looking at, and starts the worker if
    /// it is not already going round.
    /// </summary>
    private void Settle()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _pending = true;
            if (_running)
            {
                return;
            }

            _running = true;
            _work = Task.Run(Work);
        }
    }

    private void Work()
    {
        while (true)
        {
            bool wanted;
            lock (_gate)
            {
                if (!_pending || _stopped)
                {
                    _running = false;
                    return;
                }

                _pending = false;
                wanted = _settings.HideSystemVolumeIcon;
            }

            if (wanted)
            {
                _hider.Hide();
            }
            else
            {
                _hider.Restore();
            }

            Report(wanted);
        }
    }

    /// <summary>What the attempt just made came to, as the note reads it.</summary>
    /// <remarks>
    /// Anything short of hidden is the note's business, not only the engine
    /// matching nothing: the setting is on and the stock icon is still there,
    /// which is the one thing the human can see and the whole of what the note
    /// says.
    /// </remarks>
    private void Report(bool wanted) =>
        SetNotWorking(wanted && _hider.LastOutcome != SystemVolumeIconHider.HideOutcome.Hidden);

    /// <summary>Publishes the note's state, where it is not what it was.</summary>
    private void SetNotWorking(bool notWorking)
    {
        if (notWorking == _notWorking)
        {
            return;
        }

        _notWorking = notWorking;
        _dispatch(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }
}
