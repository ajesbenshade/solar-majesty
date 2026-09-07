# W2 advisor + chain beats

**Audience:** Narrative (voice pass) and LP (toast / log wiring).  
**Status:** W2 draft. Copy only — not a quest graph, not a new `FlagType`, not App Store text.  
**Ids:** Use `Docs/FLAG_TYPE_MAP_EARTH_LUNA_MARS.md` and `FlagDecreeIds`. Do not invent slugs.  
**Art:** Phase 4 **EXIT** is still art-blocked (`Docs/Roadmap/PHASE_4_EXIT.md`). Mars lines assume the Compact campus that will be (white hulls, square docks, geodesic Commons), not the empty Sol 1 still.  
**Campaign stakes (cutscenes):** [`W2_CAMPAIGN_STAKES.md`](W2_CAMPAIGN_STAKES.md) — prologue / hop / arrival / win-log beats these toasts sit under. Toast-on-Post still waits on title-match.  
**Hero personality quests:** [`W2_HERO_PERSONALITY_QUESTS.md`](W2_HERO_PERSONALITY_QUESTS.md) — ten callsign chains on these decrees (no new `FlagType`). First-ship: Anvil / Horizon / Strip / Aegis.

Flags stay **decrees**. The overseer posts them. Ego specialists take or ignore them. The advisor comments. COMMONS is the civic name. Belt and Europa stay off this chain except as the named next body after Mars.

---

## Voice rules

- **Advisor** is wry court counsel. Dry, brief, slightly too pleased with itself. It never path-commands a robot and never says “go here.”
- **Decrees** are royal overseer acts. Toast copy talks about posting, claiming, and completing a title — not issuing a unit order.
- **`SpecialistFlavor` keeps the ego.** Horizon / Anvil / Aegis / Triage / Strip / Chart / Bloom / Haul / Core / Rim stay class voice on cards and claim logs. Advisor asides sit *beside* those lines. Do not rewrite `SpecialistFlavor.cs`.
- **Body manners shift.** Earth = Sol Authority court. Luna = Freehold tariff / warrant dryness. Mars = Ares Guild Compact pride — guild-proud, not a second Sol curtsy.
- **Names that stay locked.** COMMONS (never Palace, never CMD as a place). Guild Hall wears CMD-1 dress; OPS-1 is the Mining annex. No Majesty 2 / Paradox proper names (no tax collectors guild, no royal tax, no Majesty hero titles).
- **Explore is chart / survey copy.** Fog reveal is later. Write as if the map matters; do not wait on implementation.
- **As-needed beats are as-needed.** Dens, furrows, Campus B, and crust-weaving tee when the board is loud — they are not a forced six-step quest.
- **Belt is a destination name only.** After Mars win, name the rocks. Do not author Belt / Europa decree lists here.

---

## Earth chain — Sol Authority (tutorial court)

**Spine:** empty drop → Raise the Commons → Dock the First HAB → greed Build lesson → Guild Charter → Seal Near Dens / Ward the Furrows as needed → Stage the Lunar Rocket → travel log **TO LUNA**.

Tutorial HUD still walks Commons → airlock → HAB → workshop → flag → price (`OverseerHud` 1/6–6/6). Empty-start is the live drop: the court is not pre-built. Workshop has **no decree id** — it is tutorial dress on the HAB beat (robots fabricate outdoors; humans stay indoors). `earth.explore.survey_the_claim` and `earth.extract.levy_the_meadow_farm` support the court; they are not extra FlagTypes.

### E0 — Empty drop

| | |
|---|---|
| **Decree id** | — |
| **Advisor toast** | The meadow is empty and the court is a rumor. Sol Authority does not hold court in the grass. Raise a Commons before anyone calls this a picnic. |
| **When** | travel (Earth New Game / arrival). **Replacement** for `CelestialBodyCatalog` Earth `ArrivalLog` (“Colony Commons is live” is wrong on empty-start). |
| **Claim aside** | — |
| **Hero hook** | None yet. No workshop, no Anvil. Do not invent a starter robot. |

