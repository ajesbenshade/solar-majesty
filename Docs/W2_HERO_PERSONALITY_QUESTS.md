# W2 hero personality quests

**Audience:** Narrative (hero voice + chain copy), LP (toast / claim-aside keys), Producer (glance: do ten egos earn a hop).  
**Status:** W2 quest draft. Copy + chain ids only — not a quest graph engine, not a new `FlagType`, not App Store text, not art.  
**Art:** Phase 4 **EXIT** is still GD’s lane (`Docs/Roadmap/PHASE_4_EXIT.md`). This file does not unblock it. Mars lines assume the Compact campus that will be (white hulls, square docks, geodesic Commons), not the empty Sol 1 still. Do not chase retired packed `SM_MarsCampaign_VisualTarget.png` density.  
**Ids:** Decree slugs stay [`FLAG_TYPE_MAP_EARTH_LUNA_MARS.md`](FLAG_TYPE_MAP_EARTH_LUNA_MARS.md) / `FlagDecreeIds` (twenty-four consts, eight types). Route keys (`route.*`) and find keys (`find.*`) from [`W2_TRADE_ROUTES_AND_FINDS.md`](W2_TRADE_ROUTES_AND_FINDS.md). Quest ids (`quest.{callsign}.{slug}`) are localization handles — **not** decree ids. Do not mint `FlagDecreeIds` for a quest.  
**Voice / stakes:** [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md) · [`W2_CAMPAIGN_STAKES.md`](W2_CAMPAIGN_STAKES.md). Advisor stays wry court counsel. `SpecialistFlavor` keeps the ego. Do not rewrite callsigns.

Flags stay **decrees**. The royal overseer posts them. Ego specialists take or ignore them. The advisor comments. COMMONS is the civic name — never Palace, never CMD as a place. No Majesty 2 / Paradox proper names. No click-to-move fantasy. Belt and Europa stay **named destinations after a Mars win only**.

---

## Quest design rules

A “quest” here is a **Majesty-2-style personality chain**: the overseer posts a titled decree, a hero claims or refuses it, the advisor needles, and a second (or third) decree pays off the ego. It is **not** a new node graph, a journal UI, or an auto-posted story flag.

```
post decree (existing FlagType + FlagDecreeIds title)
  → hero claim or refusal (SpecialistFlavor.ClaimLine stays first)
  → advisor claim aside (second log line)
  → complete toast
  → next decree in the chain (same hero, or the rivalry they just earned)
```

Optional mid-act text cuts from [`W2_CAMPAIGN_STAKES.md`](W2_CAMPAIGN_STAKES.md) (`cut.earth.mid_court`, `cut.luna.mid_warrant`, `cut.mars.mid_crust`) sit **beside** a chain when the civic / tithe / campus beat lands. They do not become quest steps.

| Rule | Lock |
|------|------|
| **Eight types only** | Explore, Clear Threat, Build, Extract, Defend, Research Site, Outpost, Terraform. No Tariff, no Hunt, no Quest. |
| **Decrees, not orders** | Toast copy talks about posting, claiming, completing a title. Never “go here.” Never path-command. |
| **Ego first** | Horizon / Anvil / Aegis / Triage / Strip / Chart / Bloom / Haul / Core / Rim refuse cheap work, wrong class, and each other. Refusal is content. |
| **As-needed stays as-needed** | Dens, furrows, Campus B, crust-weaving tee when the board is loud. A quiet meadow does not owe Aegis a den. |
| **Callsigns already shipped** | `SpecialistFlavor.ClassCallsign`. Advisor asides sit *beside* those lines. Do not rewrite `SpecialistFlavor.cs`. |
| **Factions** | Earth = Sol Authority court. Luna = Lunar Freehold tariff / warrant dryness. Mars = Ares Guild Compact pride — guild-proud, not a second Sol curtsy. |
| **Rewards stay soft** | MET already in escrow. PWR favor and science ticks are copy + existing `ResearchSite` / pad packages. No fifth resource. No quest wallet. |
| **No new engine this doc** | LP can later stamp a chain complete after 2–4 title-matched decrees. Note the hook. Do not spec `QuestManager`. |

