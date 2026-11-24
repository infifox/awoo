# 02. Polish the rows and tabs

## What to build

The device and application rows take the Windows 11 volume flyout's manners.
Five changes, all in the flyout:

- **Mute buttons go subtle.** Today they are stock ToggleButtons: a visible
  background at rest and the accent color when muted. They become
  Windows-volume-flyout style — no background at rest, a subtle background on
  hover, and *no accent background when muted*; the muted state reads from the
  glyph alone. This applies to the mute button in device rows and application
  rows alike.
- **Mute buttons align left with the radio button.** The control row under a
  device's name is indented today; the indent goes, so the mute button's left
  edge lines up with the default-device radio button above it. Application
  rows lose their matching indent the same way, aligning with the app icon.
- **The device-kind glyph goes.** Device rows currently show a speaker or
  microphone icon between the radio button and the name; it is redundant (the
  Outputs/Inputs headings already say which is which) and is removed. The
  radio button and name remain.
- **The scrollbar gets a gutter.** The tab scrollbar currently overlays the
  rows, covering the right edge of the device list. Give the scrolled content
  room for the scrollbar beside it instead of under it.
- **The applications zero-state** ("No applications are playing or recording
  audio") is centered in the tab, horizontally, and uses an understated
  (secondary) text color.
- **No flash when switching tabs.** Switching between Devices and
  Applications shows a flash of content today — the new tab is visible before
  the window has resized around it. Switching should be clean: content and
  window size change together, with no transient mis-paint.

## Acceptance criteria

- [ ] Mute buttons show no background at rest and no accent when muted, in
      both device and application rows; hovering shows a subtle background,
      and the glyph still reflects level and mute live.
- [ ] In a device row, the mute button's left edge aligns with the radio
      button; no device-kind icon appears before the device name.
- [ ] A device list long enough to scroll shows its scrollbar beside the
      rows, covering nothing.
- [ ] With no audio playing, the Applications tab shows its message centered
      and in the secondary text color.
- [ ] Switching tabs repeatedly shows no flash or transient mis-layout.
