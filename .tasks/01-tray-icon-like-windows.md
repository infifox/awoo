# 01. Draw the tray icon like Windows 11's

## What to build

The tray icon renderer currently draws a Segoe Fluent Icons volume glyph as a
string, and the result reads thinner and sparser than the Windows 11 tray's
own speaker. Zoomed screenshots settled the difference as exactly two things,
and both are wanted:

1. **Stroke weight.** Windows draws the same outline at roughly twice the
   weight (about 2px against awoo's 1px at 16px). It is not a different glyph —
   the candidate code points (E767, E992–E995) share identical outlines in
   Segoe Fluent Icons. Thicken by converting the glyph to a path
   (`GraphicsPath.AddString`) and drawing it twice: fill, then stroke the same
   path with a thin round-join pen. A pen width around **0.7px at 16px** was
   prototyped and matches closely; scale it with icon size, and tune the final
   constant by eye against the real taskbar. Plain `FontStyle.Bold` was
   rejected: GDI+ bold simulation barely touches the wave arcs.

2. **Ghosted inactive waves.** Windows always draws all three waves, with the
   ones above the current level at low opacity. Draw the full three-wave glyph
   (E995) as a ghost underlay — same path technique, same widening, low alpha
   (~60/255 was prototyped on dark; pick a companion value for light) — then
   the level's own glyph at full opacity on top. The glyphs share one em grid,
   so the overlay registers without offset. Mute keeps the speaker-with-x
   glyph, thickened the same way, with **no** waves and no ghost — which is
   how Windows draws it.

Weights and opacities are **constants, not settings** — settled explicitly.
The flyout's buttons and rows keep their current font-rendered glyphs; only
the tray renderer changes. Keep the existing size, DPI and light/dark taskbar
behaviour, and mind that a widened path overflows the em slightly — keep the
glyph inside the bitmap rather than letting the pen clip at the edges.

Note the path-based drawing replaces `DrawString`, so the renderer's existing
grid-fit reasoning (typographic format, AntiAliasGridFit) no longer applies as
written: use `SmoothingMode.AntiAlias` with `PixelOffsetMode.HighQuality` and
re-check crispness at 16px, which the prototype found acceptable.

**Verifying:** a Verkstead session on this machine cannot launch the built
exe (application-control policy) — draw the renderer's output to PNGs at
native icon size, upscale nearest-neighbour to inspect, and compare against
the Brief's attached screenshot. The final by-eye check is the human's: offer
a `verkstead preview` running `.\build.ps1 -Run` and tune constants on their
feedback.

## Acceptance criteria

- [ ] Every volume state (muted, silent, low, medium, high) renders with the
      heavier stroke; the non-mute states show all three waves with the
      inactive ones ghosted.
- [ ] Both light- and dark-taskbar variants render legibly at 16px with no
      clipped strokes.
- [ ] The human has eyeballed the icon live against the Windows one via a
      preview and signed off on the match.
