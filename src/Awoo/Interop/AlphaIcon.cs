using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Awoo.Interop;

/// <summary>
/// Turns a drawn bitmap into an HICON that reaches the screen at the strength
/// it was drawn at.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Bitmap.GetHicon"/> does not, and the tray is where it shows.
/// The icon it builds is sound in itself — a full alpha channel, nothing
/// thresholded — but its colours come out premultiplied by that alpha, and
/// the notification area multiplies them by it again on its way to the
/// screen. So everything partly transparent arrives squared.
/// </para>
/// <para>
/// Read off the taskbar rather than worked out: where the painted bitmap
/// holds an edge at 0.55 coverage and ghosted waves at 0.235, the icon on the
/// taskbar measured 0.28 and 0.03 — which is 0.55² and 0.235², to the second
/// decimal, and 0.03 is to say gone. That is why awoo's speaker read thinner
/// than the one Windows drew beside it, and why its waves could not be seen
/// at all. Giving the notification area the colours unscaled lets its own
/// multiplication be the only one, and what it draws is then what was
/// painted.
/// </para>
/// <para>
/// So the icon is built here instead: a 32-bit top-down DIB holding the
/// bitmap's own pixels, and the blank mask that a 32-bit icon does not
/// consult and <c>CreateIconIndirect</c> will not do without.
/// </para>
/// </remarks>
internal static class AlphaIcon
{
    /// <summary>
    /// An icon of <paramref name="bitmap"/> that keeps its strength. The
    /// caller owns the handle and must destroy it. <see cref="IntPtr.Zero"/>
    /// where GDI would not make one, which callers answer by falling back
    /// rather than by showing no icon.
    /// </summary>
    internal static IntPtr From(Bitmap bitmap)
    {
        var header = new NativeMethods.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
            biWidth = bitmap.Width,

            // Negative: the rows go in the order they are read out of the
            // bitmap rather than bottom-up, which is how a DIB is otherwise
            // laid out.
            biHeight = -bitmap.Height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = NativeMethods.BI_RGB,
        };

        var colour = NativeMethods.CreateDIBSection(
            IntPtr.Zero, ref header, NativeMethods.DIB_RGB_COLORS, out var pixels, IntPtr.Zero, 0);
        if (colour == IntPtr.Zero || pixels == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var mask = IntPtr.Zero;
        try
        {
            Copy(bitmap, pixels);

            // Zeroed by hand: CreateBitmap leaves the bits it allocates as
            // they were, and a mask of whatever was in that memory would
            // knock holes in the icon wherever a 32-bit icon does consult it.
            // All-zero is "show every pixel".
            // A one-bit bitmap's rows are word-aligned, not byte-aligned.
            var stride = (bitmap.Width + 15) / 16 * 2;
            mask = NativeMethods.CreateBitmap(
                bitmap.Width, bitmap.Height, 1, 1, new byte[stride * bitmap.Height]);
            if (mask == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var info = new NativeMethods.ICONINFO
            {
                fIcon = 1,
                hbmMask = mask,
                hbmColor = colour,
            };

            // CreateIconIndirect copies both bitmaps, so they go in the
            // finally below whether or not it worked.
            return NativeMethods.CreateIconIndirect(ref info);
        }
        finally
        {
            if (mask != IntPtr.Zero)
            {
                _ = NativeMethods.DeleteObject(mask);
            }

            _ = NativeMethods.DeleteObject(colour);
        }
    }

    /// <summary>
    /// Copies the bitmap's pixels into the DIB as they stand — colours
    /// unscaled, alpha alongside them. The scaling is what the shell does
    /// for itself; see the remarks above.
    /// </summary>
    private static unsafe void Copy(Bitmap bitmap, IntPtr destination)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            // A 32-bit DIB's rows are already four-byte aligned, so its stride
            // is the width and there is no padding to step over.
            var bytesPerRow = (long)bitmap.Width * 4;

            for (var y = 0; y < bitmap.Height; y++)
            {
                Buffer.MemoryCopy(
                    (byte*)data.Scan0 + ((long)y * data.Stride),
                    (byte*)destination + (y * bytesPerRow),
                    bytesPerRow,
                    bytesPerRow);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
