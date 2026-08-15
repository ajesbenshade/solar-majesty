# Solar Majesty – High-Level Roadmap Overview

**Project Codename:** Solar Majesty – SpaceXAI Colonization Protocol  
**Core Fantasy:** You are the Overseer AI. You never control units. You build infrastructure, post bounties/flags, and manage the economy while autonomous specialists decide how to fulfill objectives based on personality, skills, and greed.

**Inspiration Blend:**
- **Majesty 2** — Indirect control, greedy autonomous heroes, flags/bounties, guild housing, economic incentive loop
- **Age of Empires 2** — Clear progression ages, economic clarity, expansion pressure, counter systems, readable feedback
- **Alpha Centauri** — Planetary uniqueness, deep tech choices, terraforming fantasy, narrative weight, secret projects

**Visual north star:** The Mars-campaign concept-art mockup in [Phase 4 – Visual Target](05_PHASE_4_VISUAL_TARGET.md). Current greybox is not that look. Mockup squad bars are HUD chrome only — never click-to-move.

---

## Phase Summary

| Phase | Name | Duration | Goal |
|-------|------|----------|------|
| **0** | Foundation Lock | Complete | Pure systems + vertical slice locked. Do not rewrite core. |
| **1** | Campaign-Quality Demo | 4–6 weeks | 45–90 min polished Earth → Luna → Mars experience that sells the fantasy. |
| **2** | Systems Depth & Solar Expansion | 8–12 weeks | Full multi-body sandbox with real strategic depth. |
| **3** | Content Explosion & Replayability | 10–14 weeks | Rich roster, doctrines, secret projects, high replay value. |
| **4** | Visual Target (Art Production) | 8–12 weeks | Make the concept-art mockup the real in-game look (campus, units, HUD). |
| **5** | Production Values & Ship | 8–12 weeks | Animation, audio, accessibility, packaging, launch readiness. |

---

## Non-Negotiables (All Phases)
- Never add direct unit control or click-to-move that bypasses `SpecialistBrain`.
- Keep pure C# systems under `Assets/Scripts/Systems/`. Runtime only under `Runtime/`.
- All new art must follow locked Grok Imagine style keywords (Phase 0), extended by the Phase 4 visual target sheet — not replaced by it.
- Data-driven via ScriptableObjects.
- Namespace: `SolarMajesty`.

---

## Document Index

- [Phase 0 – Foundation Lock](01_PHASE_0_FOUNDATION_LOCK.md)
- [Phase 1 – Campaign-Quality Demo](02_PHASE_1_CAMPAIGN_QUALITY_DEMO.md)
- [Phase 1 friction notes](PHASE_1_FRICTION.md)
- [Phase 2 – Systems Depth & Solar Expansion](03_PHASE_2_SYSTEMS_DEPTH_SOLAR_EXPANSION.md)
- [Phase 2 exit review](PHASE_2_EXIT.md)
- [Phase 3 – Content Explosion & Replayability](04_PHASE_3_CONTENT_EXPLOSION_REPLAYABILITY.md)
- [Phase 3 exit review](PHASE_3_EXIT.md)
- [Phase 4 – Visual Target (Art Production)](05_PHASE_4_VISUAL_TARGET.md)
- [Phase 4 exit review (blocked)](PHASE_4_EXIT.md)
- [Phase 5 – Production Values & Ship](06_PHASE_5_PRODUCTION_VALUES_SHIP.md)

---

*Last updated: 2026-08-15*  
*Status: Phase 3 complete. Phase 2 complete. Phase 0 locked. Phase 1 packaged. Phase 4 visual target in progress (exit blocked: Game-tab still is empty Sol 1, not a campus match). Phase 5 (ship) after Phase 4.*
