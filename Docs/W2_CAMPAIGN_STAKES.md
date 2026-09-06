# W2 campaign stakes — text cutscenes / advisor beats

**Audience:** Narrative (stakes + modal copy), LP (string keys / ArrivalLog overlays), Producer (glance: does the hop *hurt*).  
**Status:** W2 stakes draft. Copy only — not a quest graph, not a new `FlagType`, not App Store text, not art.  
**Art:** Phase 4 **EXIT** is still art-blocked (`Docs/Roadmap/PHASE_4_EXIT.md`). This doc runs **in parallel**. Mars lines assume the Compact campus that will be, not the empty Sol 1 still.  
**Ids:** Decree slugs from [`FLAG_TYPE_MAP_EARTH_LUNA_MARS.md`](FLAG_TYPE_MAP_EARTH_LUNA_MARS.md) and `FlagDecreeIds`. Toast / travel voice already drafted in [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md). This file is the **stakes spine** those beats sit under. LP will later hook title-match toasts; do not invent slugs here.  
**Trade routes / finds:** [`W2_TRADE_ROUTES_AND_FINDS.md`](W2_TRADE_ROUTES_AND_FINDS.md) — six named lanes + eight find hooks on the existing eight types; PWR/MET framing, not a fifth resource. Not Phase 4 EXIT.

Flags stay **decrees**. The royal overseer posts them. Ego heroes (Horizon, Anvil, Aegis, Triage, Strip, Chart, Bloom, Haul, Core, Rim) take or ignore them. The wry advisor briefs. COMMONS is the civic name — never Palace, never CMD as a place. No Majesty 2 / Paradox proper names. No click-to-move fantasy. Belt and Europa are **named destinations after a Mars win only** — no decree lists.

---

## The question

The player must feel that Earth is a closing book: the meadow can feed a court and still fail to *be* one. Conquest is survival plus legitimacy — Sol Authority that stays on one green claim becomes a picnic with a charter. Each hop is a bill the crown either pays once (leave) or pays forever (stay). Sightseeing is not on the docket.

---

## Stakes thesis

- **The cradle is finite.** Eight souls, a farm, and a meadow levy (`earth.extract.levy_the_meadow_farm`) are a sustain ceiling, not a solar reign. Overcrowding is the polite word for “the court is already arguing over furrows.”
- **A Commons on grass is not a crown.** Sol Authority’s legitimacy is *posted expansion*. Raise the civic, dock the HAB, charter the hall — then admit the meadow cannot stamp a second world.
- **Leaving is how the overseer keeps the title.** Stay, and you are a landlord of creepers. Go, and the decrees still mean something past the orange disc.
- **Luna already has owners.** The Lunar Freeholds tax ice, ore, and the pad (4 MET dock fee). Chart the rille and you are charting someone else’s ledger.
- **The tithe is the lesson.** Weigh the ore, pay the receipt, watch hoppers test the HAB. Staying on Luna means paying forever for lanes you do not own.
- **Mars is the Compact’s industrial spine.** The Ares Guild Compact does not curtsy. Seat the COMMONS on red dust or the crown remains a well-fed anecdote.
- **Win Mars or the hop was a picnic.** Grid, wisps, crust — hold the campus. Then the Belt is named as the next freight frontier. Named only. No map, no decrees, no Europa copy.

---

## Text cutscenes

Numbered spine. Each `id` is a **stable string key** for LP (`cut.{body}.{beat}`). Bodies are 3–6 short lines, advisor or overseer briefing, readable as a modal. Mid-act scenes fire after the listed decrees complete (title-match later). Travel scenes overlay existing `ArrivalLog` / `VictoryLog` / `CampaignProgress.QueueTravelLog` — they do not mint `FlagDecreeIds` consts.

Advisor toasts in `W2_ADVISOR_AND_CHAIN_BEATS.md` (E0–E10, L0–L9, M0–M9) sit *beside* these modals. Do not replace `SpecialistFlavor` claim lines.

---

### 1. `cut.earth.prologue`

| | |
|---|---|
| **when** | New Game / Earth arrival |
| **decree_id** | — |
| **travel hook** | Replace `CelestialBodyCatalog` Earth `ArrivalLog` (“Colony Commons is live…” is wrong on empty-start). Same beat as advisor `advisor.travel.earth.empty_drop` (E0). |
| **title** | The Meadow Is Not a Reign |

**body**

The cradle is a closing book. Sol Authority still stamps decrees; the meadow does not stamp them back.  
A claim disc in the grass is a picnic. Raise a Commons before anyone files us as scenery.  
Horizon can chart the apron. Anvil can weld. Neither makes the green infinite.  
We stay here, we become tenants of a farm. We leave, we remain a crown.

