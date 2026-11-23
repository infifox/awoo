# 03. Devices tab

## What to build

The flyout grows the Plasma applet's two-tab structure — **Devices** and
**Applications** — and this task fills the Devices tab. (Applications is task
04; give it an empty placeholder.)

The Devices tab lists every active audio endpoint, outputs first then inputs,
mirroring the Plasma 6.4 layout:

- Each device row: device icon, friendly name, a volume slider with
  percentage, and a mute button — input devices (microphones) exactly like
  outputs.
- **Set-as-default from the row** — the headline feature of the whole app,
  settled with the human: making a device the default must take one click/tap
  from the flyout, with the current default visibly marked (Plasma uses a
  radio-button-like selection; Windows' Fluent equivalent is fine). Switching
  the default uses the undocumented `IPolicyConfig` COM interface (the route
  EarTrumpet and SoundSwitch use — define the interface by its known GUIDs).
  Set all three roles (console, multimedia, communications) unless there is a
  reason not to.
- Device hot-plug: plugging in or removing a device updates the list live via
  `IMMNotificationClient` — no reopen needed.
- Per-device volume/mute changes made elsewhere update the rows live.
- The header from task 02 stays in sync: switching default output moves the
  header to the new device.

## Acceptance criteria

- [ ] Outputs and inputs are listed with working sliders and mute, and a
      plugged-in headset appears (and disappears) without reopening the flyout.
- [ ] Clicking a non-default device makes it the Windows default output (or
      input) immediately, and the marking follows.
- [ ] The task-02 header follows a default-device switch, whether made here or
      in Windows Settings.