### E1 — Survey the Claim

| | |
|---|---|
| **Decree id** | `earth.explore.survey_the_claim` |
| **Advisor toast** | Chart the apron so the court knows where the orange disc ends and the meadow begins. Fog can wait. Guessing is undignified. |
| **When** | post (optional first F1 after drop, or after Commons sits). |
| **Claim aside** | Horizon took the cheap Explore. Forty credits and a disappearing act — classic. |
| **Hero hook** | Keep `SpecialistFlavor` Scout claim: *“Cheap Explore. Forty credits and I'm gone.”* Advisor does not overwrite it. |

### E2 — Raise the Commons

| | |
|---|---|
| **Decree id** | `earth.build.raise_the_commons` |
| **Advisor toast** | Raise the Commons first. A court that meets in the dirt is a picnic, not a reign. Place the civic, then post Build on the live order. |
| **When** | post (tutorial 1/6). complete when the Commons stands. |
| **Claim aside** | Anvil took the weld. Try not to look surprised. |
| **Hero hook** | Anvil claim stays *“Build's on the books. Don't cheap out mid-weld.”* |

### E3 — Dock the First HAB

| | |
|---|---|
| **Decree id** | `earth.build.dock_the_first_hab` |
| **Advisor toast** | Dock the first HAB. Humans stay indoors; tax does not collect itself from a meadow. Anvil welds the airlock join — square socket, not a speech. |
| **When** | post after Commons + airlock socket exist (tutorial 2/6–3/6). Workshop (4/6) is the same court lesson: no outdoor colonists. |
| **Claim aside** | Beds on the books. The meadow just became a ledger. |
| **Hero hook** | Same Anvil Build claim. No new flavor asset. |

### E4 — Greed Build lesson

| | |
|---|---|
| **Decree id** | — (reuse the live `Build` on E2 / E3; default $70) |
| **Advisor toast** | Anvil will not weld for pocket change. Seventy is a suggestion; *tempted* is a fact. Raise the bounty until the chip admits it. |
| **When** | post if a cheap Build sits idle; complete when any robot is tempted (tutorial 5/6–6/6). |
| **Claim aside** | There. Pride has a price. Remember it on Luna. |
| **Hero hook** | Anvil idle already says Build needs real pay. Do not rewrite `SpecialistFlavor`. |

### E5 — Levy the Meadow Farm

| | |
|---|---|
| **Decree id** | `earth.extract.levy_the_meadow_farm` |
| **Advisor toast** | Levy the meadow farm. ICE is life support, not a beverage program. Creepers will RSVP. |
| **When** | post once a Farm / ice node is down (sustain gate). |
| **Claim aside** | Strip took the levy. Dens can wait — they said so themselves. |
| **Hero hook** | Harvester claim: *“Ore first. Dens can wait.”* |

### E6 — Charter the Hall (Guild Charter)

| | |
|---|---|
| **Decree id** | `earth.researchsite.charter_the_hall` |
| **Advisor toast** | Charter the hall. A court without a guild is stationery. Field Survey, then Hab Ops, then the Charter — dock CMD-1 dress and assign Horizon / Anvil / Aegis / Triage. |
| **When** | post (Research Site toward `TechId.GuildCharter`). complete overlays the existing `GameLoop` “Guild Charter signed…” line — keep the assign instruction; this toast is the voice. |
| **Claim aside** | Chart logged the site. The lab can eat the sample; the hall can eat the ego. |
| **Hero hook** | Surveyor / Geologist Research Site claims stay as written. |

### E7 — Seal the Near Dens *(as needed)*

| | |
|---|---|
| **Decree id** | `earth.clearthreat.seal_the_near_dens` |
| **Advisor toast** | The court does not share the meadow. Three dens. Seal them before the creepers start charging rent. |
| **When** | post when dens / leeches pressure the plaza (F2). Skip if the board is quiet. |
| **Claim aside** | Aegis hunts. Stay behind the shield and out of the toast. |
| **Hero hook** | Defense claim: *“Hunting the den. Stay behind the shield.”* |