**What LP can hook later (not this PR):** a thin `QuestChainCatalog` that maps `quest.*` → ordered `FlagDecreeIds` + toast keys; HUD bounty/quest log chrome already exists as readout, not a path. Until then, title-match toasts + claim asides *are* the quest.

---

## Ten personality quests

One chain per callsign. Primary body is where the ego first becomes a person. Later-body beats are echoes, not required for first-ship.

---

### 1. Horizon — Scout, cheap vanishing act

**Class vibe:** Forty credits, a contour line, and a disappearing act. Horizon will chart anything that does not shoot back and will not stay for the speech. Scout idle already says cheap Explore will do.

**Primary body:** Earth (apron). Luna rille is the echo that proves they were never a hunter.

**Quest id:** `quest.horizon.apron_map`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.explore.survey_the_claim` | Explore | Keep Scout: *“Cheap Explore. Forty credits and I'm gone.”* | Horizon priced the orange disc. Fog can wait. Guessing is undignified. |
| 2 | complete | `find.earth.apron_cairn` (pair Survey the Claim) | Explore | Horizon (aside, not a rewrite): *“Cairn on the apron. REG with a wink. Invoice later.”* | Horizon found a cairn the meadow forgot to invoice. They did not stay to weigh it. That is Strip’s religion. |
| 3 | post / claim | `luna.explore.chart_the_tariff_rille` · `route.luna.rille_ledger` | Explore | Keep Surveyor/Scout Explore claim. Horizon if they hopped: *“Same joke. Worse lighting. Forty still works.”* | Horizon marks lanes the Freeholds already tax. Smugglers use the same ink. So will we. Nobody fights a contour line. |
| 4 *(optional)* | complete | `find.luna.rille_cache` | Explore | *“Cache off the rille. They will say inventory. I say found. I'm gone.”* | A Freehold cache. Horizon left a pin and a receipt-shaped hole. Chart can argue the ink. |

**Failure / refusal:** Post Clear Threat at $40 and Horizon will treat it as a spelling error. Post Explore at $15 and they will wander the apron like scenery. Wrong class: Aegis will not “chart” a den; Horizon will not “hunt” a cairn.

**Reward note:** Complete Explore already pays MET escrow. Soft: `find.earth.apron_cairn` is REG theater (no new node required). Luna cache is a toast; do not invent Salvage.

**Optional mid-cut:** None. Horizon leaves before `cut.earth.mid_court`. That is the joke.

---

### 2. Anvil — Engineer, pride with a price

**Class vibe:** Welds sit on the books or they do not sit. Anvil will raise a Commons, dock a HAB, and stage a pad — then invoice the mid-weld cheap-out. Idle already: Build flags need real pay.

**Primary body:** Earth (tutorial court). Luna crater Commons and Mars Compact seat are the same weld in worse manners.

**Quest id:** `quest.anvil.honest_weld`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.build.raise_the_commons` | Build | Keep Engineer: *“Build's on the books. Don't cheap out mid-weld.”* | Anvil took the weld. Try not to look surprised. A court that meets in the dirt is a picnic. |
| 2 | post (idle cheap Build) | — (reuse live Build on E2 / E3; default $70). Toast `advisor.lesson.earth.greed_build` | Build | Idle already: *“Idle. Build flags need real pay.”* Do not rewrite. | Seventy is a suggestion; *tempted* is a fact. Raise the bounty until the chip admits it. Pride has a price. Remember it on Luna. |
| 3 | post / claim | `earth.build.dock_the_first_hab` | Build | Same Anvil claim. | Dock the first HAB. Humans stay indoors. Anvil welds the airlock join — square socket, not a speech. Beds on the books. The meadow just became a ledger. |
| 4 | post / complete | `earth.build.stage_the_lunar_rocket` | Build | *“Pad labour. Same books. Stop being cute with the bounty.”* | Stage the Lunar Rocket. Science is 70 + 40 MET + 15 ICE. A trajectory without a pad is a toast to vacuum. |

**Failure / refusal:** Pocket-change Build sits idle — that *is* the greed lesson, not a soft-lock. Post Explore on a construction order and Anvil will tinker in town until you remember F3. Wrong class: Horizon will not weld; Haul will not “freight” a Commons.

**Reward note:** Build labour is the reward. Soft: honest bounty now trains the overseer for Luna dock fees (4 MET) and Mars guild-proud payroll. No new MET sink.

