# Awoo

A system-tray volume control for Windows 11, built to work the way the
[KDE Plasma 6.4](https://kde.org/plasma-desktop/) volume applet does — in
native Fluent clothing.

Windows gives you a volume slider. Plasma gives you every device and every
application, each with its own slider and its own mute, and lets you move an
application's sound to another device without opening a settings app. Awoo is
that, for Windows 11.

![The Awoo flyout, its Devices tab listing each output with its own slider,
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
- **Spatial sound per output.** Off, Windows Sonic for Headphones, Dolby
  Atmos, DTS — whichever of them the device will actually take — from a
  dropdown beside its name. The one setting Windows' own volume flyout has
  that Awoo did not.
- **The Plasma applet's tray gestures.** Scroll over the icon to change the
  volume, middle-click to mute.
- **A tray icon that is the volume**, drawn from the system icon font, with
  the device and its percentage on hover.
- **One speaker in the tray, not two** — a switch that takes Windows' own
  volume icon out of the notification area while Awoo is running, and puts it
  back when Awoo exits. Off by default.
- **Starts with Windows**, on by default, with a switch to turn it off.
- Volume caps at 100%: Awoo does not over-amplify. Windows' own volume-key
  OSD is left alone.

## Installing

Windows 11 (build 22000 or newer), x64. Awoo carries both the .NET and the
Windows App SDK runtimes, so there is nothing to install first — which is
also why the download is around 85 MB.

**The installer** — `awoo-<version>-setup.exe` from the
[latest release][releases]. It installs for the current user only, so it never
asks for administrator rights: the files go under
`%LOCALAPPDATA%\Programs\awoo`, you get a Start Menu entry, and Awoo is
registered to start with Windows. Uninstalling from *Settings → Apps* takes
all three away again, the autostart entry included.

**Portable** — `awoo-<version>-portable-win-x64.zip` from the same release.
Unzip it anywhere and run `awoo.exe`; there is no install step. Awoo keeps its
settings in `%LOCALAPPDATA%\awoo` and writes one registry value besides: the
*Start with Windows* entry under your user's `Run` key, on by default and
yours to turn off. Move the folder and Awoo follows itself: it keeps that
autostart entry pointing at wherever it is now.

winget is not here yet; it is for after a first release.

[releases]: https://github.com/infifox/awoo/releases/latest

### If Windows blocks it

Awoo's binaries are not code-signed, and on a machine with **Smart App
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

Left-click the tray icon. Awoo is tray-only: no main window, no taskbar
button.

It opens on two tabs, the way the Plasma applet has them. **Devices** lists
every active endpoint it has not been told to hide, outputs then inputs, each
with its own slider, percentage and mute — microphones exactly like speakers.
The marker beside a device's name is both which one Windows is using and the
way to change it: selecting it makes that device the default for all three of
Windows' roles (playback, multimedia and calls) on the spot. Plugging a device
in or pulling it out adds or removes its row while the flyout is open.

Turn **Change default audio device and communications device independently**
on in settings and each row has two markers instead: the first is the device
everything plays through, the second the device calls go through — the same
division Windows' own Sound settings draws. With two questions in the row the
name is a plain label rather than a click target, and hovering a marker says
which of the two it is.

With **Prevent Windows from automatically switching my audio device** on, the
markers here are also the only thing that moves a default: Awoo puts back
anything else that moves one, Windows' own Sound settings included.

An output that has spatial sound carries a dropdown to the right of its name:
*Off* and then every format that device can be put into — Windows Sonic for
Headphones, Dolby Atmos, DTS, whichever of them this machine has and this
endpoint's licence covers. Picking one is the same change Settings → System →
Sound makes, and the two agree in both directions. Outputs with no spatial
sound to offer, and every input, show nothing there.

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
**Default** and every output to choose from, bar the ones hidden in settings.
Windows remembers the choice against the application rather than the process,
so it holds when the application is closed and reopened, and a choice made in
Settings shows here as the flyout opens — there is no notification for it, so
that is when Awoo looks. Where the device an application was sent to is not
there any more, the button says so rather than naming nothing.

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
so Awoo asks for raw mouse input while the pointer is on its icon and gives it
up again the moment the pointer leaves. Nothing is watched while the pointer
is elsewhere.

Windows 11 files a newly registered tray icon under *Show hidden icons*. Drag
Awoo out of there to keep it on the taskbar; the gestures work in either
place, though Windows will not say where a hidden icon is, so Awoo has to
guess at the position while it is tucked away.

## Settings

**Settings** on the icon's right-click menu opens a window with the six
choices Awoo has:

- **Volume step** — how far one notch of the wheel moves the volume, 1 to 25
  percentage points.
- **Start with Windows** — on from the first run, and off for good once it is
  turned off. It is an entry under the current user's `Run` key, and it is the
  same entry Task Manager's Startup tab lists: turn Awoo off there and the
  switch here agrees.
- **Change default audio device and communications device independently** —
  off by default. On, every device row in the Devices tab gets two markers:
  the first moves the playback and multimedia roles, which is what Windows'
  own Sound settings calls the default device, and the second moves the
  communications role alone. Turning it off again moves neither — the markers
  become one again, and the next device selected takes all three roles as
  before.
- **Prevent Windows from automatically switching my audio device** — off by
  default. Turning it on remembers whichever output and input are default at
  that moment, and from then on anything that moves a default from outside
  Awoo is put straight back: Windows switching to a device as it is plugged
  in, a driver, another application, or Windows' own Sound settings page,
  which this does override. There is no polling behind it — Windows announces
  every default moving, and that announcement is what Awoo answers. Selecting
  a device in the flyout is how the remembered choice is changed, and with
  the setting above on, the playback device and the communications device are
  remembered and held separately. Unplug a remembered device and Awoo leaves
  Windows' fallback alone until it is back, then takes it again on its own.
  Turning the lock off reverts nothing ever after.
- **Hide the Windows volume icon** — off by default. Turn it on and Windows'
  own speaker leaves the notification area, so the tray has one volume icon
  rather than two; exiting Awoo brings it back, and so does restarting
  Explorer, after which Awoo hides it again. Windows 11 has no setting for
  this, so Awoo does it by reaching into Explorer's taskbar directly — which
  is why the switch is off until you ask for it, and why a Windows update
  could put the icon out of reach. If that happens the switch says so and
  nothing else about Awoo is affected.
- **Devices** — a switch for every connected output and input. Turn one off
  and it leaves the flyout: out of the Devices tab, and out of every
  application's **Play on** menu. The device itself is untouched — nothing is
  muted and nothing is disabled, Awoo only stops offering it. Whichever
  device Windows is using stays in the list while it is the default, so the
  volume you are actually listening to always has a slider; it goes once the
  default moves elsewhere. A device that is away is not listed, and keeps
  what it was set to: plug a hidden one back in and it is hidden again.

Settings live in `%LOCALAPPDATA%\awoo\settings.json` and are written as they
are changed, so there is nothing to save and nothing to lose by quitting.

## The parts Windows does not document

Switching the default device goes through `IPolicyConfig`, and sending one
application's sound to a device goes through `IAudioPolicyConfig` — neither of
which Microsoft documents, and both the route
[EarTrumpet](https://github.com/File-New-Project/EarTrumpet) and
[SoundSwitch](https://github.com/Belphemur/SoundSwitch) take. **A future
Windows build could change or withdraw either, and that much of Awoo would
stop working.**

Spatial sound is half documented. Reading which format an output is in and
putting it into another both go through the documented
`SpatialAudioDeviceConfiguration`, which takes a change from Awoo's ordinary
unelevated process — no administrator prompt, for any format. What that API
cannot answer is which formats are worth offering: its
`IsSpatialAudioFormatSupported` says yes to ones the setter then refuses,
because it asks whether the endpoint could carry the format rather than
whether this machine has it to give. So the menu is read off the endpoint's
own property store, where Windows publishes a record per format saying
whether it is usable here and now — the property store is documented, the key
is not.

Hiding Windows' own volume icon is the same shape of bargain. Windows 11 has
no setting, policy or registry value for it any more, so Awoo loads a small
DLL of its own into Explorer through `InitializeXamlDiagnosticsEx` — the
documented entry point Visual Studio's Live Visual Tree uses — walks the
taskbar's XAML tree and collapses the icon it finds there. The API is
documented; the tree is not. **A Windows update that rearranges the taskbar
would stop the hide working**, which is why the switch is off by default and
says so when it cannot do its job. Nothing else about Awoo depends on it.

It would not take the rest with it. Where a build of Windows will not hand one
over, Awoo goes quiet about that feature and nothing else is affected: the
default markers stay as read-only indicators of which device is in use, the
per-application selector is not shown at all rather than offering a control
that would do nothing, and an output whose format list cannot be read shows no
spatial dropdown rather than one full of guesses. Volume, mute, the device
list and the application list are all documented Core Audio, and are not
exposed to this.

Two things Windows itself decides, and Awoo reports rather than argues with: a
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
a release build. The result is a plain `awoo.exe` under `src\Awoo\bin`: Awoo
is an unpackaged WinUI 3 app, so there is no MSIX and no install step.

It also needs Visual Studio's **C++ x64 toolset** — the "Desktop development
with C++" workload, of either Visual Studio or the standalone Build Tools.
`src\AwooTap` is the small native DLL Awoo loads into Explorer to hide the
stock volume icon, and the .NET SDK cannot compile C++: the project finds
Visual Studio's MSBuild with `vswhere` and runs it as a step of the ordinary
build, which is the one thing here that cannot be fetched per-user. Without
it the build stops and says so, rather than producing an `awoo.exe` whose hide
setting could never work.

Explorer keeps that DLL mapped from the moment Awoo hides the icon, and
nothing unloads it again, so a rebuild cannot overwrite the copy beside
`awoo.exe`. The build moves the old one aside rather than stopping on it — but
the running Explorer carries on with the module it already has, because the
loader answers a load of that path with what is mapped there rather than
reading the file again. **A change to `src\AwooTap` reaches Explorer only when
Explorer next restarts.** To try one: quit Awoo, restart Explorer, then build
and run.

To build what a release ships — the self-contained publish, and then the
installer over it:

```powershell
dotnet publish src\Awoo\Awoo.csproj -c Release -p:SelfContained=true
iscc packaging\awoo.iss
```

`packaging\awoo.iss` is an [Inno Setup](https://jrsoftware.org/isinfo.php)
script, and takes `/DAppVersion`, `/DPayloadDir` and `/DOutputDir` to override
its defaults. `.github/workflows/build.yml` does both on every push.

A release is a hand-dispatched run of `.github/workflows/release.yml` with the
version as its only argument: it writes that version into the project, commits
and tags it, and publishes the two artifacts to a GitHub release for the tag.

Awoo is C# on WinUI 3, with the tray icon from
[H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) and audio through
Windows Core Audio. `src/Awoo` is laid out as `Audio` (everything that talks
to Core Audio, with no UI in it), `ViewModels`, `Flyout`, `Tray`, `Settings`
and `Interop`.

## License

[MIT](LICENSE).