### E8 — Ward the Furrows *(as needed)*

| | |
|---|---|
| **Decree id** | `earth.defendarea.ward_the_furrows` |
| **Advisor toast** | Ward the furrows. Soil creepers chew farms. Aegis and Triage will hold a field cheaper than they will hunt a den. Tempt them accordingly. |
| **When** | post when farm pests land (F5). Pair with E5; do not lead the tutorial with it. |
| **Claim aside** | Rim watch on a farm. Very dignified. Very necessary. |
| **Hero hook** | Aegis Defend: *“Rim watch. Nothing crosses.”* Triage: *“On the wounded. I am not a hunter.”* |

### E9 — Stage the Lunar Rocket

| | |
|---|---|
| **Decree id** | `earth.build.stage_the_lunar_rocket` |
| **Advisor toast** | Stage the Lunar Rocket. Pad labour is a Build on the order. Science is 70 + 40 MET + 15 ICE. A trajectory without a pad is a toast to vacuum. |
| **When** | post on the pad construction order; tee `TechId.LunarRocket` in the same breath. complete when craft is staged (`GameLoop` already: “Lunar Rocket staged on the Landing Pad”). |
| **Claim aside** | Anvil will take a pad weld if you stop being cute with the bounty. |
| **Hero hook** | Same Anvil Build claim. |

### E10 — Travel log TO LUNA

| | |
|---|---|
| **Decree id** | — |
| **Advisor toast** | Earth holds. Lunar Rocket on the pad. Trajectory locked: **Luna**. The Freeholds already have a receipt prepared. |
| **When** | travel (conquest win / `CampaignProgress.QueueTravelLog`). **Replacement** for Earth `VictoryLog` + the generic “Departure from Earth. Trajectory locked: Luna.” hop. HUD banner may still read **TO LUNA**. |
| **Claim aside** | — |
| **Hero hook** | — |

---

## Luna chain — Lunar Freeholds (tariff / warrant)

**Spine:** arrival → tariff / Weigh the Freehold Ore → hopper-saboteur warrant → Crater Commons / Stake the Far Rim → Commission the Mars Ship → travel log **TO MARS**.

Dock fee is 4 MET on Earth packages — the tithe. Arrival log already names hoppers + ticks; W2 leans into *who cut the hoppers loose*. `luna.explore.chart_the_tariff_rille`, `luna.defendarea.ward_the_weigh_station`, and `luna.build.fortify_the_airlocks` support the act.

### L0 — Arrival

| | |
|---|---|
| **Decree id** | — |
| **Advisor toast** | Luna insertion. The Freeholds smile like a weigh-station. Dock fee is four MET. They call it hospitality. We call it a line item. |
| **When** | travel (arrival). **Replacement** for Luna `ArrivalLog` (keep the hopper / tick tee; lose the briefing monotone). |
| **Claim aside** | — |
| **Hero hook** | — |

### L1 — Chart the Tariff Rille

| | |
|---|---|
| **Decree id** | `luna.explore.chart_the_tariff_rille` |
| **Advisor toast** | Chart the rille. Horizon marks the lanes the Freeholds tax. Smugglers use the same lines. So will we. |
| **When** | post (early F1 / survey). Fog later; the map is the joke now. |
| **Claim aside** | Chart or Horizon took a tariff lane. Nobody fights a contour line. Good. |
| **Hero hook** | Surveyor Explore: *“Charting the apron. No fights.”* |

### L2 — Weigh the Freehold Ore

| | |
|---|---|
| **Decree id** | `luna.extract.weigh_the_freehold_ore` |
| **Advisor toast** | They call it a tariff. We call it MET with a receipt. Weigh the Freehold ore. Strip and Core haul the levy. |
| **When** | post on a mine / ore node (the levy beat). |
| **Claim aside** | The receipt is in the stockpile. Do not lose it. The Freeholds will ask. |
| **Hero hook** | Strip: *“Ore first. Dens can wait.”* Core Extract: *“Reading the crust. Haul follows.”* |

### L3 — Ward the Weigh-Station *(support)*