**Optional mid-cut:** `cut.earth.mid_court` after Commons + HAB + Charter. Anvil’s weld is the civic half; Chart eats the sample.

**Luna / Mars echo (not first-ship):** `luna.build.raise_the_crater_commons`, `luna.build.fortify_the_airlocks`, `mars.build.raise_the_compact_seat`, `mars.build.string_the_solar_field`. Same claim line. Advisor: try a guild-proud bounty. They notice.

---

### 3. Aegis — Defense, warrants not sermons

**Class vibe:** Shield first, den second, toast never. Aegis hunts. Stay behind it. Clear Threat is a warrant; Defend is a rim they will take if the den is already a rumor.

**Primary body:** Earth dens, then Luna hopper warrant (the hop that makes them a person).

**Quest id:** `quest.aegis.warrant_book`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.clearthreat.seal_the_near_dens` · `find.earth.near_dens` | ClearThreat | Keep Defense: *“Hunting the den. Stay behind the shield.”* | The court does not share the meadow. Three dens. Seal them before the creepers start charging rent. Aegis hunts. Stay out of the toast. |
| 2 *(as needed)* | post / claim | `earth.defendarea.ward_the_furrows` | DefendArea | *“Rim watch. Nothing crosses.”* | Aegis will hold a field cheaper than they will hunt a den. Tempt them accordingly. Do not lead the tutorial with this. |
| 3 | claim (prefer) | `luna.clearthreat.root_the_hopper_saboteurs` · `find.luna.hopper_warren` | ClearThreat | Same Clear Threat claim. | Someone cut the hoppers loose on the HAB. Post a warrant, not a sermon. Aegis accepted. The hoppers did not. Ask later who paid them. |
| 4 *(optional)* | post / claim | `mars.clearthreat.hunt_the_wisps` · `find.mars.wisp_nest` | ClearThreat | *“Hunting the den. Stay behind the shield.”* (wisps are a den that drinks) | Aegis hunts light that should not have a mouth. Compact work. They drink complimentary. It is not. |

**Failure / refusal:** Cheap Clear Threat ($40) is an insult, not a bargain. Post Extract on a den and Aegis will idle: *“Post Clear Threat or Defend.”* Wrong class: Triage will say they are not a hunter. Horizon will vanish. Strip will say dens can wait — and mean it.

**Reward note:** Force-clear as shipped. Soft: Luna warrant is the anger beat for `cut.luna.mid_warrant`. No new combat system.

**Optional mid-cut:** `cut.luna.mid_warrant` on levy complete **or** first Aegis claim on the warrant.

---

### 4. Triage — Medic, not a hunter

**Class vibe:** On the wounded. Will hold a furrow. Will not take a warrant. The ego is the refusal: the court already has a hunter.

**Primary body:** Earth furrows. Luna weigh-station and Mars dust farms are the same hold with worse pests.

**Quest id:** `quest.triage.ward_not_hunt`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.defendarea.ward_the_furrows` | DefendArea | Keep Medic: *“On the wounded. I am not a hunter.”* | Ward the furrows. Soil creepers chew farms. Triage will hold a field. They will not seal a den. Do not ask twice. |
| 2 | post / claim | `luna.defendarea.ward_the_weigh_station` | DefendArea | *“On the wounded. Ticks are not a sermon. I hold.”* | Ticks steal from the weigh-station. That is a rim watch with worse lighting. Triage covers the clerk that isn't there. Rim can lock the rest. |
| 3 | post / claim | `mars.defendarea.ward_the_dust_furrows` | DefendArea | *“On the wounded. Compact lunch. I am still not a hunter.”* | Compact farms do not get abandoned for a prettier den hunt. Creepers chew; Triage holds. Bloom can invoice the garden after. |
| 4 *(optional rivalry)* | refuse | `earth.clearthreat.seal_the_near_dens` or `luna.clearthreat.root_the_hopper_saboteurs` | ClearThreat | Idle: *“Idle near the wounded.”* | Triage walked past the warrant. Correct. Aegis already has a book for that. |

**Failure / refusal:** Post Clear Threat and they will stand at the inn. Post Build and they will not weld. Cheap Defend they *will* take — that is the class joke. Ego clash: Aegis calls it cowardice; Triage calls it a job description.