---

### 2. `cut.earth.mid_court`

| | |
|---|---|
| **when** | After decree complete — Commons stands, first HAB docks, Guild Charter signed |
| **decree_id** | `earth.build.raise_the_commons` · `earth.build.dock_the_first_hab` · `earth.researchsite.charter_the_hall` (fire once all three complete; do not wait on dens / furrows) |
| **travel hook** | — (modal or long toast). Overlay `GameLoop` “Guild Charter signed…” with advisor `advisor.earth.charter_the_hall.complete` for tone; this cut is the stakes, not the assign instruction. |
| **title** | Court, Beds, Charter |

**body**

The Commons stands. The HAB is a ledger. The hall has a charter.  
That is a court. It is not a solar power.  
ICE and REG have a ceiling on this meadow. Eight souls and a farm do not impress anyone who already owns a lane.  
Sol Authority’s legitimacy is expansion. Farms are how we eat. They are not why we rule.

---

### 3. `cut.earth.to_luna`

| | |
|---|---|
| **when** | Conquest win / body unlock travel — Lunar Rocket staged + Earth hold |
| **decree_id** | `earth.build.stage_the_lunar_rocket` (complete; pair `TechId.LunarRocket`) |
| **travel hook** | Replace Earth `VictoryLog` + hop “Departure from Earth. Trajectory locked: Luna.” Same beat as `advisor.travel.earth.to_luna` (E10). HUD banner may still read **TO LUNA**. |
| **title** | Trajectory Locked — Luna |

**body**

Earth holds. The Lunar Rocket is on the pad, not in a toast.  
The Freeholds already have a receipt prepared. Four MET at the dock. They call it hospitality.  
We call it the first bill of a longer reign.  
Do not wave at the trajectory. They will invoice the wave.

---

### 4. `cut.luna.arrival`

| | |
|---|---|
| **when** | Luna arrival (body unlock travel) |
| **decree_id** | — |
| **travel hook** | Replace Luna `ArrivalLog`. Keep hopper / tick tee. Same beat as `advisor.travel.luna.arrival` (L0). |
| **title** | Hospitality, Itemized |

**body**

Luna insertion. The Freeholds smile like a weigh-station.  
Dock fee is four MET. Ice and ore have owners. The lanes have owners.  
Ash hoppers will test the HAB. Dust ticks will test the mines. Neither is the real tax.  
The real tax is staying. Stay, and you pay forever.

---

### 5. `cut.luna.mid_warrant`

| | |
|---|---|
| **when** | After decree complete — levy receipt on the books, or first hopper warrant claimed |
| **decree_id** | `luna.extract.weigh_the_freehold_ore` · `luna.clearthreat.root_the_hopper_saboteurs` (fire on the second of these two completes / first Aegis claim on the warrant; support ids `luna.explore.chart_the_tariff_rille`, `luna.defendarea.ward_the_weigh_station` do not gate the cut) |
| **travel hook** | — (modal). Toast-on-Post for the warrant still waits on title-match (`W2_ADVISOR` L2 / L4). |
| **title** | Receipt and Warrant |

**body**

They call it a tariff. Strip and Core hauled the receipt. Do not lose it.  
Someone cut the hoppers loose on the HAB. Aegis takes warrants, not sermons.  
The lanes were spoken for before we arrived. Buy them with MET, or leave them behind.  
A crater Commons that pays rent is a nicer picnic. We did not come for nicer.

---

### 6. `cut.luna.to_mars`

| | |
|---|---|
| **when** | Conquest win / body unlock travel — Mars Ship staged + Luna hold |
| **decree_id** | `luna.researchsite.commission_the_mars_ship` (complete; pad labour is still a `Build` — no ninth type) |
| **travel hook** | Replace Luna `VictoryLog`. Same beat as `advisor.travel.luna.to_mars` (L9). Banner **TO MARS**. |
| **title** | Trajectory Locked — Mars |

**body**

Luna holds. Mars Ship staged. Pack guild manners.  
The Compact does not curtsy. It charters.  
We have paid the Freehold tithe long enough to learn the lesson: ice and ore without a seat is someone else’s spine.  
The red campus is where a crown becomes industrial — or becomes a story about a meadow.

---

### 7. `cut.mars.arrival`

