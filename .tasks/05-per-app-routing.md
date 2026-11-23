# 05. Per-app device routing

## What to build

Plasma lets you move one application's audio to a different output straight
from the popup; this task adds that to the Applications tab. Windows supports
it — it is what Settings → System → Sound → "App volume and device
preferences" does — but only through the undocumented audio-policy COM
interface (known as `IAudioPolicyConfigFactory` /
`AudioPolicyConfig`, reverse-engineered and used by EarTrumpet's "move app to
device" feature; Windows 10 1803+ shapes of the interface exist, but only the
Windows 11 shape matters here).

- Each playback row in the Applications tab gets a compact device selector
  (a dropdown or flyout menu listing active output devices plus "Default").
- Picking a device re-routes that app's audio to it, taking effect
  immediately — Windows applies persisted routes to new streams, and the
  implementation should make it apply to the running stream the way the
  Settings page behaves (re-route is persisted per app by Windows itself).
- "Default" clears the persisted route so the app follows the default device
  again.
- The selector reflects an existing persisted route when the flyout opens
  (read back from the same interface).
- If the interface is unavailable or fails (a future Windows build breaking
  the undocumented API), the selector hides or disables gracefully — the rest
  of the app must not be dragged down by it.

## Acceptance criteria

- [ ] Moving a playing app to another output takes effect audibly without
      restarting the app, and the route survives an app restart.
- [ ] "Default" returns the app to following the default device.
- [ ] A route set in Windows Settings shows in the selector, and one set here
      shows in Windows Settings.
- [ ] Failure of the undocumented interface degrades to a hidden/disabled
      selector, not a crash.