**Reward note:** Defend-area dps / local danger as shipped. Soft: farms stay on the ledger (ICE). No medic resource.

**Optional mid-cut:** None required. If `cut.earth.mid_court` fires, Triage is the quiet proof the court has beds and wounded, not just a hunter.

---

### 5. Strip — Harvester, ore first

**Class vibe:** The levy is the job. Dens are other people’s poetry. Strip will empty a farm, weigh a Freehold receipt, and invoice anyone who asks them to hunt.

**Primary body:** Earth meadow farm, then Luna tithe (the hop that makes the receipt real).

**Quest id:** `quest.strip.receipt_first`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.extract.levy_the_meadow_farm` · `route.earth_luna.meadow_ice` | Extract | Keep Harvester: *“Ore first. Dens can wait.”* | Levy the meadow farm. ICE is life support, not a beverage program. The Freeholds will call this hospitality when it lands. Creepers will RSVP. |
| 2 | post / claim | `luna.extract.weigh_the_freehold_ore` · `route.luna_mars.ore_tithe` | Extract | Same claim. *“Receipt first. Hoppers can wait.”* | They call it a tariff. We call it MET with a receipt. Strip hauled it. Do not lose it. The Freeholds will ask. |
| 3 *(support)* | — | `luna.defendarea.ward_the_weigh_station` (Rim / Triage claim) | DefendArea | Strip does **not** take this. Aside if they idle: *“Ore first. Ticks are furniture.”* | Ticks are stealing. That is a rim watch. Strip keeps weighing. Correct. |
| 4 *(optional)* | complete | pad resupply tick as `route.earth_luna.meadow_ice` courtesy | Extract (skin) | — | Hospitality inbound. Strip already knew. The dock fee is still four MET. |

**Failure / refusal:** Post Clear Threat and Strip will keep stripping. Cheap Extract they take — class is honest about pay. Wrong class: Aegis will not weigh ore; Horizon will pin a cairn and leave the haul.

**Reward note:** Node haul as shipped. Soft: MET receipt + ICE hospitality copy. PWR is not a substitute; Freeholds will not take a spark in lieu of metal.

**Optional mid-cut:** `cut.luna.mid_warrant` — Strip’s receipt is the first bruise; Aegis’ warrant is the second.

---

### 6. Chart — Surveyor, ink not vanishing

**Class vibe:** Maps the site, feeds the tree, refuses the fight. Chart is Horizon with a lab coat and a grudge against cheap pins. Research Site is how a court gets a hall instead of a rumor.

**Primary body:** Earth Guild Charter. Luna rille / dead weigh are the professional echo.

**Quest id:** `quest.chart.site_mapped`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.researchsite.charter_the_hall` · `find.earth.old_court_stub` | ResearchSite | Keep Surveyor Research: *“Site mapped. Science into the tree.”* | Charter the hall. A court without a guild is stationery. Old court stone — we were scenery here once. Chart logged it. The lab can eat the sample; the hall can eat the ego. |
| 2 | complete | overlay `advisor.earth.charter_the_hall.complete` | ResearchSite | — | Guild Charter signed. Dock the hall. Assign a class. Flags near CMD-1 dress pull that ego. Keep the `GameLoop` assign sentence. |
| 3 | post / claim | `luna.explore.chart_the_tariff_rille` · `find.luna.rille_cache` | Explore | Keep Surveyor Explore: *“Charting the apron. No fights.”* | Chart took a tariff lane. Horizon would have left a pin. Chart left a contour and an argument. |
| 4 | post / complete | `luna.researchsite.commission_the_mars_ship` · `find.luna.dead_weigh` | ResearchSite | *“Site mapped. Science into the tree.”* Dead weigh: *“Clerk is gone. Tariff is not. Lab can eat the furniture.”* | Commission the Mars Ship. A weigh-station with no clerk. The Compact is 100 science, 80 MET, 30 ICE, 20 PWR, and a pad Anvil still has to weld. |

**Failure / refusal:** Post Clear Threat and Chart will idle Explore / Research Site. Cheap Research they take. Ego clash: Horizon calls Chart slow; Chart calls Horizon a tourist with a receipt.

**Reward note:** +12 science as shipped (Guild Charter / Mars Ship). Soft: `find.earth.old_court_stub` tees the hall; `find.luna.dead_weigh` tees +12 toward Mars Ship if LP wants the sample to matter. No new lab.

