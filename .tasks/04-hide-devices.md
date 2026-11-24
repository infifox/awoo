# 04. Hide devices from the flyout

## What to build

A per-device visibility toggle, so devices the user never touches stop
cluttering the volume control. Settled decisions, not to be reopened:

- **Where it hides from:** a hidden device disappears from the Devices tab
  *and* from the "Play on" routing menu in application rows. The point of
  hiding is "I never use this device", and that holds in both places.
- **The default exception:** a hidden device that is currently the default
  for its kind stays visible in the Devices tab while it is the default —
  otherwise the master volume would have no slider anywhere in the flyout.
  Once the default moves elsewhere, it hides.
- **The Settings list shows connected devices only.** Each connected output
  and input gets a visibility toggle in the settings window. A hidden device
  that is unplugged keeps its flag (keyed by Core Audio endpoint id, which is
  stable); it is unhidden by reconnecting it and toggling.
- **Persistence:** the hidden endpoint ids live in settings.json alongside
  the existing settings, written through the existing settings store (same
  atomic write, same tolerance of a bad file). The set is normalised like the
  rest — an edited file with junk in it must not break startup.

The settings window today holds two rows (volume step, autostart) and does not
know the audio service; the device list needs it wired in, and the list should
follow hot-plug while the window is open. Hiding and unhiding takes effect in
the flyout immediately — the device view models filter on the hidden set and
re-filter when it changes.

Hiding does not touch the device itself: no mute, no disable — it only leaves
the UI. The tray scroll gesture and tooltip keep following the default device
whether or not it is hidden.

## Acceptance criteria

- [ ] The settings window lists every connected output and input with a
      toggle; toggling one off removes it from the Devices tab and from every
      "Play on" menu at once, and toggling it back restores it.
- [ ] The flags survive a restart, and an unplugged hidden device is hidden
      again when reconnected.
- [ ] A hidden device that is the current default still shows in the Devices
      tab, and hides once another device becomes the default.
- [ ] A settings.json with unknown or malformed entries in the hidden list
      still loads; awoo starts with the valid remainder.
