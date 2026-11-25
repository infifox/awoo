# 01. Change default audio device and communications device independently

## What to build

A new optional setting, **off by default**, in the settings window beneath
*Start with Windows*, labelled exactly **"Change default audio device and
communications device independently"**.

**Off** is today's behaviour, unchanged: one radio button per device row whose
label is the device's name, and a click on either makes that device the
default for all three Windows roles (console, multimedia, communications) at
once.

**On**, every device row in the flyout's Devices tab — outputs and inputs
alike — draws **two radio buttons** beside a plain, non-clickable device name.
The first reflects and sets the **default audio device**: the console and
multimedia roles together, which is what Windows' own Sound settings means by
"default". The second reflects and sets the **default communications device**:
the communications role alone. That makes two radio groups per device kind, so
outputs and inputs never unselect each other and neither do the two columns.

The columns are told apart by **tooltips alone** — no column headers, no
legend. The split radios' tooltips read **"Set as default audio device"** and
**"Set as default communications device"**; the single radio in the unsplit
view keeps today's unqualified "Set as default device", because its one click
sets both. Give the two radios distinct automation names as well, so a screen
reader can tell them apart.

Underneath, the audio service has to learn the communications default: the
published device snapshot carries whether each device is the communications
default for its kind, a change Windows announces on the communications role
triggers a refresh (today only the console role does), and setting the
communications device alone becomes possible. The existing set-as-default
echo-handling pattern applies: a click re-asserts the whole group once the
service says where the default actually landed, which is also how a failed
switch snaps back.

The toggle takes effect live: an open flyout redraws when the setting changes,
without restarting awoo — the same settings-change-event pattern the hidden
devices use.

Settled with the human — do not reopen:

- Default radio = console + multimedia; communications radio = communications
  alone.
- Turning the setting **off moves nothing**: the UI collapses to one radio per
  row showing the default audio device, and the next click sets all three
  roles together as before, whatever the communications device was.
- Per-application routing is untouched.
- Tooltips only; no headers or glyph columns.

## Acceptance criteria

- [ ] Setting off: behaviour identical to today — one radio per row, its
      click (including on the name) sets all three roles.
- [ ] Setting on: two radios per row for outputs and inputs, each reflecting
      and setting only its own role-set, with the tooltips worded as above;
      the device name is a plain label and clicking it does nothing.
- [ ] A communications default changed in Windows' own Sound settings shows in
      the second column while the flyout is open.
- [ ] Toggling the setting redraws an open flyout without restarting awoo, and
      toggling it off moves no defaults.
