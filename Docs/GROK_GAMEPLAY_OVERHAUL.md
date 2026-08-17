# Solar Majesty — Gameplay overhaul

Committed design. Numbers are implementation targets, not vibes.
Does **not** rewrite `SpecialistBrain.ScoreFlag`. Does **not** add click-to-move.
Companion to `GROK_GAMEPLAY_BRIEF.md`. Art stays Phase 4; this is systems only.

---

## 1. Verdict

This is a working overseer toy with a complete campaign skeleton. It is not yet a strategy game. The brain, the greed gate, and the Lego campus are real. Almost every other loop is a timer that pays you for waiting.

The diagnosis in the brief is right about the symptom (nothing costs) and slightly wrong about the root. **The root cause is illegible incentives, not missing death.** A player who cannot see *why* a robot refused, *where* money went, or *whether* a building does anything will not experience stakes even after you add scrap. Death without refusal drama is just a random tax. Refusal drama without death is still a comedy.

Highest-leverage change: **make bidding the only crisis verb, and make every bid visible, costly, and able to fail.** That is three coupled rules, not one: (1) robots can be lost and must be re-bought through a workshop, (2) bounty MET tithes back so pay is an investment, (3) a robot that says no writes the price on the flag in the player's face.

Do that and the existing Engineer-needs-79 moment becomes the whole game instead of a debug curiosity.

---

## 2. The consequence layer

### What is lost

| Event | What is lost | What is kept |
|-------|----------------|--------------|
| First down (HP ≤ 0.02) | 12 s on the ground (was 8). Cannot work, hunt, or claim. | Robot, credits, suit, workshop. Self-revives at HP 0.28, fatigue 0.60. |
| Second down within 90 s of the last stand-up | **Scrap roll 40%.** Fail: same as first down. Success: robot is gone. | Workshop. Suit is destroyed. **40% of credits** return to colony MET (salvage). |
| HAB collapse (hoppers) | Occupants die (`KillResidents` already exists). Beds gone until rebuilt. | Campus graph otherwise intact. |
| Power siphon (leeches/wisps) | That node's gen = 0 while latched. Stockpile can hit 0. | Building stands (already shipped). |
| All living robots scrapped or down, and no workshop can re-fab | Body fail after **20 s** with no living robot. | Campus snapshot still saves. |

Permanent death of a *class* only happens if the workshop is also gone. That is the overseer fantasy: you did not lose a unit you controlled — you lost a capability you have to re-purchase.

### Recovery path (money, not clicking)

**Re-fabrication (automatic, no new player verb):**
- When a robot scraps, its workshop (if alive) enqueues a construction order: **70% of that workshop's original MET cost**, **40 s** build time, **no PWR extra**.
- Engineers may apply Build-flag labour to it (existing `ContributeBuildLabor`).
- Until the order completes, that class is missing. Build flags for other work still compete.
- If the workshop is destroyed, the log reads `ENG scrapped — rebuild the Engineer Workshop first.` No ghost fab.

**Field revive (Y) — no longer free:**
- Cost: **40 MET + 8 ICE**, deducted immediately. Cannot press Y if unaffordable.
- Cooldown: **120 s** after a successful revive.
- Effect: all currently downed (not scrapped) robots stand up at **HP 0.50**, fatigue 0.40. Scrapped robots are **not** restored.
- Rating: **−8** on the next `OverseerScore` total (clamp 0–100), applied once per body.
- Empty roster: `IsOutpostOverwhelmed` is true when `livingRobots == 0` **or** (all living are incapacitated). The 20 s fail timer starts. Y during that window is legal if you can pay.

**Power / ICE actually bind:**
- Power short (`PowerDraw > PowerGen` and PWR < 8): `EffectiveWorkRate × 0.70` on every robot (agent-side, not brain). Construction labour slows. Camp `ProductionScale` stays 0.45.
- ICE < 4 at an upkeep tick: births halt; **1 colonist dies** per 24 s tax tick while ICE < 4 and Population > 0. Log: `Life support failing — 1 colonist lost.` This is the human-side fail, not the robot-side.

### Game-over

**Per body (Campaign):**
- Fail if (a) 20 s with zero living robots and no in-progress re-fab, or (b) Population hits 0 after at least one HAB has existed this body (colony died).
- Modal: `OUTPOST LOST` · Retry body (reload scene, keep campaign unlocks, consume the save snapshot as-is) · Title.
- Endless: same fail, then `KEEP COLONIZING` is replaced by `TRY AGAIN` — no forced hop.

