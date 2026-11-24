# awoo

A system-tray volume control for Windows 11, built to work the way the
KDE Plasma 6.4 volume applet does — in native Fluent clothing.

Early days. The tray icon and its flyout are standing: the header drives the
default output device for real, the Devices tab lists every output and input
with its own volume, mute and one-click set-as-default, and the Applications
tab gives everything playing or recording a slider of its own. Per-app device
routing comes next.

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

Under the header are two tabs, the way the Plasma applet has them. **Devices**
lists every active endpoint, outputs then inputs, each with its own slider,
percentage and mute — microphones exactly like speakers. The marker beside a
device's name is both which one Windows is using and the way to change it:
selecting it makes that device the default for all three of Windows' roles
(playback, multimedia and calls) on the spot, and the header moves onto it.
Plugging a device in or pulling it out adds or removes its row while the
flyout is open.

**Applications** is the same idea one level down: a row for every application
playing, and under them a row for every application recording, each with its
own icon, name, slider and mute. The volume there is the app's own, the one
Windows' volume mixer moves, so a change made in either shows up in the other
straight away. Several streams from one program — a browser with sound in two
tabs — are one row between them, as they are in the mixer. Windows' own
notification sounds get a row like anything else.

A row is there while the application is actually playing or recording, the way
the Plasma applet's is rather than the way the Windows mixer's is: start
something and its row appears, stop it and the row goes a moment later. When
nothing at all is making or taking sound, the tab says so.

Switching the default device goes through `IPolicyConfig`, which Windows does
not document — the same route EarTrumpet and SoundSwitch take. Where a build
of Windows will not hand it over, the markers stay as read-only indicators of
which device is in use and nothing else in awoo is affected.

Windows 11 files a newly registered tray icon under *Show hidden icons*. Drag
awoo out of there to keep it on the taskbar.
