# Phase 4 – Visual Target (Art Production)

**Status:** In progress — Week 3 junction turrets + solar-field landmark (after Week 1 campus / HUD / Mars atmosphere and Week 2 hero kits)  
**Duration:** 8–12 weeks  
**Goal:** Make the Mars-campaign concept-art mockup the real in-game look — environment, modular campus, hero unit meshes, construction juice, and Overseer HUD chrome — without changing the control model.

Current greybox / Lego airlocks / blockout robots are **not** this look. Phase 4 is the production pass that closes that gap. Phase 3 still owns content (classes, fauna, doctrines, wonders). Phase 5 then ships audio, accessibility, packaging, and first-hour polish on top of this visual bar.

**Week 1 (in):** Mars albedo/sky/long shadows + distant dust-devil dressing; corrugated tube cladding + orange square airlock hubs on the existing Lego docks; yellow gantry cranes / incomplete cladding on build sites; Overseer HUD carbon/gold chrome (5-chip top bar, bounty log, status roster, camera-only minimap).

**Week 2 (in):** HAB / Palace Keep / landing pad+ship / water vs regolith extractor **hero kits** on the square Lego grid (`HeroBuildingKits` via `ModularBuildingFactory`). Footprints unchanged. Tubes/domes remain dressing. Square airlocks stay; no click-to-move.

**Week 3 (in):** Junction dual-barrel gun/sensor pods on square airlock hubs; Power buildings are a **blue-glow solar field** on the existing 4×4 footprint (not a side overlay); Defense Command is a bunker+roof turret kit (not a keep-dome). Week 1 shield bubbles stay. No click-to-fire. Bounded leftover: Engineer / Geologist / Scout blockout remesh started (Defense stays the Imagine tracked guardian, not a biped walker).

---

## Visual target

![Mars campaign visual target — high-fidelity concept-art mockup of a Solar Majesty colony](SM_MarsCampaign_VisualTarget.png)

*Mars campaign visual target (2026 concept-art mockup). This is the production look for terrain, campus architecture, unit silhouettes, construction juice, and HUD chrome. StarCraft/Anno-style squad bars and click-commands in the mockup are **presentation inspiration only** — the player remains the Overseer AI.*

---

## How this extends Phase 0 (does not replace it)

Phase 0 Grok Imagine keywords stay mandatory:

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

Phase 4 **extends** them toward this sheet (append when prompting / briefing):

> reddish Mars regolith and hazy orange sky, long low-angle shadows, white/black/orange industrial SpaceX-adjacent campus, pressurized corridor tubes linking habs, large central white command dome with orange trim, blue-glow solar arrays, circular landing pad with white Starship-like rocket, yellow gantry cranes on modules under construction, rugged extractors with piping tanks and scaffolding, junction defense turrets, translucent shield readability, dark metallic carbon HUD with gold/orange accents

Do not throw out the Phase 0 lock for a new art bible. This sheet is the fidelity target on top of that lock.

---

## Visual pillars (from the mockup)

1. **Mars atmosphere** — reddish-brown cratered regolith, dusty matte ground, hazy orange-to-pale sky, long soft shadows, distant dust-devil scale.
2. **Tube campus** — pressurized white corridor/tube network linking habs; a large central **domed command hub** (Keep / CMD visual), not a scatter of identical boxes.
3. **Power and arrival landmarks** — solar field with readable blue glow; circular tiered landing pad; white vertical Starship-like rocket with black heat-shielding.
4. **Construction juice** — modules under construction carry **yellow gantry cranes** (and incomplete cladding) so build state is obvious at isometric range.
5. **Rugged extractors** — standalone water / regolith kits with piping, tanks, and scaffolding — not the same Lego box as a HAB.
6. **Defense readability** — turrets on corridor junctions; translucent shield-state over key structures.
7. **Unit silhouettes at a glance** — Engineer: small white biped; Geologist: wheeled rover; Scout: hovering drone; Defense: bulky dark walker. (Courier / convoy as a rugged wheeled hauler when that class is in.)
8. **Overseer HUD chrome** — dark metallic / carbon with gold/orange accents; top resource bar; bounty/quest log; specialist roster as **status/readout**; circular minimap. Action-bar shapes map to existing Overseer tools, never to WASD-click armies.

---

## Control mapping (chrome ≠ commands)

