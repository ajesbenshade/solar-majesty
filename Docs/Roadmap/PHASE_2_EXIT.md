# Phase 2 — Exit review

**Date:** 2026-08-14  
**Status:** Complete (Weeks 1–12 in). Titan, Imagine→Blender remesh, and an external 45–90 min playtest remain optional leftovers.

Phase 2 turned the Earth → Luna → Mars demo into a five-body overseer sandbox. Core systems were not rewritten: `SpecialistBrain` scoring is unchanged; the player still never path-commands units.

---

## What shipped

| Slice | In |
|-------|----|
| Weeks 1–3 | Belt + Europa profiles, kits, campaign spine, Belt Hauler / Icebreaker |
| Weeks 4–6 | Drop-off haul, power/housing pressure, pad-gated resupply, freight hops, Campus B outpost |
| Weeks 7–9 | Doctrine techs, 3 Secret Projects, Guild Hall, Harvester/Surveyor, I/O/U flags |
| Weeks 10–12 | Body ecology, multi-campus rest/workshops, fauna retreat/expansion, body art tints, this review |

## Body ecology (Weeks 10–12)

| Body | Movement | Fauna | Hazard |
|------|----------|-------|--------|
| Earth / Luna / Mars | 1.0 | Default mites/leeches | Ambient scales with campus size |
| **Belt** | Low-g (~1.18) | Faster **rock mites** prefer mines | Swarm pressure; farms starve |
| **Europa** | Heavy (~0.88) | **Fissure leeches** on power | Radiation drain outside campus / outpost radius |

- Ambient threat uses `AmbientThreat + ExpansionThreat × pieces` from `CelestialBodyProfile`.
- After dens are cleared, mites/leeches **scatter** (no timed waves). Uncleared dens can restock **one** extra stalker once the campus grows (≥8 pieces).
- Campus B can host extract + Harvester/Defense workshops. Hurt robots at B rest at the cyan plaza, not the A inn.

## How to smoke

1. `Docs/SMOKE_TEST.md` 10-minute boot + Earth loop (haul, outpost, doctrines).
2. **Shift+F10** → Belt: `LOW-G` chip, rock mites on mines, **F5**.
3. Europa: `RAD` chip, leave campus — cyan hit flash, HP ticks down; fissure leeches on Power, **F2**.
4. Clear all dens → overseer log “mites and leeches scatter.”
5. **F7** Campus B: place Mine + Harvester Workshop on the cyan disc; a hurt robot should flee to the B rest beacon.

## Known leftovers (not Phase 2 blockers)

- Titan / outer system (Phase 2 stretch; still listed in the body table).
- Imagine→Blender unit remesh (Phase 0 art debt; production look is [Phase 4 – Visual Target](05_PHASE_4_VISUAL_TARGET.md)).
- Continue still omits flags, fauna, and specialist HP.
- External 45–90 min playtest not yet run (same as Phase 1).
- Full combat sim / multiplayer / heightmap terrain.

## Ready for Phase 3

Yes. Next: `Docs/Roadmap/04_PHASE_3_CONTENT_EXPLOSION_REPLAYABILITY.md` — roster depth, body-native monster lists, doctrine/secret-project content. Doctrines and Secret Projects already exist as systems; Phase 3 fills them with more content, not new cores. After Phase 3: [visual target](05_PHASE_4_VISUAL_TARGET.md), then [ship](06_PHASE_5_PRODUCTION_VALUES_SHIP.md).
