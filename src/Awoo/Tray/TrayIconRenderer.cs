using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Awoo.Audio;
using Awoo.Interop;
using Awoo.ViewModels;
using Microsoft.Win32;

namespace Awoo.Tray;

/// <summary>
/// Draws the tray icon from the same icon font the flyout uses, rather than
/// shipping one <c>.ico</c> per volume level.
/// </summary>
/// <remarks>
/// A rendered glyph is what lets the icon follow the volume live and pick its
/// own colour: the notification area has no theme of its own to ask, so the
/// icon has to be the right one for a light or a dark taskbar, and that can
/// change under us.
/// </remarks>
internal static class TrayIconRenderer
{
    /// <summary>
    /// Whichever icon font this Windows has. Looked up once: an absent family
    /// is substituted silently by GDI+, which would draw letters instead of
    /// speakers.
    /// </summary>
    private static readonly string Font = ResolveFont();

    /// <summary>
    /// An icon for <paramref name="level"/>, sized for the notification area.
    /// The caller owns it and must dispose it.
    /// </summary>
    internal static RenderedIcon Render(VolumeLevel level)
    {
        var size = IconSize();
        var colour = TaskbarIsLight() ? Color.FromArgb(230, 0, 0, 0) : Color.FromArgb(240, 255, 255, 255);

        // Every glyph of this font is exactly em-square: its advance width,
        // its line height and its cell ascent are all one em, and the cell
        // is what the designer drew the icon to fill. So an em of exactly
        // the bitmap's size, drawn from the origin, puts the glyph's own
        // grid over the bitmap's with no fractional offset left anywhere —
        // which is the whole of making it crisp. Scaling the em to a
        // fraction of the bitmap instead lands every outline between
        // pixels, and that is what the icon used to be soft from.
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font(Font, size, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(colour))

        // Typographic rather than the default format, which pads a string
        // with the font's overhang allowance and would push the glyph back
        // off that grid.
        using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
        {
            format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
            format.Alignment = StringAlignment.Near;
            format.LineAlignment = StringAlignment.Near;

            graphics.Clear(Color.Transparent);

            // Not ClearType: sub-pixel rendering has no meaning over alpha.
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            graphics.DrawString(VolumeGlyph.For(level), font, brush, PointF.Empty, format);
        }

        // Icon.FromHandle does not take ownership of the handle, so the
        // handle has to be carried alongside the icon and destroyed with it.
        var handle = bitmap.GetHicon();
        return new RenderedIcon(Icon.FromHandle(handle), handle);
    }

    /// <summary>
    /// The size the notification area draws an icon at: the small-icon metric
    /// under the DPI of whichever monitor the taskbar is on. Hand the shell
    /// any other size and it scales the icon to this one, which is blur
    /// nothing done inside the bitmap can undo.
    /// </summary>
    private static int IconSize()
    {
        var width = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXSMICON, TaskbarDpi());
        if (width <= 0)
        {
            width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        }

        return width > 0 ? width : 16;
    }

    /// <summary>
    /// The DPI of the monitor holding the taskbar, which is not the system
    /// DPI once a second monitor is scaled differently — and it is the
    /// taskbar's monitor the icon is drawn on.
    /// </summary>
    private static uint TaskbarDpi()
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero)
        {
            var monitor = NativeMethods.MonitorFromWindow(taskbar, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (monitor != IntPtr.Zero &&
                NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 &&
                dpi > 0)
            {
                return dpi;
            }
        }

        var system = NativeMethods.GetDpiForSystem();
        return system > 0 ? system : 96;
    }

    /// <summary>
    /// Whether the taskbar is light. This is the system theme rather than the
    /// app theme — Windows lets them differ, and the icon sits on the taskbar.
    /// </summary>
    private static bool TaskbarIsLight()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme",
                0);

            return value is int flag && flag != 0;
        }
        catch (Exception)
        {
            // No such value on an older build: dark is the Windows 11 default.
            return false;
        }
    }

    private static string ResolveFont()
    {
        foreach (var candidate in new[] { VolumeGlyph.FontFamily, VolumeGlyph.FallbackFontFamily })
        {
            try
            {
                using var family = new FontFamily(candidate);
                return candidate;
            }
            catch (ArgumentException)
            {
                // Not installed; try the next.
            }
        }

        return VolumeGlyph.FallbackFontFamily;
    }
}
