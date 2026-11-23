using System.ComponentModel;
using Awoo.ViewModels;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;

namespace Awoo.Tray;

/// <summary>
/// awoo's presence in the notification area: the icon, its tooltip, its
/// left-click and its right-click menu. The icon and tooltip are drawn from
/// the volume view model, so the tray says the same thing as the flyout.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    private readonly VolumeViewModel _volume;
    private readonly Action<Action> _dispatch;
    private readonly TaskbarIcon _taskbarIcon;

    private RenderedIcon? _icon;
    private bool _disposed;

    /// <param name="volume">What the icon and tooltip show.</param>
    /// <param name="dispatch">
    /// Posts to the UI thread. Windows raises theme changes on a thread of
    /// its own, and the tray icon may only be touched from the one that made
    /// it.
    /// </param>
    internal TrayIconController(VolumeViewModel volume, Action<Action> dispatch)
    {
        _volume = volume;
        _dispatch = dispatch;

        var exit = new MenuFlyoutItem
        {
            Text = "Exit",
            Command = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty)),
        };

        var menu = new MenuFlyout();
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
        };

        DrawIcon();

        _volume.PropertyChanged += OnVolumeChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Left click on the icon.</summary>
    internal event EventHandler? Clicked;

    /// <summary>Exit chosen from the right-click menu.</summary>
    internal event EventHandler? ExitRequested;

    /// <summary>Puts the icon in the notification area.</summary>
    internal void Show()
    {
        // false: efficiency mode would have Windows throttle us, and awoo has
        // to keep up with volume changes made elsewhere.
        _taskbarIcon.ForceCreate(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _volume.PropertyChanged -= OnVolumeChanged;

        _taskbarIcon.Dispose();
        _icon?.Dispose();
        _icon = null;
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

        _dispatch(() =>
        {
            if (!_disposed)
            {
                DrawIcon();
            }
        });
    }

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
