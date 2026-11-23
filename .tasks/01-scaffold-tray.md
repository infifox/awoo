# 01. Scaffold and tray presence

## What to build

A runnable WinUI 3 desktop app (C#, latest stable .NET, unpackaged — no MSIX)
that lives in the system tray and shows an empty flyout. This is the skeleton
every later task fills in.

- A tray icon (via the H.NotifyIcon library's WinUI flavor) using a speaker
  glyph that fits the Windows 11 tray (Segoe Fluent Icons style).
- Left-click on the icon opens a flyout window anchored near the tray: a small,
  rounded, Fluent-styled window (acrylic/mica backdrop, light and dark theme
  following the system) positioned above the taskbar's notification area. It is
  empty apart from placeholder content.
- The flyout dismisses when it loses focus, like Windows' own quick settings.
- Right-click on the icon opens a context menu with at least Exit, which shuts
  the app down cleanly (no orphaned icon, no lingering process).
- No main window, no taskbar button: the app is tray-only.

Decisions already settled (do not reopen): WinUI 3 over WPF; unpackaged over
MSIX; Windows 11 only; the look is native Fluent, not a KDE Breeze replica.
The UX model being reproduced overall is the KDE Plasma 6.4 volume applet —
this task only needs the shell of it.

## Acceptance criteria

- [ ] Building produces an exe that runs on Windows 11 without MSIX packaging.
- [ ] The tray icon appears; left-click opens the flyout by the tray, and
      clicking elsewhere dismisses it.
- [ ] The flyout follows the system light/dark theme.
- [ ] Right-click → Exit removes the icon and ends the process.