**Optional mid-cut:** `cut.earth.mid_court` when Commons + HAB + Charter complete. Chart’s sample is the charter half.

---

### 7. Bloom — Terraformer, garden on an invoice

**Class vibe:** Slow work. Worth it. Every grain billed. Earth is already green — Bloom will not cosplay a meadow. Mars is the first crust that needs a creed.

**Primary body:** Mars (`TerraformCharter` licenses the shop; the flag is still Terraform).

**Quest id:** `quest.bloom.garden_invoice`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | refuse (Earth tease) | any Earth `Terraform` (no W2 decree id — do not mint one) | Terraform | Idle: *“Idle. Terraform is cheap.”* If claimed: *“Greening this crust. Slow work. Worth it.”* | The meadow is already a garden. Bloom will invoice you for painting grass. Save the creed for Compact dust. |
| 2 | post / claim | `mars.terraform.weave_the_crust` | Terraform | Keep Terraformer: *“Greening this crust. Slow work. Worth it.”* | They want a garden. Pay for a garden. Bloom weaves +0.08 and will invoice you for every grain. Do not lead the act with this. |
| 3 *(support)* | — | `mars.defendarea.ward_the_dust_furrows` (Triage / Rim / Aegis) | DefendArea | Bloom does not hunt. Aside: *“Hold the furrow. I am billing the dirt, not the creeper.”* | Compact lunch stays on the books so Bloom can be slow on purpose. |
| 4 *(optional prestige)* | aside, no new id | Climate Loom after the three Mars gates | — | *“Jewelry. I still invoice the grain.”* | Loom is Compact jewelry. Place it if the guild wants to show off. Not a ninth findable. |

**Failure / refusal:** Cheap Terraform on Mars sits until the bounty admits the creed. Post Extract on a garden and Bloom will idle. Wrong class: Strip will strip the node; Core will sample it; neither will weave.

**Reward note:** +0.08 farm yield (cap +0.6) as shipped. Soft: Compact lunch / ICE. No seed currency.

**Optional mid-cut:** `cut.mars.mid_crust` — first of solar / wisps / crust. Do not force Bloom.

---

### 8. Haul — Courier, freight after

**Class vibe:** Cyan disc, then freight. They have a slogan. It works. Haul will not wander Horizon’s apron for forty credits when a lodge is on the books.

**Primary body:** Luna far rim. Mars Campus B is the Compact rhyme.

**Quest id:** `quest.haul.freight_after`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `luna.establishoutpost.stake_the_far_rim` | EstablishOutpost | Keep Courier Outpost: *“Claim the cyan disc. Freight after.”* | Stake the far rim. Campus B, not a picnic site. Haul took the disc. Believe the slogan. |
| 2 | post / claim | `mars.establishoutpost.stake_campus_b` | EstablishOutpost | Same claim. | Stake Campus B. A Compact forward lodge — not a Freehold weigh-station, not a Sol picnic. Cyan disc. Haul already knows. |
| 3 *(optional)* | complete | `find.mars.compact_cairn` · `route.earth_mars.watt_favor` (survey skin) | Explore (Haul may not claim) | If Haul is on Explore, not Outpost: *“Hauling the flag. Keep the path clear.”* | Compact cairn stacked like a sermon. Freight after. Horizon would have left. Haul will actually move it — if you posted Extract, not a speech. |
| 4 *(coda)* | — | `route.mars_belt.named_haul` · `mars.researchsite.commission_the_belt_hauler` (Chart / Core claim the site; Haul does not become a lab) | ResearchSite | Haul idle if you wave the rocks at them: *“Idle. Explore or Outpost.”* | Belt is a name on a pad. Haul will freight it when there is a disc. We are not writing the rocks today. |

**Failure / refusal:** Post Outpost nowhere near the cyan disc and Haul will not invent a campus. Cheap Outpost they may still take (class likes the disc). Wrong class: Rim will not wander to Campus B; Horizon will pin it and vanish; Anvil will ask where the weld is.

**Reward note:** Campus B claim as shipped (18 m of cyan disc). Soft: forward-lodge freight / MET tithe. No caravan sim.

**Optional mid-cut:** None. Campus B is optional on both bodies — skip if Campus A is the whole story.