**Per campaign:**
- Losing a body does **not** lock the next world. It costs the letter grade (F, total 0) and the snapshot. The player can New Game or Retry.
- Spine complete still requires winning Europa.

### Seeing the crisis in time to bid

| Signal | When | What the player can bid |
|--------|------|-------------------------|
| Flag chip `ENG wants 79` | On post, if greed gate fails | Raise bounty (+) |
| Robot chip `DOWN` + 12 s pip | First down | Defend / Clear Threat on the attacker; or wait |
| Log `SCRAP RISK` orange | Robot stood up < 90 s ago and HP < 0.55 | Pay a Medic (Defend Area near them — Medic stronglyAttracts) or Y if they go down |
| Threat meter ≥ 50% | Ambient + dens | Clear Threat on dens; Defend Area on campus |
| PWR chip red + `work 70%` | Power short | Build Power / Clear leeches (F2) |
| ICE chip red + `life support` | ICE < 4 | Extract / Farm; not a robot order |
| `RE-FAB 0:28 ENG` on workshop | After scrap | Build flag on that workshop (labour) |

The player never clicks the downed robot except to inspect. Money and flags are the only answers.

### Second-order: Engineer scraps at minute 12

- Greed gate still 79 for the replacement the moment they spawn (`greedHunger` 0.55, credits 20).
- Re-fab costs `round(36 × 0.70) = 25 MET` and 40 s. If the player just dumped 80 on a Clear Threat, they may not have 25. Then they cannot rebuild the Engineer until a Mine tick or cancel a flag.
- No Engineer → no autonomous repair, Build flags only attract low-pref classes (Scout buildPref is not 1.0). Construction sites stall.
- Letter grade: roster term is `12 × clamp(count/4) × meanHP`. 3 robots at 0.7 HP → 6 points vs 12 at full 4. Y revive −8 on top if they panic.
- This is the intended punch: the greedy specialist is the one you can least afford to lose, and the one who is hardest to hire again.

---

## 3. Closing the economic loop

```
Colony MET  --escrow-->  Flag bounty
                |
                v
         Robot credits  --tithe 12%/30s-->  Colony MET
                |
                +--shop 45/90-->  veteran suit (kept until scrap)
                |
                +--scrap-->  40% credits back as MET salvage
```

### Tithe (hero tax)

On each economy upkeep (30 s), for every living non-downed robot with `credits > 25`:

```
tithe = floor(credits × 0.12)
credits -= tithe
colony MET += tithe
```

Cap tithe per robot at **18** MET per tick so a 400-credit veteran does not print the run. Log once per tick if total tithe > 0: `Payroll returned 14 MET.`

Broke robots (`credits ≤ 25`) pay 0. They are a liability: they still draw PWR and upkeep, they still refuse cheap flags until `greedHunger > 0.75`.

Rich robots are an asset: they self-armour at the inn (existing shop), they tithe, they salvage.

### Wage pressure (no ScoreFlag edit)

When colony MET < 20 after escrow:

- Set agent context `GreedHunger += 0.15` (clamp 1) for that think tick only — **desperate colony, desperate hires.** Cheap flags start clearing the 0.75 bypass more often.
- HUD: MET chip flashes `PAYROLL THIN`.

When colony MET ≥ 80 and mean robot credits ≥ 60:

- Do **not** change hunger. The tithe is the sink. Do not inflate wages in the brain.

### Shop stays at the inn

Do not add a shop building. Inn already exists as a rest beacon. Shop purchases remain the veteran path. Field Shell (45) and Hardplate (90) stay. A scrapped Hardplate is gone — re-buying is why you tithe.

### Credits on fabricate

New robots still start at **20 credits, hunger 0.55**. Re-fabs are not veterans. That is the cost of scrap.

---

## 4. Making refusal the star

### On the flag (primary)

When a flag is posted or re-priced, `RefreshFlagInterest` already walks `WouldTakeFlag`. Extend the marker (existing `FlagMarker` / HUD flag log) with **one** subtitle, 3.5 s refresh:

| Condition (first match) | Subtitle |
|-------------------------|----------|
| ≥1 robot WouldTakeFlag | `3 tempted · Scout · Engineer` (keep) |
| Nearest matching class fails greed gate | `ENG wants 79 — raise +` |
| Nearest matching class fails distance/consider | `too far for SCOUT` |
| Nearest matching class panicked or hard-rest | `DEF is hurt — wait` |
| Class pref < 0.25 for this flag type | `not a Scout job` |
| Else | `Ignored — raise bounty (+)` |

