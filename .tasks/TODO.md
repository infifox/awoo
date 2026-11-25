# device-lock-and-split

Two optional settings for awoo, both off by default. The first splits "set as
default" in two: a default-audio radio and a default-communications radio per
device row, the same division Windows' own Sound settings draws. The second
locks the defaults: awoo remembers which devices the user selected and reverts
any change made from outside awoo the moment Windows announces it.

The split goes first because its per-role plumbing — the audio service
learning, watching and setting the communications default separately — is what
the lock then guards role-by-role.

## Tasks

- [x] 01: Change default audio device and communications device independently — [details](01-split-communications-device.md) — on windows
- [ ] 02: Prevent Windows from automatically switching my audio device — [details](02-default-device-lock.md) — on windows
