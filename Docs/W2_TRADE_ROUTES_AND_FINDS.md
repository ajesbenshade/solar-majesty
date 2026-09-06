# W2 trade routes and finds

**Audience:** Narrative (route / discovery beats), LP (toast + decree-title skins), GD (board flavor), Producer (glance: four resources, no new `FlagType`).  
**Status:** Sketch. Copy + hook ids only — not a quest graph, not a caravan sim, not a new `FlagType`, not App Store text, not art.  
**Ids:** Decree slugs stay [`FLAG_TYPE_MAP_EARTH_LUNA_MARS.md`](FLAG_TYPE_MAP_EARTH_LUNA_MARS.md) / `FlagDecreeIds`. Route keys (`route.*`) and find keys (`find.*`) are localization handles. Do not mint `FlagDecreeIds` until a beat needs a new title.  
**Stakes / voice:** [`W2_CAMPAIGN_STAKES.md`](W2_CAMPAIGN_STAKES.md) · [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md). Advisor stays wry court counsel. COMMONS stays COMMONS. No Majesty 2 / Paradox proper names.

**North-star shift (look, not EXIT):** Phase 4 is **no longer** chasing packed density from `SM_MarsCampaign_VisualTarget.png` as look north star. Campus should read **spaced**, fun, cartoon-ish overseer — room between modules for foliage, rocks, fauna, and things to find. This doc invests that space with solar trade and discovery. It does **not** unblock `PHASE_4_EXIT.md`, and it does not touch `CaptureStill` or Phase 4 stills.

Flags stay **decrees**. The royal overseer posts them. Ego specialists take or ignore them. Trade routes are **narrative + Extract / Explore skins + advisor copy**, not a pathfinding system. Belt and Europa are **named destinations only**.

---

## Player fantasy

You are a crown that arrived late to lanes someone else already drew. Chart the cairns, weigh the ore, and bargain with Lunar Freeholds who invoice hospitality and an Ares Guild Compact that spends surplus watts like guild credit. Metals remain the hard tithe — dock fees, payroll, bounties. Power is how you spend favor: loudly, on purpose, and never as a fifth coin. The spaced campus is the board those bargains sit on.

---

## Currency framing

Four stockpiles only (`ResourceId`: REG / ICE / MET / PWR). No fifth resource. MET is already the hard coin in code (flag escrow, dock fees, tax, payroll tithe). This sketch gives **PWR a soft trade-credit feel** in copy — surplus grid as favor — without renaming HUD chips or adding a wallet.

| Resource | Player-facing name | Trade feel | Who cares |
|----------|--------------------|------------|-----------|
| **REG** | Regolith · Fill | Bulk freight. Cheap volume. Cairns and construction dirt. | **Sol Authority** treats it as meadow fill. **Lunar Freeholds** tax the volume at the weigh-station. **Ares Guild Compact** stacks it for crust and pad beds. |
| **ICE** | Water Ice · Life-support ice | Hospitality cargo. Everyone drinks; someone already owns the ledger. | **Authority** levies the meadow farm. **Freeholds** own the receiving receipt. **Compact** keeps lunch — farms are not scenery. |
| **MET** | Metals · Tithe metal | Hard levy. Dock fees (Luna 4 / Mars 6), bounty escrow, tax, payroll return. | **Authority** posts bounties in MET. **Freeholds** invoice the pad and call it hospitality. **Compact** pays industrial spine and guild pride in metal. |
| **PWR** | Power · Watt favor | Soft trade credit. Surplus watts spent like a crown spending favor — not a fifth coin, not a shop currency. | **Authority** gifts surplus like court favor. **Freeholds** will not take a spark in lieu of MET. **Compact** treats a fat grid as guild credit; wisps steal the joke. |

**Do not:** add Credits / Favor / Tariff as a stockpile. **Do:** let advisor copy say “favor” when PWR is fat and “tithe” when MET leaves the pad.

Existing mechanical packages already move cargo without a caravan: pad resupply (+25 MET, +15 ICE, +10 PWR, minus dock fee). Narrative can skin those ticks as route courtesy. Do not ask LP for a freight sim in this pass.

---

## Pre-established routes (Earth → Luna → Mars)

Six named lanes. They existed before the orange disc. Each skins an existing `FlagType` (Extract / Explore first; one ResearchSite for the named haul). Prefer the listed `FlagDecreeIds`. New titles later = const + map row; **do not grow the enum**.

