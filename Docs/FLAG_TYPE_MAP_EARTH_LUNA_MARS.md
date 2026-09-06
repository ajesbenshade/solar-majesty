# Flag-type map — Earth → Luna → Mars

**Audience:** Narrative (W2 advisor + chain beats) and LP (shared ids).  
**Not:** a quest graph, a new `FlagType` enum, or App Store copy.  
**Status:** Design + data. Game Designer still-capture gate is signed (still16). Phase 4 **EXIT** stays art-blocked (`PHASE_4_EXIT.md`). Do this in parallel with leftover buildings.

**W2 advisor copy:** [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md) — toasts / travel logs / claim asides against the ids below. Phase 4 EXIT still art-blocked.

Narrative can draft advisor lines from this doc alone. Flags stay **decrees**: the royal overseer posts them; ego specialists take or ignore them; the wry advisor comments. Do **not** copy Majesty 2 guild/flag names or Paradox assets.

---

## How to use this

| Role | Use |
|------|-----|
| **Narrative** | Write advisor toasts / travel logs / claim asides against **decree ids** (`earth.build.raise_the_commons`). W2 draft copy: [`W2_ADVISOR_AND_CHAIN_BEATS.md`](W2_ADVISOR_AND_CHAIN_BEATS.md). |
| **LP** | Keep posting the existing eight `FlagType`s. A decree is a **title + body + beat**, not a new mechanic. Look up ids in `FlagDecreeIds`. |
| **Code** | `FlagManager.Post` still takes `FlagData`. `CampaignProgress` still unlocks bodies. No click-to-move. Do not rewrite `SpecialistBrain`. |

**Id convention:** `{body}.{typetoken}.{slug}` — lowercase, stable, no spaces.

- `body` = `earth` · `luna` · `mars` (`CelestialBodyId`)
- `typetoken` = `FlagDecreeIds.TypeToken(FlagType)` (`explore`, `clearthreat`, `build`, `extract`, `defendarea`, `researchsite`, `establishoutpost`, `terraform`)
- `slug` = short snake_case title

Example: `earth.explore.survey_the_claim` → Earth + `FlagType.Explore`.

---

## Locked pitch (do not rename)

| Piece | Lock |
|-------|------|
| Voice | Royal overseer, wry advisor, ego heroes. Flags are **decrees**, not unit orders. |
| Factions | **Sol Authority** (Earth cradle) → **Lunar Freeholds** (tariff / sabotage) → **Ares Guild Compact** (Mars stronghold; Phase 4 campus). |
| Spine | Tutorial court + guild induction on Earth → tariff wars on Luna → Compact seat on Mars. |
| HUD names | **COMMONS** (never Palace / CMD). Guild Hall = CMD-1 dress. OPS-1 is the Mining annex. |
| Callsigns (already in `SpecialistFlavor`) | Horizon · Anvil · Aegis · Triage · Strip · Chart · Bloom · Haul · Core · Rim |

Belt and Europa stay on the mechanical spine (`CampaignProgress` → `CelestialBodyCatalog.Last`). They are **out of W2 narrative scope**.

---

## Mechanical types (already shipped)

These eight values in `GameTypes.FlagType` are the only bounty kinds. Every decree below maps to one of them. Hotkeys from `FlagPlacementInput`. Assets live in `Assets/Data/Flags/` and `Assets/Resources/DemoContent/Flags/`.

| `FlagType` | HUD (`SpecialistFlavor.FlagShort`) | Key | Default $ / work / risk | Completion (today) | Typical class |
|------------|-------------------------------------|-----|-------------------------|--------------------|---------------|
| `Explore` | Explore | F1 | 40 / 4 / 0.08 | Pays credits. **No fog reveal yet.** | Scout, Surveyor, Courier |
| `ClearThreat` | Clear Threat | F2 | 80 / 6 / 0.40 | Force-clears nearest den within 12 m | Defense |
| `Build` | Build | F3 | 70 / 8 / 0.10 | Labour on a construction order within 6 m | Engineer |
| `Extract` | Extract | F4 | 55 / 7 / 0.12 | Hauls a node to the nearest matching drop-off | Harvester, Geologist |
| `DefendArea` | Defend | F5 | 65 / 9 / 0.25 | While worked: ~9 dps to nearby fauna; lowers local danger | Defense, Medic, Sentinel |
| `ResearchSite` | Research Site | I | 50 / 6 / 0.10 | +12 science to the active tech | Surveyor, Geologist, Scout |
| `EstablishOutpost` | Outpost | O | 75 / 10 / 0.22 | Claims Campus B if within 18 m of the cyan disc | Courier |
| `Terraform` | Terraform | U | 70 / 11 / 0.14 | +0.08 farm yield (cap +0.6) | Terraformer |

