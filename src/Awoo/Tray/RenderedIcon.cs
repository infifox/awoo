using System.Drawing;
using Awoo.Interop;

namespace Awoo.Tray;

/// <summary>
/// An icon drawn at runtime, together with the HICON it was drawn into.
/// <see cref="Icon.FromHandle"/> borrows a handle rather than adopting it, so
/// something has to hold the handle until the icon is finished with — this is
/// that something.
/// </summary>
internal sealed class RenderedIcon(Icon icon, IntPtr handle) : IDisposable
{
    private IntPtr _handle = handle;

    internal Icon Icon { get; } = icon;

    public void Dispose()
    {
        Icon.Dispose();

        if (_handle != IntPtr.Zero)
        {
            _ = NativeMethods.DestroyIcon(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