---

### 9. Core — Geologist, crust then haul

**Class vibe:** Reads the crust. Haul follows. Lab can eat the sample. Core is Strip with a notebook and Chart with dirt on it.

**Primary body:** Luna levy + dead weigh. Mars Belt Hauler is the industrial sample.

**Quest id:** `quest.core.lab_can_eat`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `luna.extract.weigh_the_freehold_ore` · `route.luna_mars.ore_tithe` | Extract | Keep Geologist Extract: *“Reading the crust. Haul follows.”* | Strip and Core haul the levy. Strip wants the receipt. Core wants the grain that made the receipt honest. |
| 2 | complete | `find.luna.dead_weigh` | ResearchSite | *“Core sample logged. Lab can eat this.”* | A weigh-station with no clerk. The tariff persists. The furniture did not. +12 toward Mars Ship if the sample should matter. |
| 3 | post / complete | `luna.researchsite.commission_the_mars_ship` | ResearchSite | Same Research Site claim. | The lab ate the sample. The pad will eat Anvil’s afternoon. Pack guild manners. |
| 4 | post / complete | `mars.researchsite.commission_the_belt_hauler` · `route.mars_belt.named_haul` | ResearchSite | *“Core sample logged. Lab can eat this. The rocks can wait in the catalog.”* | Commission the Belt Hauler — and place the pad. Science without a landing is a Compact joke we do not tell twice. The rocks are a name. |

**Failure / refusal:** Post Clear Threat and Core will idle Extract / Research Site. Cheap Research they take. Ego clash: Strip calls Core slow; Core calls Strip a receipt without a grain. Chart will argue about who logged the site first — let them.

**Reward note:** Extract haul + +12 science as shipped. Soft: Mars Ship / Belt Hauler tees. Pad labour remains Anvil’s `Build`.

**Optional mid-cut:** `cut.luna.to_mars` / `cut.mars.belt_named` on the commission completes. Core does not need a unique modal.

---

### 10. Rim — Sentinel, I hold, I do not wander

**Class vibe:** Perimeter locked. Defend is cheap for them — and they will remind you. Rim will not hunt a prettier den, will not freight a cyan disc, will not chart an apron.

**Primary body:** Luna weigh-station. Earth furrows are a dignified warmup; Mars dust farms are Compact lunch.

**Quest id:** `quest.rim.nothing_crosses`

| Beat | When | Decree / find / route | FlagType | Hero aside | Advisor beat |
|------|------|------------------------|----------|------------|--------------|
| 1 | post / claim | `earth.defendarea.ward_the_furrows` | DefendArea | Keep Sentinel: *“Perimeter locked. I hold. I do not wander.”* | Rim watch on a farm. Very dignified. Very necessary. Aegis could hunt instead. Rim will not. |
| 2 | post / claim | `luna.defendarea.ward_the_weigh_station` | DefendArea | Same claim. | Ticks are stealing from the weigh-station. Rim took the watch. The levy continues. Poetry later. |
| 3 *(support)* | post | `luna.build.fortify_the_airlocks` (Anvil welds; Rim does not) | Build | Rim idle: *“Idle. Defend is cheap for me.”* | Anvil fortifies. Aegis will pretend it was their idea. Rim will stand where the weld ends and refuse to wander past it. |
| 4 | post / claim | `mars.defendarea.ward_the_dust_furrows` | DefendArea | Same claim. | Rim or Aegis on a greenhouse. The Compact keeps its lunch. Campus B can wait. Rim already said they do not wander. |

**Failure / refusal:** Post Outpost and Rim will not claim the cyan disc — that is Haul’s slogan. Post Explore and they hold. Post Clear Threat and they may pest the rim (`HuntLine`: *“Pest on the rim — engaging.”*) but they will not take a den warrant if Aegis is in the hall. Cheap Defend they take. That is the class.

**Reward note:** Defend-area as shipped. Soft: levy continues (MET / ICE stay on the board). No patrol unlock.

**Optional mid-cut:** None. Rim is the quiet proof `cut.luna.mid_warrant` was not only anger.

---

## Cross-hero rivalries

Short pairings. Fire as a one-line advisor aside when both heroes have claimed (or one has refused) in the same act. Not a fourth quest. Not a new type.

