# awoo

A system-tray volume control for Windows 11, built to work the way the
KDE Plasma 6.4 volume applet does — in native Fluent clothing.

Early days. The tray icon and its flyout are standing: the header drives the
default output device for real, the Devices tab lists every output and input
with its own volume, mute and one-click set-as-default, and the Applications
tab gives everything playing or recording a slider of its own and a way to
send it to a different output. The icon takes the Plasma applet's gestures —
scroll to change the volume, middle-click to mute — and awoo starts with
Windows. An installer, CI and proper documentation come next.

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
button. Left-click the speaker icon for the flyout; right-click it for
Settings and Exit.

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

Beside a playing application's name is the output its sound goes to, which is
the Plasma applet's "move this app to another device" and the same thing
Windows buries in Settings → System → Sound → *App volume and device
preferences*. The button shows a speaker alone while the application follows
whichever device is the default, and names the device once it has been sent to
one of its own; opening it lists **Default** and every output to choose from.
Windows remembers the choice against the application rather than the process,
so it holds when the application is closed and reopened, and a choice made in
Settings shows here as the flyout opens — there is no notification for it, so
that is when awoo looks. Where the device an application was sent to is not
there any more, the button says so rather than naming nothing.

Recording rows have no selector: moving an application's microphone is not
something this version does.

Switching the default device goes through `IPolicyConfig`, and sending one
application's sound to a device goes through `IAudioPolicyConfig` — neither of
which Windows documents, and both the route EarTrumpet and SoundSwitch take.
Where a build of Windows will not hand one over, that much of awoo goes quiet
and nothing else is affected: the default markers stay as read-only
indicators of which device is in use, and the per-application selector is not
shown at all rather than offering a control that would do nothing.

Two things Windows itself decides, and awoo reports rather than argues with: a
device has to exist to be sent sound, and an application has to be making
sound to be moved. So an application whose chosen device has since been
unplugged keeps its route and says the device is unavailable, and a selector
that Windows declines snaps back to where the sound is actually going.

At the foot of the flyout, **More sound settings** opens Windows' own sound
page — the Plasma applet's *Configure Audio Devices…* under the name Windows'
own volume flyout gives the same link.

## Gestures

The tray icon takes the two gestures the Plasma applet has, beyond the
left-click that opens the flyout:

- **Scroll** over the icon to raise and lower the default output, five
  percentage points at a time by default. The icon, its tooltip and the
  flyout all follow as it moves, and it stops at 100% — awoo does not
  over-amplify.
- **Middle-click** the icon to mute and unmute the default output.

Windows does not hand the wheel to a tray icon the way it hands over clicks,
so awoo asks for raw mouse input while the pointer is on its icon and gives it
up again the moment the pointer leaves. Nothing is watched while the pointer
is elsewhere.

## Settings

**Settings** on the icon's right-click menu opens a window with the two
choices awoo has:

- **Volume step** — how far one notch of the wheel moves the volume, 1 to 25
  percentage points.
- **Start with Windows** — on from the first run, and off for good once it is
  turned off. It is an entry under the current user's `Run` key, which is the
  fit for an app with no installer behind it, and it is the same entry Task
  Manager's Startup tab lists: turn awoo off there and the switch here agrees.
  Move or reinstall awoo and the entry follows it to the new path.

Settings live in `%LOCALAPPDATA%\awoo\settings.json` and are written as they
are changed, so there is nothing to save and nothing to lose by quitting.

Windows 11 files a newly registered tray icon under *Show hidden icons*. Drag
awoo out of there to keep it on the taskbar. The gestures work in either
place.
