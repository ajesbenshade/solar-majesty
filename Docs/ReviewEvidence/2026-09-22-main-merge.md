# Main integration — 2026-09-22

Integrated the complete workspace commit `d6702ce` with upstream main `270c11c`.

Conflict decisions:

- Preserve the requested Earth → Luna → Mars → Belt → Europa campaign and Earth first-hour demo.
- Preserve the later smooth Commons lock; retain upstream boxy tan HAB, orange ribbed airlocks, spaced yards, and 17 animated unit assets. Visual exit remains blocked.
- Combine authoritative per-world saves, backup recovery, future-version protection, veteran equipment, exact den ownership, and research progress with upstream indexed parties, levy cargo, and Ironman state.
- Save schema v5 reads both upstream v3 structured rosters and stabilization v3/v4 encoded rosters. New snapshots include the encoded roster and exact per-agent veteran records; indexed parties preserve same-class members and leadership.
- Retain upstream economy and advisor work. Resolve the research-cost helper naming collision and retain ship power costs. Remove duplicate resource restore methods introduced by automatic merging.
- Align obsolete test expectations with four-cell yard spacing, ship power costs, and matching-radius limits; preserve distance bounds.

Validation uses a separate project and player preferences profile, not the working project's saves.

- EditMode: 359 passed, 0 failed, 0 skipped.
- PlayMode: 6 passed, 0 failed, 0 skipped. Includes two disk/scene round trips of a developed colony with distinct same-class veterans, equipment, indexed party leadership, Ironman, damaged buildings, research, and occupied/cleared dens.
- Windows standalone build succeeded (145,756,031 bytes). Compilation/build validation only; no native Windows play session was run.
- NUnit reports: `2026-09-22-merge-editmode.xml`, `2026-09-22-merge-playmode.xml`.

The merge does not close production-readiness or visual-review gates. Earlier playtest ZIPs predate this integration and schema v5.