| Mockup chrome | Maps to (keep) | Never becomes |
|---------------|----------------|---------------|
| Selected “squad” portrait + IDLE / stats | Specialist / party **status** (who is working which flag, fatigue, greed) | Move, attack, or build-unit commands |
| Bottom action icons (build, infra, defense, menu) | **B** build, **G** flag, research, camera, parties | Direct army orders |
| Unit group roster (Engineer Squad, Scout Wing, …) | Workshop / class **readout** and party list | Control groups that issue orders |
| Hotkeys under minimap | Existing keys (Space camera, **B** build, **R** rest, …) | New click-to-move bindings |
| Bounty / quest banners | Flag / mission / bounty log | Quest markers that path a selected squad |

Non-negotiable: no click-to-move, no selected-unit commands, nothing that bypasses `SpecialistBrain`. Humans live in HABs only; outdoor units are workshop robots. Empty start; Palace Keep first.

---

## Grid & campus construction (decision)

**Keep the current square Lego airlock grid.** Do not switch to a hexagonal layout because the mockup reads as a tube graph.

- Footprints, docking, and placement stay square-cell Lego airlocks.
- Tubes, corridor cladding, the command dome, pad rings, and extractor scaffolding are **meshes and dressing on that grid**.
- Visual goal: a connected pressurized campus that *looks* like the mockup’s tube network while still placing like today’s modules.

---

## Economy HUD mapping (do not invent a fifth resource)

The mockup shows four top icons (metal, bio/food, energy, pop). Live economy stays `Regolith / WaterIce / Metals / Power` plus population/beds.

| Mockup icon | Live readout | Note |
|-------------|--------------|------|
| Metal / iron | **Metals** | Primary industrial stockpile. |
| Bio / food | **WaterIce** | Life-support analog (ice, hydroponics, sustain). **Not** a new food resource. |
| Energy | **Power** | Capacity / surplus, not a fifth currency. |
| Population | **Beds / population** | HAB housing pressure, not a stockpile. |
| *(missing in mockup)* | **Regolith** | Keep visible. The mockup collapsed bulk feedstock; we do not drop it to match a 4-slot bar. |

HUD layout may use five industrial/pop chips (four resources + beds) in the same dark-carbon / gold-orange chrome. Do not add Bio as a fifth economy without an explicit design note.

---

## Non-goals (explicit)

- Direct squad commands, control groups that issue orders, WASD-click armies, or any player pathing.
- Replacing square airlocks with a hex grid or freeform tube graph as the placement model.
- Rewriting `SpecialistBrain` scoring or class identities overnight to match mockup labels.
- Treating the mockup’s four-resource bar as a new economy.
- Reopening Phase 1/2 as mandatory art remakes (those phases stay packaged/complete; this phase pays the visual debt).
- Full audio, save/load, accessibility, store packaging — those are **Phase 5**.
- Heightmap terrain rewrite as a blocker (readable regolith + scatter + sky/lighting can land without a new world-gen stack). Call out a heightmap only if isometric readability truly requires it.

---

## Body-specific grades

| Body | Grade in this phase |
|------|---------------------|
| **Mars** | **Hero example.** Match the mockup: sky, ground albedo, shadows, campus kit, pad/ship, extractors. Sign-off shot is an isometric Mars campus that a stranger would match to the concept art. |
| **Earth / Luna** | Same architectural language (tubes, dome, pad, cranes) with body-correct sky, grade, and lighting (Earth atmosphere / lunar black sky). Not a unique art bible. |
| **Belt / Europa** | Kit reuse + body tint / dressing already in Phase 2. Unique silhouette pieces only where ecology demands it (tethered Belt modules, insulated Europa). Do not block Mars hero look on full outer-system uniqueness. |
| **Titan / outer** | Out of scope unless already in content; inherit the same chrome and tube language. |

---

## Task checklist

### Terrain / sky / lighting
- [x] Mars ground: reddish cratered regolith, rock scatter, matte dust (readable at iso camera)
- [x] Hazy orange Martian sky + long low-angle shadows
- [x] Distant dust-devil / haze scale (dressing or light VFX, not a new threat type unless Phase 3 already has it)
- [x] Earth / Luna lighting grades using the same campus kit