`wants N` uses the existing greed formula: `ceil((18 + baseGreed × 95) × 0.78)`. Engineer 79, Scout 38, Defense 31. Show that integer. This teaches the gate in one glance.

### Over the robot (secondary)

When a flag is posted within that robot's consider range and `WouldTakeFlag` is false, spawn a 2.4 s TextMesh (same path as `SpecialistStatusDisplay`, height +0.55):

| Reason | Chip |
|--------|------|
| Greed gate | `TOO CHEAP` |
| dist > consider | `TOO FAR` |
| panicked / restScore > 0.78 | `NOT NOW` |
| pref < 0.25 | `NO` |
| huntScore beating the flag (warriors only) | `HUNTING` |

Colour: orange `(0.96, 0.42, 0.08)`. One chip per robot per 4 s so posting 8 flags does not spam.

### First 3 minutes (no extra tutorial modal)

Keep beats 1–5. Change beat 6:

- After the first flag is posted, if it is ignored, the bottom tooltip becomes:
  `They named a price. Select the flag and press + until the chip reads tempted.`
- Advance tutorial 6/6 when `WouldTakeFlag` is true for any robot on any flag — **not** when T is opened.

Default first-flag suggestion in the tooltip stays F-keys, but the **Build $70** engineer refusal is the intended lesson. If the player posts Explore $40, Scout (need ~38) may take it — then the lesson is weaker. Bias the tutorial copy to: `G, Build, leave it at 70. Watch the Engineer.`

### Inspect card

Selected robot already shows `LastReason`. Add one line:

`Hire: 79 MET min` (from greed gate) and `Purse: 20` (credits).

---

## 5. Fixing the broken promises

| Promise | Verdict | Mechanic |
|---------|---------|----------|
| **Defense Battery** | **Keep. Make it fire.** | Auto-acquires one fauna in **18 m**, **4.0 dps**, 0.5 s retarget. Draw already paid in the 8 PWR build cost; add **+2 PWR** to `powerDraw` (total 4 if authored 2). Does **not** clear dens. Weaker than a Defense Mech (9.2 dps) so flags still matter. Placement is the decision (coverage), not a unit order. |
| **Airlock junction turrets** | **Keep as dress.** | If they fire, every campus is a free kill-box and Clear Threat dies. Do not activate them. |
| **Climate Loom / Aegis Spire / Deep Archive** | **Keep. Move the bonus to the building.** | ★ tech **unlocks placement** only. Bonus (`TechEffects`) applies **while the landmark is alive**. Destroyed wonder → bonus off until rebuilt. HUD line becomes `Landmark — bonuses while standing.` |
| **Explore** | **Keep. Give it a survey disc.** | On complete: 22 m disc for **90 s**. Extract yield ×**1.25** from nodes inside. Research Site complete inside +**8** science (on top of 12). Nearest uncleared den inside is **scouted**: Clear Threat workRequired ×**0.70** on that den (applied when the Clear flag is posted in 12 m). Minimap: scouted den pip turns cyan. |
| **Defend Area** | **Keep. Persist a watch.** | While claimed/worked: existing 9 dps + bodyDanger ×0.55 in 14 m. **On complete:** leave a 16 m watch for **50 s** that still deals **4 dps** to fauna (half) and keeps the bodyDanger reduction. Orange ring VFX (existing `ClaimRing`). Paying Defend is how you buy a quiet minute. |
| **Build** | **Keep. Fix the radius.** | Labour radius **6 m → 28 m**. If no construction order exists, completion still pays credits but the log is `No site in 28 m — paid for showing up.` Ghost footprint already shows sites; this makes the flag a real accelerator. |
| **Parties** | **Keep. Followers work.** | Leader claims (ClaimCount +1). Followers within **8 m** of the flag apply work at **0.55×** `EffectiveWorkRate` **without** adding ClaimCount. They still copy Flee/Rest. Party of 4 on an 8 s Build: `1.0 + 3×0.55 = 2.65×` work, crowd penalty stays 0.18. That is the party fantasy. |

No deletions in this list except "junction turrets as weapons." Subtractive cuts live in §10.

---

## 6. Depth without new systems

### Spatial payoff (Lego stays)

All radii are flat XZ. No new nav graph.

