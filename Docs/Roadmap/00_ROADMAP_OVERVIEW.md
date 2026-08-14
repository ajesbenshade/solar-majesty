# Solar Majesty – High-Level Roadmap Overview

**Project Codename:** Solar Majesty – SpaceXAI Colonization Protocol  
**Core Fantasy:** You are the Overseer AI. You never control units. You build infrastructure, post bounties/flags, and manage the economy while autonomous specialists decide how to fulfill objectives based on personality, skills, and greed.

**Inspiration Blend:**
- **Majesty 2** — Indirect control, greedy autonomous heroes, flags/bounties, guild housing, economic incentive loop
- **Age of Empires 2** — Clear progression ages, economic clarity, expansion pressure, counter systems, readable feedback
- **Alpha Centauri** — Planetary uniqueness, deep tech choices, terraforming fantasy, narrative weight, secret projects

---

## Phase Summary

| Phase | Name | Duration | Goal |
|-------|------|----------|------|
| **0** | Foundation Lock | Complete | Pure systems + vertical slice locked. Do not rewrite core. |
| **1** | Campaign-Quality Demo | 4–6 weeks | 45–90 min polished Earth → Luna → Mars experience that sells the fantasy. |
| **2** | Systems Depth & Solar Expansion | 8–12 weeks | Full multi-body sandbox with real strategic depth. |
| **3** | Content Explosion & Replayability | 10–14 weeks | Rich roster, doctrines, secret projects, high replay value. |
| **4** | Production Values & Ship | 8–12 weeks | Full art/audio polish, accessibility, packaging, launch readiness. |

---

## Non-Negotiables (All Phases)
- Never add direct unit control or click-to-move that bypasses `SpecialistBrain`.
- Keep pure C# systems under `Assets/Scripts/Systems/`. Runtime only under `Runtime/`.
- All new art must follow locked Grok Imagine style keywords.
- Data-driven via ScriptableObjects.
- Namespace: `SolarMajesty`.

---

## Document Index

- [Phase 0 – Foundation Lock](01_PHASE_0_FOUNDATION_LOCK.md)
- [Phase 1 – Campaign-Quality Demo](02_PHASE_1_CAMPAIGN_QUALITY_DEMO.md)
- [Phase 1 friction notes](PHASE_1_FRICTION.md)
- [Phase 2 – Systems Depth & Solar Expansion](03_PHASE_2_SYSTEMS_DEPTH_SOLAR_EXPANSION.md)
- [Phase 3 – Content Explosion & Replayability](04_PHASE_3_CONTENT_EXPLOSION_REPLAYABILITY.md)
- [Phase 4 – Production Values & Ship](05_PHASE_4_PRODUCTION_VALUES_SHIP.md)

---

*Last updated: 2026-08-14*  
*Status: Living document – update checklists as milestones complete.*