### Anvil vs cheap Builds

The overseer is the other half of this rivalry. E4 is the tutorial; Luna / Mars repeats it with dock fees and guild pride.

- **Trigger:** a $70 (or cheaper) Build sits with zero interest, or Anvil idles *“Build flags need real pay.”*
- **Anvil:** *“Don't cheap out mid-weld.”*
- **Advisor:** There. Pride has a price. Horizon would have taken forty and left a socket half-true. We do not hire Horizon to weld.

### Aegis vs Triage — hunt vs hold

- **Trigger:** Clear Threat and Defend posted in the same act (Earth dens + furrows, or Luna warrant + weigh-station).
- **Aegis:** *“Hunting the den. Stay behind the shield.”*
- **Triage:** *“On the wounded. I am not a hunter.”*
- **Advisor:** Aegis wants a warrant. Triage wants a pulse. Post both or enjoy a hero argument and a dead farm.

### Horizon vs Chart — pin vs ink

- **Trigger:** both complete an Explore on the same body (`survey_the_claim` / `chart_the_tariff_rille` / `survey_the_red_apron`).
- **Horizon:** Forty credits and gone.
- **Chart:** *“Charting the apron. No fights.”* (and they stay for the contour.)
- **Advisor:** Horizon left a pin. Chart left a map. The Freeholds invoice maps. Pins they call graffiti.

### Strip vs Aegis — ore first vs dens first

- **Trigger:** Extract and Clear Threat both live (meadow levy + near dens, or Freehold ore + hopper warrant).
- **Strip:** *“Ore first. Dens can wait.”*
- **Aegis:** The dens will not wait. That is why they are dens.
- **Advisor:** Pay both. A receipt under a creeper is a receipt the Freeholds will still collect.

**Honorable mention (do not ship as a pairing toast unless the board is loud):** Haul vs Rim — freight after vs I do not wander. Bloom vs Strip — garden invoice vs ore first. Let them idle at each other.

---

## Vertical-slice priority

Ship the Earth court → Luna hop first. Four chains. The other six can stay copy-complete and unwired.

| Priority | Quest id | Why first | Bodies in slice | Beats that must fire |
|----------|----------|-----------|-----------------|----------------------|
| **1** | `quest.anvil.honest_weld` | Tutorial court. Greed lesson. Commons / HAB / pad. The player learns ego. | Earth (Luna crater weld optional) | Beats 1–3 required; beat 4 with Lunar Rocket |
| **2** | `quest.horizon.apron_map` | First F1. Cheap Explore. Cairn find. Teaches refusal of fights. | Earth (Luna rille if they hop) | Beats 1–2; beat 3 on Luna arrival |
| **3** | `quest.strip.receipt_first` | Sustain levy becomes the Freehold tithe. The hop *hurts*. | Earth → Luna | Beat 1 on Earth; beat 2 on Luna (gates `cut.luna.mid_warrant` with Aegis) |
| **4** | `quest.aegis.warrant_book` | Combat ego + Luna warrant. Pair with Strip for the mid-cut. | Earth dens *(as needed)* → Luna HAB | Beat 1 if dens are loud; beat 3 on Luna (prefer **claim** fire) |

**Hold for Luna mid / Mars campus (copy ready, do not block slice):**

| Quest id | Why later |
|----------|-----------|
| `quest.chart.site_mapped` | Charter is already a `GameLoop` line. Wire Chart asides after Anvil / Horizon land. |
| `quest.triage.ward_not_hunt` | Furrows are as-needed. Ship the refusal line with Aegis rivalry. |
| `quest.rim.nothing_crosses` | Weigh-station watch is Luna support. |
| `quest.haul.freight_after` | Campus B is optional on both bodies. |
| `quest.core.lab_can_eat` | Needs Luna levy + Mars Ship science. Pair with Chart. |
| `quest.bloom.garden_invoice` | Mars only. Do not force `weave_the_crust`. |

Producer glance: **four first-ship quests, ten authored.** Earth court should feel like Anvil + Horizon. The hop should feel like Strip’s receipt and Aegis’ warrant.

---

## Wire notes for LP

