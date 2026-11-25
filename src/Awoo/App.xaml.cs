using Awoo.Audio;
using Awoo.Flyout;
using Awoo.Settings;
using Awoo.Tray;
using Awoo.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Awoo;

/// <summary>
/// Composition root. awoo has no main window and no taskbar button: the tray
/// icon is the whole of its presence, and the flyout is created once at
/// startup and then shown and hidden rather than opened and closed.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Held for the life of the process, so a second launch can see the
    /// first. Static: the lock is the process's rather than this object's.
    /// </summary>
    private static Mutex? _instanceLock;

    /// <summary>
    /// A dev-only switch: the hide engine answers every attempt with
    /// "nothing found" instead of touching Explorer, which is what the
    /// settings window's "cannot hide it" note is for. The hide itself is the
    /// settings toggle's, not a switch's.
    /// </summary>
    private const string HideNotFoundSwitch = "--volume-icon-hide-not-found";

    private AudioService? _audio;
    private SettingsService? _settings;
    private DefaultDeviceLock? _deviceLock;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private TrayIconController? _tray;
    private SystemVolumeIconService? _volumeIcon;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // One awoo per session, settled before anything is created: the tray
        // icon's identity derives from the exe path, so a second copy
        // registering it would take the icon from the copy already running —
        // which would keep running with no icon and no way to be told to
        // exit. The second copy bows out instead.
        _instanceLock = new Mutex(initiallyOwned: true, @"Local\awoo-single-instance", out var createdNew);
        if (!createdNew)
        {
            _instanceLock.Dispose();
            _instanceLock = null;
            Exit();
            return;
        }

        // Core Audio calls back on its own threads; everything awoo does with
        // it runs here instead, on the one thread the UI also lives on.
        var queue = DispatcherQueue.GetForCurrentThread();
        var dispatch = (Action action) =>
        {
            if (!queue.TryEnqueue(() => action()))
            {
                // The queue is shutting down — there is nothing left to tell.
            }
        };

        _settings = new SettingsService();

        // Before anything reads the settings: this is the run that decides
        // whether autostart has ever been registered.
        _settings.ApplyFirstRunDefaults();

        _audio = new AudioService(dispatch);

        // Nothing of the UI's: it listens to the service and puts back any
        // default something else moved, which has to happen whether or not a
        // window is open to see it.
        _deviceLock = new DefaultDeviceLock(_audio, _settings);

        var volume = new VolumeViewModel(_audio);
        var devices = new DevicesViewModel(_audio, _settings);
        var apps = new AppsViewModel(_audio, _settings);

        _flyout = new FlyoutWindow(devices, apps);

        // Windows' own volume icon, kept in whatever state the setting asks
        // for. Created before the tray so the taskbar's restarts can be handed
        // to it, and started after, because the tray being up is what says
        // awoo is working whatever this comes to: hiding reaches into
        // Explorer, and every way that can fail ends in awoo carrying on.
        _volumeIcon = new SystemVolumeIconService(
            _settings,
            new SystemVolumeIconHider(pretendNothingFound: HasSwitch(HideNotFoundSwitch)),
            dispatch);

        _tray = new TrayIconController(volume, _settings, dispatch);
        _tray.Clicked += (_, _) => _flyout?.Toggle();
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.TaskbarRecreated += (_, _) => _volumeIcon?.TaskbarRecreated();
        _tray.Show();

        _volumeIcon.Start();
    }

    /// <summary>Whether this switch was on awoo's command line.</summary>
    private static bool HasSwitch(string name) =>
        Environment.GetCommandLineArgs().Any(
            arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Opens the settings window, or brings up the one already open: two
    /// windows of the same settings could only disagree with each other.
    /// </summary>
    private void ShowSettings()
    {
        if (_settings is null || _audio is null || _volumeIcon is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            var window = new SettingsWindow(_settings, _audio, _volumeIcon);
            window.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow = window;
        }

        _settingsWindow.Reveal();
    }

    /// <summary>
    /// Removes the tray icon before ending the process, so no dead icon is
    /// left behind in the notification area.
    /// </summary>
    private void Shutdown()
    {
        // Before the tray goes: the human chose that exiting awoo always
        // brings the stock icon back, so no volume icon is ever lost with awoo
        // gone. A no-op if nothing was hidden.
        _volumeIcon?.Dispose();
        _volumeIcon = null;

        _tray?.Dispose();
        _tray = null;

        _settingsWindow?.Close();
        _settingsWindow = null;

        _flyout?.CloseForExit();
        _flyout = null;

        // Before the service it listens to, so no revert is attempted on the
        // way out.
        _deviceLock?.Dispose();
        _deviceLock = null;

        _audio?.Dispose();
        _audio = null;

        _settings = null;

        Exit();
    }
}
