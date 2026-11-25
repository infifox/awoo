# 02. The hide engine

## What to build

The mechanism that hides the stock Windows volume icon from the taskbar while
awoo runs, proven end to end behind a dev-only trigger (a command-line switch
such as `--hide-system-volume-icon`). No settings UI in this task — that is
task 03. This is the risky, novel piece, so it gets a slice of its own.

**Why injection:** verified on this machine (Windows 11 build 26300, scanning
the binaries directly): no taskbar, shell or Settings binary reads
`HideSCAVolume` under either registry key, the Settings app has no
tray-volume toggle, and the group-policy route has been dead since 24H2. The
only working mechanism is the one TranslucentTB and Windhawk use: have
Windows load a diagnostics DLL into Explorer's taskbar XAML runtime via the
documented `InitializeXamlDiagnosticsEx` API, walk the visual tree to the
system tray's volume icon element (the tray lives in the
`Microsoft.Windows.SystemTray` XAML namespace, hosted by Taskbar.View.dll),
and set it collapsed.

Two pieces:

- **A native C++ TAP DLL, x64 only** (matching the app — settled). It
  implements `IObjectWithSite`/the XAML diagnostics TAP contract, receives
  the visual tree service, finds the volume element, collapses it, and can
  restore it. Fresh code: TranslucentTB's and Windhawk's implementations are
  GPL and awoo is MIT — learn from the API shape, never copy. Keep a narrow,
  versioned contract between awoo and the DLL (hide / restore / report
  found-or-not), however it is carried (exported calls, a window message —
  the task decides).
- **A C# host in awoo** that finds Explorer's process, calls
  `InitializeXamlDiagnosticsEx` naming the DLL and its CLSID, asks it to hide
  or restore, and reports whether the element was found.

**Graceful failure is the contract** (settled): if the API fails, the DLL
cannot load, or no volume element is found — as after a Windows update that
moves things — nothing is touched, awoo carries on fully working, and the
host surfaces found/not-found so task 03 can show its note. Worth knowing:
injecting into Explorer is the kind of thing antivirus occasionally side-eyes;
nothing here should retry aggressively or fight the shell.

**Verifying:** a Verkstead session cannot run the built exe here
(application-control policy blocks it), and the injection also cannot be
exercised against the sandbox user's session. Build everything in-session;
the live proof rounds through the human pressing a preview (build then run
with the switch) and confirming the stock icon vanishes and returns.

## Acceptance criteria

- [ ] Launching awoo with the dev switch hides the stock volume icon from the
      taskbar; exiting awoo cleanly restores it.
- [ ] With the DLL missing or the element not found, awoo starts and runs
      exactly as before — no crash, no hang, no error dialog — and the host
      reports the failure to its caller.
- [ ] The human has seen hide and restore work live via a preview.
