# Persistence hardening — second implementation checkpoint

Continues [the first stabilization pass](STABILIZATION_PROGRESS_2026-09-22.md) and M0 of [the production plan](PRODUCTION_PLAN.md). This is additional implementation and automated evidence, not a declaration that M0 or the visual exit is complete.

## Fixes

- **Paused research survives saving.** Previously only the selected technology's progress was serialized; switching projects and then loading discarded work on the previous project. Save version 4 retains all in-progress projects, without spending banked science again while loading.
- **Saved doctrine/replay settings are restored before world initialization.** A stale preferences value no longer silently changes the saved colony's rules on Continue.
- **Dens retain their living creatures.** Restoring fauna now reattaches each creature to its saved den. The old references pointed to despawned objects, allowing an occupied den to count as empty after loading. Roaming creatures remain unowned in new saves.
- **Scouting and expansion history survive.** A cleared den no longer drops its earlier scouted flag. Its one-time expansion-spawn state is saved, preventing repeat expansion restocks after reload.
- **Each living robot retains its own veteran record.** Two Engineers can have different levels, XP, equipment, and revive histories. The class-based legacy roster remains for migration and existing corpse/refabrication behavior; living robot restoration now uses the per-agent record when present.
- **Malformed saves cannot silently become new colonies.** Empty/invalid world headers and malformed entity rows are rejected, allowing the existing backup recovery path to run. If neither file can be read, Continue stays on the title screen and leaves the files unchanged.
- **Newer-version files are protected from writes as well as reads.** Continue explains that a newer game version is required; save writes also refuse to replace a newer primary or backup. This closes the previous path where a failed read could fall back to legacy preferences and then autosave over the incompatible file.

## New verification

The developed-colony fixture uses the real game scene, disk saves and two successive scene reloads. It supplies a deliberate snapshot rather than placing its buildings through mouse input. Its assertions cover:

- Three living robot records, including two differently equipped Engineers and a downed Defense Mech.
- Veteran level/XP, purse, equipment and revive history; injury and fatigue; downed recovery timer.
- A claimed Build flag with partially completed work and escrow that must not be charged twice.
- Partially constructed HAB, damaged Commons, stockpile, active and paused research, and banked science.
- Mission elapsed/sustain time, simulation step count, depleted resource node, cleared/scouted den, and a surviving creature still attached to an occupied den after the den's next tick.
- Saved doctrine overriding a stale preference.

Additional tests cover version 1/2/3 migration, parseable-but-invalid files, corrupt-file backup recovery, a partially written temporary file, refusing future-version writes, and the actual Continue path for unsupported/unreadable saves. The prior five-world travel/Continue/retry scenarios remain in the suite.

See [verification evidence](ReviewEvidence/2026-09-22-persistence.md) for exact totals and the updated candidate.

## Compatibility and remaining limits

New snapshots use **save version 4**. Versions 1–3 can still load. They lack creature ownership IDs, so migration associates legacy stalkers with the nearest uncleared den when that information is available. This is an approximation, not a reconstruction of data the old version never saved. New snapshots store exact associations.

Use the updated candidate for version-4 saves; the previous playtest player predates these protections and should not be used to open them. Keep older candidate archives as historical artifacts, not interchangeable players for the same save directory.

Still required:

1. A single atomic campaign envelope covering all worlds and campaign-level state. Separate per-world files are not a cross-file transaction.
2. Stable robot/workshop IDs for party membership, corpse/refabrication identity, and multiple robots of the same class across all systems. The per-agent veteran fix does not claim to solve those remaining class-based relationships.
3. Save coverage for refabrication already paid/in progress, corpses, parties, civic assignments, economy/ability timers and other transient state.
4. Longer resumed sessions, human tutorial/placement checks, full mission-gate completion, balance and performance testing, and native Windows launch testing.
5. Remote CI observation and the existing visual acceptance gates.

No AI scoring or new gameplay content was added. Tests and builds remain isolated from the working project's settings and normal developer saves.