### Corridor campus & building kits
- [x] Pressurized corridor / tube dressing on existing square docks
- [x] HAB kit: white/black/orange habs that read as living modules, not generic boxes
- [x] Central **domed command hub** as Palace Keep / CMD hero mesh
- [x] Power: solar array with blue status glow
- [x] Landing pad: circular tiered pad + white Starship-like upright stack
- [x] Extractors: distinct **water** and **regolith** kits (piping, tanks, scaffolding), standalone from the tube spine
- [x] Defense: junction turrets; translucent shield readability on key structures
- [ ] Guild / lab / wonder footprints can stay data-sized; dress them in the same industrial language

### Construction juice
- [x] Yellow gantry cranes (or equivalent) on pieces under construction
- [x] Incomplete cladding / scaffolding states that clear on complete
- [x] Build-site readability at isometric zoom without selecting anything

### Units & fauna (hero art)
- [x] Engineer — small white biped *(blockout remesh; Imagine refine still open)*
- [x] Geologist — wheeled rover *(blockout remesh; Imagine refine still open)*
- [x] Scout — hovering drone *(blockout remesh; Imagine refine still open)*
- [ ] Defense — bulky walker *(tracked Imagine guardian remesh in; biped walker still open)*
- [ ] Remaining Phase 3 classes (Medic, Harvester, Surveyor, Terraformer, Courier, variants) get the same silhouette bar, not identical chassis
- [ ] Fauna / threats: readable silhouettes at range (Stalker, mite, leech, body-natives) in the same industrial-wildlife language
- [ ] Grok Imagine turnarounds → Blender refine → `Assets/Resources/Units/SM_Unit_*` (and building FBX pipeline)

### HUD / presentation
- [x] Dark metallic / carbon frame, gold/orange accents
- [x] Top bar: Regolith, WaterIce, Metals, Power, population/beds (rates where we already show them)
- [x] Bounty / quest log chrome (flags + mission beats), not an RTS command queue
- [x] Specialist roster as **status** (class, idle/work/rest, party) — no order buttons on the portrait
- [x] Minimap of the colony (campus footprint, flags, threats)
- [x] Build / research / flag navigation keeps Overseer verbs (B / G / TECH · T, etc.)

### Sign-off
- [ ] Side-by-side: mockup vs in-engine Mars isometric (lighting, campus, units, HUD)
- [ ] Phase 4 exit review — visual bar met; gameplay still Overseer-only

---

## Borrowed look vs borrowed control

| Source | Take | Leave |
|--------|------|-------|
| Mockup / StarCraft / Anno chrome | Density of information, metallic HUD, minimap, construction cranes | Selected-unit commands |
| Majesty 2 | Heroes you watch, not click | — |
| AoE2 | Economic and construction readability | Villager micro |
| Alpha Centauri | Planetary atmosphere as identity (Mars first) | — |

---

## Success metrics

- A still of the Mars campus is recognizably the concept art (tubes, dome, pad/ship, extractors, sky).
- A player can tell Engineer / Geologist / Scout / Defense apart at a glance without nameplates.
- Construction and shield/power state are readable without opening a panel.
- HUD feels like the mockup’s **material language** while every click still goes through build, flags, research, camera, or parties.
- No new player verb that bypasses `SpecialistBrain`.

---

## Risks & mitigations

| Risk | Mitigation |
|------|------------|
| Scope explodes into a full art reboot of every body | Mars hero + shared kit; other bodies tint/dress |
| Mockup seduces a control-model rewrite | Non-goals above; chrome mapping table |
| Hex / freeform tubes break placement | Square Lego docks stay; tubes are dressing |
| Phase 3 content still landing | Start Phase 4 after Phase 3 exit; silhouette notes in Phase 3 prevent rework |
| Extractors still look like HABs | Unique kits are a Phase 4 exit item, not optional dressing |

---

## Exit criteria

- [ ] Mars in-engine matches the visual target on the eight pillars
- [x] Square-grid tube campus + unique HAB / power / extractor / pad / keep-dome kits
- [x] Construction cranes and shield/power readability in
- [ ] Hero silhouettes for core classes (at least Engineer, Geologist, Scout, Defense)
- [ ] HUD chrome shipped as Overseer presentation (resources mapped correctly; roster is status)
- [ ] Phase 0 Grok Imagine keywords still on every new sheet
- [ ] Ready for Phase 5 ship (audio, accessibility, save, first-hour, packaging) without another art-direction reset
