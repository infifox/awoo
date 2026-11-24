using System.ComponentModel;
using System.Diagnostics;
using Awoo.Interop;
using Awoo.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace Awoo.Flyout;

/// <summary>
/// The tray flyout: a borderless, rounded, acrylic window that lives by the
/// notification area. It is created once at startup and then shown and hidden
/// — never reopened — so the only thing that ends it is Exit.
/// </summary>
public sealed partial class FlyoutWindow : Window
{
    private const int WidthDips = 400;

    /// <summary>A floor, for the case the content has not been measured yet.</summary>
    private const int MinHeightDips = 72;

    /// <summary>
    /// A click on the tray icon deactivates the flyout before the click
    /// handler runs. Inside this window, a toggle is read as "that click is
    /// what closed it" rather than as a request to open it again.
    /// </summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly IntPtr _hwnd;

    private DateTimeOffset _hiddenAt = DateTimeOffset.MinValue;
    private bool _exiting;
    private bool _relayoutQueued;

    internal FlyoutWindow(DevicesViewModel devices, AppsViewModel apps)
    {
        // Assigned before InitializeComponent: x:Bind reads the properties as
        // the generated bindings are built.
        Devices = devices;
        Apps = apps;
        InitializeComponent();

        // For a Window root the XAML compiler wires x:Bind's first evaluation
        // to Activated rather than Loading, so a flyout Windows declines to
        // activate — focus-stealing prevention, a locked session — would come
        // up empty. Evaluating here is what makes the two lists independent
        // of ever getting focus.
        Bindings.Update();

        Title = "awoo";
        _hwnd = WindowNative.GetWindowHandle(this);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // A border with no caption: the frame is what DWM rounds, and
            // dropping the title bar is what makes it read as a flyout.
            presenter.SetBorderAndTitleBar(true, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Tray-only: no taskbar button, no Alt+Tab entry.
        AppWindow.IsShownInSwitchers = false;
        RoundCorners();

        Activated += OnActivated;
        AppWindow.Closing += OnClosing;

        // A device plugged in or pulled out, or an app starting or stopping,
        // changes how tall the flyout has to be, and either can happen while
        // it is open.
        Devices.Outputs.CollectionChanged += (_, _) => QueueRelayout();
        Devices.Inputs.CollectionChanged += (_, _) => QueueRelayout();
        Apps.Playback.CollectionChanged += (_, _) => QueueRelayout();
        Apps.Recording.CollectionChanged += (_, _) => QueueRelayout();
    }

    /// <summary>Every active endpoint, as the Devices tab lists them.</summary>
    internal DevicesViewModel Devices { get; }

    /// <summary>Everything playing or recording, as the Applications tab lists it.</summary>
    internal AppsViewModel Apps { get; }

    /// <summary>Shows the flyout if it is hidden, hides it if it is showing.</summary>
    public void Toggle()
    {
        if (AppWindow.IsVisible || DateTimeOffset.UtcNow - _hiddenAt < ReopenGuard)
        {
            Hide();
            return;
        }

        Show();
    }

    /// <summary>Closes for real; everything else only hides.</summary>
    public void CloseForExit()
    {
        _exiting = true;
        Close();
    }

    private void Show()
    {
        // Where an application's sound goes is the one thing nothing reports
        // changing — not awoo's own write, and not one made in Windows'
        // Settings — so it is re-read as the flyout opens, which is the
        // moment a stale selector would be seen. Before Place, because a
        // route's device name is part of how wide a row wants to be.
        Apps.Refresh();

        Place();
        AppWindow.Show(activateWindow: true);
        Activate();

        // Activation from a tray click does not always carry the foreground,
        // and without it the window never deactivates — so never dismisses.
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    private void Hide()
    {
        if (!AppWindow.IsVisible)
        {
            return;
        }

        AppWindow.Hide();
        _hiddenAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sizes and positions the flyout. Done twice because the first move is
    /// what puts the window on the target monitor, and only then does it
    /// report that monitor's DPI — which the size depends on.
    /// </summary>
    private void Place()
    {
        var scale = 0.0;

        for (var pass = 0; pass < 2; pass++)
        {
            var next = Scale();
            if (Math.Abs(next - scale) < 0.001)
            {
                break;
            }

            scale = next;

            // What has to fit is the content; the frame goes on top of it.
            // AppWindow.ResizeClient would do this, but it adds room for a
            // title bar this window does not have.
            var frame = FrameExtra();
            var size = new SizeInt32(
                (int)Math.Round(WidthDips * scale) + frame.Width,
                (int)Math.Round(ContentHeightDips() * scale) + frame.Height);

            AppWindow.Resize(size);
            AppWindow.Move(FlyoutPlacement.For(size, scale));
        }
    }

    /// <summary>
    /// How much bigger the window is than the content inside it. Measured
    /// rather than assumed: this window has a border and no title bar, and
    /// the border is a different thickness at every scaling factor.
    /// </summary>
    private SizeInt32 FrameExtra()
    {
        if (!NativeMethods.GetWindowRect(_hwnd, out var window) ||
            !NativeMethods.GetClientRect(_hwnd, out var client))
        {
            return new SizeInt32(0, 0);
        }

        return new SizeInt32(
            (window.Right - window.Left) - (client.Right - client.Left),
            (window.Bottom - window.Top) - (client.Bottom - client.Top));
    }

    /// <summary>
    /// How tall the content wants to be at <see cref="WidthDips"/>. Measured
    /// rather than declared, so a row added to the flyout does not also have
    /// to be added to a constant that somebody forgets.
    /// </summary>
    private double ContentHeightDips()
    {
        if (Content is not FrameworkElement content)
        {
            return MinHeightDips;
        }

        content.Measure(new Windows.Foundation.Size(WidthDips, double.PositiveInfinity));
        return Math.Max(content.DesiredSize.Height, MinHeightDips);
    }

    private double Scale()
    {
        var dpi = NativeMethods.GetDpiForWindow(_hwnd);
        return (dpi == 0 ? 96.0 : dpi) / 96.0;
    }

    private void RoundCorners()
    {
        var preference = NativeMethods.DWMWCP_ROUND;
        _ = NativeMethods.DwmSetWindowAttribute(
            _hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference,
            sizeof(int));
    }

    /// <summary>
    /// Re-sizes and re-anchors the flyout after its content has changed
    /// shape, which only matters while it is on screen — the next
    /// <see cref="Show"/> measures it anyway.
    /// </summary>
    private void QueueRelayout()
    {
        if (_relayoutQueued || _exiting)
        {
            return;
        }

        // Queued rather than immediate: the rows a hot-plug just added have
        // to be through a layout pass before the content can be measured.
        _relayoutQueued = DispatcherQueue.TryEnqueue(() =>
        {
            _relayoutQueued = false;

            if (!_exiting && AppWindow.IsVisible)
            {
                Place();
            }
        });
    }

    /// <summary>
    /// Gives the switcher's two items half the flyout's width each.
    /// SelectorBar stacks its items at the width their own text wants, so
    /// equal halves are not something the markup can ask for; the control
    /// itself is stretched, so what it is arranged at is the whole of what
    /// there is to divide.
    /// </summary>
    private void OnTabsSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var half = args.NewSize.Width / 2;

        if (half <= 0 || half == DevicesTabItem.Width)
        {
            return;
        }

        DevicesTabItem.Width = half;
        ApplicationsTabItem.Width = half;
    }

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var showDevices = sender.SelectedItem == DevicesTabItem;

        DevicesTab.Visibility = showDevices ? Visibility.Visible : Visibility.Collapsed;
        ApplicationsTab.Visibility = showDevices ? Visibility.Collapsed : Visibility.Visible;

        QueueRelayout();
    }

    /// <summary>
    /// Windows' own sound settings, which is where everything awoo does not
    /// do lives — per-app defaults, enhancements, formats, troubleshooting.
    /// The Plasma applet has the same link at the foot of its flyout.
    /// </summary>
    private void OnSoundSettingsClick(object sender, RoutedEventArgs args)
    {
        // Dismissed first, because the flyout has done its part and the
        // Settings app is about to take the foreground anyway.
        Hide();

        try
        {
            // UseShellExecute, because this is a URI for the shell to resolve
            // rather than a program to run.
            using var _ = Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // No Settings app to open: an install without it, or a policy
            // that blocks it. Nothing to do but leave it closed.
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Losing focus dismisses it, the way Windows' own quick settings does.
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Hide();
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting)
        {
            return;
        }

        // Alt+F4 dismisses the flyout; it does not quit awoo.
        args.Cancel = true;
        Hide();
    }
}
