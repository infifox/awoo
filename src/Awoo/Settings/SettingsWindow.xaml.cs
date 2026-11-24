using Awoo.Audio;
using Awoo.Interop;
using Awoo.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    /// <summary>
    /// How much room the device list leaves beside its rows for its own
    /// scrollbar. WinUI's vertical ScrollBar is <c>ScrollBarSize</c> wide —
    /// 16 — inside a one-pixel margin either side, and the ScrollViewer
    /// template draws it over the content rather than beside it, which would
    /// have it sitting on the switches. The flyout's tabs do the same.
    /// </summary>
    private const double ScrollGutterDips = 18;

    private readonly IntPtr _hwnd;

    private bool _refitQueued;

    internal SettingsWindow(SettingsService settings, IAudioService audio)
    {
        // Assigned before InitializeComponent: x:Bind reads the properties as
        // the generated bindings are built.
        ViewModel = new SettingsViewModel(settings, audio);
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

        // A device plugged in or pulled out while the window is open adds or
        // removes a row, and the window is sized to its content.
        ViewModel.Outputs.CollectionChanged += (_, _) => QueueRefit();
        ViewModel.Inputs.CollectionChanged += (_, _) => QueueRefit();

        if (Content is FrameworkElement content)
        {
            // The measurement above is taken before anything has been laid
            // out, so the device rows an ItemsRepeater makes are not there
            // yet to be measured. This is the pass that has them.
            content.Loaded += (_, _) => Place();
        }

        // Nothing here outlives the window: the view model is listening to
        // the audio service, and a closed window's rows would be kept alive
        // by that alone.
        Closed += (_, _) => ViewModel.Dispose();
    }

    /// <summary>The settings, and the devices, as the window shows them.</summary>
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
    private void Place() => Place(centre: true);

    /// <summary>
    /// The same, except that <paramref name="centre"/> false leaves the
    /// window where the user has it and only keeps it inside the monitor it
    /// is already on — what a window that has grown a row while open wants,
    /// rather than being gathered back to the middle of wherever the pointer
    /// happens to be.
    /// </summary>
    private void Place(bool centre)
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

            // The monitor the pointer is on for a window being opened, and
            // the one the window is already on for a window being refitted.
            var work = (centre
                ? DisplayArea.GetFromPoint(CursorPosition(), DisplayAreaFallback.Nearest)
                : DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)).WorkArea;

            AppWindow.Move(centre
                ? Centred(work, AppWindow.Size)
                : KeptInside(work, AppWindow.Size, AppWindow.Position));
        }
    }

    /// <summary>
    /// Re-sizes the window after its device list has changed shape. Queued
    /// rather than immediate, the way the flyout's relayout is: the row a
    /// hot-plug just added has to be through a layout pass before the content
    /// can be measured.
    /// </summary>
    private void QueueRefit()
    {
        if (_refitQueued)
        {
            return;
        }

        _refitQueued = DispatcherQueue.TryEnqueue(() =>
        {
            _refitQueued = false;
            Place(centre: false);
        });
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

        if (content.IsLoaded)
        {
            // Laid out before it is measured: the device rows are an
            // ItemsRepeater's, and one it has not realised yet is not there
            // to be measured.
            content.UpdateLayout();
        }

        content.Measure(new Windows.Foundation.Size(WidthDips, double.PositiveInfinity));
        return Math.Max(content.DesiredSize.Height, MinHeightDips);
    }

    /// <summary>
    /// Gives the device list's scrollbar room of its own beside the rows, and
    /// only while there are rows enough to scroll — a list that fits keeps the
    /// window's padding even on both sides. The list's height is pinned to its
    /// MaxHeight exactly when it is scrollable, so its own resize is when this
    /// changes.
    /// </summary>
    private void OnDeviceListSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (sender is not ScrollViewer list || list.Content is not FrameworkElement rows)
        {
            return;
        }

        // Measured against what the rows wanted rather than against
        // ScrollableHeight, which the ScrollViewer may not have published yet
        // at the point its own size change is announced.
        var gutter = rows.DesiredSize.Height > args.NewSize.Height ? ScrollGutterDips : 0;

        if (Math.Abs(list.Padding.Right - gutter) > 0.5)
        {
            list.Padding = new Thickness(0, 0, gutter, 0);
        }
    }

    private double Scale()
    {
        var dpi = NativeMethods.GetDpiForWindow(_hwnd);
        return (dpi == 0 ? 96.0 : dpi) / 96.0;
    }

    /// <summary>The middle of one monitor's working area.</summary>
    private static PointInt32 Centred(RectInt32 work, SizeInt32 window) =>
        new(
            work.X + Math.Max((work.Width - window.Width) / 2, 0),
            work.Y + Math.Max((work.Height - window.Height) / 2, 0));

    /// <summary>
    /// Where the window already is, moved no further than it takes to keep it
    /// inside the working area — a window that has just grown a row should not
    /// grow off the bottom of the screen.
    /// </summary>
    private static PointInt32 KeptInside(RectInt32 work, SizeInt32 window, PointInt32 position) =>
        new(
            Math.Clamp(position.X, work.X, Math.Max(work.X, work.X + work.Width - window.Width)),
            Math.Clamp(position.Y, work.Y, Math.Max(work.Y, work.Y + work.Height - window.Height)));

    private static PointInt32 CursorPosition() =>
        NativeMethods.GetCursorPos(out var point)
            ? new PointInt32(point.X, point.Y)
            : new PointInt32(0, 0);
}
