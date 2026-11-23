# awoo

A system-tray volume control for Windows 11, built to work the way the
KDE Plasma 6.4 volume applet does — in native Fluent clothing.

Early days. The tray icon and its flyout are standing, and the flyout's header
drives the default output device's volume and mute for real. Device switching
and per-app volume come next.

## Building

Needs Windows 11. `build.ps1` finds a .NET 10 SDK, fetching a per-user one
into `.dotnet/` if the machine has none:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Run
```

Drop `-Run` to build without starting it, and add `-Configuration Release` for
a release build.

awoo is an unpackaged WinUI 3 app: the result is a plain `awoo.exe` with no
MSIX and no install step. It is tray-only — no main window and no taskbar
button. Left-click the speaker icon for the flyout; right-click it for Exit.

The tray icon is drawn from the system icon font, so it shows the volume level
and mute state as they change, and hovering it names the device and its
volume. The flyout's header is the same thing in full: device name, mute
toggle, slider and percentage, all of it the real system volume — change it
from Windows, a hardware key or another app and the header follows.

Windows 11 files a newly registered tray icon under *Show hidden icons*. Drag
awoo out of there to keep it on the taskbar.
