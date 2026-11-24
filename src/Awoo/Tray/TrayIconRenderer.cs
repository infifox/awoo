using System.Drawing;
using System.Drawing.Drawing2D;
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

        // The glyphs sit inside the font's em box with room around them, so
        // the em size goes slightly past the bitmap to fill it the way the
        // shell's own icons do.
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font(Font, size * 0.86f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(colour))
        using (var format = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        })
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Not ClearType: sub-pixel rendering has no meaning over alpha.
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            graphics.DrawString(
                VolumeGlyph.For(level),
                font,
                brush,
                new RectangleF(0, 0, size, size),
                format);
        }

        // Icon.FromHandle does not take ownership of the handle, so the
        // handle has to be carried alongside the icon and destroyed with it.
        var handle = bitmap.GetHicon();
        return new RenderedIcon(Icon.FromHandle(handle), handle);
    }

    /// <summary>The tray's own small-icon size, which is already DPI-scaled.</summary>
    private static int IconSize()
    {
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        return width > 0 ? width : 16;
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
