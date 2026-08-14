# Phase 3 – Content Explosion & Replayability

**Status:** Complete (Weeks 1–14 in). See [PHASE_3_EXIT.md](PHASE_3_EXIT.md). Phase 4 visual target is next.  
**Duration:** 10–14 weeks  
**Goal:** Fill the expanded solar systems with rich, readable content so every playthrough feels distinct. Specialists become characters, threats become memorable ecology, and optional systems (Doctrines, Secret Projects, challenge modes) create high replay value.

Phase 2 already shipped doctrine techs, three Secret Projects, Harvester/Surveyor, and per-body ecology. Phase 3 expands that content — it does not rewrite cores. Full mockup fidelity is [Phase 4](05_PHASE_4_VISUAL_TARGET.md); this phase still aims new class **silhouettes** at that sheet so hero art is not thrown away later.

---

## Primary Objectives

1. Deliver a full roster of specialist classes with strong, differentiated personalities.
2. Create a deep, body-organized monster / threat roster.
3. Implement optional “Doctrine” / social engineering flavor (light Alpha Centauri).
4. Flesh out Secret Projects / Wonders as major mid-to-late game goals.
5. Add challenge modes, endless colonization, and scoring for long-term engagement.
6. Ensure procedural + hand-authored content supports multiple viable strategies.

---

## Content Targets

### Specialist Roster (Target 8–12 classes)
Each must have:
- Clear silhouette and role
- Distinct personality levers (greed, courage, workaholic bias, task preferences)
- Preferred flag types and rejection behaviors
- Unique voice / status flavor text

Suggested expansion (beyond existing Scout / Engineer / Defense):
- Geologist / Extractor teams
- Terraforming specialists
- Medic / Support
- Scout variants (deep-space, atmospheric)
- Defense variants (anti-swarm, anti-heavy)
- Research-focused specialists
- Logistics / Pilot types (for inter-body)

**Silhouette / readability (aim at the visual target, do not wait for Phase 4 to invent shapes):**
- Engineer — small white biped
- Geologist — wheeled rover
- Scout — hovering drone
- Defense — bulky walker
- Courier / logistics — rugged wheeled hauler when present

Blockout meshes in Weeks 1–4 should be distinguishable at a glance. Hero materials, Mars lighting, tube campus, and HUD chrome stay Phase 4. Do not change class identities overnight just to match mockup labels.

### Monster / Threat Roster
Organized by celestial body. Each body needs 4–8 native threats with clear counters and ecological roles (predator, scavenger, environmental hazard, adaptive).

Examples of desired flavor:
- Lunar / Martian surface fauna and dust hazards
- Belt swarm and micro-meteor threats
- Europa ice and radiation-adapted life
- Titan atmospheric and chemical threats

**Weeks 1–4 roster (in play):**

| Body | Signature mix | Defend (F5) | Clear Threat (F2) |
|------|----------------|-------------|-------------------|
| Earth | Soil creeper + watt leech | Creeper, mite | Leech, stalker |
| Luna | Ash hopper + dust tick | Dust tick | Hopper, leech, stalker |
| Mars | Dust wisp + dust creeper | Creeper, mite | Wisp, hopper, stalker |
| Belt | Rock mite/tick swarm + shard hopper | Mite, tick | Hopper, stalker |
| Europa | Fissure leech + ice wisp + ice creeper | Creeper | Leech, wisp, stalker |

No intelligent aliens — pure fauna, adaptive wildlife, and environmental hazards.

### Buildings & Modules
- Complete modular kits per body *(data + greybox footprints this phase; tube/dome/crane/extractor hero kits are Phase 4)*
- Guild / hall variants that attract specific specialist classes
- Advanced power, research, and terraforming structures
- Wonder / Secret Project buildings with unique footprints and effects

### Doctrines (Optional Light SMAC Layer)
Player-selectable or unlockable colony philosophies that:
- Slightly bias specialist preference weights
- Modify economy (efficiency vs growth vs defense)
- Unlock unique flags or buildings
- Create narrative flavor without forcing a single path

