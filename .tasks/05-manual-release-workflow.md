# 05. Manual release workflow

## What to build

A manually triggered GitHub Actions workflow that performs a release end to
end. Settled decisions, not to be reopened:

- **workflow_dispatch with one input:** the semantic version (such as
  `1.2.0`, prerelease suffixes allowed). Validate the shape before doing
  anything irreversible, and fail early on a version whose tag already
  exists.
- **It writes the version:** the given version is written into the project
  file's `<Version>` on the branch the workflow was dispatched on, committed,
  and tagged `v<version>`; commit and tag are both pushed. The repository's
  version then always matches the latest release.
- **It builds and publishes itself:** the same build the existing CI does —
  portable zip and Inno Setup installer, both stamped with the version — and
  then a GitHub release for the tag, created immediately (not a draft), with
  generated notes and both artifacts attached.
- **The old tag-publish step comes out of build.yml.** A tag pushed by this
  workflow's own token never triggers other workflows, so the existing
  tag-triggered release step is dead the moment this lands; remove it rather
  than leave it as a trap. The per-push build CI itself stays as it is.

Reuse the existing build steps' approach (version derivation, Inno Setup via
the pinned NuGet tools package, the artifact-presence check) rather than
inventing a second way to build. The version-bump commit follows the
repository's commit message conventions and needs the workflow to have
contents write permission.

## Acceptance criteria

- [ ] Dispatching the workflow with a version produces a `v<version>` tag, a
      commit on the dispatched branch with that version in the project file,
      and a published GitHub release carrying the portable zip and the
      installer, both named with that version.
- [ ] Dispatching with a malformed version, or one whose tag exists, fails
      before any commit, tag or release is created.
- [ ] build.yml no longer contains a tag-triggered release step, and the
      ordinary push/PR build still passes.