**No new systems. No new `FlagType`. No new `FlagDecreeIds`.** `FlagManager.Post` still takes `FlagData` by type. Toast-on-Post still waits on title-match (`FlagDecreeIds` title + current `CelestialBodyId` until an id is stamped on `FlagHandle`). Do not auto-post story flags. Do not touch `SpecialistBrain`, `CaptureStill`, or art. Do not rewrite `SpecialistFlavor`.

### Docs-only now

| What | How |
|------|-----|
| Ten `quest.*` ids | Localization handles. **Not** decree ids. Suggested keys below. |
| Beat chains | Each beat points at an existing `FlagDecreeIds` const, a `find.*` / `route.*` key, or a travel/greed toast already in the advisor doc. |
| Hero asides | Second log line next to `SpecialistFlavor.ClaimLine`. Quoted asides that are *not* the shipped claim are optional color — do not replace the callsign line. |
| Rivalry asides | One extra advisor line when two triggers coincide. Same toast pipe. |
| Mid-cuts | Reuse `cut.earth.mid_court`, `cut.luna.mid_warrant`, `cut.mars.mid_crust`. Do not mint `cut.quest.*`. |

### Suggested toast keys (handles only)

`quest.{callsign}.{slug}.{beat}.{when}` — e.g. `quest.horizon.apron_map.1.post`, `quest.anvil.honest_weld.2.lesson`, `quest.aegis.warrant_book.3.claim`.

Rivalries: `quest.rival.anvil_cheap_build`, `quest.rival.aegis_triage`, `quest.rival.horizon_chart`, `quest.rival.strip_aegis`.

Where a beat **is** an existing advisor row, **reuse** `advisor.{body}.{slug}.{when}` and do not duplicate the line. Quest keys are for the extra hero aside / rivalry / find punchline only.

Complete overlays already drafted — keep them:

- `advisor.earth.charter_the_hall.complete`
- `advisor.earth.stage_the_lunar_rocket.complete`
- `advisor.luna.commission_the_mars_ship.complete`
- `advisor.mars.commission_the_belt_hauler.complete`

### Needs code later (explicitly out of scope)

- Title-match toast fire (already noted in the advisor doc). Same hook serves quest beats.
- Stamping a `quest.*` complete after N title-matched decrees — a later `QuestChainCatalog` is enough. Not a graph. Not auto-post.
- HUD quest log beyond current bounty / mission chrome.
- Fog reveal (write chart lines now).
- New `FlagDecreeIds` unless a title cannot skin an existing row — then const **and** a map row, same eight types.
- Belt / Europa quests.
- Art, foliage, `CaptureStill`, Phase 4 EXIT stills, Phase 5 ship.

Lookup remains `FlagDecreeIds.TryGet(...)`. Quest ids are **not** in that table.

---

## Thin pointers

Spine docs stay the voice / stakes / lane source. This file only owns **hero chains + rivalries + first-ship order**.

| Doc | What to reuse |
|-----|----------------|
| [`FLAG_TYPE_MAP_EARTH_LUNA_MARS.md`](FLAG_TYPE_MAP_EARTH_LUNA_MARS.md) | Twenty-four decree ids, eight types, callsigns, COMMONS lock. |
| [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md) | E0–E10 / L0–L9 / M0–M9 toasts, claim asides, greed lesson, travel logs. Quest asides sit beside those lines. |
| [`W2_CAMPAIGN_STAKES.md`](W2_CAMPAIGN_STAKES.md) | Optional mid-cuts (`cut.earth.mid_court`, `cut.luna.mid_warrant`, `cut.mars.mid_crust`) and hop stakes. Quests do not replace the spine. |
| [`W2_TRADE_ROUTES_AND_FINDS.md`](W2_TRADE_ROUTES_AND_FINDS.md) | `route.*` / `find.*` keys used as beat skins (cairn, rille cache, hopper warren, dead weigh, compact cairn, wisp nest, meadow ice, ore tithe, named haul). |

---

## Out of scope

- Gameplay code, new `FlagType`, new `FlagDecreeIds`, quest nodes, auto-posted story flags.
- Art, `CaptureStill`, Phase 4 EXIT stills, Phase 5 ship.
- Belt / Europa decree lists or hero quests.
- Rewriting `SpecialistFlavor` or `SpecialistBrain`.
- A fifth resource, PWR wallet, or quest shop.
- Majesty 2 / Paradox proper names (Palace, tax collectors guild, hero titles).