| | |
|---|---|
| **Decree id** | `luna.defendarea.ward_the_weigh_station` |
| **Advisor toast** | Ticks are stealing from the weigh-station. That is not a den hunt. That is a rim watch with worse lighting. |
| **When** | post when dust ticks sit on mines (F5). |
| **Claim aside** | Rim took the watch. The levy continues. Poetry later. |
| **Hero hook** | Rim: *“Perimeter locked. I hold. I do not wander.”* |

### L4 — Root the Hopper Saboteurs

| | |
|---|---|
| **Decree id** | `luna.clearthreat.root_the_hopper_saboteurs` |
| **Advisor toast** | Someone cut the hoppers loose on the HAB. Post a warrant, not a sermon. Clear Threat. Ask later who paid them. |
| **When** | post on HAB hopper pressure (F2). Same mechanical raid as today’s ash hoppers — sabotage is the skin. |
| **Claim aside** | Aegis accepted the warrant. The hoppers did not. |
| **Hero hook** | Aegis Clear Threat claim, as shipped. |

### L5 — Raise the Crater Commons

| | |
|---|---|
| **Decree id** | `luna.build.raise_the_crater_commons` |
| **Advisor toast** | Raise a Commons on grey rock. Authority that sits in a crater still has to sit somewhere. Same civic gate as Earth. HUD still says COMMONS. |
| **When** | post if the crater drop is empty (it is). complete when the plaza stands. |
| **Claim aside** | Anvil will weld a crater court if the MET is honest. |
| **Hero hook** | Anvil Build claim. |

### L6 — Stake the Far Rim

| | |
|---|---|
| **Decree id** | `luna.establishoutpost.stake_the_far_rim` |
| **Advisor toast** | Stake the far rim. The cyan disc is Campus B, not a picnic site. Haul claims it. Freight after. |
| **When** | post when the cyan disc is the story (O). Optional if Campus A already holds. |
| **Claim aside** | Haul took the disc. They already said freight after. Believe them. |
| **Hero hook** | Courier Outpost: *“Claim the cyan disc. Freight after.”* Do not rewrite. |

### L7 — Fortify the Airlocks *(support)*

| | |
|---|---|
| **Decree id** | `luna.build.fortify_the_airlocks` |
| **Advisor toast** | Fortify the airlocks. Junction turrets are dressing — they look like courage. The labour is still a Build on a Battery or workshop order. |
| **When** | post on a live Defense Battery / workshop order after hoppers have visited. |
| **Claim aside** | Anvil fortifies. Aegis will pretend it was their idea. |
| **Hero hook** | Anvil Build claim. |

### L8 — Commission the Mars Ship

| | |
|---|---|
| **Decree id** | `luna.researchsite.commission_the_mars_ship` |
| **Advisor toast** | Commission the Mars Ship. The Compact is not a rumor. It is 100 science, 80 MET, 30 ICE, 20 PWR, and a pad you still have to weld. |
| **When** | post (Research Site toward `TechId.MarsShip`). complete when staged. Pad labour reuses `Build` — no ninth type. |
| **Claim aside** | The lab ate the sample. The pad will eat Anvil’s afternoon. |
| **Hero hook** | Surveyor / Geologist Research Site claims. |

### L9 — Travel log TO MARS

| | |
|---|---|
| **Decree id** | — |
| **Advisor toast** | Luna holds. Mars Ship staged. Next body: **Mars**. Pack guild manners. The Compact does not curtsy. |
| **When** | travel (win). **Replacement** for Luna `VictoryLog`. Banner **TO MARS**. |
| **Claim aside** | — |
| **Hero hook** | — |

---

## Mars chain — Ares Guild Compact (campus that will be)

**Spine:** arrival → Compact seat → solar / wisps → dust furrows → Campus B / Weave the Crust optional → Belt Hauler + pad Build note → win travel log (Belt named only).

Write for the packed white / orange campus Phase 4 is aiming at: geodesic Commons, HAB cylinder, square paneled airlocks, solar field, pad + craft. Do **not** write the empty Sol 1 still as canon. Wonders (Climate Loom / Aegis Spire / Deep Archive) are optional prestige asides after the three gates — no new decree ids.

