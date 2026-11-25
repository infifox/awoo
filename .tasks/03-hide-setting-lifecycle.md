# 03. The hide setting and its lifecycle

## What to build

The user-facing half of the hide feature, over task 02's engine:

- **A settings toggle** — "Hide the Windows volume icon", with a caption in
  the voice of the existing rows, in the Settings window beside the other
  toggles. **Off by default** (settled), persisted in the settings record the
  way the existing values are. No first-run defaulting dance: off is simply
  the record's default.
- **Lifecycle.** On startup with the setting on, hide. On flipping the
  toggle, hide or restore live. On clean exit (the tray menu's Exit, and any
  other orderly shutdown path), restore — the human chose restoring so no
  volume icon is ever lost with awoo gone. When **Explorer restarts** it
  comes back with the icon visible again, so watch for the taskbar's
  recreation (the `TaskbarCreated` broadcast the tray icon already rides on)
  and re-apply after the tray settles. An awoo crash leaving the icon hidden
  until Explorer restarts was accepted as tolerable.
- **The broken-hide note** (settled): when the setting is on but the engine
  reports it found nothing to hide — the post-Windows-update failure mode —
  the toggle gains a quiet caption-sized note saying the hide isn't working,
  in the Settings window only. No dialogs, no toasts, and awoo otherwise
  carries on as if the setting were off.

Retire the dev-only switch from task 02 once the toggle drives the engine, or
keep it if it still earns its place for testing — the task's call; the
setting is the real interface.

**Verifying:** as with the other tasks, the live checks round through the
human: a preview that builds and runs, then flipping the toggle, restarting
Explorer from Task Manager, and exiting awoo.

## Acceptance criteria

- [ ] The toggle hides and restores the stock icon live, persists across awoo
      restarts, and is off on a fresh settings file.
- [ ] With the setting on, killing and restarting Explorer ends with the
      stock icon hidden again; exiting awoo restores it.
- [ ] Forcing the engine's not-found path shows the note beside the toggle,
      and awoo behaves normally otherwise.