| Rule | Radius | Effect | Feeds brain via |
|------|--------|--------|-----------------|
| Workshop pull (exists) | 14 m | Flag near matching shop | `FlagWorkshopBonus` (unchanged ScoreFlag) |
| Survey disc (new) | 22 m / 90 s | Extract ×1.25, Research +8, den scouted | Flag complete side-effect only |
| Defend watch (new) | 16 m / 50 s | 4 dps, bodyDanger ×0.55 | `HasActiveDefendNear` (already ×0.55 in 14 m) — extend timer |
| Battery coverage | 18 m | 4.0 dps | None (world sim) |
| Haul (exists) | nearest drop-off | ExtractLogistics | Unchanged |
| **Dock tax** | module not airlock-adjacent | N/A — illegal already | — |
| **Commons shade** | 20 m from Commons | `bodyDanger × 0.85` for robots inside (campus feels safer) | `PushThreatToSpecialists` context |

Placement game: put workshops 14 m from the work, batteries 18 m over extractors, Commons in the middle for the shade, Defend flags on the rim that the battery does not cover.

### Work stacking (non-party)

`FlagManager.ApplyWork` accepts overlapping workers.

```
stackMult(n) = 1.0                    // first claimant (highest workRate if you sort; else first)
             + 0.55                   // 2nd
             + 0.35                   // 3rd
             + 0.20                   // 4th+
```

Each extra **claim** still increments `ClaimCount`, so `crowdPenalty = clamp01(n × 0.18)` in ScoreFlag **without editing ScoreFlag**. Two volunteers: −0.36 score. Three: −0.54. High bounty is how you buy a pile-on. Parties cheat the crowd term (followers do not claim) — that is the point of **P**.

Implementation: every agent on `PursueFlag` for that handle calls `ApplyWork`. Today only the "active" worker does. Change: all pursuers apply `work × stackShare` where `stackShare` is 1.0 / 0.55 / 0.35 / 0.20 by claim order. Claim order = sort by `EffectiveWorkRate` descending so the Engineer is the 1.0.

### Flag pricing minigame

No expiry (cancelling is already the out). No NPC negotiation.

**One new rule: posted bounty is the escrow; +/− after post top-up or refunds the delta.** If that already exists, keep it. If +/− only affects new posts, make live re-price adjust escrow (`TryEscrow` the difference or refund). The flag subtitle `ENG wants 79` makes +/− the core loop.

**Decay: none.** Decay punishes the player for thinking. Crowd penalty already punishes overstaffing.

**Competing flags:** already works (each robot picks max score). Do not add exclusive locks.

### Class identity (autonomous, no new art, no ScoreFlag)

Keep all **10** classes. Merging workshops is a content/UI disaster for one developer. Differentiate in `SpecialistAgent` side-effects and vocation:

| Class | One distinctive autonomous behaviour |
|-------|--------------------------------------|
| **Scout** | Vocation prefers unscouting dens. Explore complete creates the survey disc. |
| **Engineer** | Repair (exists). Build labour 28 m. |
| **Defense Mech** | Hunt (exists). **×1.35 dps vs Stalker/Hopper only** (`ApplyCombatDamage` multiplier). |
| **Medic** | Heal aura (exists). Downed ally in 3.6 m: `_recoverTimer` ticks **×2**. |
| **Harvester** | Extract complete: yield **×1.25** (stacks with survey 1.25 → 1.56 if both). |
| **Surveyor** | Research Site complete: **+8** science extra (stacks with survey disc). Vocation loiters at Lab. |
| **Terraformer** | Vocation within 10 m of a Farm: every 30 s `AddTerraformPulse` **+0.02** (cap still 0.6). |
| **Courier** | While FlatDist to Landing Pad < 8 m and not fleeing: `ResupplyIntervalSeconds × 0.85` (one courier max). Establish Outpost work ×1.20. |
| **Geologist** | Extract at a Mine/OPS drop-off: **+2 MET** extra on complete. |
| **Sentinel** | Hunt dps **×0.85** vs Stalker (not the hero killer). Defend complete watch **+20 s** (70 s total if Sentinel was the completer). |

Scout vs Surveyor vs Courier are still cousins. The side-effects are enough to feel at this scale. Do not add abilities the player triggers.

---

## 7. Threat that escalates

### Stop the backwards spawn

Per-kind caps no longer scale with `Farms` / `PowerPlants` / `Habs`.

```
campusFaunaCap = body.CampusFaunaCap × ReplayRules.FaunaCapScale
                 + (HasOutpost ? 2 : 0)
                 + unclearedLairs          // dens feed pests, not your economy
```