### M0 — Arrival

| | |
|---|---|
| **Decree id** | — |
| **Advisor toast** | Mars descent. Compact ground. Dust wisps already have opinions about Power. Raise the seat before the dust files a claim of its own. |
| **When** | travel (arrival). **Replacement** for Mars `ArrivalLog`. Keep wisp / creeper tees. |
| **Claim aside** | — |
| **Hero hook** | — |

### M1 — Survey the Red Apron

| | |
|---|---|
| **Decree id** | `mars.explore.survey_the_red_apron` |
| **Advisor toast** | Survey the red apron. Chart the packed-dust plaza the Compact will actually sit on — white hulls, square docks, a Commons that is not a rumor. |
| **When** | post (early F1). Copy-only chart. |
| **Claim aside** | Horizon priced the dust. Forty credits. They did not stay for the speech. |
| **Hero hook** | Scout / Chart Explore claims. |

### M2 — Raise the Compact Seat

| | |
|---|---|
| **Decree id** | `mars.build.raise_the_compact_seat` |
| **Advisor toast** | Raise the Compact seat. HUD still says COMMONS. The guild will forgive a Sol accent. It will not forgive a dirt court. |
| **When** | post (first civic Build). complete when Commons stands. |
| **Claim aside** | Anvil welds for the Compact now. Try a guild-proud bounty. They notice. |
| **Hero hook** | Anvil Build claim, unchanged. |

### M3 — String the Solar Field

| | |
|---|---|
| **Decree id** | `mars.build.string_the_solar_field` |
| **Advisor toast** | String the solar field. PWR-1 first. Wisps RSVP themselves. We do not send thank-you notes. |
| **When** | post on the PWR-1 / solar landmark order. Tees M5. |
| **Claim aside** | The grid is on the books. The wisps can read. |
| **Hero hook** | Anvil Build claim. |

### M4 — Hunt the Wisps

| | |
|---|---|
| **Decree id** | `mars.clearthreat.hunt_the_wisps` |
| **Advisor toast** | Hunt the wisps. They drink the grid like it is complimentary. It is not. Pair with the ten dens when the plaza gets loud. |
| **When** | post on Power drain / den pressure (F2). |
| **Claim aside** | Aegis hunts light that should not have a mouth. Compact work. |
| **Hero hook** | Aegis Clear Threat claim. |

### M5 — Ward the Dust Furrows

| | |
|---|---|
| **Decree id** | `mars.defendarea.ward_the_dust_furrows` |
| **Advisor toast** | Ward the dust furrows. Compact farms do not get abandoned for a prettier den hunt. Creepers chew; we hold. |
| **When** | post when dust creepers sit on farms (F5). |
| **Claim aside** | Rim or Aegis on a greenhouse. The Compact keeps its lunch. |
| **Hero hook** | Aegis / Rim Defend claims. |

### M6 — Stake Campus B *(optional)*

| | |
|---|---|
| **Decree id** | `mars.establishoutpost.stake_campus_b` |
| **Advisor toast** | Stake Campus B. A Compact forward lodge — not a Freehold weigh-station, not a Sol picnic. Cyan disc. Haul already knows. |
| **When** | post if the forward lodge is the play (O). Skip if Campus A is the whole story. |
| **Claim aside** | Haul claimed the disc. Freight after. They have a slogan. It works. |
| **Hero hook** | Courier Outpost claim, as shipped. |

### M7 — Weave the Crust *(optional)*

| | |
|---|---|
| **Decree id** | `mars.terraform.weave_the_crust` |
| **Advisor toast** | They want a garden. Pay for a garden. Bloom weaves +0.08 and will invoice you for every grain. `TerraformCharter` licenses the shop; the flag is still Terraform. |
| **When** | post after farms exist and the Charter is in (U). Do not lead the act with it. |
| **Claim aside** | Bloom took the slow work. Worth it, they said. Check the yield before you argue. |
| **Hero hook** | Terraformer claim: *“Greening this crust. Slow work. Worth it.”* |

