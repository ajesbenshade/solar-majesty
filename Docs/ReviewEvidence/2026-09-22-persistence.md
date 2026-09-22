# Persistence pass 2 — verification

Unity **6000.5.10f1**, isolated project `/private/tmp/solar-stabilization-project`, dedicated test identity `SolarMajestyValidation / StabilizationTests`.

- **293 EditMode passed, 0 failed, 0 skipped.** [Full NUnit result](2026-09-22-persistence-editmode.xml).
- **6 PlayMode passed, 0 failed, 0 skipped.** [Full NUnit result](2026-09-22-persistence-playmode.xml).
- Previous checkpoint: 283 EditMode + 4 PlayMode passing. This pass adds 10 EditMode cases and two runtime cases.
- Both final runs use the final source changes; the Windows build uses that same source.
- Working `ProjectSettings/ProjectSettings.asset` matches the pre-pass backup byte for byte. Unity is only allowed to normalize its disposable copy.
- `git diff --check` passes.

[Implementation details and remaining limits](../PERSISTENCE_PROGRESS_2026-09-22.md). [Exact authored-source manifest](2026-09-22-persistence-source-manifest.json).

The developed-colony fixture supplies a constructed snapshot, then exercises actual scene initialization, disk loading, capture, and a second reload. It does not assert that these buildings were placed through the UI, that the player completed the mission, or that the colony remained balanced during a long session. The other runtime cases retain all-five-world initialization/revisit checks and the earlier travel/Continue/retry regressions.

Old-save tests use explicit small versioned JSON fixtures. Missing historical den ownership is approximated during migration; information already lost by an older build cannot be recovered exactly.

The interrupted-write case verifies that a leftover partial `.tmp` file cannot displace a complete primary file. It is not a power-cut/filesystem durability test or proof of atomicity across the separate campaign/world files.

## Candidate

**Build succeeded:** 209 MB player, 15.2 seconds. [Build manifest](2026-09-22-persistence-build-manifest.json) · [compressed build log](2026-09-22-persistence-build.log.gz).

Archive: `Builds/Persistence-2026-09-22/SolarMajesty-Windows-persistence.zip` (78.7 MiB); ZIP integrity check passed. Unity’s `DoNotShip` debug folder is excluded.

SHA-256: `7e8cc51dcacdb2e0ab8c695c15bce356247221ac9e4578707fd614c36aa5635d`.

[Isolated Unity settings normalization](2026-09-22-persistence-isolated-normalization.diff) is recorded separately and was not copied into the working project. Native Windows launch and remote CI are not tested on this macOS host. This is an uncommitted working-tree candidate containing the user's existing work and both implementation passes.