All eight types are **playable on every body from drop**. Narrative “unlocks” are advisor tees and tech/building gates, not new enum values. Do not add a `Tariff` or `Sabotage` `FlagType` — those are decree skins on `Extract` / `ClearThreat`.

### System hooks (all worlds)

| Hook | Types | Code |
|------|-------|------|
| **Economy** | `Extract`; every post escrows MET | `SimpleEconomy`, `ResourceManager` (REG / ICE / MET / PWR). Luna dock fee 4 MET; Mars 6. Earth resupply free. |
| **Heroes / specialists** | All | `SpecialistBrain` scores type + bounty + risk. Guild Hall (`TechId.GuildCharter`) pulls the assigned class. Workshops fabricate robots. |
| **Buildings** | `Build` (must sit on a live `ConstructionOrder`) | `BuildingPlacer`. Commons first. Pad is the launch site. |
| **Threat (now)** | `ClearThreat` dens / hoppers / leeches / wisps; `DefendArea` mites / ticks / creepers | `DustStalkerAgent`, `ThreatPressure`, `StalkerLair` |
| **Threat / fog (later)** | `Explore` can grow a reveal; do not wait on it for W2 copy | Write “chart / survey” lines now; implementation is later |
| **Research** | `ResearchSite` | `ResearchManager` + `TechCatalog`. Launch techs: Lunar Rocket / Mars Ship / Belt Hauler |
| **Campaign gate** | `EstablishOutpost` (Campus B); launch = tech + pad | `MissionController` + `CampaignProgress.UnlockNextFrom` |

---

## Unlock / gate spine

`CampaignProgress` persists `SM_CampaignMaxBody`. New Game = Earth. Conquest win unlocks the next body. Debug: Shift+F10 / Shift+click a locked chip.

| Body | Faction frame | Dens | Pop / hold | Launch tech + pad | On win |
|------|---------------|------|------------|-------------------|--------|
| **Earth** | Sol Authority court | 3 | 8 / 25 s | `TechId.LunarRocket` | Travel log → **LUNA** unlocked |
| **Luna** | Lunar Freeholds | 8 | 12 / 40 s | `TechId.MarsShip` | Travel log → **MARS** unlocked |
| **Mars** | Ares Guild Compact | 10 | 16 / 50 s | `TechId.BeltHauler` | Travel log → Belt (not W2 copy) |

Sustain also needs Commons, housing ≥ goal, ≥1 Farm, ≥1 Mine, floors ICE ≥ 8 / MET ≥ 12 / REG ≥ 10 (`MissionController` + `Settlement`).

**Tutorial (Earth, six skippable beats):** Commons → airlock → HAB → workshop → flag → TECH (`OverseerHud`). Teach **Build $70** greed (Anvil refuses cheap weld) on beat 5.

**Research tees (advisor, not auto-quest):**

| Body | Tee toward | Also useful |
|------|------------|-------------|
| Earth | Field Survey → Hab Ops → **Guild Charter** (induction) → Lunar Rocket | Extract Basics, Life Support, Ore Refining, Power Systems |
| Luna | Deep Survey / Med Protocols → **Mars Ship** | Harvest / Survey doctrines if the Freehold levy is the story |
| Mars | **Belt Hauler** | Terraform Charter, Freight Doctrine, wonders (Climate Loom / Aegis Spire / Deep Archive) as Compact prestige |

---

## Earth — Sol Authority (tutorial court)

**Theme:** Cradle court on the meadow claim. Raise the Commons, induct a hall, stage the Lunar Rocket.  
**Fauna:** Soil Creeper on farms (`DefendArea`, F5). Watt Leech on Power (`ClearThreat`, F2). Three dens.  
**De-emphasize until after the six-beat tutorial:** `EstablishOutpost`, `Terraform` (Earth is already green; Bloom is a later flourish).

### Types in play

| Type | Purpose here |
|------|----------------|
| `Explore` | Chart the meadow apron around the orange claim disc. |
| `Build` | Raise Commons / dock HAB / stage the pad. Greed lesson. |
| `Extract` | First farm / camp levy — ICE and REG for sustain. |
| `DefendArea` | Ward soil creepers off the furrows. |
| `ClearThreat` | Seal the three near dens; swat leeches on Power. |
| `ResearchSite` | Field Survey ticks; science toward Guild Charter and Lunar Rocket. |

### Example decrees

