# 06. Capitalize the display name

## What to build

The app presents itself as "awoo"; it should be "Awoo" everywhere a person
reads the name. Settled scope, not to be reopened: **display strings only.**
The executable name, the install and settings folders, the autostart registry
value, the artifact filenames and the repository name all stay lowercase —
renaming those touches the installer upgrade path and the Run key for no
visible gain, and Windows' filesystem is case-insensitive anyway.

Becomes "Awoo":

- Window titles (the flyout's, the settings window's).
- The tray tooltip, wherever the name appears in it.
- The installer's display name — what Add/Remove Programs, the Start Menu
  entry and the setup wizard show — and its publisher string if it carries
  the name.
- The assembly's Product, Title, Company and Copyright strings, which are
  what the exe's file properties show.
- README prose and headings, and any other user-facing documentation prose.

Stays lowercase: `awoo.exe` and the assembly name, `%LOCALAPPDATA%\awoo` and
`%LOCALAPPDATA%\Programs\awoo`, the `awoo-<version>-*` artifact names, the
single-instance mutex name, the radio-group name, URLs and code identifiers.
In README body text, commands and paths keep their literal lowercase spelling
while prose about the app says "Awoo".

## Acceptance criteria

- [ ] The flyout and settings window titles, tray tooltip, exe file
      properties, installer wizard, Add/Remove Programs entry and Start Menu
      entry all show "Awoo".
- [ ] An upgrade install over an existing lowercase install still works and
      keeps its settings; paths, exe name and artifact names are unchanged.
- [ ] No user-visible "awoo" remains outside literal paths, commands and
      identifiers.
