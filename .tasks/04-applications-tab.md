# 04. Applications tab

## What to build

The flyout's second tab, mirroring Plasma 6.4's Applications view: every
application currently playing or recording audio, each with its own volume
control.

- Playback streams: enumerate audio sessions on render devices
  (`IAudioSessionManager2` / `IAudioSessionControl2` per device), showing for
  each app its icon, display name, a 0–100% slider (relative app volume, as
  the Windows mixer models it), and a mute button.
- Recording streams: apps capturing from input devices, listed under a
  separate heading below playback, the way Plasma separates them.
- Sessions appear when an app starts playing and disappear when its session
  expires, live, with the flyout open. Per-session volume/mute changes made
  elsewhere (e.g. Windows' volume mixer) update live via session events.
- System sounds appear as a row like Plasma's "Notification Sounds"
  equivalent (the system-sounds session reports itself as such).
- Group by session where one app has several streams, the way the Windows
  mixer does — one row per app is fine; do not over-engineer stream-level
  rows.
- Empty state: a quiet "No applications are playing or recording audio"
  placeholder, as Plasma shows.

Per-app *device routing* (moving an app to another output) is task 05 — leave
room in the row design for a device selector, but do not build it here.

## Acceptance criteria

- [ ] Starting audio in a browser makes it appear with a working slider and
      mute; stopping it removes the row, all without reopening the flyout.
- [ ] An app recording from the microphone shows under the recording heading.
- [ ] Changes made in Windows' own volume mixer reflect here live, and vice
      versa.
- [ ] The empty state shows when nothing plays or records.
