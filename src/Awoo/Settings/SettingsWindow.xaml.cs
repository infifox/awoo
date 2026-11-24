using Awoo.Interop;
using Awoo.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Awoo.Settings;

/// <summary>
/// awoo's settings: an ordinary window, with a title bar and a taskbar
/// button, opened from the tray icon's menu. Unlike the flyout it is closed
/// rather than hidden — there is nothing in it worth keeping alive, and the
/// tray menu opens another in no time.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const int WidthDips = 480;

    /// <summary>A floor, for the case the content has not been measured yet.</summary>
    private const int MinHeightDips = 120;

    private readonly IntPtr _hwnd;

    internal SettingsWindow(SettingsService settings)
    {
        // Assigned before InitializeComponent: x:Bind reads the properties as
        // the generated bindings are built.
        ViewModel = new SettingsViewModel(settings);
        InitializeComponent();

        // For a Window root the XAML compiler wires x:Bind's first evaluation
        // to Activated, and a window Windows declines to activate would come
        // up empty. Evaluating here is what makes the controls independent of
        // ever getting focus — the same reason the flyout does it.
        Bindings.Update();

        Title = "awoo settings";
        _hwnd = WindowNative.GetWindowHandle(this);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // Sized to its content, so there is nothing to gain by dragging
            // its edges.
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        SetIcon();
        Place();
    }

    /// <summary>The two settings, as the window shows them.</summary>
    internal SettingsViewModel ViewModel { get; }

    /// <summary>
    /// Brings the window up, whether it is new or was already open behind
    /// something. A tray application rarely holds the foreground, and
    /// <see cref="Window.Activate"/> alone does not always take it.
    /// </summary>
    public void Reveal()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        AppWindow.Show(activateWindow: true);
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    /// <summary>
    /// awoo's own icon in the title bar and on the taskbar button. An
    /// unpackaged app gets the executable's icon for free in some places and
    /// not in this one.
    /// </summary>
    private void SetIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "awoo.ico");
        if (File.Exists(path))
        {
            AppWindow.SetIcon(path);
        }
    }

    /// <summary>
    /// Sizes the window to its content and centres it on the monitor the
    /// pointer is on. Twice, for the reason the flyout does it twice: the
    /// first move is what puts the window on that monitor, and only then does
    /// it report that monitor's scaling, which the size depends on.
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

            // ResizeClient rather than Resize: what has to fit is the
            // content, and this window does have a title bar for the frame to
            // account for.
            AppWindow.ResizeClient(new SizeInt32(
                (int)Math.Round(WidthDips * scale),
                (int)Math.Round(ContentHeightDips() * scale)));

            var work = DisplayArea.GetFromPoint(CursorPosition(), DisplayAreaFallback.Nearest).WorkArea;
            var outer = AppWindow.Size;

            AppWindow.Move(new PointInt32(
                work.X + Math.Max((work.Width - outer.Width) / 2, 0),
                work.Y + Math.Max((work.Height - outer.Height) / 2, 0)));
        }
    }

    /// <summary>
    /// How tall the content wants to be at <see cref="WidthDips"/>. Measured
    /// rather than declared, so a setting added here does not also have to be
    /// added to a constant somebody forgets.
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

    private static PointInt32 CursorPosition() =>
        NativeMethods.GetCursorPos(out var point)
            ? new PointInt32(point.X, point.Y)
            : new PointInt32(0, 0);
}