| Id | Title | `FlagType` | Intent (one line) |
|----|-------|------------|-------------------|
| `earth.explore.survey_the_claim` | Survey the Claim | `Explore` | Horizon maps the meadow so the court knows the apron. |
| `earth.build.raise_the_commons` | Raise the Commons | `Build` | First civic landmark; tutorial court opens. Place Commons, then post Build on the order. |
| `earth.build.dock_the_first_hab` | Dock the First HAB | `Build` | Beds and tax. Humans stay indoors; Anvil welds the airlock join. |
| `earth.extract.levy_the_meadow_farm` | Levy the Meadow Farm | `Extract` | ICE for life support. Creepers follow the harvest. |
| `earth.defendarea.ward_the_furrows` | Ward the Furrows | `DefendArea` | Hold farm pests without a hunt. Aegis / Triage cheap to tempt. |
| `earth.clearthreat.seal_the_near_dens` | Seal the Near Dens | `ClearThreat` | Combat gate — three dens. Advisor: “The court does not share the meadow.” |
| `earth.researchsite.charter_the_hall` | Charter the Hall | `ResearchSite` | +12 science toward `TechId.GuildCharter`. Then place the hall and assign Horizon / Anvil / Aegis / Triage. |
| `earth.build.stage_the_lunar_rocket` | Stage the Lunar Rocket | `Build` | Pad construction labour. Pair with researching Lunar Rocket (70 sci + 40 MET + 15 ICE). Unlocks Luna. |

**Advisor cues (not a graph):** empty drop → Commons decree → cheap Build ignored → raise bounty → first claim line → Guild Charter toast already in `GameLoop` → “TO LUNA” travel log.

---

## Luna — Lunar Freeholds (tariff wars / sabotage)

**Theme:** Crater Freeholds levy ore and ice; someone is cutting hoppers loose on the HAB. Decrees read as tariffs and warrants, not Earth court manners.  
**Fauna:** Ash Hopper on HABs (`ClearThreat`, F2). Dust Tick on mines (`DefendArea`, F5). Eight dens.  
**Economy beat:** dock fee 4 MET on Earth packages — the tithe the Freeholds extract for using their pad.

### Types in play

| Type | Purpose here |
|------|----------------|
| `Explore` | Chart tariff rilles and smuggler approaches. Fog later. |
| `Extract` | Weigh Freehold ore / ice. This is the levy. |
| `DefendArea` | Ward ticks at the weigh-station (mines). |
| `ClearThreat` | Root hopper “saboteurs” on HABs; clear eight dens. |
| `Build` | Crater Commons, airlock forts, Mars Ship pad. |
| `EstablishOutpost` | Stake Campus B on the far rim (cyan disc). |
| `ResearchSite` | Science toward Mars Ship. |
| `Terraform` | Optional ice-polar dress; do not lead the act with it. |

### Example decrees

| Id | Title | `FlagType` | Intent (one line) |
|----|-------|------------|-------------------|
| `luna.explore.chart_the_tariff_rille` | Chart the Tariff Rille | `Explore` | Horizon / Chart marks the crater lanes the Freeholds tax. |
| `luna.extract.weigh_the_freehold_ore` | Weigh the Freehold Ore | `Extract` | Strip / Core haul the levy. Advisor dry: “They call it a tariff. We call it MET.” |
| `luna.defendarea.ward_the_weigh_station` | Ward the Weigh-Station | `DefendArea` | Ticks steal from mines. Rim watch, not a den hunt. |
| `luna.clearthreat.root_the_hopper_saboteurs` | Root the Hopper Saboteurs | `ClearThreat` | Ash hoppers on the HAB — sabotage flavor on the existing hopper raid. |
| `luna.build.raise_the_crater_commons` | Raise the Crater Commons | `Build` | Authority plaza on grey rock. Same Commons gate as Earth. |
| `luna.establishoutpost.stake_the_far_rim` | Stake the Far Rim | `EstablishOutpost` | Courier claims Campus B. Freight after. |
| `luna.researchsite.commission_the_mars_ship` | Commission the Mars Ship | `ResearchSite` | +12 toward `TechId.MarsShip` (100 sci + 80 MET + 30 ICE + 20 PWR). |
| `luna.build.fortify_the_airlocks` | Fortify the Airlocks | `Build` | Defense Battery / workshop labour. Junction turrets stay dressing (no fire). |

**Advisor cues:** arrival log already names hoppers + ticks. Lean into tithe / warrant / “who cut the hoppers loose.” Win travel log already: “Luna holds. Mars Ship staged. Next body: Mars.”

---

## Mars — Ares Guild Compact (Phase 4 campus)

