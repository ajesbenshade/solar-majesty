# Phase 4 – Visual Target (Art Production)

**Status:** In progress — **exit blocked** ([PHASE_4_EXIT.md](PHASE_4_EXIT.md)). After #29 (tube web gone): Aaron brief is island yard gaps, geodesic Commons, distant Mars haze, spacesuit dressing. An **editor** Mars still exists (`SM_MarsCampaign_EditorStill.png`) — Commons + airlock + HAB, no HUD. Empty Game-tab still (`SM_MarsCampaign_PlayModeStill.png`) plus campus v1–**v5**. Latest Game-tab is still6 (empty Sol 1). `SM_Capture_NoTubes` is the post-#29 Capture (not a sign-off). Phase 4 is **not** complete. **Not ready for Phase 5.** **Do not stamp exit.**  
**Duration:** 8–12 weeks  
**Goal:** Land Aaron’s **2026-09-07 spaced ITS campus** in engine vs [`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — no interconnect tube webs, empty dirt OK, geodesic Commons, distant haze, spacesuited crossings — without changing the control model. Do **not** pack every cell to match the retired `SM_MarsCampaign_VisualTarget.png`. Bake-off PRs **#26 / #27 / #28** are **not** the EXIT claim. **Do not stamp Phase 4 EXIT.**

Current greybox / Lego airlocks / blockout robots are **not** this look. Phase 4 is the production pass that closes that gap. Phase 3 still owns content (classes, fauna, doctrines, wonders). Phase 5 then ships audio, accessibility, packaging, and first-hour polish on top of this visual bar.

**Week 1 (in):** Mars albedo/sky/long shadows + distant dust-devil dressing; corrugated tube cladding + orange square airlock hubs on the existing Lego docks; yellow gantry cranes / incomplete cladding on build sites; Overseer HUD carbon/gold chrome (5-chip top bar, bounty log, status roster, camera-only minimap).

**Week 2 (in):** HAB / Colony Commons / landing pad+ship / water vs regolith extractor **hero kits** on the square Lego grid (`HeroBuildingKits` via `ModularBuildingFactory`). Footprints unchanged. Tubes/domes remain dressing. Square airlocks stay; no click-to-move.

**Week 4 (in):** Player-facing **Palace → Colony Commons** (HUD **COMMONS**). Guild Hall / Laboratory / Climate Loom / Aegis Spire / Deep Archive industrial dress. Medic hover-stretcher, Harvester tracked scoop, Surveyor tripod, Courier six-wheel hauler, Sentinel dual-barrel turret remesh.

**Week 4 continued (this slice):** Campus Game-tab stills v1–**v6** plus a look-kit hide pass for still5 unused Commons hull-drum ports (`CommonsPort_*` / aliases). still16 signed the remaining look fail as a **dark rectangular airlock box** (wrap `Dress_HubDoor`). Hub is a smaller white paneled square; orange collars on docked faces only; no fourth `CampusTubeRoot`. still18 signed pad / solar / extractors. still19 leftover=skip-frame. still20 leftover=workshop. still21 leftover=inn+wonder extraHab/solar/defense is **not** a VisualTarget density gate. Aaron 2026-09-07 locked the ITS concept and rejected bake-off Captures (#26/#27/#28): CaptureStill prefers a **readable spaced campus** (play ortho 10; leftover kits do **not** stamp; no `CampusDress_TubeRuns`; interior dirt is not force-filled). Shutter hold still suppresses cut/narrative modals. Empty-Sol-1 still6 stays archived. Honest notes below — **not** a Phase 4 exit. **Needs a new Game-tab still from GD. Do not stamp exit.**

---

## Visual target (spaced overseer — PNG retired)

`SM_MarsCampaign_VisualTarget.png` is **retired**. It was a packed-density concept-art mockup and is no longer the Phase 4 EXIT look north star. still21 / still22 do **not** have to match that sheet’s fill-every-dirt-patch packing.

**Dream Loop concept (locked north star):** [`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — ITS language, not the prior tube-web campus. GD capture / judge notes: [`DREAM_LOOP_MARS_LOOK.md`](DREAM_LOOP_MARS_LOOK.md). Bake-off PRs **#26 / #27 / #28** are **not** EXIT. **Not an exit stamp.**

**Aaron look brief (2026-09-07)**

1. **Forgo interconnect tubes** between buildings. Square Lego airlocks may remain as ports. `SpawnTubeRuns` is **gone** (not a `StampTubeRuns` flag). Leftover `CampusDress_TubeRuns` roots are destroyed.
2. **More space** between buildings — empty dirt is intentional; do not pack AABB or fill leftover sockets. Landmark yards use `MinYardGapCells` **4**. Leftover Inn / wonder / extra HAB / extra solar / Defense do **not** stamp on `StampPhase4*`.
3. **Distant haze** toward the horizon — Mars fog 0.014 + `MarsHazeRoot` cards.
4. **Polyhedron / geodesic Commons** — faceted `SM_CommonsGeodesic` + carbon lattice, not a smooth hemisphere-only.
5. Open-ground crossings **read as spacesuited** (`CampusDress_Colonists` dressing). No new `FlagTypes`. No `SpecialistBrain` rewrite.

Keep `StillCaptureHold`, Mars Scale 1x, docked Lego airlock arms, `spawnShowcaseColony` false, Colony Commons, five HUD chips. Bake-off `#26` / `#27` / `#28` parked. **Do not stamp exit.**

Phase 4 EXIT look (Aaron look-clear vs concept — not stamped):

- **Spaced campus** — empty dirt is OK; leave room for rocks, foliage, creatures, and discovery
- **No interconnect tube web** — docked Lego ports only
- **Fun cartoon Majesty-2 overseer** — watch heroes, readable silhouettes, not a packed RTS wallpaper
- Landmark kits (geodesic Commons, HAB cylinder, pad/Starship, solar, extractors) on the **square Lego** grid when they CanFit
- Five HUD chips (**REG / ICE / MET / PWR / BEDS**); Colony Commons; `spawnShowcaseColony` false

StarCraft/Anno-style squad bars and click-commands remain **presentation inspiration only** — the player remains the Overseer AI.

---

## How this extends Phase 0 (does not replace it)

Phase 0 Grok Imagine keywords stay mandatory:

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

Phase 4 **extends** them toward a spaced Mars overseer campus (append when prompting / briefing):

> reddish Mars regolith and hazy orange sky, distant horizon haze, long low-angle shadows, white/black/orange industrial SpaceX-adjacent campus, **no pressurized interconnect tubes**, separate dirt pads with empty yards, large central white **geodesic / polyhedron** command dome with orange trim, square Lego airlock ports only, blue-glow solar arrays, circular landing pad with white Starship-like rocket, spacesuited figures on open ground, yellow gantry cranes on modules under construction, rugged extractors with piping tanks and scaffolding, junction defense turrets, translucent shield readability, dark metallic carbon HUD with gold/orange accents

Do not throw out the Phase 0 lock for a new art bible. Do not revive the retired packed-density PNG as a fidelity target.

---

## Visual pillars (spaced overseer campus)

1. **Mars atmosphere** — reddish-brown cratered regolith, dusty matte ground, hazy orange-to-pale sky, long soft shadows, distant dust-devil scale.
2. **Spaced pads, not a tube web** — separate dirt yards with empty ground between them; square Lego airlock **ports** only. A large central **geodesic / polyhedron** command hub (Colony Commons visual), not a pressurized corridor campus and not a scatter of identical boxes.
3. **Power and arrival landmarks** — solar field with readable blue glow; circular tiered landing pad; white vertical Starship-like rocket with black heat-shielding.
4. **Construction juice** — modules under construction carry **yellow gantry cranes** (and incomplete cladding) so build state is obvious at isometric range.
5. **Rugged extractors** — standalone water / regolith kits with piping, tanks, and scaffolding — not the same Lego box as a HAB.
6. **Defense readability** — turrets on corridor junctions; translucent shield-state over key structures.
7. **Unit silhouettes at a glance** — Engineer: small white biped; Geologist: wheeled rover; Scout: hovering drone; Defense: bulky dark walker. (Courier / convoy as a rugged wheeled hauler when that class is in.)
8. **Overseer HUD chrome** — dark metallic / carbon with gold/orange accents; top resource bar; bounty/quest log; specialist roster as **status/readout**; circular minimap. Action-bar shapes map to existing Overseer tools, never to WASD-click armies.
9. **Breathing room** — do not fill every dirt patch. A readable campus with empty ground between yards is the look, not packed AABB density.

---

## Control mapping (chrome ≠ commands)

| Mockup chrome | Maps to (keep) | Never becomes |
|---------------|----------------|---------------|
| Selected “squad” portrait + IDLE / stats | Specialist / party **status** (who is working which flag, fatigue, greed) | Move, attack, or build-unit commands |
| Bottom action icons (build, infra, defense, menu) | **B** build, **G** flag, research, camera, parties | Direct army orders |
| Unit group roster (Engineer Squad, Scout Wing, …) | Workshop / class **readout** and party list | Control groups that issue orders |
| Hotkeys under minimap | Existing keys (Space camera, **B** build, **R** rest, …) | New click-to-move bindings |
| Bounty / quest banners | Flag / mission / bounty log | Quest markers that path a selected squad |

Non-negotiable: no click-to-move, no selected-unit commands, nothing that bypasses `SpecialistBrain`. Humans live in HABs only; outdoor units are workshop robots. Empty start; Colony Commons first.

---

## Grid & campus construction (decision)

**Keep the current square Lego airlock grid.** Do not switch to a hexagonal layout because the mockup reads as a tube graph.

- Footprints, docking, and placement stay square-cell Lego airlocks.
- Tubes, corridor cladding, the command dome, pad rings, and extractor scaffolding are **meshes and dressing on that grid**.
- Visual goal: separate ITS pads on today’s square modules — spaced enough for dirt, rocks, and fauna between yards. Airlock ports stay; interconnect tube webs do not.

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
| **Mars** | **Hero example.** Sky, ground albedo, shadows, campus kit, pad/ship, extractors on a **spaced** isometric campus. Sign-off shot is a Majesty-2 overseer still — empty dirt OK — not a match to the retired packed PNG. |
| **Earth / Luna** | Same architectural language (Lego ports, geodesic Commons, pad, cranes — no interconnect tube web) with body-correct sky, grade, and lighting. Earth New Game: meadow albedo + cobalt sky + grass/trees/pond in the ortho 16 shot (not the old olive plane, not Mars orange). Luna stays grey crater / black sky. |
| **Belt / Europa** | Kit reuse + body tint / dressing already in Phase 2. Unique silhouette pieces only where ecology demands it (tethered Belt modules, insulated Europa). Do not block Mars hero look on full outer-system uniqueness. |
| **Titan / outer** | Out of scope unless already in content; inherit the same chrome and spaced-pad language. |

---

## Task checklist

### Terrain / sky / lighting
- [x] Mars ground: reddish cratered regolith, rock scatter, matte dust (readable at iso camera)
- [x] Hazy orange Martian sky + long low-angle shadows
- [x] Distant dust-devil / **horizon haze** (thicker Mars fog + `MarsHazeRoot` cards; campus hulls stay unwashed)
- [x] Earth / Luna lighting grades using the same campus kit *(Earth drop: meadow albedo + cobalt sky via lifted SkyTint + grass/trees/pond in camera; still not Mars orange)*

### Corridor campus & building kits
- [x] Square Lego airlock **ports** on existing docks *(Airlock Junction: panel-lined white hub + orange frames/doors; docked faces only. Aaron 2026-09-07: **no** interconnect `CampusDress_TubeRuns`)*
- [x] HAB kit: white/black/orange habs that read as living modules, not generic boxes *(HAB-1 horizontal cylinder on skids; square airlocks still attach; carbon rings + spine seams)*
- [x] Central **geodesic / polyhedron** Colony Commons *(faceted `SM_CommonsGeodesic` + carbon `CommonsLattice` + cupola + equatorial panel rings; player-facing **COMMONS** — not a smooth hemisphere-only)*
- [x] Power: solar array with blue status glow *(PWR-1 node + field)*
- [x] Landing pad: circular tiered pad + white Starship-like upright stack *(orange rings + H; heat-shield belly)*
- [x] Extractors: distinct **water** and **regolith** kits (piping, tanks, scaffolding), standalone from the tube spine *(Farm = AG-1 vaulted greenhouse + ice tanks; Camp = horizontal drum + hopper; Mine = twin silos + A-frame)*
- [x] Defense: junction turrets; translucent shield readability on key structures
- [x] Guild / lab / wonder footprints stay data-sized; dressed in the same industrial language (Guild Hall = **CMD-1** stepped civic + banner + hull panel bands, **LAB-1 cylinder** + dish + carbon rings, Climate Loom lattice, Aegis Spire rings, Deep Archive buried silos). OPS-1 is the Mining annex — **not** remapped onto Commons — with wrap bands + roof seams.
- [x] Workshop hangar + Inn porch as `SM_Hero_*` FBX (tall hangar for Defense / Sentinel shops). Square airlocks still attach.

### Construction juice
- [x] Yellow gantry cranes (or equivalent) on pieces under construction
- [x] Incomplete cladding / scaffolding states that clear on complete
- [x] Build-site readability at isometric zoom without selecting anything

### Units & fauna (hero art)
- [x] Engineer — small white biped *(Imagine v2 sheet-matched blockout: backpack crate, chest docks, cyan visor)*
- [x] Geologist — wheeled rover *(Imagine LO-GEO-1 sheet-matched: vertical orange-housing drill + vial rack)*
- [x] Scout — hovering drone *(Imagine LO-SCT-1 fuselage + hover rotors; not a Surveyor tripod)*
- [ ] Defense — bulky walker *(tracked Imagine Guardian remesh in — continuous treads, red viewport, shoulder pods; procedural fallback is also tracked so it does not clone Engineer; biped walker still open vs PNG)*
- [x] Medic — hover capsule *(Imagine LO-MED-1 sheet-matched: white/black hull, cyan cross, IV pole, four hover discs)*
- [x] Harvester — tracked hopper *(Imagine LO-HAR-1 sheet-matched: orange front blade, rear hopper, side excavator)*
- [x] Surveyor — tripod mast rover *(Imagine LO-SRV-1 sheet-matched: three pad-feet + dish mast ~2.55 m)*
- [x] Courier — six-wheel freight hauler *(Imagine LO-COU-1 sheet-matched: white crate, orange corners, whip antenna)*
- [x] Sentinel — squat dual-barrel turret chassis *(Imagine LO-SEN-1 sheet-matched: continuous treads, orange V chevron, cyan visor; not Defense)*
- [x] Terraformer — tracked dozer *(Imagine LO-TRF-1 sheet-matched: orange front blade + orange rear rake; RTS ~2.5 m class)*
- [x] Fauna RTS silhouettes — Stalker (long predator), Hopper (six-leg shrimp, ~1.7 m), Creeper (graphite isopod ~2 m), Tick (wide crab)
- [x] Fauna leftover — Mite (pillbug), Leech (white ray + cyan groove), Wisp (seven-point ice-star) sheet-matched vs Tick / Creeper / Scout
- [x] Spacesuited colonists on open dirt (`CampusDress_Colonists` walkers — still dressing only; no `SpecialistBrain` rewrite; no new FlagTypes)
- [x] Grok Imagine turnarounds → Blender refine against sheets → `Assets/Resources/Units/SM_Unit_*` *(all ten specialists + seven fauna sheet-matched; Defense PNG biped walker still open)*

### HUD / presentation
- [x] Dark metallic / carbon frame, gold/orange accents
- [x] Top bar: Regolith, WaterIce, Metals, Power, population/beds (rates where we already show them)
- [x] Bounty / quest log chrome (flags + mission beats), not an RTS command queue
- [x] Specialist roster as **status** (class, idle/work/rest, party) — no order buttons on the portrait
- [x] Minimap of the colony (campus footprint, flags, threats)
- [x] Build / research / flag navigation keeps Overseer verbs (B / G / TECH · T, etc.)

### Sign-off
- [ ] Spaced overseer Game-tab still (lighting, campus, units, HUD) — editor still in; empty Game-tab still archived; campus v1–**v4** in (`SM_MarsCampaign_PlayModeCampusStill4.png`) and **fails** orange box airlock / unused orange stub / hull grade. Empty-dirt framing is **not** a fail. Do not chase retired PNG density.
- [ ] Phase 4 exit review — **blocked** ([PHASE_4_EXIT.md](PHASE_4_EXIT.md)); gameplay still Overseer-only

#### Mars look notes (honest — this slice)

`SM_MarsCampaign_VisualTarget.png` is retired — do not score stills against that packed density. Code/dressing was read against `CampusDressing`, `OverseerHud`, `HeroBuildingKits`, `PlanetaryMapDressing`, `DemoAtmosphere`. **Editor still captured** (`SM_MarsCampaign_EditorStill.png`, Camera.Render, Commons + airlock + HAB, avgLum 90.6). **Empty Game-tab still** (`SM_MarsCampaign_PlayModeStill.png`) archived. **Campus v1–v4** — v4 (`SM_MarsCampaign_PlayModeCampusStill4.png`) is geodesic Commons + HAB cylinder, no hex pads, no hopper chip, tutorial 4/5, HUD **REG/ICE/MET/PWR/BEDS**, orange box airlock. Do not stamp exit.

**Reads like the sheet (in-engine today)**
- Mars grade: reddish cratered ground + hazy orange sky, long low-angle shadows. After campus v2, fill/grade/ambient are **cooled** so white hulls can read against the dirt (ground albedo stays Mars orange)
- Tube campus on the **square** Lego grid: corrugated **round** white corridors, **panel-lined square** airlock hubs (carbon corners, inset carbon hatches — not wrap-around orange doors), **orange collars at docked joints only**; unused cardinal sleeves stay hidden so they do not read as orange-hatch boxes; connected docks are **round white tubes** that meet the hub at the cell boundary (not a cube plus, not punched through the hull)
- Packed-dust **aprons** under modules so campus reads as flattened paths vs wild regolith (circular packed dust only — grey cube slabs looked like leftover hex pads in iso and were removed). Earth meadow stays sparse. Landing-pad lights only spawn on a real Landing Pad.
- HAB as a **horizontal HAB-1 cylinder** on skids (white/black/orange, carbon rings + spine seam, not a box, not a Commons dome)
- Colony Commons as the large central **geodesic command-dome citadel** (player-facing **COMMONS**, not Palace / not mockup “Command Center” label) with faceted lattice ribs, cupola, and drum meridians — not a smooth UV sphere
- Solar field: **PWR-1 node** + tilted blue-cyan panels with orange corner brackets
- Circular pad + white/black Starship-like stack (orange rings, H, heat-shield belly, forward flaps)
- Distinct water-ice vs regolith extractor kits *(Farm = vaulted greenhouse + ice tanks; Camp = drum hopper; Mine = silos + A-frame)*; junction dual-barrel turrets (dressing, no click-to-fire)
- Yellow gantry cranes + incomplete cladding on build sites
- Floating **status pips** (gold star language on Commons, cyan shield language on Power / Defense) — primitive spheres, not authored icon meshes
- HUD: dark carbon + gold/orange; five chips (REG / ICE / MET / PWR / BEDS) with gold tabs **and color swatches**; bounty log with flag-color pips; roster as status + class counts (SCT/ENG/DEF/MED); camera-only minimap titled **MAJESTY COLONY** with campus pips. **Verified on campus v4** (Sol 1 chips + planet chips + bounties + MAJESTY COLONY + PTY dock + tutorial 4/5).
- Bottom dock: BLD / FLG / TEC / CAM / **PTY** / MENU — Overseer verbs only (P still forms a party, never a move order)
- Core class reads: Engineer small white biped · Geologist six-wheel rover · Scout hover probe (Imagine fuselage + rotors) · Defense bulky **tracked** guardian (red viewport, continuous treads)
- Terraformer is a tracked dozer with an **orange front blade** and **orange rear rake** (not on the PNG; distinct from Engineer and from Harvester hopper)
- Remaining classes sheet-matched to Imagine JPGs: Medic hover capsule · Harvester orange-blade hopper · Surveyor tripod · Courier white-crate hauler · Geologist vertical drill · Sentinel continuous-tread turret (not Defense)
- Mite is a compact pillbug (not a Tick crab); Leech is a **white ray** with a cyan dorsal groove (not a Creeper millipede); Wisp is a seven-point ice-star (not a Scout)
- Stalker is a long spined predator with four orange eyes and wrapping bone plates
- Hero building FBX (`SM_Hero_*`) prefers Play Mode for HAB / Commons / Power / Farm / Camp / Mine / Defense / pad / guild / LAB / wonders / **workshop hangar / tall hangar / Inn** / **OPS-1**. HAB / Commons / LAB / Power / pad stay **sheet-matched** to ConceptSheets. HAB / Commons / LAB / CMD-1 / OPS-1 now carry **geometric panel lines** (rings, meridians, wrap bands) plus bevelled box hulls. Guild Hall is **CMD-1** civic dress (banner kept). Square airlocks still attach. Fit-to-footprint uses the tighter axis so cylinders are not inflated into squares.
- Earth New Game: meadow albedo in the ortho shot, cobalt sky (procedural SkyTint lifted — catalog blue is no longer used as a dusk multiply), cumulus + grass/trees/pond around the claim, carbon HUD chrome on Playing — empty of buildings, not empty of Phase 4 look
- Empty Mars drop: boulder/capsule outcrops + a crater bowl + a dune ridge in ortho 16 (`PlanetaryMapDressing.EnsureMarsVista`); resource nodes are mounds/capsules (not metal cubes); dens are crater bowls + bone spines (not dark cylinder pads); world-gen rocks are sphere/capsule clusters. `spawnShowcaseColony` stays false.
- Workshops: white hangar bay + orange door tracks + yellow chevrons (`SM_Hero_Workshop`). Defense / Sentinel shops use the taller roof-turret hangar. Inn is a porch-lantern rest hall (`SM_Hero_Inn`), not a hangar clone.
- Campus clutter: crates, barrels, cable spools, pallets, bollards, **orange cones** around extractors / shops (colliders stripped). **Landing pad** keeps pylons / bollards. Commons / HAB no longer get pad-light bollards. Power gets spool + cone.
- Hero kits keep orange/cyan/carbon — building spawn no longer stomps `_BaseColor` via material property block
- Landing pad: extra yellow tier ring under the Starship stack

**Still greybox / not an exit stamp**
- Campus Game-tab still v4 (`SM_MarsCampaign_PlayModeCampusStill4.png`): geodesic Commons + HAB cylinder, **no grey hex pads**, **no idle hopper chip**, hulls whiter than v3 — but **orange box airlock**, unused orange ribbed stub, square hub not readable. still5 (`SM_MarsCampaign_PlayModeCampusStill5.png`): unused Commons cardinal still shows an orange **port ring**. still21 leftover=inn+wonder extraHab/solar/defense is **not** a packed-density fail. Bake-off PRs **#26 / #27 / #28** are **not** EXIT. Empty dirt is OK. Play Mode code after Aaron 2026-09-07: hull drum ports (`CommonsPort_*`) start hidden; RefreshTubes enables docked faces only (white sleeve + one collar) and **does not** stamp `CampusDress_TubeRuns` (`SpawnTubeRuns` removed); leftover Inn / wonder / extra HAB / extra solar / Defense do **not** stamp; still camera stays at play ortho 10 (`FitStillOrtho` must not crop); interior dirt is not force-filled; smaller paneled hub; no CommonsStub; no stacked `CampusTubeRoot`. Needs a **fresh** spaced Game-tab look vs the locked concept — do not stamp v4/v5/still21/bake-off.
- Construction cranes stay runtime dressing (not authored FBX)
- Earth vista trees/pond/grass are primitive dressing (readable at iso, not a heightmap / photogrammetry biome)
- Hero building FBX (`SM_Hero_*`) now sits under the procedural kits. HAB / Commons / LAB / Power / pad match the ConceptSheets at RTS scale (not the sheet's 8×12 m / 40 m / 122 m numbers — footprints stay 4×4 / 6×6). HAB / Commons / LAB / CMD-1 / OPS-1 hulls are **panel-lined** (bevelled boxes + carbon seams), not smooth primitives. CMD-1 is **Guild Hall dress** (not Commons); OPS-1 is the **Mining** annex. Defense bunker is labeled **Defense Battery**, not Command.
- Mockup circular HAB cluster vs our square-dock graph (placement stays square). Aaron 2026-09-07: **no interconnect tube web** — docked Lego ports only. Radial stubs on Commons are visual only (unused cardinals hide when undocked). Airlock Junction is a **panel-lined square hub** (not authored FBX); docks stay square-grid and mate flush at the Lego face.
- Defense in the PNG is a bulky **biped walker**; live mesh stays the Imagine **tracked** guardian so it does not clone the Engineer biped
- Unit meshes are Majesty-readable **blockouts**. All ten specialists + seven fauna are sheet-matched to `ConceptSheets/` turnarounds (Scout keeps hover rotors; Defense stays the Imagine tracked guardian). Imagine scale bars that swapped length/height were ignored for Soil Creeper (~2 m) and Ash Hopper (~1.7 m)
- Status pips / aprons / dust-devils are primitive dressing (spheres, cylinders), not painted mockup icons or VFX
- Ground is albedo + scatter + craters, not a heightmap at mockup crater fidelity (Phase 4 non-goal unless iso readability fails)
- Mockup squad bars / action commands stay HUD chrome — they must not become click-to-move. Class readout is status, not control groups. PTY is party, not a control group.
- Mockup 4-icon resource bar vs our five chips (Regolith kept on purpose). Rates on chips are camp/tax/grid estimates, not a new economy
- HUD is IMGUI carbon/gold, not the mockup’s painted high-fidelity chrome

**Do not treat Phase 4 as exited.** See [PHASE_4_EXIT.md](PHASE_4_EXIT.md): blocked; campus v4 is not an exit stamp. Leftovers are not Phase 5 polish. Pillars are directionally in. HAB / Commons / LAB / CMD / OPS panel bevels are in. Dock sockets mate flush at the Lego face. Gameplay remains Overseer-only.

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

- A still of the Mars campus reads as Aaron’s 2026-09-07 ITS campus (geodesic Commons, spaced pads, no tube web, distant haze, pad/ship, extractors, empty dirt OK). Bake-off #26/#27/#28 are not this metric.
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

- [ ] Mars in-engine matches the spaced overseer look on the pillars *(directionally in; do not score vs the retired packed PNG — see look notes)*
- [x] Square-grid Lego docks + unique HAB / power / extractor / pad / geodesic Commons kits (interconnect tube webs retired for still campus)
- [x] Construction cranes and shield/power readability in
- [x] Hero silhouettes for core classes (Engineer biped, Geologist rover, Scout hover, Defense tracked guardian) — mockup biped walker still open; all ten specialists + seven fauna sheet-matched to `ConceptSheets/` JPGs
- [x] HUD chrome shipped as Overseer presentation (resources mapped correctly; roster is status)
- [x] Phase 0 Grok Imagine keywords still on every new sheet
- [x] Workshop hangar + Inn porch FBX (`SM_Hero_Workshop` / `WorkshopTall` / `Inn`) with procedural fallback
- [ ] Ready for Phase 5 ship (audio, accessibility, save, first-hour, packaging) without another art-direction reset — **blocked**
