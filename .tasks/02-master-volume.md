# 02. Master volume end to end

## What to build

The flyout's header: a volume slider and mute button for the current default
output device, wired to real audio via Windows Core Audio
(`IAudioEndpointVolume` on the default render endpoint). This establishes the
audio service layer the later tasks build on, so keep the Core Audio access in
a service class separate from the UI.

- Header shows the default output device's name, a 0–100% slider, the current
  percentage, and a mute toggle — the same at-a-glance layout as the Plasma
  applet's header row.
- Dragging the slider changes the real system volume live; the mute button
  mutes/unmutes.
- Changes made elsewhere (Windows' own flyout, hardware keys, another app)
  update the slider, percentage and mute state live via the endpoint volume
  change callback — no polling.
- When the default device changes, the header follows it.
- The tray icon reflects state: distinct glyphs for muted and for low/medium/
  high volume, updating live. Hovering the icon shows device name and
  percentage as the tooltip.
- Volume is capped at 100% — over-amplification was settled out of scope.

## Acceptance criteria

- [ ] The slider moves real system volume, and volume changed from Windows
      updates the flyout within a beat, both while open.
- [ ] Mute from the flyout and mute from Windows stay in sync both ways.
- [ ] The tray icon and tooltip reflect volume level and mute state live.
- [ ] Core Audio access lives in a service layer the UI binds to, not in
      code-behind.