### M8 — Commission the Belt Hauler + pad Build

| | |
|---|---|
| **Decree id** | `mars.researchsite.commission_the_belt_hauler` |
| **Advisor toast** | Commission the Belt Hauler — and place the pad. Science without a landing is a Compact joke we do not tell twice. Pad labour is a `Build` on that order. No ninth type. The rocks are next. We are not writing their decrees today. |
| **When** | post (Research Site toward `TechId.BeltHauler`). complete when staged. Pad weld is the same mechanical Build as Earth / Luna pads. |
| **Claim aside** | The lab logged the haul. Anvil still wants a pad. Pay both. |
| **Hero hook** | Research Site claims + Anvil Build on the pad order. |

### M9 — Win travel log (Belt named only)

| | |
|---|---|
| **Decree id** | — |
| **Advisor toast** | Mars holds. Belt Hauler on the pad. Trajectory into the rocks is open. **Belt** can wait for its own map. |
| **When** | travel (win). **Replacement** for Mars `VictoryLog`. Banner may read **TO BELT**. No Belt decree list. No Europa copy. |
| **Claim aside** | — |
| **Hero hook** | Optional prestige aside after the gates (not a decree): Loom / Spire / Archive are Compact jewelry. Place them if the guild wants to show off. |

---

## Toast table

Primary fire per row. Claim asides live in the chain beats. Travel rows have no `FlagDecreeIds` const — do not mint one.

