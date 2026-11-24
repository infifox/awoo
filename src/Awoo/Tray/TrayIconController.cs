using System.ComponentModel;
using Awoo.Settings;
using Awoo.ViewModels;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;

namespace Awoo.Tray;

/// <summary>
/// awoo's presence in the notification area: the icon, its tooltip, its
/// gestures and its right-click menu. The icon and tooltip are drawn from the
/// volume view model, so the tray says the same thing as the flyout.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    private readonly VolumeViewModel _volume;
    private readonly SettingsService _settings;
    private readonly Action<Action> _dispatch;
    private readonly TaskbarIcon _taskbarIcon;

    private RenderedIcon? _icon;
    private TrayWheel? _wheel;
    private MessageWindow? _messages;
    private bool _disposed;

    /// <param name="volume">What the icon and tooltip show, and what the gestures move.</param>
    /// <param name="settings">Where the scroll gesture's step comes from.</param>
    /// <param name="dispatch">
    /// Posts to the UI thread. Windows raises theme changes on a thread of
    /// its own, and the tray icon may only be touched from the one that made
    /// it.
    /// </param>
    internal TrayIconController(VolumeViewModel volume, SettingsService settings, Action<Action> dispatch)
    {
        _volume = volume;
        _settings = settings;
        _dispatch = dispatch;

        var configure = new MenuFlyoutItem
        {
            Text = "Settings",
            Command = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty)),
        };

        var exit = new MenuFlyoutItem
        {
            Text = "Exit",
            Command = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty)),
        };

        var menu = new MenuFlyout();
        menu.Items.Add(configure);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exit);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = _volume.TrayToolTip,
            ContextFlyout = menu,

            // A native Win32 popup menu rather than a XAML flyout: with no
            // window there is no XamlRoot for a flyout to be shown in.
            ContextMenuMode = ContextMenuMode.PopupMenu,

            // Without this a left click waits out the double-click interval
            // before the flyout appears.
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(() => Clicked?.Invoke(this, EventArgs.Empty)),

            // The Plasma applet's other gesture, and the one every other tray
            // volume control has: the middle button is mute.
            MiddleClickCommand = new RelayCommand(_volume.ToggleMute),
        };

        DrawIcon();

        _volume.PropertyChanged += OnVolumeChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Left click on the icon.</summary>
    internal event EventHandler? Clicked;

    /// <summary>Settings chosen from the right-click menu.</summary>
    internal event EventHandler? SettingsRequested;

    /// <summary>Exit chosen from the right-click menu.</summary>
    internal event EventHandler? ExitRequested;

    /// <summary>Puts the icon in the notification area.</summary>
    internal void Show()
    {
        // false: efficiency mode would have Windows throttle us, and awoo has
        // to keep up with volume changes made elsewhere.
        _taskbarIcon.ForceCreate(false);

        // Only now is there a window for raw input to arrive at and an icon
        // for the shell to give a rectangle for, so the wheel is wired up
        // here rather than in the constructor.
        var icon = _taskbarIcon.TrayIcon;
        _wheel = new TrayWheel(icon.WindowHandle, icon.Id, OnWheelNotches);

        _messages = icon.MessageWindow;
        _messages.MouseEventReceived += OnTrayMouseEvent;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _volume.PropertyChanged -= OnVolumeChanged;

        if (_messages is not null)
        {
            _messages.MouseEventReceived -= OnTrayMouseEvent;
            _messages = null;
        }

        // Before the taskbar icon: the subclass it undoes is on that icon's
        // own window.
        _wheel?.Dispose();
        _wheel = null;

        _taskbarIcon.Dispose();
        _icon?.Dispose();
        _icon = null;
    }

    /// <summary>One notch of the wheel is one step of the configured size.</summary>
    private void OnWheelNotches(int notches) => _volume.Step(notches * _settings.VolumeStep);

    private void OnTrayMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs args)
    {
        // The shell reports the pointer moving over the icon, which is the
        // only notice anything gets that the wheel is now over it.
        if (args.MouseEvent == MouseEvent.MouseMove)
        {
            _wheel?.PointerOverIcon(args.Point.X, args.Point.Y);
        }
    }

    private void OnVolumeChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(VolumeViewModel.Glyph):
                DrawIcon();
                break;
            case nameof(VolumeViewModel.TrayToolTip):
                _taskbarIcon.ToolTipText = _volume.TrayToolTip;
                break;
        }
    }

    /// <summary>
    /// Light and dark taskbars need different icons, and the theme can change
    /// while awoo is running.
    /// </summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs args)
    {
        if (args.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        Redraw();
    }

    /// <summary>
    /// The icon is drawn at the size the taskbar's monitor asks for, and a
    /// scaling change — or the taskbar moving to a monitor scaled otherwise —
    /// changes that size. Drawn at the old one it would be the shell scaling
    /// it, which is the blur the size was chosen to avoid.
    /// </summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs args) => Redraw();

    /// <summary>
    /// Redraws on the UI thread. Windows raises these on threads of its own,
    /// and the tray icon may only be touched from the one that made it.
    /// </summary>
    private void Redraw() => _dispatch(() =>
    {
        if (!_disposed)
        {
            DrawIcon();
        }
    });

    private void DrawIcon()
    {
        var drawn = TrayIconRenderer.Render(_volume.Level);
        var previous = _icon;

        _icon = drawn;
        _taskbarIcon.Icon = drawn.Icon;

        // Only once the tray has the new one: disposing it destroys the HICON.
        previous?.Dispose();
    }
}
