# 06. Tray gestures, settings, autostart

## What to build

The Plasma tray behaviors and the app's small configuration surface.

Gestures on the tray icon:

- Scroll wheel over the icon raises/lowers the default output volume by the
  configured step (default 5%, matching Plasma's default), with the tray
  icon/tooltip updating live. Capped at 100%.
- Middle-click toggles mute on the default output.
- (Left-click open and right-click menu exist from task 01.)

Settings — a small window reachable from the tray right-click menu:

- Volume step for the scroll gesture.
- Start with Windows: **on by default**, implemented per-user (the
  `HKCU\...\Run` registry key is the fit for an unpackaged exe), with the
  toggle reflecting and controlling the actual registry state.
- Keep it minimal; a single page is plenty.

Flyout footer: a button equivalent to Plasma's "Configure Audio Devices…" that
opens Windows' own sound settings (`ms-settings:sound`).

First-run: on first launch the app registers autostart (the on-by-default
decision) — but only once, so a user who turns it off stays off.

## Acceptance criteria

- [ ] Scrolling over the tray icon changes volume by the configured step;
      middle-click mutes and unmutes.
- [ ] The autostart toggle matches reality: on by default after first run,
      honored after a reboot/re-login, and staying off once turned off.
- [ ] The footer button opens Windows' sound settings page.
- [ ] Settings persist across app restarts.
