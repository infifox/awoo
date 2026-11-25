# 04. Ship the engine

## What to build

The native TAP DLL from task 02 carried by every way awoo ships. Today the
whole pipeline is .NET-only: `build.ps1` builds and publishes the one C#
project with the SDK's own MSBuild (which cannot build C++), the portable zip
is the publish folder, and the Inno installer copies that folder wholesale.
So the work is:

- **Build the DLL as part of the normal build** — `build.ps1` and the GitHub
  build/release workflows (the shared package action) both produce it. The
  SDK's `dotnet msbuild` cannot drive a vcxproj, so either locate VS MSBuild
  (vswhere — CI's windows runners carry MSVC) or compile with `cl.exe`
  directly from the script; prefer whatever stays closest to the repo's
  existing self-bootstrapping style (no machine-wide installs, no admin).
- **Land it in the output and publish folders** beside `awoo.exe`, so the
  portable zip and the installer pick it up with no change to the
  installer's wholesale copy — and so a plain Debug build run from `bin`
  finds it too.
- **Keep the x64-only line** — the project, installer and new DLL all say
  x64 and nothing else (settled).

A local `.\build.ps1 -Package` run must produce artifacts containing the DLL
even on a machine without Visual Studio installed, or fail with a message
saying what is missing — silent artifacts without the DLL (shipping a
permanently broken hide feature) are the failure to design against.

## Acceptance criteria

- [ ] `.\build.ps1` and `.\build.ps1 -Package` both produce the DLL beside
      the exe, and the portable zip and installer payload contain it.
- [ ] The CI build workflow is green and its uploaded artifacts contain the
      DLL.
- [ ] Running the installed awoo with the hide setting on still hides the
      stock icon — the shipped DLL is the working one, confirmed by the human
      via a preview or an installer run.