| | |
|---|---|
| **when** | Mars arrival (body unlock travel) |
| **decree_id** | — (first civic tee is `mars.build.raise_the_compact_seat`; do not wait on it to show this cut) |
| **travel hook** | Replace Mars `ArrivalLog`. Keep wisp / creeper tee. Same beat as `advisor.travel.mars.arrival` (M0). Write the campus that will be. |
| **title** | Compact Ground |

**body**

Mars descent. Compact ground. Dust already filing opinions about Power.  
Raise the seat — HUD still says COMMONS — before the dust files a claim of its own.  
This is the industrial spine. Not a second Earth court. Not a Freehold weigh-station with worse lighting.  
Wisps drink grids. Creepers chew furrows. The Compact expects both handled without a Sol accent.

---

### 8. `cut.mars.mid_crust`

| | |
|---|---|
| **when** | After decree complete — solar field on the books, or wisps hunted, or crust woven (first of those three is enough; do not force Bloom) |
| **decree_id** | `mars.build.string_the_solar_field` · `mars.clearthreat.hunt_the_wisps` · `mars.terraform.weave_the_crust` (optional; `TechId.TerraformCharter` licenses the shop). Support: `mars.defendarea.ward_the_dust_furrows`. |
| **travel hook** | — (modal or long toast). Prestige wonders (Climate Loom / Aegis Spire / Deep Archive) stay asides — no new ids. |
| **title** | Grid, Wisps, Garden |

**body**

The solar field is on the books. The wisps can read. Aegis hunts light that should not have a mouth.  
Bloom will weave the crust if you pay for a garden. The Compact creed is not a picnic blanket.  
A stronghold that cannot keep Power or lunch is a failed picnic with better dust.  
Hold the campus. Then we name the rocks. We do not write their decrees today.

---

### 9. `cut.mars.belt_named`

| | |
|---|---|
| **when** | Conquest win — Belt Hauler staged + Mars hold |
| **decree_id** | `mars.researchsite.commission_the_belt_hauler` (complete; still place a Landing Pad — pad labour is `Build`) |
| **travel hook** | Replace Mars `VictoryLog`. Same beat as `advisor.travel.mars.to_belt` (M9). Banner may read **TO BELT**. No Belt / Europa decree list. |
| **title** | The Rocks Have a Name |

**body**

Mars holds. Belt Hauler on the pad. Trajectory into the rocks is open.  
**Belt** is the next freight frontier. It is a name. It is not this week’s map.  
Europa can wait in the catalog. We are not writing their decrees.  
The crown is a solar power, or it was a meadow with a rocket. Choose which log we keep.

---

## Earth act — Sol Authority (tutorial court)

**Answer:** cradle overcrowding / sustain ceiling / Sol Authority legitimacy requires expansion, not just farms.

The empty drop is the live start (`cut.earth.prologue`). The meadow claim is a disc, not a dominion. Tutorial HUD still walks Commons → airlock → HAB → workshop → flag → price; workshop has **no decree id**. Supporting decrees — `earth.explore.survey_the_claim`, `earth.extract.levy_the_meadow_farm`, as-needed `earth.clearthreat.seal_the_near_dens` / `earth.defendarea.ward_the_furrows` — keep the court alive. They do not answer the question.

Mid-court (`cut.earth.mid_court`) fires when the civic triangle is real: `earth.build.raise_the_commons`, `earth.build.dock_the_first_hab`, `earth.researchsite.charter_the_hall`. Beds and a guild make the overcrowding *visible*. Eight population / 25 s hold is a sustain gate, not a destiny. Anvil’s greed lesson (default Build $70) is how the overseer learns that pride has a price — remember it when the Freeholds invoice the pad.

Departure (`cut.earth.to_luna`) is `earth.build.stage_the_lunar_rocket` plus the launch tech. The hop is not tourism. It is the first time the crown admits the cradle cannot underwrite the title.

Advisor chain: E0–E10 in `W2_ADVISOR_AND_CHAIN_BEATS.md`.

---

## Luna act — Lunar Freeholds (tariff / warrant)

**Answer:** ice/ore levy + someone else already owns the lanes; staying means paying forever.

Arrival (`cut.luna.arrival`) is the tariff reality: 4 MET dock fee on Earth packages, hoppers on the HAB, ticks on the mines. The Freeholds were here. The rille is their contour line (`luna.explore.chart_the_tariff_rille`).

Mid (`cut.luna.mid_warrant`) is the double bruise — `luna.extract.weigh_the_freehold_ore` (the levy, Strip / Core) and `luna.clearthreat.root_the_hopper_saboteurs` (the warrant, Aegis). Someone cut the hoppers loose. Ask later who paid them. Support only: `luna.defendarea.ward_the_weigh_station`, `luna.build.fortify_the_airlocks`, `luna.build.raise_the_crater_commons`, `luna.establishoutpost.stake_the_far_rim`. A crater COMMONS that still pays rent is not sovereignty.

