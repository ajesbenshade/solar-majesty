# Phase 1 – Campaign-Quality Demo

**Status:** Packaged (Week 1–6 in; playtest/remesh optional). Phase 2 complete; Phase 3 in progress.  
**Duration:** 4–6 weeks  
**Goal:** Deliver a polished 45–90 minute experience (Earth tutorial → Luna → Mars) that feels like a finished vertical slice and strongly sells the unique fantasy. A new player should finish the arc and feel “this is Majesty 2 in space with AoE progression and SMAC planetary weight.”

---

## Primary Objectives

1. Make the existing multi-body flow feel tight, readable, and dramatic.
2. Elevate specialist personality and flag readability so greed and preference are obvious.
3. Add economic and visual clarity borrowed from Age of Empires 2.
4. Complete a focused art pass on current modules and units.
5. Package a clean demo that can be shown or playtested without explanation.

---

## Key Deliverables

### 1. Mission & Progression Polish
- Tighten conquest gates (dens / sustain / Lunar Rocket / TO LUNA / Mars equivalent).
- Clear failure states and victory conditions per body.
- Improve toast/gate copy and mission stakes feedback.
- Ensure empty-start claim disc + player-placed campus feels intentional, not sparse.
- Add light narrative framing (Overseer logs / SpaceXAI voice) for each body transition.

### 2. Specialist & Flag Clarity (Majesty core)
- Lock and tune the first 3–4 specialist personalities with highly readable affinities.
- Visible interest meter or claim feedback when a flag is posted (how many specialists are tempted).
- Party formation and rest behavior feel natural.
- Low-bounty / high-risk flags are clearly ignored (the fun of Majesty greed).

### 3. Economic Feedback (AoE2 influence)
- Clear visual indicators for what is producing, what is under-supplied, and power/housing pressure.
- Housing and power create real expansion tension.
- Resource drop-off / extraction sites feel purposeful.
- Upkeep and Earth resupply events are telegraphed.

### 4. Threat & Counter Clarity
- Dust Stalker pressure is readable and counterable with Defense specialists + ClearThreat flags.
- Introduce 1–2 additional early threat types with clear counters.
- Threat ecology responds to player presence without becoming pure spawn waves.

### 5. Art & Presentation Pass
- Refine existing unit turnarounds (EngineerBot, ScoutDrone, DefenseMech, DustStalker) via Grok Imagine → Blender.
- Complete campus kits for Earth / Luna / Mars (readable silhouettes at isometric scale).
- Improve industrial dressing, lighting, and atmosphere per body.
- HUD polish: better resource readouts, flag status, specialist status panels.
- Basic VFX for construction, claim, combat, death, launch plume.

**Visual debt vs target:** This pass is demo-grade (blockouts, body lighting, industrial scatter). It is **not** the [Mars visual target](05_PHASE_4_VISUAL_TARGET.md). Do not reopen Phase 1 to remake campus/HUD to mockup fidelity — that is Phase 4. Deferred unit remesh stays optional leftover until then.

### 6. Demo Package
- Title → New Game → Earth empty drop → full arc → Mars feels complete.
- Skippable tutorial that teaches by doing.
- Pause / settings / continue slot functional.
- Smoke-test checklist and DEMO.md updated.

---

## Borrowed Mechanics to Implement

| Source | What to Add |
|--------|-------------|
| Majesty 2 | Stronger visible interest in flags; party synergies; clear “heroes ignore cheap work” moments |
| AoE2 | Age-transition drama, economic readability, housing/power pressure, counter clarity |
| Alpha Centauri | Body-specific flavor text and light tech philosophy on transitions; secret-project teaser |

---

## Task Checklist

**Week 1 – Flow & Stakes**
- [x] Full playthrough of current Earth → Luna → Mars; document every friction point
- [x] Tighten gate conditions and fail/win messaging
- [x] Improve sustain hints and launch site presentation
- [x] Add Overseer log lines for body transitions

**Week 2 – Specialists & Flags**
- [x] Lock personality matrices for Scout / Engineer / Defense (+ optional Medic)
- [x] Add flag interest feedback (UI or world indicator)
- [x] Tune greed thresholds and preference scores for readability
- [x] Party + rest behavior polish

**Week 3 – Economy & Threat**
- [x] Housing / power pressure indicators
- [x] Extraction and upkeep readability
- [x] 1–2 new early threats + counters
- [x] Threat response to campus expansion

**Week 4 – Art & Juice**
- [ ] Unit mesh refinement pass (Imagine → Blender → Unity) — *deferred: sheets + blockouts already in; needs Blender artist*
- [x] Campus kit pass for three bodies (sun angle, grade filter, industrial scatter)
- [x] VFX + audio polish for key moments
- [x] HUD and camera framing improvements

**Week 5–6 – Package & Test**
- [x] Full demo packaging and smoke tests
- [x] Update DEMO.md and DEVELOPER_HANDOFF.md
- [ ] External playtest (if available) and iteration
- [ ] Phase 1 exit review

---

## Success Metrics

- A new player can complete Earth → Luna → Mars in 45–90 minutes without getting stuck.
- Specialists clearly accept high-bounty preferred work and ignore low-bounty dangerous work.
- Economic state is readable at a glance (AoE-style clarity).
- The fantasy “I am the Overseer, not a general” is felt, not just explained.
- Demo can be shown to outsiders and communicates the unique pitch in under two minutes of watching.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Scope creep into full systems | Strictly limit new specialist classes and threats to 1–2 |
| Art time explosion | Prioritize existing assets only; no new body kits beyond the three |
| Brain tuning breaks existing feel | Keep scoring model; only adjust data values and UI feedback |
| Demo feels sparse | Focus on density of existing campus + clear goals rather than more content |

---

## Exit Criteria

- [ ] Playable, polished Earth–Luna–Mars arc
- [x] Documented smoke-test path (`Docs/SMOKE_TEST.md`)
- [ ] Art and UI at “demo-ready” quality for current assets
- [ ] NEXT_STEPS.md and this document updated
- [ ] Ready to begin Phase 2 content expansion without foundation rework *(Phase 2 has since completed)*