Keep this system optional and data-driven so it can be tuned or disabled.

### Secret Projects / Wonders
Major milestones that:
- Require significant focused investment
- Grant permanent or powerful temporary bonuses
- Have strong narrative payoff
- Are visible goals on the tech / progression map

---

## Systems to Support Content

- Robust data-driven spawning and affinity tables
- Specialist “memory” or reputation that improves over a campaign
- Procedural map elements mixed with hand-authored points of interest
- Challenge modifiers (harder greed thresholds, faster threat escalation, limited Earth resupply, etc.)
- End-game scoring / Overseer rating that rewards efficient colonization and specialist satisfaction

---

## Task Checklist (High Level)

**Weeks 1–4 – Specialist & Threat Expansion**
- [x] Blender hero meshes for Medic / Harvester / Surveyor / Terraformer / Courier / Geologist / Sentinel / Mite / Leech / Ice Wisp / Rock Tick / Soil Creeper / Ash Hopper
- [x] Design and data-author 4–6 new specialist classes *(4 in: Terraformer, Courier, Geologist, Sentinel — 10 classes total)*
- [x] Full personality and affinity matrices *(existing GetPreference + stronglyAttracts; no brain rewrite)*
- [x] Body-organized threat roster (minimum 3–4 bodies fully populated) *(Earth / Luna / Mars / Belt / Europa each have 4–6 natives: stalker + body mix; Soil Creeper / Ash Hopper added)*
- [x] Counter clarity and ecological behaviors *(mites/ticks/creepers = Defend; leeches/wisps/hoppers = Clear Threat)*

**Weeks 5–7 – Buildings, Guilds, Wonders**
- [x] Complete modular building sets for all supported bodies *(greybox hull tints per body; tube/dome/crane hero kits wait for Phase 4)*
- [x] Guild / hall attraction system *(Guild Hall inherits nearest workshop class or assign SCOUT/ENG/DEF/MED; flags nearby pull that class — same workshop-bonus path)*
- [x] 4–6 Secret Project / Wonder designs implemented *(6: Anvil, Skyhook, Gene Vault, Climate Loom, Aegis Spire, Deep Archive — last three also place 6×6 landmarks)*
- [x] Art pipeline for new content (Grok Imagine → Blender), using Phase 0 keywords plus [Phase 4 silhouette goals](05_PHASE_4_VISUAL_TARGET.md) — prompt sheets in `Docs/GROK_IMAGINE_UNIT_PROMPTS.md`; remesh waits for Phase 4

**Weeks 8–10 – Doctrines & Replay Systems**
- [x] Doctrine data and light behavioral modifiers *(Settings stance: Balanced / Open Hands / Aegis Watch / Survey First — nudges `SpecialistContext` hunger, courage, workshop bonus, and `Brain.ConsiderRange` only; `ScoreFlag` unchanged)*
- [x] Challenge mode framework *(Austere 55% start stockpile · Swarm fauna/ambient · Tight Purse resupply + dock fee)*
- [x] Endless / free-play colonization mode *(campaign hop hidden on win; keep colonizing; Shift+F10 still hops)*
- [x] Scoring and rating systems *(OverseerScore letter S/A/B/C/D from dens/sustain/launch/economy/roster/pace; HUD + win banner)*

**Weeks 11–14 – Integration, Balancing, Content Pass**
- [x] Full content integration and balancing *(ReplayRules readability bump; guild/wonder place costs; Swarm spawn cadence; Tight Purse fee/interval; S-letter dens+roster+pace gate)*
- [x] Multiple strategy viability testing *(desktop: extract/haul, guild+workshop, aegis/defense — noted in PHASE_1_FRICTION; live 45–90 min leftover)*
- [x] Narrative / flavor text pass *(SpecialistFlavor voice; guild callsigns; body briefing/endless logs; claim lines)*
- [x] Documentation of all new content *([PHASE_3_EXIT.md](PHASE_3_EXIT.md) catalog)*
- [x] Phase 3 exit review