Departure (`cut.luna.to_mars`) is `luna.researchsite.commission_the_mars_ship`. We leave because the lanes will never be ours at this price. The Compact seat is the first place the crown can stop renting the dark.

Advisor chain: L0–L9.

---

## Mars act — Ares Guild Compact (campus that will be)

**Answer:** Compact stronghold is the industrial spine; Belt is the next freight frontier (named only).

Arrival (`cut.mars.arrival`) is Compact ground. First civic is `mars.build.raise_the_compact_seat` — HUD still **COMMONS**. `mars.explore.survey_the_red_apron` charts the packed-dust plaza. This is not a second Sol court.

Mid (`cut.mars.mid_crust`) is the campus earning its keep: `mars.build.string_the_solar_field` tees `mars.clearthreat.hunt_the_wisps`; `mars.defendarea.ward_the_dust_furrows` keeps lunch; `mars.terraform.weave_the_crust` is Bloom’s optional garden invoice. `mars.establishoutpost.stake_campus_b` is a forward lodge, not a Freehold weigh-station. Fail this act and the hop was a picnic with better dust.

Win log (`cut.mars.belt_named`) is `mars.researchsite.commission_the_belt_hauler` plus pad. **Belt** is spoken. Europa stays in `CelestialBodyCatalog.Last` as catalog furniture. No W2 decree tables for either.

Advisor chain: M0–M9.

---

## Player investment checklist

| Emotion | Scene id | When fired | Note for LP |
|---------|----------|------------|-------------|
| Unease — closing cradle | `cut.earth.prologue` | New Game / Earth arrival | **Modal.** Replace stale Earth `ArrivalLog`. Pair toast `advisor.travel.earth.empty_drop`. |
| Pride with a ceiling | `cut.earth.mid_court` | Commons + HAB + Charter complete | **Modal** (or one long toast). Title-match the three decree titles. Keep Guild assign sentence in `GameLoop`. |
| Resolve — first departure | `cut.earth.to_luna` | Lunar Rocket complete + Earth win | **Travel log.** Overlay `VictoryLog` + hop string. Banner TO LUNA. Toast `advisor.travel.earth.to_luna`. |
| Humiliation — itemized tithe | `cut.luna.arrival` | Luna arrival | **Modal.** Replace Luna `ArrivalLog`; keep hopper / tick tee. Toast `advisor.travel.luna.arrival`. |
| Anger — receipt + warrant | `cut.luna.mid_warrant` | Levy complete **or** hopper warrant claimed | **Modal.** Toast-on-Post still waits on title-match. Warrant prefers **claim** fire (Aegis). |
| Ambition — stop renting lanes | `cut.luna.to_mars` | Mars Ship complete + Luna win | **Travel log.** Overlay Luna `VictoryLog`. Banner TO MARS. Toast `advisor.travel.luna.to_mars`. |
| Gravity — Compact seat | `cut.mars.arrival` | Mars arrival | **Modal.** Replace Mars `ArrivalLog`; keep wisp / creeper tee. Toast `advisor.travel.mars.arrival`. |
| Stewardship — spine or picnic | `cut.mars.mid_crust` | Solar **or** wisps **or** crust (first hit) | **Modal** or long toast. Do not force `weave_the_crust`. Title-match later. |
| Promise — rocks named | `cut.mars.belt_named` | Belt Hauler complete + Mars win | **Travel log.** Overlay Mars `VictoryLog`. Banner TO BELT optional. Toast `advisor.travel.mars.to_belt`. No Belt content dump. |

---

## Wire notes for LP

**No new systems.** Display these text beats. `FlagManager.Post` still takes `FlagData` by `FlagType`. `CampaignProgress` still queues one travel string. `OverseerHud.Toast` / `GameLoop.LogOverseer` can show advisor lines. Do not auto-post story flags. Do not grow `FlagType`. Do not touch `SpecialistBrain`, `CaptureStill`, or art. Toast-on-Post **still waits on title-match** (`FlagDecreeIds` title + current `CelestialBodyId` until an id is stamped on `FlagHandle`).

### Stable keys

