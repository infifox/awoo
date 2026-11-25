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
    /// The dev-only switch that drives task 02's hide engine. Task 03 replaces
    /// it with a real settings toggle; until then it is how the feature is
    /// exercised. <c>--hide-system-volume-icon</c>.
    /// </summary>
    private const string HideVolumeIconSwitch = "--hide-system-volume-icon";

    private AudioService? _audio;
    private SettingsService? _settings;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private TrayIconController? _tray;
    private SystemVolumeIconHider? _volumeIconHider;

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
        var volume = new VolumeViewModel(_audio);
        var devices = new DevicesViewModel(_audio, _settings);
        var apps = new AppsViewModel(_audio, _settings);

        _flyout = new FlyoutWindow(devices, apps);

        _tray = new TrayIconController(volume, _settings, dispatch);
        _tray.Clicked += (_, _) => _flyout?.Toggle();
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.Show();

        // The hide engine, behind the dev switch for this task. Injecting into
        // Explorer and waiting for the DLL to report in would stall startup,
        // and none of it needs the UI thread, so it runs off to the side; the
        // tray is already up and awoo is fully working whatever it comes to.
        if (WantsVolumeIconHidden())
        {
            var hider = new SystemVolumeIconHider();
            _volumeIconHider = hider;
            Task.Run(() => hider.Hide());
        }
    }

    /// <summary>Whether the dev switch to hide the stock volume icon was passed.</summary>
    private static bool WantsVolumeIconHidden() =>
        Environment.GetCommandLineArgs().Any(
            arg => string.Equals(arg, HideVolumeIconSwitch, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Opens the settings window, or brings up the one already open: two
    /// windows of the same two settings could only disagree with each other.
    /// </summary>
    private void ShowSettings()
    {
        if (_settings is null || _audio is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            var window = new SettingsWindow(_settings, _audio);
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
        _volumeIconHider?.Restore();
        _volumeIconHider = null;

        _tray?.Dispose();
        _tray = null;

        _settingsWindow?.Close();
        _settingsWindow = null;

        _flyout?.CloseForExit();
        _flyout = null;

        _audio?.Dispose();
        _audio = null;

        _settings = null;

        Exit();
    }
}
