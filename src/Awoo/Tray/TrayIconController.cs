using System.Drawing;
using Awoo.Interop;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace Awoo.Tray;

/// <summary>
/// awoo's presence in the notification area: the icon, its tooltip, its
/// left-click and its right-click menu. Later tasks drive the glyph and
/// tooltip from the audio state; this one just puts it there.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;
    private Icon? _icon;

    public TrayIconController()
    {
        var exit = new MenuFlyoutItem
        {
            Text = "Exit",
            Command = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty)),
        };

        var menu = new MenuFlyout();
        menu.Items.Add(exit);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "awoo",
            ContextFlyout = menu,

            // A native Win32 popup menu rather than a XAML flyout: with no
            // window there is no XamlRoot for a flyout to be shown in.
            ContextMenuMode = ContextMenuMode.PopupMenu,

            // Without this a left click waits out the double-click interval
            // before the flyout appears.
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(() => Clicked?.Invoke(this, EventArgs.Empty)),
        };

        _icon = LoadIcon();
        if (_icon is not null)
        {
            _taskbarIcon.Icon = _icon;
        }
    }

    /// <summary>Left click on the icon.</summary>
    public event EventHandler? Clicked;

    /// <summary>Exit chosen from the right-click menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Puts the icon in the notification area.</summary>
    public void Show()
    {
        // false: efficiency mode would have Windows throttle us, and awoo has
        // to keep up with volume changes made elsewhere.
        _taskbarIcon.ForceCreate(false);
    }

    public void Dispose()
    {
        _taskbarIcon.Dispose();
        _icon?.Dispose();
        _icon = null;
    }

    /// <summary>
    /// The app icon at the tray's own small-icon size, loaded from beside the
    /// exe — an unpackaged app has no ms-appx:// to read assets from.
    /// </summary>
    private static Icon? LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "awoo.ico");
        if (!File.Exists(path))
        {
            return null;
        }

        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSMICON);
        if (width <= 0 || height <= 0)
        {
            return new Icon(path);
        }

        return new Icon(path, width, height);
    }
}