| decree_id | body | FlagType | advisor_toast | when_to_fire |
|-----------|------|----------|---------------|--------------|
| — | Earth | — | The meadow is empty and the court is a rumor. Sol Authority does not hold court in the grass. Raise a Commons before anyone calls this a picnic. | travel |
| `earth.explore.survey_the_claim` | Earth | Explore | Chart the apron so the court knows where the orange disc ends and the meadow begins. Fog can wait. Guessing is undignified. | post |
| `earth.build.raise_the_commons` | Earth | Build | Raise the Commons first. A court that meets in the dirt is a picnic, not a reign. Place the civic, then post Build on the live order. | post |
| `earth.build.dock_the_first_hab` | Earth | Build | Dock the first HAB. Humans stay indoors; tax does not collect itself from a meadow. Anvil welds the airlock join — square socket, not a speech. | post |
| — | Earth | Build | Anvil will not weld for pocket change. Seventy is a suggestion; tempted is a fact. Raise the bounty until the chip admits it. | post |
| `earth.extract.levy_the_meadow_farm` | Earth | Extract | Levy the meadow farm. ICE is life support, not a beverage program. Creepers will RSVP. | post |
| `earth.researchsite.charter_the_hall` | Earth | ResearchSite | Charter the hall. A court without a guild is stationery. Field Survey, then Hab Ops, then the Charter — dock CMD-1 dress and assign Horizon / Anvil / Aegis / Triage. | post |
| `earth.clearthreat.seal_the_near_dens` | Earth | ClearThreat | The court does not share the meadow. Three dens. Seal them before the creepers start charging rent. | post |
| `earth.defendarea.ward_the_furrows` | Earth | DefendArea | Ward the furrows. Soil creepers chew farms. Aegis and Triage will hold a field cheaper than they will hunt a den. Tempt them accordingly. | post |
| `earth.build.stage_the_lunar_rocket` | Earth | Build | Stage the Lunar Rocket. Pad labour is a Build on the order. Science is 70 + 40 MET + 15 ICE. A trajectory without a pad is a toast to vacuum. | post |
| — | Earth | — | Earth holds. Lunar Rocket on the pad. Trajectory locked: Luna. The Freeholds already have a receipt prepared. | travel |
| — | Luna | — | Luna insertion. The Freeholds smile like a weigh-station. Dock fee is four MET. They call it hospitality. We call it a line item. | travel |
| `luna.explore.chart_the_tariff_rille` | Luna | Explore | Chart the rille. Horizon marks the lanes the Freeholds tax. Smugglers use the same lines. So will we. | post |
| `luna.extract.weigh_the_freehold_ore` | Luna | Extract | They call it a tariff. We call it MET with a receipt. Weigh the Freehold ore. Strip and Core haul the levy. | post |
| `luna.defendarea.ward_the_weigh_station` | Luna | DefendArea | Ticks are stealing from the weigh-station. That is not a den hunt. That is a rim watch with worse lighting. | post |
| `luna.clearthreat.root_the_hopper_saboteurs` | Luna | ClearThreat | Someone cut the hoppers loose on the HAB. Post a warrant, not a sermon. Clear Threat. Ask later who paid them. | claim |
| `luna.build.raise_the_crater_commons` | Luna | Build | Raise a Commons on grey rock. Authority that sits in a crater still has to sit somewhere. Same civic gate as Earth. HUD still says COMMONS. | post |
| `luna.establishoutpost.stake_the_far_rim` | Luna | EstablishOutpost | Stake the far rim. The cyan disc is Campus B, not a picnic site. Haul claims it. Freight after. | post |
| `luna.build.fortify_the_airlocks` | Luna | Build | Fortify the airlocks. Junction turrets are dressing — they look like courage. The labour is still a Build on a Battery or workshop order. | post |
| `luna.researchsite.commission_the_mars_ship` | Luna | ResearchSite | Commission the Mars Ship. The Compact is not a rumor. It is 100 science, 80 MET, 30 ICE, 20 PWR, and a pad you still have to weld. | post |
| — | Luna | — | Luna holds. Mars Ship staged. Next body: Mars. Pack guild manners. The Compact does not curtsy. | travel |
| — | Mars | — | Mars descent. Compact ground. Dust wisps already have opinions about Power. Raise the seat before the dust files a claim of its own. | travel |
| `mars.explore.survey_the_red_apron` | Mars | Explore | Survey the red apron. Chart the packed-dust plaza the Compact will actually sit on — white hulls, square docks, a Commons that is not a rumor. | post |
| `mars.build.raise_the_compact_seat` | Mars | Build | Raise the Compact seat. HUD still says COMMONS. The guild will forgive a Sol accent. It will not forgive a dirt court. | post |
| `mars.build.string_the_solar_field` | Mars | Build | String the solar field. PWR-1 first. Wisps RSVP themselves. We do not send thank-you notes. | post |
| `mars.clearthreat.hunt_the_wisps` | Mars | ClearThreat | Hunt the wisps. They drink the grid like it is complimentary. It is not. Pair with the ten dens when the plaza gets loud. | post |
| `mars.defendarea.ward_the_dust_furrows` | Mars | DefendArea | Ward the dust furrows. Compact farms do not get abandoned for a prettier den hunt. Creepers chew; we hold. | post |
| `mars.establishoutpost.stake_campus_b` | Mars | EstablishOutpost | Stake Campus B. A Compact forward lodge — not a Freehold weigh-station, not a Sol picnic. Cyan disc. Haul already knows. | post |
| `mars.terraform.weave_the_crust` | Mars | Terraform | They want a garden. Pay for a garden. Bloom weaves +0.08 and will invoice you for every grain. | post |
| `mars.researchsite.commission_the_belt_hauler` | Mars | ResearchSite | Commission the Belt Hauler — and place the pad. Science without a landing is a Compact joke we do not tell twice. The rocks are next. We are not writing their decrees today. | post |
| — | Mars | — | Mars holds. Belt Hauler on the pad. Trajectory into the rocks is open. Belt can wait for its own map. | travel |

**Complete overlays (same ids, second fire — do not add consts):**

| toast key | when_to_fire | line |
|-----------|--------------|------|
| `advisor.earth.charter_the_hall.complete` | complete | Guild Charter signed. Dock the hall. Assign a class. Flags near CMD-1 dress pull that ego. |
| `advisor.earth.stage_the_lunar_rocket.complete` | complete | Lunar Rocket is on the pad. The meadow just became a departure lounge. |
| `advisor.luna.commission_the_mars_ship.complete` | complete | Mars Ship staged. The Compact will not send a welcome basket. |
| `advisor.mars.commission_the_belt_hauler.complete` | complete | Belt Hauler staged. The rocks are a body. They are not this week’s copy. |