| Id | Bodies | Primary cargo | Faction friction | FlagType skin | Advisor line |
|----|--------|---------------|------------------|---------------|--------------|
| `route.earth_luna.meadow_ice` | Earth → Luna | **ICE** | Authority levies a farm the Freeholds will receipt as hospitality. | `Extract` · `earth.extract.levy_the_meadow_farm` | Levy the meadow. The Freeholds will call this hospitality when it lands. |
| `route.earth_luna.cradle_fill` | Earth → Luna | **REG** | Meadow dirt shipped as freight; Freeholds weigh volume, not manners. | `Explore` · `earth.explore.survey_the_claim` | Chart the fill cairns. Someone will invoice the volume. It will not be us, yet. |
| `route.luna.rille_ledger` | Luna (lanes already drawn) | **ICE** | Charting the rille is reading someone else’s contour line. Smugglers use the same ink. | `Explore` · `luna.explore.chart_the_tariff_rille` | Horizon marks the lanes they already tax. We are late to our own freight. |
| `route.luna_mars.ore_tithe` | Luna → Mars | **MET** | Freehold levy becomes Compact spine metal. Stay and you pay forever. | `Extract` · `luna.extract.weigh_the_freehold_ore` | They call it a tariff. We call it MET with a receipt. Do not lose the receipt. |
| `route.earth_mars.watt_favor` | Earth → Luna → Mars | **PWR** | Authority spends surplus watts as favor; Compact books them as credit; Freeholds still want metal. | `Explore` · `mars.explore.survey_the_red_apron` | Surplus watts are court favor. The Compact spends them like credit. The Freeholds will not take a spark in lieu of metal. |
| `route.mars_belt.named_haul` | Mars → **Belt** (Europa named as ice stop only) | **MET** | Compact prestige haul toward the rocks. No Belt / Europa decree list. | `ResearchSite` · `mars.researchsite.commission_the_belt_hauler` | Belt is a name on a pad. Europa is a colder name. We are not writing their decrees. |

**Support skins (do not mint routes):** `mars.build.string_the_solar_field` is how the watt-favor surplus gets *built* (still `Build`, not a ninth type). `luna.defendarea.ward_the_weigh_station` keeps ticks off the tithe. Pad labour on any hop stays `Build`.

---

## Findables

Eight discovery hooks. Text + ids first. Mechanical type is `Explore` / `ResearchSite` / `ClearThreat` only. Complete can toast even before fog reveal exists — write as if the map matters.

| Id | Body | FlagType | What you find | Toast / aside |
|----|------|----------|---------------|---------------|
| `find.earth.apron_cairn` | Earth | `Explore` | Ore / fill cairn on the meadow apron — stacked REG with a MET wink, off the orange disc. Pair `earth.explore.survey_the_claim`. | Horizon found a cairn the meadow forgot to invoice. REG with a sense of theater. |
| `find.earth.old_court_stub` | Earth | `ResearchSite` | Pre-charter ruin: Sol Authority stone that was scenery before the Commons. Science toward Guild Charter, not a new lab. | Old court stone. We were scenery here once. Chart logged it; the lab can eat the sample. |
| `find.earth.near_dens` | Earth | `ClearThreat` | Creature dens already on the board (three). Skin `earth.clearthreat.seal_the_near_dens`. | Three dens. The court does not share the meadow — or the rent. |
| `find.luna.rille_cache` | Luna | `Explore` | Freehold / smuggler cache off the tariff rille. Inventory they will claim was always theirs. Pair `luna.explore.chart_the_tariff_rille`. | A Freehold cache off the rille. They will say it was inventory. We will say it was found. |
| `find.luna.hopper_warren` | Luna | `ClearThreat` | Ash-hopper warren dressed as sabotage. Same raid as today’s hoppers — skin `luna.clearthreat.root_the_hopper_saboteurs`. | Someone cut the hoppers loose. The warren is the punchline. Aegis has the warrant. |
| `find.luna.dead_weigh` | Luna | `ResearchSite` | Abandoned weigh-station. The clerk is gone; the tariff is not. +12 toward Mars Ship if you want the sample to matter. | A weigh-station with no clerk. The tariff persists. The furniture did not. |
| `find.mars.compact_cairn` | Mars | `Explore` | Compact ore / ice cairn on the spaced red apron — stacked like a sermon, not a packed plaza. Pair `mars.explore.survey_the_red_apron`. | Compact cairn. Ice or ore — the guild stacked it like a sermon. Freight after. |
| `find.mars.wisp_nest` | Mars | `ClearThreat` | Dust-wisp nest on the grid. Same Power drain. Skin `mars.clearthreat.hunt_the_wisps`. | Wisps nest on the grid. They drink complimentary. It is not. |

