# 01. Rebalance the flyout frame

## What to build

The flyout currently opens with a header — the default output's name, then a
mute/slider/percent row, then a divider — above the Devices/Applications
switcher. That header duplicates what the Devices tab already shows, so it goes
entirely: the name line, the control row and the divider under them. The flyout
opens straight on the switcher. The view model behind the header stays — the
tray icon, tooltip and scroll gesture still read it — only the flyout's use of
it is removed.

With the header gone, the frame is made even:

- The Devices/Applications switcher fills the flyout's full width as two equal
  halves, instead of sitting left-aligned at its natural size.
- The divider above "More sound settings" runs edge to edge of the flyout, the
  way the Windows volume flyout's does — through the window's own padding, not
  stopping at it.
- The padding around the "More sound settings" button is equal on all sides,
  and that padding matches the padding between the flyout's content and its
  other outer edges, so the foot looks balanced against the rest.

The flyout measures its own content to size the window, so removing rows needs
no height constant touched — but verify the window still sizes correctly on
open and on hot-plug after the change.

## Acceptance criteria

- [ ] The flyout opens with the Devices/Applications switcher at the top; no
      device name or duplicate volume row appears above it.
- [ ] The switcher's two items together span the flyout's width in equal
      halves.
- [ ] The bottom divider touches both edges of the flyout, and the "More sound
      settings" button sits with equal padding on all sides, matching the
      flyout's outer padding.
- [ ] Master volume remains adjustable from the Devices tab and the tray
      scroll gesture; the window height still fits the content.