Suggested string keys for decree rows: `advisor.{body}.{slug}.{when}` — e.g. `advisor.earth.raise_the_commons.post`, `advisor.luna.weigh_the_freehold_ore.post`. Framing rows (no `FlagDecreeIds` const) use `advisor.travel.{body}.{event}` so they cannot be mistaken for decree ids: `advisor.travel.earth.empty_drop`, `advisor.travel.earth.to_luna`, `advisor.travel.luna.arrival`, `advisor.travel.luna.to_mars`, `advisor.travel.mars.arrival`, `advisor.travel.mars.to_belt`, plus `advisor.lesson.earth.greed_build`. **Decree ids stay the `FlagDecreeIds` consts.** Toast keys are localization handles only.

---

## Wire notes for LP

**No new systems.** `FlagManager.Post` still takes `FlagData` by `FlagType`. `CampaignProgress` still queues one travel string. `OverseerHud.Toast` / `GameLoop.LogOverseer` can display these lines. Do not auto-post story flags. Do not grow `FlagType`. Do not touch `CaptureStill`, `SpecialistBrain`, `BuildingPlacer`, or art.

### Ready to hook (copy is final enough for a glance)

| Key / id | Hook |
|----------|------|
| All 24 `FlagDecreeIds` toasts in the table | Fire on `Post` when the posted flag is that decree (title match or id stamped later). Until id is stamped on `FlagHandle`, match **title** from `FlagDecreeIds` + current `CelestialBodyId`. |
| Claim asides in E1–E9, L1–L8, M1–M8 | Fire next to `SpecialistFlavor.ClaimLine` — second log line, not a replacement. |
| E4 greed toast | Fire when tutorial wants the price lesson (`TutorialWantsPriceLesson`) or a $70 Build sits with zero interest. |
| Hopper warrant (`luna.clearthreat.root_the_hopper_saboteurs`) | Prefer **claim** (table) so it lands on the first Aegis take; post toast optional if you need a tee. |
| Complete overlays | Guild Charter / craft-staged already exist in `GameLoop` (~1523, ~1548, ~1622). Swap or pair with the complete lines above. Keep the mechanical assign / pad instruction. |

### Placeholders / do not ship as-is (existing runtime strings)

| Current string | W2 status |
|----------------|-----------|
| Earth `ArrivalLog`: “Colony Commons is live…” | **Stale** vs empty-start. Use E0. |
| Earth / Luna / Mars `VictoryLog` | Functional. Use E10 / L9 / M9 when you want advisor voice. |
| `GameLoop` hop: “Departure from {from}. Trajectory locked: {to}.” | Keep as fallback; overlay the travel toasts when the hop is a conquest win. |
| Luna / Mars `ArrivalLog` | Functional fauna tees. Use L0 / M0 for voice. |
| Tutorial 1/6: “Colony Commons is already on the claim.” | HUD leftover. W2 writes empty drop. Do not block wiring on a tutorial rewrite. |
| Guild Charter `LogOverseer` | Keep the assign sentence; add `advisor.earth.charter_the_hall.complete` for tone. |

### Out of scope this doc

- Belt / Europa decree ids or toast tables.
- Fog implementation.
- New `FlagDecreeIds` consts (map and code already match: 8 / 8 / 8).
- Rewriting `SpecialistFlavor` (comment-only hooks above).
- Phase 4 exit stills or art notes beyond “write the campus that will be.”
- Hero personality quest chains — authored in [`W2_HERO_PERSONALITY_QUESTS.md`](W2_HERO_PERSONALITY_QUESTS.md). Same eight types; no quest graph here.

Lookup remains `FlagDecreeIds.TryGet("luna.clearthreat.root_the_hopper_saboteurs", out var d)`. If a future beat needs a new title, add a const **and** a row in `FLAG_TYPE_MAP_EARTH_LUNA_MARS.md` first. Do not grow the enum.
