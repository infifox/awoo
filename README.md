# awoo

A system-tray volume control for Windows 11, built to work the way the
[KDE Plasma 6.4](https://kde.org/plasma-desktop/) volume applet does — in
native Fluent clothing.

Windows gives you a volume slider. Plasma gives you every device and every
application, each with its own slider and its own mute, and lets you move an
application's sound to another device without opening a settings app. awoo is
that, for Windows 11.

![The awoo flyout, its Devices tab listing each output with its own slider,
percentage and mute](docs/flyout-devices.png)

## Features

- **The device you are listening on, switched from the flyout.** Every active
  output and input, each with its own slider, percentage and mute, and one
  click to make any of them Windows' default.
- **Every application playing or recording**, with its own icon, slider and
  mute — the same volumes Windows' mixer moves.
- **Per-application output routing.** Send one application's sound to a
  device of its own, the thing Windows buries in Settings → System → Sound →
  *App volume and device preferences*.
- **The Plasma applet's tray gestures.** Scroll over the icon to change the
  volume, middle-click to mute.
- **A tray icon that is the volume**, drawn from the system icon font, with
  the device and its percentage on hover.
- **Starts with Windows**, on by default, with a switch to turn it off.
- Volume caps at 100%: awoo does not over-amplify. Windows' own volume-key
  OSD is left alone.

## Installing

Windows 11 (build 22000 or newer), x64. awoo carries both the .NET and the
Windows App SDK runtimes, so there is nothing to install first — which is
also why the download is around 85 MB.

**The installer** — `awoo-<version>-setup.exe` from the
[latest release][releases]. It installs for the current user only, so it never
asks for administrator rights: the files go under
`%LOCALAPPDATA%\Programs\awoo`, you get a Start Menu entry, and awoo is
registered to start with Windows. Uninstalling from *Settings → Apps* takes
all three away again, the autostart entry included.

**Portable** — `awoo-<version>-portable-win-x64.zip` from the same release.
Unzip it anywhere and run `awoo.exe`; there is no install step. awoo keeps its
settings in `%LOCALAPPDATA%\awoo` and writes one registry value besides: the
*Start with Windows* entry under your user's `Run` key, on by default and
yours to turn off. Move the folder and awoo follows itself: it keeps that
autostart entry pointing at wherever it is now.

winget is not here yet; it is for after a first release.

[releases]: https://github.com/infifox/awoo/releases/latest

### If Windows blocks it

awoo's binaries are not code-signed, and on a machine with **Smart App
Control** switched on Windows may refuse to start an unsigned program it has
not seen before:

> This command cannot be run due to the error: An Application Control policy
> has blocked this file.

That is Smart App Control judging a new file on reputation rather than a fault
in the build. Event Viewer records it under
`Microsoft-Windows-CodeIntegrity/Operational` as event 3077 next to a 3118
"Smart App Control Block", if you want to confirm which it is. The installer
tends to fare better than a bare exe out of a zip. Otherwise the choices are
to wait for the release to pick up reputation, or to turn Smart App Control
off — which cannot be undone without reinstalling Windows, so it is not a step
to take lightly.

## The flyout

Left-click the tray icon. awoo is tray-only: no main window, no taskbar
button.

It opens on two tabs, the way the Plasma applet has them. **Devices** lists
every active endpoint, outputs then inputs, each with its own slider,
percentage and mute — microphones exactly like speakers. The marker beside a
device's name is both which one Windows is using and the way to change it:
selecting it makes that device the default for all three of Windows' roles
(playback, multimedia and calls) on the spot. Plugging a device in or pulling
it out adds or removes its row while the flyout is open.

**Applications** is the same idea one level down: a row for every application
playing, and under them a row for every application recording, each with its
own icon, name, slider and mute. The volume there is the app's own, the one
Windows' volume mixer moves, so a change made in either shows up in the other
straight away. Several streams from one program — a browser with sound in two
tabs — are one row between them, as they are in the mixer. Windows' own
notification sounds get a row like anything else.

![The Applications tab, with a row per application playing, each with its own
icon, slider and mute](docs/flyout-applications.png)

A row is there while the application is actually playing or recording, the way
the Plasma applet's is rather than the way the Windows mixer's is: start
something and its row appears, stop it and the row goes a moment later. When
nothing at all is making or taking sound, the tab says so.

Beside a playing application's name is the output its sound goes to, which is
the Plasma applet's "move this app to another device". The button shows a
speaker alone while the application follows whichever device is the default,
and names the device once it has been sent to one of its own; opening it lists
**Default** and every output to choose from. Windows remembers the choice
against the application rather than the process, so it holds when the
application is closed and reopened, and a choice made in Settings shows here
as the flyout opens — there is no notification for it, so that is when awoo
looks. Where the device an application was sent to is not there any more, the
button says so rather than naming nothing.

Recording rows have no selector: moving an application's microphone is not
something this version does.

At the foot of the flyout, **More sound settings** opens Windows' own sound
page — the Plasma applet's *Configure Audio Devices…* under the name Windows'
own volume flyout gives the same link.

## Gestures

The tray icon takes the two gestures the Plasma applet has, beyond the
left-click that opens the flyout:

- **Scroll** over the icon to raise and lower the default output, five
  percentage points at a time by default. The icon, its tooltip and the
  flyout all follow as it moves, and it stops at 100%.
- **Middle-click** the icon to mute and unmute the default output.

Windows does not hand the wheel to a tray icon the way it hands over clicks,
so awoo asks for raw mouse input while the pointer is on its icon and gives it
up again the moment the pointer leaves. Nothing is watched while the pointer
is elsewhere.

Windows 11 files a newly registered tray icon under *Show hidden icons*. Drag
awoo out of there to keep it on the taskbar; the gestures work in either
place, though Windows will not say where a hidden icon is, so awoo has to
guess at the position while it is tucked away.

## Settings

**Settings** on the icon's right-click menu opens a window with the two
choices awoo has:

- **Volume step** — how far one notch of the wheel moves the volume, 1 to 25
  percentage points.
- **Start with Windows** — on from the first run, and off for good once it is
  turned off. It is an entry under the current user's `Run` key, and it is the
  same entry Task Manager's Startup tab lists: turn awoo off there and the
  switch here agrees.

Settings live in `%LOCALAPPDATA%\awoo\settings.json` and are written as they
are changed, so there is nothing to save and nothing to lose by quitting.

## The parts Windows does not document

Switching the default device goes through `IPolicyConfig`, and sending one
application's sound to a device goes through `IAudioPolicyConfig` — neither of
which Microsoft documents, and both the route
[EarTrumpet](https://github.com/File-New-Project/EarTrumpet) and
[SoundSwitch](https://github.com/Belphemur/SoundSwitch) take. **A future
Windows build could change or withdraw either, and that much of awoo would
stop working.**

It would not take the rest with it. Where a build of Windows will not hand one
over, awoo goes quiet about that feature and nothing else is affected: the
default markers stay as read-only indicators of which device is in use, and
the per-application selector is not shown at all rather than offering a
control that would do nothing. Volume, mute, the device list and the
application list are all documented Core Audio, and are not exposed to this.

Two things Windows itself decides, and awoo reports rather than argues with: a
device has to exist to be sent sound, and an application has to be making
sound to be moved. So an application whose chosen device has since been
unplugged keeps its route and says the device is unavailable, and a selector
that Windows declines snaps back to where the sound is actually going.

## Building from source

Needs Windows 11 and a .NET 10 SDK — `build.ps1` fetches a per-user one into
`.dotnet/` if the machine has none:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Run
```

Drop `-Run` to build without starting it, and add `-Configuration Release` for
a release build. The result is a plain `awoo.exe` under `src\Awoo\bin`: awoo
is an unpackaged WinUI 3 app, so there is no MSIX and no install step.

To build what a release ships — the self-contained publish, and then the
installer over it:

```powershell
dotnet publish src\Awoo\Awoo.csproj -c Release -p:SelfContained=true
iscc packaging\awoo.iss
```

`packaging\awoo.iss` is an [Inno Setup](https://jrsoftware.org/isinfo.php)
script, and takes `/DAppVersion`, `/DPayloadDir` and `/DOutputDir` to override
its defaults. `.github/workflows/build.yml` does both on every push, and
publishes the two artifacts to a GitHub release on a `v*` tag.

awoo is C# on WinUI 3, with the tray icon from
[H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) and audio through
Windows Core Audio. `src/Awoo` is laid out as `Audio` (everything that talks
to Core Audio, with no UI in it), `ViewModels`, `Flyout`, `Tray`, `Settings`
and `Interop`.

## License

[MIT](LICENSE).
