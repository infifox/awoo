using Awoo.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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
    private const int WidthDips = 360;
    private const int HeightDips = 180;

    /// <summary>
    /// A click on the tray icon deactivates the flyout before the click
    /// handler runs. Inside this window, a toggle is read as "that click is
    /// what closed it" rather than as a request to open it again.
    /// </summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly IntPtr _hwnd;

    private DateTimeOffset _hiddenAt = DateTimeOffset.MinValue;
    private bool _exiting;

    public FlyoutWindow()
    {
        InitializeComponent();

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
    }

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
            var size = new SizeInt32(
                (int)Math.Round(WidthDips * scale),
                (int)Math.Round(HeightDips * scale));

            AppWindow.Resize(size);
            AppWindow.Move(FlyoutPlacement.For(size, scale));
        }
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