### Weeks 8–10 mapping (in play)

| Control | Effect | When it applies |
|---------|--------|-----------------|
| **Open Hands** | Hunger +0.26 (cheaper flags via existing greed bypass at 0.75); courage ×0.90 | Live (context) |
| **Aegis Watch** | Courage ×1.22; workshop bonus +0.18; hunger −0.08 | Live |
| **Survey First** | Consider range ×1.50; workshop bonus +0.10 | Range on settings close / boot; workshop live |
| **Austere** | Start stockpile ×0.55 | New Game / scene reload (Continue keeps saved pile) |
| **Swarm** | Fauna cap ×1.50; spawn weights ×1.22; ambient ×1.28; spawn interval ×1.35 | Reload |
| **Tight Purse** | Resupply interval ×1.55; extra dock fee 8 | Live via `RefreshTechEffects` on settings close |
| **Endless** | Win copy “keep colonizing”; body `EndlessLog`; no **TO {next}** | Live |

Tune these in `ReplayRules` constants only. Do not retune `SpecialistBrain.ScoreFlag`.

### Weeks 11–14 balance / flavor notes (done)

Desktop pass (not a live 45–90 min). Numbers live in `ReplayRules`:

- **Open Hands vs Aegis vs Survey** — Open Hands hunger +0.26 crosses the 0.75 cheap-flag bypass at spawn. Aegis workshop +0.18 / courage ×1.22. Survey consider ×1.50. Still no ScoreFlag rewrite.
- **Austere** Earth 187 MET: Palace + airlock + HAB + workshop = 164. Farm waits on extract or the ship.
- **Swarm** cap ×1.50 with spawn interval ×1.35 — more F5/F2 after buildings exist, not a dump on the empty drop.
- **Tight Purse** interval ×1.55 and +8 MET fee. Drop pile unchanged.
- **S rating** requires dens + gates + ≥3 robots + mean HP ≥0.55 + elapsed ≤12 min. Stockpile cannot buy S.
- Flavor: `SpecialistFlavor` voice on cards and claim logs; body `EndlessLog`; guild callsigns (Horizon / Anvil / Aegis / Triage).
- Three mid-game strategies stay viable on paper (extract/haul, guild+workshop, aegis/defense). Notes in `PHASE_1_FRICTION.md`.

---

## Borrowed Mechanics Emphasis

| Source | Application |
|--------|-------------|
| Majesty 2 | Deep hero personality, guild attraction, memorable individual specialists |
| AoE2 | Clear unit roles and counters, multiple viable compositions |
| Alpha Centauri | Doctrines / social engineering, Secret Projects, planetary uniqueness in content |

---

## Success Metrics

- Players form emotional attachment to individual specialists (“my favorite Engineer always takes the risky builds”).
- Different bodies and doctrines create noticeably different play styles.
- At least three distinct viable strategies exist for mid-to-late game.
- Replay value is high: players want to try “one more colonization” with different doctrines or challenge settings.
- Content density supports 10–20+ hour campaigns without feeling repetitive.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Content bloat without depth | Prioritize strong personalities and clear counters over pure quantity |
| Doctrine system feels mandatory | Keep it optional and lightly weighted |
| Balancing nightmare across bodies | Use data-driven tuning and focused playtests per body |
| Art production bottleneck | Strict Grok Imagine + modular Blender pipeline; reuse components aggressively |

---

## Exit Criteria

- [x] 8+ specialist classes with distinct, readable personalities *(10 classes)*
- [x] Full threat roster for all supported bodies *(Earth / Luna / Mars / Belt / Europa)*
- [x] Guilds, Secret Projects, and optional Doctrines functional
- [x] Challenge and free-play modes available
- [x] Content is balanced enough for enjoyable multi-hour sessions *(desktop pass; live 45–90 min leftover)*
- [x] Ready for [Phase 4 visual-target production](05_PHASE_4_VISUAL_TARGET.md) without needing more core systems
