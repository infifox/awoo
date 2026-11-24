# Tray volume control

**awoo** is a Windows 11 system-tray volume control that reproduces the UX of
the KDE Plasma 6.4 volume applet in native Windows (Fluent) clothing. C# on
WinUI 3, unpackaged, with the tray icon via the H.NotifyIcon library and audio
through Windows Core Audio. Default-device switching and per-app device routing
use the undocumented-but-stable `IPolicyConfig` / audio-policy COM interfaces
(the same route EarTrumpet and SoundSwitch take). The headline feature, settled
with the human, is switching both input and output devices straight from the
flyout without opening Settings.

Settled scope: Windows 11 only; native Fluent look (not a Breeze replica);
volume capped at 100% (no over-amplification); Windows' own volume-key OSD left
alone; MIT license; published as FOSS; autostart on by default with a toggle;
portable exe plus a simple installer, winget later.

## Tasks

- [x] 01: Scaffold and tray presence — [details](01-scaffold-tray.md) — on windows
- [x] 02: Master volume end to end — [details](02-master-volume.md) — on windows
- [x] 03: Devices tab — [details](03-devices-tab.md) — on windows
- [x] 04: Applications tab — [details](04-applications-tab.md) — on windows
- [x] 05: Per-app device routing — [details](05-per-app-routing.md) — on windows
- [ ] 06: Tray gestures, settings, autostart — [details](06-gestures-settings.md) — on windows
- [ ] 07: Installer, CI, docs — [details](07-installer-ci-docs.md) — on windows
