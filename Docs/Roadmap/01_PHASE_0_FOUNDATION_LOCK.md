# Phase 0 – Foundation Lock

**Status:** Complete  
**Duration:** Finished (as of current repo state)  
**Goal:** Establish an unbreakable pure-C# foundation and a playable vertical slice so all future work builds on solid, non-negotiable architecture.

---

## What Was Achieved

### Pure Systems (Assets/Scripts/Systems/)
- `SpecialistBrain` — Utility AI decision core (greed, preference, risk, fatigue, hysteresis)
- `FlagManager` — Runtime bounty flag lifecycle
- `BuildingPlacer` — Placement validation + construction orders
- `ResourceManager` + `SimpleEconomy` — Stockpile, upkeep, Earth resupply
- `ResearchManager` + TechCatalog — Alpha Centauri-style tech tree (TECH · T)
- CampaignProgress — Earth → Luna → Mars unlock spine
- Data-driven ScriptableObjects for Buildings, Flags, Specialists, Resources, Monsters

### Runtime Vertical Slice
- SpecialistAgent + party system
- Flag and building placement input
- Isometric camera + HUD (OverseerHud)
- NavMesh pathing
- Threat pressure (Dust Stalkers)
- Multi-body scaffold (Campus A/B, body framing)
- Empty-start levels with claim disc and player-placed buildings
- Workshop-built robots (no free starter specialists)
- Demo shell (title, pause, settings, continue, tutorial)

### Art Pipeline
- Blender modular blockouts (HAB-1, LAB-1, CMD/OPS, PWR, Landing Pad, Modular Tube)
- Unit blockouts (EngineerBot, ScoutDrone, DefenseMech, DustStalker)
- ConceptSheets and Grok Imagine style locked
- URP setup + basic atmosphere/volume

### Docs
- ARCHITECTURE.md, DEMO.md, DEVELOPER_HANDOFF.md, phase-by-phase notes

---

## Design Pillars Locked

| Pillar | Consequence |
|--------|-------------|
| Indirect control only | Player APIs limited to build, post flag, set bounty, research, economy. No move/attack commands. |
| Autonomous heroes | `SpecialistBrain` is the sole decision authority. |
| Data-driven content | Numbers and affinities live on ScriptableObjects. |
| Extensible multi-body | Systems accept body profiles and positions; no single-scene assumptions. |

---

## Non-Negotiables Going Forward

1. Do **not** rewrite `SpecialistBrain` scoring model without explicit design sign-off.
2. Do **not** add any player command that bypasses the brain.
3. Keep pure systems free of MonoBehaviours.
4. All new art must include the locked style keywords:
   > isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

---

## Success Criteria (Met)

- [x] Playable Earth → Luna → Mars flow exists
- [x] Specialists accept/reject flags based on personality + bounty
- [x] Economy loop (extraction, upkeep, resupply) functions
- [x] Threat (Dust Stalkers) creates pressure
- [x] Tech tree unlocks progression
- [x] Blender → FBX → Unity pipeline proven

---

## Known Gaps (Deferred)

- Unit meshes are blockouts (pending Imagine refinement)
- Continue slot is stockpile + last body only (not full snapshot)
- No multiplayer, heightmap terrain, or full combat rewrite
- Building footprints now fill grid cells (Lego docks); remaining gap is art fidelity, not scale

---

## Handoff Notes

Any work that begins after this phase must treat the pure systems as frozen architecture. New features wrap or extend; they do not replace the hero decision loop.

See `Docs/DEVELOPER_HANDOFF.md` for the paste-ready pickup prompt.
