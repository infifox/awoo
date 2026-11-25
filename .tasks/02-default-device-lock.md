# 02. Prevent Windows from automatically switching my audio device

## What to build

A second optional setting, **off by default**, labelled exactly **"Prevent
Windows from automatically switching my audio device"**, in the settings
window alongside the split setting from task 01.

When enabled, awoo remembers which devices the user has selected and holds
Windows to them. Enabling it captures the current defaults as the remembered
selection; every pick made through awoo's flyout updates it. From then on,
**any change to a default made outside awoo** — Windows auto-switching when a
device appears, a driver, another app, or the user in Windows' own Sound
settings — **is reverted the moment Windows announces it**. While the lock is
on, awoo's flyout is where devices get switched; that Windows' Sound settings
page is overridden too is the agreed behaviour, not a bug.

Purely notification-driven: the audio service already hears every default
change from Windows, and there is **no polling timer**.

The remembered selection covers the default **output and input** both. With
the split setting from task 01 also on, the default audio device and the
communications device are remembered and guarded **separately**, per kind and
per role — the user may lock output A as default and output B for
communications. It is stored in the settings file beside the other settings,
so the lock survives restarts.

An absent device: when the remembered device is unplugged, Windows' fallback
is left alone — no reverting while the chosen device is away — and the moment
it returns, awoo retakes it as the default without any user action. The
remembered id is kept through the absence, the way hidden-device ids already
survive a device being away.

Keep the guard quiet and loop-free: a change that lands on the remembered
device is never acted on, and a revert asserts the remembered device once per
announcement rather than retrying a set that failed.

Settled with the human — do not reopen:

- Revert everything not done through awoo, Windows' Sound settings included.
- Notifications only; no timer of any kind.
- Remember through absence and retake on return; persist across restarts.
- Lock off: awoo reverts nothing, exactly as today.

## Acceptance criteria

- [ ] Lock on: a default output or input changed from Windows' Sound settings
      snaps back at once; a pick in awoo's flyout sticks and becomes the new
      remembered selection.
- [ ] Unplugging the remembered device leaves Windows' fallback alone;
      replugging it makes it the default again without any user action.
- [ ] The remembered selection survives restarting awoo.
- [ ] With the split on, the default audio and communications devices are
      remembered and guarded independently.
- [ ] Lock off: no reverting of any kind.
