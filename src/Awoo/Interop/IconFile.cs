using System.Drawing;

namespace Awoo.Interop;

/// <summary>
/// Reads an icon out of a file the way the shell does, given a reference of
/// the kind an audio session carries: an executable's path, or a resource
/// written <c>@%SystemRoot%\System32\AudioSrv.Dll,-203</c>.
/// </summary>
internal static class IconFile
{
    /// <summary>
    /// The size, in pixels, to ask the shell for. 32 covers a 20-dip row at
    /// 150% scaling.
    /// </summary>
    internal const int Pixels = 32;

    /// <summary>
    /// The icon in <paramref name="reference"/> as a bitmap the caller owns,
    /// or null where there is none to be had — no reference, a file that has
    /// gone, or a resource the shell will not extract.
    /// </summary>
    internal static Bitmap? Read(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var (file, index) = Split(reference);
        if (file.Length == 0)
        {
            return null;
        }

        // Both halves of the size are the same: nothing here wants the small
        // icon, but the call fills in both handles regardless.
        var sizes = Pixels | (Pixels << 16);

        if (NativeMethods.SHDefExtractIcon(file, index, 0, out var large, out var small, sizes) != 0)
        {
            Destroy(large);
            Destroy(small);
            return null;
        }

        var handle = large != IntPtr.Zero ? large : small;

        try
        {
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            using var icon = Icon.FromHandle(handle);
            return icon.ToBitmap();
        }
        catch (ArgumentException)
        {
            // Not an icon GDI will take. One row without its icon.
            return null;
        }
        finally
        {
            Destroy(large);
            Destroy(small);
        }
    }

    /// <summary>
    /// Splits an icon reference into the file and the resource index after
    /// it. The leading <c>@</c> some references carry is a marker for the
    /// shell's string loader rather than part of the path, and the path
    /// itself may be written with environment variables in it.
    /// </summary>
    internal static (string File, int Index) Split(string reference)
    {
        var text = reference.Trim().TrimStart('@');
        var comma = text.LastIndexOf(',');

        if (comma > 0 && int.TryParse(text[(comma + 1)..], out var index))
        {
            text = text[..comma];
        }
        else
        {
            index = 0;
        }

        return (Environment.ExpandEnvironmentVariables(text.Trim('"')), index);
    }

    private static void Destroy(IntPtr icon)
    {
        if (icon != IntPtr.Zero)
        {
            _ = NativeMethods.DestroyIcon(icon);
        }
    }
}
