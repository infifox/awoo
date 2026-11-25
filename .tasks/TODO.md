# Win11 tray icon

Make awoo's tray icon read exactly like Windows 11's own volume icon — the
same stroke weight and the ghosted inactive waves — and give awoo an
off-by-default setting that hides the stock Windows volume icon while awoo
runs, by the XAML-diagnostics injection technique, since build 26300 offers no
sanctioned way. Settled with the human over Question Sets 29–32: thicker
strokes *and* ghosted waves, constants not settings knobs, hide off by
default, restore the stock icon on clean exit, a quiet note in Settings when
hiding stops working, x64 only.

## Tasks

- [x] 01: Draw the tray icon like Windows 11's — [details](01-tray-icon-like-windows.md) — on windows
- [x] 02: The hide engine — [details](02-hide-engine.md) — on windows
- [x] 03: The hide setting and its lifecycle — [details](03-hide-setting-lifecycle.md) — on windows
- [x] 04: Ship the engine — [details](04-ship-the-engine.md) — on windows