| Key | Kind | Overlays / sits beside |
|-----|------|------------------------|
| `cut.earth.prologue` | modal | Earth `ArrivalLog` (stale) · `advisor.travel.earth.empty_drop` |
| `cut.earth.mid_court` | modal | complete on `earth.build.raise_the_commons` + `earth.build.dock_the_first_hab` + `earth.researchsite.charter_the_hall` · `advisor.earth.charter_the_hall.complete` |
| `cut.earth.to_luna` | travel log | Earth `VictoryLog` · hop “Departure from Earth. Trajectory locked: Luna.” · `advisor.travel.earth.to_luna` |
| `cut.luna.arrival` | modal | Luna `ArrivalLog` · `advisor.travel.luna.arrival` |
| `cut.luna.mid_warrant` | modal | complete/claim `luna.extract.weigh_the_freehold_ore` · `luna.clearthreat.root_the_hopper_saboteurs` |
| `cut.luna.to_mars` | travel log | Luna `VictoryLog` · `advisor.travel.luna.to_mars` |
| `cut.mars.arrival` | modal | Mars `ArrivalLog` · `advisor.travel.mars.arrival` |
| `cut.mars.mid_crust` | modal | first complete among `mars.build.string_the_solar_field` / `mars.clearthreat.hunt_the_wisps` / `mars.terraform.weave_the_crust` |
| `cut.mars.belt_named` | travel log | Mars `VictoryLog` · `advisor.travel.mars.to_belt` |

Cutscene keys (`cut.*`) are localization handles. **Decree ids stay the `FlagDecreeIds` consts.** Suggested toast keys remain `advisor.{body}.{slug}.{when}` and `advisor.travel.{body}.{event}` from the advisor doc.

### Existing runtime strings (do not ship as-is where noted)

| Current string | Stakes status |
|----------------|---------------|
| Earth `ArrivalLog`: “Earth drop confirmed. Colony Commons is live…” | **Stale** vs empty-start. Use `cut.earth.prologue`. |
| Earth `VictoryLog`: “Earth secured. Lunar Rocket is on the pad — trajectory to Luna is open.” | Functional. Prefer `cut.earth.to_luna`. |
| Luna `ArrivalLog`: “Luna insertion complete. Ash hoppers…” | Functional fauna tee. Prefer `cut.luna.arrival`. |
| Luna `VictoryLog`: “Luna holds. Mars Ship staged. Next body: Mars.” | Functional. Prefer `cut.luna.to_mars`. |
| Mars `ArrivalLog`: “Mars descent. Dust wisps on Power…” | Functional fauna tee. Prefer `cut.mars.arrival`. |
| Mars `VictoryLog`: “Mars holds. Belt Hauler is on the pad — trajectory into the rocks is open.” | Functional. Prefer `cut.mars.belt_named` (names **Belt**). |
| `GameLoop` hop: “Departure from {from}. Trajectory locked: {to}.” | Keep as fallback; overlay travel cuts on conquest hops. |
| Belt / Europa `ArrivalLog` / `VictoryLog` | Catalog only. Do not author W2 cuts. |

### Decree ids referenced (all exist in `FlagDecreeIds`)

`earth.build.raise_the_commons` · `earth.build.dock_the_first_hab` · `earth.researchsite.charter_the_hall` · `earth.build.stage_the_lunar_rocket` · `earth.explore.survey_the_claim` · `earth.extract.levy_the_meadow_farm` · `earth.clearthreat.seal_the_near_dens` · `earth.defendarea.ward_the_furrows` · `luna.explore.chart_the_tariff_rille` · `luna.extract.weigh_the_freehold_ore` · `luna.clearthreat.root_the_hopper_saboteurs` · `luna.defendarea.ward_the_weigh_station` · `luna.build.fortify_the_airlocks` · `luna.build.raise_the_crater_commons` · `luna.establishoutpost.stake_the_far_rim` · `luna.researchsite.commission_the_mars_ship` · `mars.build.raise_the_compact_seat` · `mars.explore.survey_the_red_apron` · `mars.build.string_the_solar_field` · `mars.clearthreat.hunt_the_wisps` · `mars.defendarea.ward_the_dust_furrows` · `mars.terraform.weave_the_crust` · `mars.establishoutpost.stake_campus_b` · `mars.researchsite.commission_the_belt_hauler`

Twenty-four consts. No new rows. Lookup remains `FlagDecreeIds.TryGet(...)`.

### Out of scope

- Gameplay code, new `FlagType`, new `FlagDecreeIds`, quest nodes, auto-posted story flags.
- Art, `CaptureStill`, Phase 4 EXIT stills, Phase 5 ship.
- Belt / Europa decree lists or cutscene catalogs.
- Rewriting `SpecialistFlavor`.
- Implementing title-match toasts (LP later). This doc is the spine those toasts hang on.
