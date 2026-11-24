using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Awoo.Interop;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Awoo.ViewModels;

/// <summary>
/// Carries the icon an audio session points at from GDI, which is what can
/// extract one from a file, into XAML, which is what can draw it. The two
/// share no image type.
/// </summary>
/// <remarks>
/// <para>
/// Results are remembered by reference, because the Applications tab rebuilds
/// its rows whenever anything about any application changes and the same
/// handful of executables come round again each time. A miss is remembered
/// too: an icon that could not be extracted will not extract next time
/// either, and the row has a glyph to fall back on.
/// </para>
/// <para>
/// Touched only from the thread the audio service dispatches onto, which is
/// also the UI thread — the cache needs no lock because nothing else reaches
/// it.
/// </para>
/// </remarks>
internal static class AppIconLoader
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The icon in <paramref name="iconFile"/>, or null where there is none
    /// to be had.
    /// </summary>
    internal static ImageSource? Load(string? iconFile)
    {
        if (string.IsNullOrWhiteSpace(iconFile))
        {
            return null;
        }

        if (Cache.TryGetValue(iconFile, out var known))
        {
            return known;
        }

        var loaded = Convert(iconFile);
        Cache[iconFile] = loaded;
        return loaded;
    }

    private static ImageSource? Convert(string iconFile)
    {
        using var bitmap = IconFile.Read(iconFile);
        if (bitmap is null)
        {
            return null;
        }

        try
        {
            return ToBitmapSource(bitmap);
        }
        catch (Exception e) when (e is ArgumentException or ExternalException)
        {
            // GDI would not hand the pixels over. One row without its icon.
            return null;
        }
    }

    /// <summary>
    /// Copies a bitmap's pixels into a XAML one. Both sides are 32-bit BGRA,
    /// but XAML wants it premultiplied, which is what asking GDI+ to lock the
    /// bits as <c>Format32bppPArgb</c> produces.
    /// </summary>
    private static ImageSource ToBitmapSource(Bitmap bitmap)
    {
        var target = new WriteableBitmap(bitmap.Width, bitmap.Height);
        var locked = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);

        try
        {
            var row = new byte[bitmap.Width * 4];
            using var pixels = target.PixelBuffer.AsStream();

            // Row by row rather than in one go: a locked bitmap's stride is
            // its own business and need not be the width times four.
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(locked.Scan0 + (y * locked.Stride), row, 0, row.Length);
                pixels.Write(row, 0, row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(locked);
        }

        target.Invalidate();
        return target;
    }
}
