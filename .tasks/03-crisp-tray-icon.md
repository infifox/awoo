# 03. Crisp tray icon

## What to build

The tray icon looks blurry. It is drawn by rendering a Segoe Fluent Icons
speaker glyph into a bitmap with GDI+ at the size GetSystemMetrics(SM_CXSMICON)
reports, and the result is soft against the shell's own icons. It should match
the normal Windows 11 volume control icon: same glyph shapes (it already uses
the same font), same crispness.

Keep what the renderer gets right — the glyph follows volume level and mute
live, the color follows the taskbar's light/dark theme, and the icon re-renders
on theme change. The work is making the rendering sharp:

- Render at the size the notification area actually displays, under the
  monitor's real DPI — today's size can come back 16px while the taskbar
  shows larger, which is where the blur comes from.
- GDI+ text rendering of a PUA glyph may itself be the softness; if a crisp
  result needs a different text rasterizer (DirectWrite, or rendering larger
  and letting the shell scale down is *not* acceptable — that is more blur),
  change the rendering, not the approach of drawing the glyph from the icon
  font.
- Re-render when the DPI changes (monitor change, scaling change), not just
  when the theme does, if that is what keeps it sharp.

## Acceptance criteria

- [ ] At 100%, 150% and 200% display scaling the tray icon is as sharp as the
      shell's own icons next to it, on light and dark taskbars.
- [ ] The icon still changes with volume level and mute, and re-renders on
      theme change, with no handle leaks (each rendered icon disposed as
      before).