**Theme:** The red campus is the Compact’s stronghold, not a second Earth court. Commons is the Compact seat; Guild Hall is the Compact charter made stone; wonders are prestige, not Palace.  
**Fauna:** Dust Wisp on Power (`ClearThreat`, F2). Dust Creeper on farms (`DefendArea`, F5). Ten dens.  
**Look note:** Phase 4 EXIT is still art-blocked (airlock panels, pad / Starship / solar density). Narrative writes against the **campus that will be**, not the empty Sol 1 still.

### Types in play

| Type | Purpose here |
|------|----------------|
| `Explore` | Survey the red apron and dust sea. |
| `Build` | Compact seat (Commons), solar field, pad / Belt Hauler staging. |
| `DefendArea` | Ward dust creepers off Compact farms. |
| `ClearThreat` | Hunt wisps; clear ten dens. |
| `EstablishOutpost` | Stake Campus B as a Compact forward lodge. |
| `Terraform` | Weave the crust — Compact creed (`TechId.TerraformCharter` unlocks the shop). |
| `ResearchSite` | Science toward Belt Hauler. |
| `Extract` | Ice-biased nodes; keep MET flowing for Compact bounties. |

### Example decrees

| Id | Title | `FlagType` | Intent (one line) |
|----|-------|------------|-------------------|
| `mars.explore.survey_the_red_apron` | Survey the Red Apron | `Explore` | Chart the packed-dust plaza the mockup sells. |
| `mars.build.raise_the_compact_seat` | Raise the Compact Seat | `Build` | Colony Commons as Ares civic — HUD still **COMMONS**. |
| `mars.build.string_the_solar_field` | String the Solar Field | `Build` | PWR-1 landmark labour. Wisps will come; tees `hunt_the_wisps`. |
| `mars.defendarea.ward_the_dust_furrows` | Ward the Dust Furrows | `DefendArea` | Creepers chew farms. Compact does not abandon the greenhouse. |
| `mars.clearthreat.hunt_the_wisps` | Hunt the Wisps | `ClearThreat` | Drain on Power. Pair with dens (ten). |
| `mars.establishoutpost.stake_campus_b` | Stake Campus B | `EstablishOutpost` | Cyan disc — Compact forward lodge, not a Freehold weigh-station. |
| `mars.terraform.weave_the_crust` | Weave the Crust | `Terraform` | Bloom’s slow +0.08 farm yield. Advisor: “They want a garden. Pay for a garden.” |
| `mars.researchsite.commission_the_belt_hauler` | Commission the Belt Hauler | `ResearchSite` | +12 toward `TechId.BeltHauler`. **Also place a Landing Pad** (launch gate). Pad labour is a `Build` on that order — no ninth type. |

**Advisor cues:** arrival log already names wisps + creepers. Compact voice is guild-proud, not Sol court. Wonders (Loom / Spire / Archive) are optional prestige asides after the three gates.

---

## Code mapping (do not invent a second schema)

| Name | Where | Role |
|------|-------|------|
| `FlagType` | `GameTypes.cs` | Mechanical kind (0–7). Frozen for W2. |
| `FlagData` | `Data/FlagData.cs` + `Flag_*.asset` | SO defaults (bounty, risk, work, color). |
| `FlagHandle` | `Core/SpecialistTypes.cs` | Runtime post (`FlagManager`). |
| `FlagDecreeIds` | `Systems/FlagDecreeIds.cs` | Shared string ids + title/intent for the tables above. |
| `FlagDecree` | same file | `{ Id, Body, Type, Title, Intent }` |
| `CelestialBodyId` | `World/CelestialBodyProfile.cs` | Earth=0, Luna=1, Mars=2, Belt=3, Europa=4 |
| `CampaignProgress` | `Systems/CampaignProgress.cs` | Unlock spine + travel log |
| `MissionController` | `Runtime/MissionController.cs` | Dens / sustain / launch gates |
| `SpecialistFlavor` | `Systems/SpecialistFlavor.cs` | Claim / card lines (class voice, not advisor) |

**Advisor vs specialist voice:** `SpecialistFlavor` is the ego hero. W2 advisor lines are new copy (toasts / briefing / travel). Do not replace callsigns.

**Lookup:** `FlagDecreeIds.TryGet("luna.clearthreat.root_the_hopper_saboteurs", out var d)` → `d.Type == FlagType.ClearThreat`, `d.Body == CelestialBodyId.Luna`.

---

## Out of scope (on purpose)

- New `FlagType` values, quest nodes, or auto-posted story flags.
- Fog-of-war implementation (write Explore as if charting matters).
- Belt / Europa decree lists.
- Art, `CaptureStill`, or Phase 5 ship.
- Majesty 2 / Paradox proper names (Palace, tax collectors guild, etc.).

When a beat needs a new title, add a `FlagDecreeIds` const and a row here. Do not grow the enum.
