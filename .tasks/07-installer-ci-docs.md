# 07. Installer, CI, docs

## What to build

Everything that turns the working app into a publishable FOSS project. Settled
decisions: **MIT license**, portable exe plus a simple installer (winget can
come later, after a first release), GitHub-hosted.

- **CI**: a GitHub Actions workflow that builds the app on a `windows-latest`
  runner on every push/PR, and on a version tag produces release artifacts —
  a zipped portable build and the installer.
- **Portable build**: a self-contained single-folder publish that runs from
  anywhere without installation.
- **Installer**: a simple per-user installer (Inno Setup is the conventional
  fit for an unpackaged WinUI app) that installs to the per-user programs
  directory, creates a Start Menu entry, registers autostart per the task-06
  default, and uninstalls cleanly including the autostart entry.
- **LICENSE**: MIT, in the repo root.
- **README**: what awoo is (the Plasma 6.4 volume UX for Windows 11), a
  screenshot of the flyout (both tabs), install instructions (installer and
  portable), a feature list, a note that default-device switching and per-app
  routing use undocumented Windows interfaces and may break in a future
  Windows build, and build-from-source instructions.
- Sensible repo hygiene if not already present: `.gitignore` for .NET/WinUI,
  and a short CONTRIBUTING note is welcome but not required.

## Acceptance criteria

- [ ] CI builds green on push, and a tag produces both release artifacts.
- [ ] The installer installs on a clean Windows 11 machine, the app runs, and
      uninstall removes it along with its autostart entry.
- [ ] The portable zip runs without installation.
- [ ] LICENSE (MIT) and a README with screenshot and install instructions are
      in the repo root.
