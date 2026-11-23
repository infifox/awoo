using Awoo.Audio;
using Awoo.Flyout;
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
    private AudioService? _audio;
    private FlyoutWindow? _flyout;
    private TrayIconController? _tray;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
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

        _audio = new AudioService(dispatch);
        var volume = new VolumeViewModel(_audio);
        var devices = new DevicesViewModel(_audio);

        _flyout = new FlyoutWindow(volume, devices);

        _tray = new TrayIconController(volume, dispatch);
        _tray.Clicked += (_, _) => _flyout?.Toggle();
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.Show();
    }

    /// <summary>
    /// Removes the tray icon before ending the process, so no dead icon is
    /// left behind in the notification area.
    /// </summary>
    private void Shutdown()
    {
        _tray?.Dispose();
        _tray = null;

        _flyout?.CloseForExit();
        _flyout = null;

        _audio?.Dispose();
        _audio = null;

        Exit();
    }
}
