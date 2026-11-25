using System.Drawing;
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
    /// What the glyph is drawn in on a dark taskbar and on a light one. Flat
    /// out rather than slightly transparent, which is what the tray's own
    /// icons measure at and part of why they read heavier.
    /// </summary>
    private static readonly Color DarkTaskbar = Color.FromArgb(255, 255, 255, 255);

    private static readonly Color LightTaskbar = Color.FromArgb(255, 0, 0, 0);

    /// <summary>
    /// How far the ghosted waves carry: a quarter of the way from the
    /// taskbar to the glyph, measured off the tray's own speaker. One number
    /// for both themes, because an alpha this low lands the same distance
    /// from a dark background as from a light one. A constant rather than a
    /// setting, deliberately.
    /// </summary>
    private const int GhostAlpha = 60;

    /// <summary>
    /// An icon for <paramref name="level"/>, sized for the notification area.
    /// The caller owns it and must dispose it.
    /// </summary>
    internal static RenderedIcon Render(VolumeLevel level)
    {
        var colour = TaskbarIsLight() ? LightTaskbar : DarkTaskbar;

        using var bitmap = TrayIconPainter.Paint(
            VolumeGlyph.For(level),
            GhostFor(level),
            IconSize(),
            Font,
            colour,
            Color.FromArgb(GhostAlpha, colour));

        // Icon.FromHandle does not take ownership of the handle, so the
        // handle has to be carried alongside the icon and destroyed with it.
        var handle = bitmap.GetHicon();
        return new RenderedIcon(Icon.FromHandle(handle), handle);
    }

    /// <summary>
    /// What is drawn faintly behind the level's own speaker: every wave, so
    /// the ones above this level show as present and off, which is how
    /// Windows draws them.
    /// </summary>
    /// <remarks>
    /// Nothing for mute, which Windows draws with no waves at all; and
    /// nothing at full volume either, where the speaker already has every
    /// wave and a ghost of the same outline would only fringe its edges.
    /// </remarks>
    private static string? GhostFor(VolumeLevel level) =>
        level is VolumeLevel.Muted or VolumeLevel.High ? null : VolumeGlyph.For(VolumeLevel.High);

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