Per-kind max = `min(3, 1 + unclearedLairs / 3)` for mites/leeches/etc. Building more farms does **not** spawn more creepers. Uncleared dens do.

Steal amount stays 1 / 0.8 s. Economy growth is no longer the pest cause; neglect of dens is.

### Raiders do not abort on loiter

Delete the "specialist within 4 m → return false" abort in `TickRaidVillage`.

New abort: raider HP < 50% **or** took combat damage in the last **1.5 s**. A loitering Engineer does nothing. A hunting Defense Mech or a live Defend watch / battery does.

Hoppers still collapse HABs (`ApplyRaidDamage`). That is the human-death path.

### Cadence

| Clock | What |
|-------|------|
| Ecology tick (keep ~8–18 s) | Fill toward cap from dens |
| Pressure wave | **75 s** (was 90), **2** stalkers from a random uncleared den (keep) |
| **Frenzy** | When any **2 of 3** win gates are complete: remaining den stalkers `moveSpeed × 1.25`, bite ×1.20, pressure wave **50 s**. Log: `Dens frenzy — they know you're leaving.` |
| Dens quiet | Campus pests retreat (keep). Ambient ×0.55 (keep). |

### A bad night (player can only bid)

Minute 11, 4 dens left, frenzy not yet. Pressure wave dumps 2 stalkers on the HAB spine. Hoppers already in cap. Battery on the mine is the wrong coverage. Player posts **Clear Threat $95** on the den (escrow 95 MET) and **Defend Area $65** on the HAB. Defense Mech takes Clear (combat 1.0, courage 0.94). Sentinel takes Defend. Engineer refuses both (greed). Tithe last tick was 8 MET — payroll is thin. If the HAB pops, colonists die, sustain hold resets. If the Defense Mech downs twice in 90 s, 40% scrap, 25 MET re-fab, and the den is still up. That is the game.

---

## 8. The run arc

### Sustain gate change

Keep Commons + pop/housing goal + stockpile floors.

**Replace** `Farms > 0 && Mines > 0` with:

```
IsSustainable =
  HasCommons
  && MeetsPopulationGoal
  && StockpileHealthy
  && NetMetalsPerMin >= 1.5     // from last 60 s of mine ticks + tax − upkeep − net escrow
  && IceIncomePerMin >= 1.0     // farm ticks
```

One Farm + one Mine still *usually* hits this. Two Farms and extract flags can hit it without a Mine. Belt (thin farms) can hit it with mines + extract. The checklist becomes a rate.

**Hold time:** Earth 25 s stays. Others unchanged. Frenzy can overlap the hold — that is the climax.

### Minute 2 / 8 / 14 (Earth tutorial body, ~15 min)

| Time | Worry | Typical bid |
|------|--------|-------------|
| **2** | Engineer ignored Build $70. First den exists but is quiet. | + on Build to 85. Or post Explore $40 for the Scout. |
| **8** | Leech on the Power Node (siphon, PWR chip red, work 70%). HAB beds filling. 1–2 dens still up. | F2 on the leech. Second HAB. Maybe a Battery covering the farm. |
| **14** | Two gates done → frenzy. Sustain hold ticking. Pad up, rocket tech cooking. | Clear Threat on last dens at 90+. Defend watch on the HAB. Do not Y unless a scrap chain starts. |

Luna/Mars stretch the same shape: minute 8 is "economy under siphon/steal," minute 14 is "frenzy vs launch."

---

## 9. Legibility fixes

| Current | Replacement | Where |
|---------|-------------|--------|
| ICE (life support hidden) | Chip tooltip `ICE  life support + farms` | HUD chip hover / first ICE toast |
| Power Node vs Solar Array | One name: **Power Node** in catalog. Solar Array SO displayName → `Power Node` | BuildingData |
| OPS-1 / Mine | Catalog: **Ore Mine** (production) and **OPS Drop-off** (haul only, subtitle `does not grow MET`) | Build list line 2 |
| Guild / CMD-1 / Commons | HUD already says COMMONS. Guild catalog line: `Guild Hall — assign a class` | Keep Commons; never say Palace/CMD in HUD |
| Defense Battery | Subtitle `auto-fires 18 m` | Build list |
| Wonders | `unlock from ★ tech — bonus while standing` | Build list |
| SHOP chip (already DUTY) | Keep **DUTY** | Status display |
| Tutorial 6/6 on opening T | 6/6 when a robot is tempted | TickTutorial |
| `ignored — raise $` | `ENG wants 79 — raise +` | Flag log |
| Revive Party (Y) | `FIELD REVIVE  40 MET  8 ICE  (120s)` | Fail modal |
| BEDS 6/6 red | Keep; add `colonists die if ICE<4` on first ICE-fail toast | Once per body |
| Party (P) | Tooltip `followers work at 55%` | PTY dock |
| Explore | Subtitle `survey 22 m / 90 s` | Flag list |
| Defend Area | Subtitle `watch 50 s after` | Flag list |

