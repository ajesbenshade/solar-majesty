# Stabilization verification — 22 September 2026

## Test results

- Unity **6000.5.10f1** on macOS.
- **EditMode: 283 passed / 0 failed / 0 skipped.** [NUnit XML](2026-09-22-editmode.xml).
- **PlayMode: 4 passed / 0 failed / 0 skipped.** [NUnit XML](2026-09-22-playmode.xml).
- Original baseline: 270 / 279 passing. All nine original failures have replacement-compatible passing coverage; four additional EditMode cases and four PlayMode cases were added.
- Runtime smoke includes actual `LunarOutpost_Sandbox` scene reloads across Earth, Luna, Mars, Belt and Europa. The longer scenario checks world-local campuses, campaign stockpile continuity, Continue with erased legacy campus preferences and a deliberately stale seed preference, and retry without deleting Luna.
- The delayed-reader test uses the real Engineer greed gate; no AI scoring was changed.
- `git diff --check` passes.

These are automated initialization/state tests, not long-session balance, performance, visual approval, or manual mission-completion evidence. The last runtime run preceded only the addition of build-GUID metadata; the final EditMode run compiled and tested that metadata addition. The Windows candidate includes it.

## Isolation and identity

Unity ran against `/private/tmp/solar-stabilization-project`. Tests used a dedicated company/product identity, `SolarMajestyValidation / StabilizationTests`, so their destructive campaign setup could not touch the developer game's normal saves/preferences. Build preparation restored the normal product settings in that **copy**.

The working `ProjectSettings/ProjectSettings.asset` is byte-identical to the pre-implementation backup. Unity's isolated build populated the generated URP runtime-settings list; that normalization was not copied back. [URP normalization diff](2026-09-22-isolated-urp-normalization.diff). Unity also normalized settings in the build copy only: [PlayerSettings diff](2026-09-22-isolated-player-settings-normalization.diff). Neither was copied into the working project.

[Source manifest](2026-09-22-source-manifest.json) records SHA-256 for every input file under Assets, Packages and ProjectSettings. Aggregate source ID:

`5e63da47a8293a5e574e7e1a6f42be1ad720cf53b3df649c2bb9dfffb578a7a9`

This is an **uncommitted working-tree** candidate based on `6a8ea94`, not a clean release tag. It includes the user's pre-existing work plus this stabilization pass. Player telemetry and save snapshots now record `Application.buildGUID` to distinguish built players in returned logs.

## Reproduction

Copy Assets, Packages and ProjectSettings into a disposable project (optionally clone Library for speed). Change the copied PlayerSettings company/product to the dedicated validation identity. Run Unity with `-batchmode -nographics -projectPath <copy> -runTests -testPlatform EditMode` and then `PlayMode`, using separate `-testResults` XML and `-logFile` paths. Do not run two Unity processes on the same project simultaneously.

The campaign smoke fixtures deliberately skip in an ordinary developer profile, unless running under ephemeral CI. They reset campaign files/preferences in their dedicated profile. Do not force CI mode against a developer project just to bypass this guard.

## Windows build

**Succeeded:** Windows x64 player, 209 MB, 139.6 seconds. The ZIP excludes Unity’s `DoNotShip` debug folder.

Archive: `Builds/Stabilization-2026-09-22/SolarMajesty-Windows-stabilization.zip` (78.7 MiB).

Archive SHA-256: `b2c16750e4b51856db5e25a5420e452f637537ad178cf74bb377d30fcee9213e`.

[Build manifest](2026-09-22-windows-build-manifest.json) · [compressed full build log](2026-09-22-windows-build.log.gz). Local compilation cannot establish that the player runs correctly on a native Windows machine. Remote GitHub CI has not been run in this session.