Optional prestige aside (not a ninth findable, no new id): Deep Archive jewelry after the three Mars gates — already a wonder, not a ruin hunt.

---

## Creatures / rocks / foliage

Narrative color for GD. **No art asks** beyond notes. Do not brief `CaptureStill`. Do not chase the packed visual-target carpet.

**Already in (seven fauna + body skins):**

| Fauna | Where it already lives | FlagType | Flavor if the campus is spaced |
|-------|------------------------|----------|--------------------------------|
| Dust Stalker | Dens / lairs (all bodies) | `ClearThreat` | Landmark threat between modules, not clutter in a packed courtyard. |
| Soil Creeper / Dust Creeper | Earth / Mars farms | `DefendArea` | Furrow pests you *see* coming across open dirt. |
| Ash Hopper | Luna HABs | `ClearThreat` | Saboteur punchline — warren as a findable, not a ninth type. |
| Watt Leech | Earth Power | `ClearThreat` | Favor-thief. They drink the court gift. |
| Dust Wisp | Mars Power | `ClearThreat` | Compact credit-thief. Same joke, red dust. |
| Dust Tick / Rock Tick | Luna mines | `DefendArea` | Weigh-station lice. The levy continues. |
| Regolith Mite | Extractors | `DefendArea` | Pillbug on cairns and camps — cute until the haul is late. |

Belt / Europa skins (shard hoppers, rock mites, fissure leeches, ice wisps, ice creepers) stay **destination color only**. No W2 decree tables.

**Desired board flavor (copy, not a mesh list):** rocks as cairn markers and route stones; Earth meadow already has grass / trees / pond — keep them as things behind the disc; Mars wants hardy scrub and basalt scatter in the gaps a spaced campus creates. Cartoon-ish overseer readability (silhouettes you can tease in a toast), not a grim wildlife sim. Fauna remain pests and dens. Do not invent a pet or a hunt-for-sport `FlagType`.

---

## Wire notes for LP

**No new systems this sketch.** `FlagManager.Post` still takes `FlagData` by `FlagType`. Toast-on-Post still waits on title-match (`FlagDecreeIds` title + current `CelestialBodyId` until an id is stamped on `FlagHandle`). Do not auto-post story flags. Do not grow `FlagType`. Do not touch `SpecialistBrain`, `CaptureStill`, or art.

### Can ship later (copy on existing rails)

| Hook | How |
|------|-----|
| Six `route.*` keys | Advisor toasts on **post / complete** of the listed decrees. Suggested keys: `advisor.route.{slug}.post` / `.complete`. Decree ids stay the consts. |
| Eight `find.*` keys | Explore / ResearchSite **complete** toasts and ClearThreat **claim** asides (hopper warrant already prefers claim). `advisor.find.{body}.{slug}.{when}`. |
| Watt-favor voice | When PWR is fat or `mars.build.string_the_solar_field` completes — one aside, no new chip. |
| Tithe voice | Already drafted on `luna.extract.weigh_the_freehold_ore` and Luna dock-fee arrival. Keep MET as the invoice. |
| Resupply tick | Existing pad package can log as route courtesy (“hospitality inbound”) — optional one-liner, not a freight UI. |

### Needs systems later (explicitly out of scope)

- Caravans, lane pathfinding, moving cargo props, or a trade-map overlay.
- Fog-of-war / Explore reveal (write chart lines now; implementation later).
- A fifth resource, PWR wallet, or barter UI.
- New `FlagType` (no Tariff, no Salvage, no Discover).
- New `FlagDecreeIds` unless a title cannot skin an existing row — then const **and** a map row, same eight types.
- Belt / Europa decree lists or find tables.
- Art, foliage/rock meshes, `CaptureStill`, Phase 4 EXIT stills, Phase 5 ship.
- Rewriting `SpecialistFlavor` or `SpecialistBrain`.

Lookup remains `FlagDecreeIds.TryGet(...)`. Route and find keys are **not** decree ids.

---

## Pointers

Spine and toast docs stay the stakes / voice source. This file only owns **lanes + finds + PWR/MET framing**.

- Flag map: [`FLAG_TYPE_MAP_EARTH_LUNA_MARS.md`](FLAG_TYPE_MAP_EARTH_LUNA_MARS.md)
- Stakes: [`W2_CAMPAIGN_STAKES.md`](W2_CAMPAIGN_STAKES.md)
- Advisor: [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md)