---

## 10. Cut list

| Cut | Why |
|-----|-----|
| Free Y revive | Makes scrap and bidding optional. |
| `SpendUpTo` as the only failure for PWR/ICE | Keep SpendUpTo for MET upkeep (robots don't freeze), but ICE<4 kills colonists and PWR short slows work. |
| Auto-select next tech | Overseer picks the tech. Empty active slot is allowed; science banks. |
| Global `SM_ResearchUnlocks` PlayerPrefs | Unlocks are **per campaign** (reset on New Game). Continue/hop keeps them. Endless keeps them for the run. |
| Per-kind fauna caps scaled by Farms/Power/Habs | Backwards incentive. |
| 4 m loiter abort on raids | Trivialises hoppers. |
| Junction turrets as weapons | Would delete the flag layer. |
| Mission deadline (Inspector, off) | Leave the field, do not enable. Frenzy is the clock. |
| `RestThreshold` dead field | Delete or wire to 0.78. Prefer delete the field. |
| Duplicate pressure identity | Keep pressure waves; do not add a third spawn path. |
| Wonder bonus on tech complete | Cut that path; bonus on building. |
| Tutorial "open T" as win for 6/6 | Cut. |
| Full 10-class roster HUD | Not a cut of classes — but stop pretending the bottom roster must list 10. Keep SCT/ENG/DEF/MED + `+N`. |

**Not cut:** 10 classes, 8 flag types, 3 wonders, Defense Battery, parties, Endless, doctrines, letter grade, Lego airlocks, inn shop.

---

## 11. Implementation plan

Each package leaves a playable Game tab. Hours are one-developer, C# only, no art.

| # | Package | Touches | Layer | Hours | Depends |
|---|---------|---------|-------|-------|---------|
| **A** | Refusal on flag + robot chip + inspect `Hire: N`. Tutorial 6/6 = tempted. | `GameLoop.RefreshFlagInterest`, `FlagMarker`, `OverseerHud`, `SpecialistStatusDisplay`, `TickTutorial` | Runtime | 4 | — |
| **B** | Live bounty re-price escrow delta. Build labour 28 m. | `SimpleEconomy`, `FlagPlacementInput`, `SpecialistAgent.ContributeBuildLabor` | Systems + Runtime | 2 | — |
| **C** | Tithe 12%/30s, salvage 40% on scrap (scrap comes in D). Credits line in upkeep log. | `SimpleEconomy`, `SpecialistAgent`, `GameLoop` upkeep | Systems + Runtime | 3 | — |
| **D** | Down 12 s; scrap 40% on second down in 90 s; re-fab order 70%/40 s; Y costs 40 MET+8 ICE, 120 s, HP 0.50, −8 rating; empty roster fail 20 s. | `SpecialistAgent`, `VillageExpansion`/`GameLoop` fabricate, `MissionController`, `OverseerHud`, `OverseerScore` | Mixed | 8 | C |
| **E** | Parties: followers ApplyWork 0.55×, no extra claim. Stacking 1.0/0.55/0.35/0.20 for multi-claim. | `FlagManager`, `SpecialistAgent` Hunt/Pursue | Systems + Runtime | 4 | — |
| **F** | Explore survey disc 90 s; Defend watch 50 s; scouted den ×0.70 Clear work. | `GameLoop` complete handlers, `StalkerLair`, `ExtractLogistics`, `ResearchManager` | Runtime + Systems | 5 | A |
| **G** | Defense Battery 18 m / 4.0 dps / +2 PWR. Commons shade bodyDanger ×0.85 in 20 m. | New thin `DefenseBattery` MB or tick in `GameLoop`; `PushThreatToSpecialists` | Runtime | 4 | — |
| **H** | Wonders: `TechEffects` from placed alive landmarks only. | `ResearchManager`/`TechEffects`, `Settlement` counts, `GameLoop` | Systems + Runtime | 3 | — |
| **I** | Fauna cap from dens; remove 4 m abort; frenzy at 2/3 gates; pressure 75 s. | `DustStalkerAgent`, `GameLoop.TrySpawnCampusFauna`, `MissionController` | Runtime | 4 | D |
| **J** | Power short → workRate ×0.70. ICE<4 colonist death. Sustain = net rates not Farm∧Mine. | `SpecialistAgent`, `Settlement`, `SimpleEconomy`, `MissionController` | Mixed | 4 | I |
| **K** | Class side-effects table (§6). | `SpecialistAgent`, `GameLoop` completes | Runtime | 4 | F |
| **L** | Research: no auto-queue; per-campaign unlocks; HUD copy. | `ResearchManager`, `DemoSettings`/`CampaignProgress`, `OverseerHud` | Systems + Runtime | 3 | — |
| **M** | Legibility strings, Battery/Explore/Defend/PTY subtitles, revive modal. | `OverseerHud`, `DemoContentBuilder` displayNames | Runtime + Editor data | 3 | A, D, F, G |

**Suggested ship order:** A → B → E → C → D → F → G → I → J → H → K → L → M.

After **A+B** the game already teaches the 79 MET lesson. After **D** it can hurt. After **F+G+I** the map is a board. Stop whenever Phase 4 art needs the machine.

---

## 12. REQUIRES DESIGN SIGN-OFF

**None of the above rewrites `ScoreFlag`.** Crowd penalty, greed gate, workshop 14 m, hysteresis all stay.

| Item | Why not "just context" | Risk | Decision |
|------|------------------------|------|----------|
| Multi-worker `ApplyWork` | Context cannot make two agents advance one timer; that is `FlagManager`. | Faster flags, bounty feels cheaper. Mitigated by crowdPenalty 0.18 and stack taper. | **Approved in this doc** as FlagManager behaviour, not brain. |
| Party followers working without ClaimCount | Soft-cheats crowdPenalty. | Parties become strictly better. Intended. | **Approved.** |
| Scouted den `workRequired × 0.70` | Changes flag duration, not score. | Explore becomes a Clear Threat setup. Intended. | **Approved.** |
| `GreedHunger += 0.15` when MET < 20 | Uses the existing sanctioned context seam (`ReplayRules`). | Cheap flags get taken in a crash. Intended desperation. | **Approved.** |
| New player verb | None added. Y already exists; it now costs. | Players who mashed Y for free will feel punished. | **Approved.** |

If implementation of stacking requires robots to *prefer* crowded flags, that would need a ScoreFlag change — **do not do that**. Let bounty overcome crowdPenalty naturally.

---

## 13. Tuning appendix

| Key | Old | New | Rationale |
|-----|-----|-----|-----------|
| Incapacitate recover | 8 s | **12 s** | Readable DOWN window to bid Defend/Clear. |
| Scrap window | n/a | **90 s** after stand-up | Second down in window rolls scrap. |
| Scrap chance | 0% | **40%** | Hurts without making every bite a delete. |
| Re-fab cost | n/a | **70% workshop MET** | Engineer workshop 36 → 25 MET. |
| Re-fab time | n/a | **40 s** | One Build-flag cycle-ish. |
| Salvage | n/a | **40% of credits → MET** | Closes loop on death. |
| Y cost | 0 | **40 MET + 8 ICE** | Real bid. |
| Y cooldown | 0 | **120 s** | No panic mash. |
| Y HP | 1.0 | **0.50** | Not a full wipe undo. |
| Y rating | 0 | **−8** | S almost impossible if you panic. |
| Fail timer, 0 robots | never | **20 s** | Time to pay Y or lose the body. |
| `IsOutpostOverwhelmed` empty roster | false | **true if living==0** | Bugfix. |
| Tithe | 0 | **12% credits/30 s**, cap 18 | Payroll returns. |
| Tithe floor | n/a | credits **> 25** | Broke robots don't pay. |
| MET thin hunger | 0 | **+0.15** if MET < 20 | Desperation hires. |
| Power short work | 1.0 | **×0.70** | PWR binds. |
| ICE death | n/a | **1 pop / 24 s** while ICE < 4 | Humans bind. |
| ProductionScale on PWR short | 0.45 | **0.45** (keep) | Already correct. |
| Build labour radius | 6 m | **28 m** | Flag matches campus scale. |
| Stack 1/2/3/4+ | 1 / 0 / 0 / 0 | **1.0 / 0.55 / 0.35 / 0.20** | Crisis can be bought. |
| Party follower work | 0 | **0.55×**, no claim | P is not a trap. |
| Party follower range | n/a | **8 m** to flag | Must actually be there. |
| Explore survey | none | **22 m, 90 s** | Flag does something. |
| Survey extract | 1.0 | **×1.25** | Scout sets up Harvester. |
| Survey research extra | 0 | **+8** | Scout sets up Surveyor. |
| Scouted den Clear work | 1.0 | **×0.70** | Explore sets up Defense. |
| Defend watch | 0 s | **50 s**, 4 dps, 16 m | Bought quiet. |
| Sentinel watch extra | 0 | **+20 s** | Class identity. |
| Battery range / dps | 0 | **18 m / 4.0** | Building promise. |
| Battery extra PWR | 0–2 | **+2 draw** | Coverage costs grid. |
| Commons shade | none | **20 m, bodyDanger ×0.85** | Spatial payoff. |
| Hunt Defense vs Stalker/Hopper | 1.0 | **×1.35** | Ace hunter. |
| Hunt Sentinel vs Stalker | 1.0 | **×0.85** | Watchman, not ace. |
| Medic downed recover | 1.0 | **×2** in 3.6 m | Class identity. |
| Harvester extract | 1.0 | **×1.25** | Class identity. |
| Surveyor science extra | 0 | **+8** | Class identity. |
| Geologist extract extra | 0 | **+2 MET** at mine/OPS | Class identity. |
| Terraformer vocation pulse | 0 | **+0.02 / 30 s** near farm | Class identity. |
| Courier pad resupply | 1.0 | **×0.85** interval if < 8 m | Class identity. |
| Courier outpost work | 1.0 | **×1.20** | Class identity. |
| Fauna cap vs buildings | Farms/Power/Habs scale | **uncleared dens** | Fix backwards spawn. |
| Per-kind cap | up to 4 from buildings | **min(3, 1+lairs/3)** | Neglect dens, get pests. |
| Raid 4 m abort | yes | **no**; abort on damage 1.5 s or HP<50% | Loiter does nothing. |
| Pressure interval | 90 s | **75 s** | Slightly meaner. |
| Frenzy trigger | none | **2 of 3 gates** | Climax. |
| Frenzy speed / bite | 1 / 1 | **×1.25 / ×1.20** | Readable spike. |
| Frenzy pressure | 75 s | **50 s** | Climax. |
| Sustain Farm∧Mine | required | **replaced by net MET≥1.5/min, ICE in≥1.0/min** | Rate not checklist. |
| Sustain hold Earth | 25 s | **25 s** | Keep tutorial short. |
| Research auto-next | yes | **no**; science banks | Overseer picks. |
| Research persist | global PlayerPrefs | **per campaign** | New Game is New Game. |
| Wonder bonus | on ★ complete | **while landmark alive** | Building promise. |
| Refusal chip duration | n/a | **2.4 s**, retrigger 4 s | Readable, not spam. |
| Flag subtitle refresh | 0.45 s interest | **keep 0.45 s** | Already good. |
| Greed display | hidden | **ceil((18+g×95)×0.78)** | Teach 79. |
| Crowd penalty | 0.18 / claim | **unchanged** | Stacking's brake. |
| ScoreFlag weights | as shipped | **unchanged** | Constraint. |
| Engineer greed need | ~79 | **~79** | Signature stays. |
| Default Build bounty | 70 | **70** | Signature stays. |

Internal consistency checks:

- Engineer re-fab 25 MET < default Clear Threat 80 — you can afford a den *or* a new Engineer, not always both.
- Y at 40 MET is cheaper than a new Defense workshop (38) plus wait, but does not restore scraps and costs rating — panic button, not a plan.
- Battery 4 dps vs Mech 9.2 — three batteries ≈ one lazy warrior, with PWR tax.
- Survey ×1.25 and Harvester ×1.25 stack to 1.56 on a scouted node — worth an Explore setup.
- Tithe cap 18 / 30 s × 8 robots = 144 MET/min theoretical; real mid-run 2–4 payers × ~5 = ~10–20 MET/min, in the same band as one Mine (4 MET / 8 s = 30/min). Payroll is a second mine if you keep veterans alive.

---

*End of committed overhaul. Implement from §11 A→M and the appendix table. Do not rewrite `SpecialistBrain.ScoreFlag`.*
