# Grok brief — deepen Solar Majesty's gameplay

Copy everything below the line into Grok. It is self-contained: Grok does not have the repo,
so every number it needs to reason about is embedded. Companion to
`GROK_IMAGINE_UNIT_PROMPTS.md` (art) — this one is systems only.

---

# ROLE

You are a senior systems designer with shipped credits on colony-sim and indirect-control
strategy games (think Majesty 2, Rimworld, Dwarf Fortress, Against the Storm, Frostpunk).
You are auditing a real, playable Unity 6 game called **Solar Majesty** and producing a
prioritized design overhaul.

I am the sole developer. I do not need encouragement, I need a plan that survives contact
with a codebase. Be blunt. If a system I built is bad, say it is bad and say what replaces it.

# THE GAME

**Pitch:** Majesty 2 in space. The player is an **Overseer AI** running a colony on
Earth → Luna → Mars → Belt → Europa. The player has **no direct control over any unit.**
They post **bounty flags** on the map and autonomous robot specialists decide, on their own,
whether the pay is worth the risk. If nobody wants the job, nothing happens.

**Engine:** Unity 6000.5.x, URP, C#, namespace `SolarMajesty`. Isometric orthographic camera,
fixed 30°/45° angle. Grid is a flat 256×256 XZ grid at 1.5 m cells (384 m sandbox).
IMGUI HUD (`OverseerHud`, drawn in OnGUI).

**Scale of a run:** a single body takes roughly 10–20 minutes. Full campaign is 5 bodies.
Typical roster is 3–8 robots. Typical campus is 6–20 modules.

**Fantasy in one sentence:** you are a god of paperwork — you set incentives and watch
personalities collide with them, and your only lever on a crisis is money.

# HARD CONSTRAINTS — never violate these

1. **No click-to-move. No direct orders. Ever.** No move commands, no attack commands, no
   control groups that issue orders, no "select 5 units and send them." The player's entire
   verb set is: place buildings, post/cancel/re-price flags, research tech, move the camera,
   form parties, inspect things. Any proposal that hands the player a unit order is rejected
   on sight. This is the whole game.
2. **`SpecialistBrain.ScoreFlag` and the scoring cascade are frozen** unless you explicitly
   put the change in a section titled `REQUIRES DESIGN SIGN-OFF` and justify it. You may freely
   propose changes to what feeds *into* the brain (context values, bonuses, hunger, courage,
   flag risk, consider range) — that path is already used for difficulty modifiers and is the
   intended extension point.
3. **Humans live only inside HABs.** They are population, beds, tax and births — never outdoor
   agents. Everything walking around outside is a **robot** fabricated by a workshop.
4. **Buildings are Lego.** Colony Commons (6×6) is the first landmark. Square 2×2 **Airlock
   Junctions** snap only to the face midlines of a module, and every other module must dock onto
   an airlock end. Tubes and domes are cosmetic cladding, not a pathfinding graph. Do not
   propose free-form placement or rotation.
5. **No new art requirements.** Art is mid-pass in a separate phase and is the bottleneck.
   Assume you can reuse existing primitives, existing building kits, HUD text, and simple VFX
   (rings, sparks, scorch decals, floating text). Do not design anything that needs a new
   animation, rig, or mesh to read.
6. **Architecture:** pure C# with no MonoBehaviours under `Assets/Scripts/Systems/`.
   MonoBehaviours under `Assets/Scripts/Runtime/`. Numbers live in ScriptableObjects where
   practical. Prefer thin additions over rewrites.
7. **No multiplayer, no procedural-narrative LLM features, no roguelike meta-shop.** Single
   player, single session, one save slot.

# ARCHITECTURE FACTS (so your proposals are implementable)

- `SpecialistBrain` (pure C#) is called per robot every **0.4–0.6 s** and returns one
  `BrainDecision`: `Flee`, `Rest`, `PursueFlag`, `Hunt`, `Repair`, or `Wander`.
- `SpecialistAgent` (MonoBehaviour) executes that decision, ticks needs, and owns HP/fatigue/credits.
- `FlagManager` (pure C#) owns flag handles, bounty, risk, claim counts and work progress.
- `BuildingPlacer` (pure C#) owns the grid, footprints, dock legality and construction orders.
- `Settlement` (pure C#) owns population, beds, tax, births, camp production.
- `SimpleEconomy` (pure C#) owns the 4-resource stockpile, upkeep ticks, bounty escrow, resupply.
- `MissionController` owns win/lose gates. `ResearchManager` owns the tech DAG.
- `ThreatPressure` produces a 0–1 `bodyDanger` that is pushed into every robot's context.
- `GameLoop` (MonoBehaviour, ~3200 lines) wires all of it and owns fauna spawning.
- `ReplayRules` (static) holds difficulty/doctrine modifiers and **only nudges brain context**,
  never brain formulas. This is the sanctioned tuning seam.

# WHAT IS ALREADY BUILT — with real numbers

Treat this section as ground truth. These are the actual values in the shipping code.

## Resources

Four resources: **Regolith (REG)**, **Water Ice (ICE, doubles as life support)**,
**Metals (MET, the currency)**, **Power (PWR)**. Plus **beds** as a soft cap on population.

- **Upkeep every 30 s.** Adds `PowerGen`, then spends `PowerDraw + 1` PWR, then per-robot
  upkeep. All spending uses a `SpendUpTo` helper — **if you cannot afford it, you simply pay
  less and nothing breaks.**
- **Power shortage** (draw > gen and PWR stock < 8) sets `Settlement.ProductionScale = 0.45`.
  Robots keep working. Buildings keep running.
- **Camp production every 8 s:** Farm +3 ICE each, Mine +4 MET each, Regolith Camp +6 REG each,
  times yield scales times `ProductionScale`.
- **Tax every 24 s:** `Population × 2` MET, cut to 65% if overcrowded.
- **Births every 18 s** if population < housing and a HAB has a free bed. 3 beds per HAB.
- **Resupply every 90 s** (body-scaled) if a Landing Pad exists: +25 MET, +15 ICE, +10 PWR,
  minus a dock fee (Luna 4 MET, Mars 6).
- **Starting stockpile (Luna):** 90 REG, 55 ICE, 300 MET, 110 PWR.

## The brain (frozen formulas, given for context)

Decision cascade, in strict priority order:

1. **Flee** if `injury > 0.55`, or (`injury > 0.32` and `bodyDanger > 0.4` and `courage < 0.55`),
   and HP < 0.62. Score 0.95.
2. **Hard rest** if `restScore > 0.78`, where
   `restScore = clamp01((fatigue × 0.7 + injury × 0.55) × (1.1 − workaholicBias))`.
3. **Score every flag** within
   `consider = max(40 + explorePreference × 35, ConsiderRange × 0.7)` metres (~56–75 m).

   ```
   bountyFactor    = clamp01(bounty / 100)
   greedScore      = bountyFactor × (0.55 + baseGreed × 0.7) + greedHunger × 0.18 × bountyFactor
   preferenceScore = classPreference(flagType) × 0.9
                     + 0.22 if this class is in flag.stronglyAttracts
                     + workshopBonus × (1 − distToWorkshop/14)   [only if workshop within 14 m of flag]
   distPenalty     = clamp01(dist / 45) × 0.55
   riskPenalty     = (flag.Risk + bodyDanger × 0.4) × (1.15 − courage)
   crowdPenalty    = clamp01(claimCount × 0.18)
   fatiguePenalty  = fatigue × 0.25 × (dist / 30)
   score           = clamp01(greed + preference − dist − risk − crowd − fatigue)
   ```
   Plus **+0.15 hysteresis** if already working that flag.

4. **Acceptance threshold:** `clamp(0.38 + baseGreed × 0.25 − greedHunger × 0.22, 0.22, 0.72)`.
5. **Greed gate** (separate hard gate): `need = 18 + baseGreed × 95`; the robot refuses unless
   `bounty ≥ need × 0.78`, or `greedHunger > 0.75` (starving robots take anything).
   → Engineer (greed 0.88) needs **≈79 MET**. The default Build flag pays **70**, so the
   Engineer refuses it until you raise the bounty. This is the game's signature moment.
6. **Hunt** (free, no bounty) if not a Medic, `combatPreference ≥ 0.2`, HP > 0.38.
   Wins if `huntScore ≥ acceptance` and beats the best flag.
7. **Repair** (Engineers only, free, no bounty) at `acceptance × 0.82`.
8. Medic triage wander, then mild rest at `restScore > 0.45`, then **vocation wander**
   (loiter at workshop / patrol / tinker). A robot is never truly idle.

`ConsiderRange` default 80. `RestThreshold` field exists and is **dead code**.

## Flags — all 8 types and what they mechanically do

Posting escrows `round(bounty)` MET out of the stockpile. On completion the robot is paid that
amount in **personal credits** and the MET is gone forever. Cancelling with RMB refunds in full.
Bounty adjusts in steps of 15. **Only one robot's work advances a flag — progress does not stack.**

| Flag | Work | Default pay | Risk | What completion actually does |
|---|---|---|---|---|
| Explore | 4 s | 40 | 0.08 | **Nothing.** Pays credits. No reveal, no science, no map change. |
| Clear Threat | 6 s | 80 | 0.40 | Force-clears the nearest den within 12 m |
| Build | 8 s | 70 | 0.10 | Adds labour to a construction order **within 6 m** — useless otherwise |
| Extract | 7 s | 55 | 0.12 | Hauls a resource node's yield to the nearest matching drop-off |
| Defend Area | 9 s | 65 | 0.25 | **Nothing lasting.** While being worked, fauna within ~5 m take 9 dps and nearby robots get `bodyDanger × 0.55` |
| Research Site | 6 s | 50 | 0.10 | +12 science to the active tech |
| Establish Outpost | 10 s | 75 | 0.22 | Claims the forward outpost if within 18 m of Campus B |
| Terraform | 11 s | 70 | 0.14 | +0.08 farm yield, capped at +0.6 |

## Combat

- **Robot → fauna:** must close to **3.4 m**, then deals `workRate × 8` dps. Defense Mech
  (workRate 1.15) = 9.2 dps. Stalker has 28 HP, so ~3 seconds.
- **Fauna → robot:** bites within **3.2 m**. Stalker 0.18 dps against a 0–1 HP scale, so
  ~5.5 s from full health to down. Pests are much weaker: mite 0.06, leech 0.04, wisp 0.03,
  tick 0.05, creeper 0.05, hopper 0.07.
- **Mitigation:** shop armour only — 18% or 32%.
- **No ranges, no cooldowns, no projectiles, no counters, no focus fire.** Combat is contact dps.
- **`BuildingCategory.Defense` — "Defense Battery", 60 MET — has zero combat code.** It is a
  damageable box with a decorative turret model that never acquires or fires. Same for the
  turrets modelled on top of every airlock junction.
- **Fauna (7 kinds):** Stalkers spawn from dens and raid; Mites/Ticks/Creepers steal from
  extractors; Leeches/Wisps sit on Power Nodes and zero out their output; Hoppers raid HABs and
  can collapse them. Raiders **abort entirely if any robot is within 4 m**, so one loitering
  robot trivialises a raid.
- Pests steal **1 unit of a resource every 0.8 s** while latched.
- Counter-flag split: pests answer to **Defend Area**, stalkers/hoppers/leeches to **Clear Threat**.
  This is the only "counter" in the game.

## Fauna spawning

- Ambient threat = `bodyAmbient (~0.12) + 0.018 × min(14, campusPieceCount)`, × 0.55 once all
  dens are cleared.
- Campus pest cap is per-body (Luna 5, Mars 6) and **per-kind caps scale off your own buildings**
  — more farms means more creepers, more power nodes means more leeches.
- Clearing every den stops campus pests spawning entirely and makes existing ones retreat.
- Dens: 3 on Earth, 8 Luna, 10 Mars, 7 Belt, 9 Europa. Each holds 2–3 stalkers. A den also
  clears if a Clear Threat flag merely *starts* being worked within 8 m.
- Separately, `MissionController` throws 2 extra stalkers every 90 s while any den is uncleared.

## Progression and win/lose

**Win requires all three gates on the current body:**
1. All dens cleared and no stalkers alive.
2. Hold "sustainable" for N seconds: Commons placed, population ≥ goal, housing ≥ goal,
   ≥1 Farm, ≥1 Mine, and stockpile floors ICE ≥ 8, MET ≥ 12, REG ≥ 10.
3. Launch tech researched **and** a Landing Pad placed.

| Body | Pop goal | Sustain hold | Launch tech | Dens |
|---|---|---|---|---|
| Earth | 8 | 25 s | Lunar Rocket | 3 |
| Luna | 12 | 40 s | Mars Ship | 8 |
| Mars | 16 | 50 s | Belt Hauler | 10 |
| Belt | 10 | 35 s | Icebreaker | 7 |
| Europa | 14 | 45 s | *(none)* | 9 |

**Losing is nearly impossible:**
- A downed robot is incapacitated for **8 s**, self-heals, and stands back up. There is no death.
- If every robot is down, a modal offers **Revive Party (Y)** — full heal, **free**, no penalty,
  no rating hit, unlimited uses.
- The mission deadline exists in code but is **disabled by default**, so there is no time pressure.
- With zero robots alive the "overwhelmed" check returns false, so an empty roster cannot even
  trigger the fail modal.
- Running out of any resource just means partial payment.

**Rating:** letter grade S/A/B/C/D out of 100 — dens 25, sustain 25, launch 15, economy 15,
roster 12, pace 10.

## Research

27 techs in a real DAG. Science accrues passively at
`0.45 + labs × 0.85 + labWorkers × 0.25`, times a per-body multiplier. One active tech at a time.
Research Site flags add +12 each. Launch techs additionally cost stockpile on completion
(Lunar Rocket: 70 science + 40 MET + 15 ICE).

Problems: on completion the game **auto-selects the next tech** for you and deliberately defers
the 6 ★ Secret Projects until the ordinary tree is exhausted. Unlocked techs are stored in
**global PlayerPrefs and persist across runs and bodies**. The three wonder buildings
(Climate Loom, Aegis Spire, Deep Archive, 88–100 MET) grant their bonus **when the tech
completes** — the building itself is a decorative landmark that does nothing.

## Buildings (25 placeable)

Commons 70 MET (gates everything, produces nothing) · HAB 50 (3 beds) · Airlock 8 (topology only) ·
Power Node 35 (+6 PWR) · Farm 28 (+3 ICE/8 s) · Mine 32 (+4 MET/8 s) · Regolith Camp 22 (+6 REG/8 s) ·
OPS-1 45 (extract drop-off; **does not count as a Mine for the sustain gate**) · Laboratory 55
(science) · Landing Pad 40 (launch gate + resupply) · Defense Battery 60 (**does nothing**) ·
10 class workshops 34–42 (each fabricates exactly **one** robot of its class on completion) ·
Guild Hall 56 (assign a class; flags near it pull that class) · 3 wonders 88–100 (**do nothing**).

No upgrade paths — building upgrades were deliberately cut.

## Specialists (10 classes)

Personalities are applied in code at runtime and override the ScriptableObject values.

| Class | Greed | Courage | Combat | Signature pref | Speed | Work |
|---|---|---|---|---|---|---|
| Scout Drone | 0.32 | 0.52 | 0.10 | explore 1.0 | 4.4 | 1.00 |
| Engineer Bot | **0.88** | 0.22 | 0.05 | build 1.0 | 3.1 | **1.35** |
| Defense Mech | 0.22 | **0.94** | **1.00** | defend 0.92 | 3.0 | 1.15 |
| Medic | 0.24 | 0.42 | 0.06 | defend 0.90 | 3.6 | 1.10 |
| Harvester Bot | 0.42 | 0.38 | 0.08 | extract 1.0 | 3.4 | 1.28 |
| Surveyor Bot | 0.28 | 0.48 | 0.08 | explore 1.0 | 4.1 | 1.08 |
| Terraformer Bot | 0.34 | 0.40 | 0.06 | build 0.82 | 3.0 | 1.22 |
| Courier Bot | 0.30 | 0.44 | 0.08 | explore 0.92 | 4.3 | 1.05 |
| Geologist Bot | 0.36 | 0.40 | 0.08 | extract 0.92 | 3.3 | 1.18 |
| Sentinel Mech | 0.18 | 0.88 | 0.62 | defend 1.0 | 2.9 | 1.12 |

All ten share **one identical action set**. No abilities, no cooldowns, no unique verbs.
Harvester/Geologist, Scout/Surveyor/Courier and Defense/Sentinel are near-duplicates.

## The two economies

- **Colony MET** buys buildings and escrows bounties.
- **Robot credits** are a separate personal wallet, earned from bounties and spent only at the
  inn on 5 shop items: armour 45/90 MET-equivalent, or a 60–90 s consumable gene 28–35
  (+courage, +work, or +speed).
- Money paid to a robot **never returns to the colony in any form.** There is no hero tax, no
  marketplace, no reinvestment. Bounties are a pure sink.
- `greedHunger` (a 0–1 "how broke am I" value) drops 0.25 on every completed flag and creeps up
  while idle. It is the only thing connecting the two economies.

## Parties

**P** groups up to 4 robots; the leader is whoever has the highest courage. Followers mirror
Rest and Flee, but for Hunt and Pursue they merely **wander toward the leader** — they do not
claim the flag and **contribute no work to it.** Partying is currently a downgrade.

## HUD and feedback

Top bar with 5 resource chips and rates. Left panel: overseer log, planet chips. Right panel:
the 3 win gates plus a flag log. Bottom dock: Build / Flag / Tech / Camera / Party / Menu plus a
threat meter. A "Majesty Colony" minimap. Clicking a robot shows HP, fatigue, current action,
its internal reason string, and credits.

**Refusal feedback — the single most important signal in an overseer game — is one line of text
in a log panel:** either `"Ignored — raise bounty (+) or pick a type they want"` or
`"3 tempted: Scout · Engineer"`. There is no bubble over the robot, no reaction, no per-robot
reason like "too poor" / "too scared" / "too far", and nothing on the flag itself. A player who
does not read the log panel will never learn the game's central mechanic.

Six-beat tutorial: Commons → airlock → HAB → workshop → flag → open the tech panel.

## Meta

Campaign or Endless. Challenge modifiers (Austere = 55% starting stockpile, Swarm = 1.5× fauna
cap, Tight Purse = slower/pricier resupply). Three doctrines that nudge brain context
(Open Hands = +0.26 hunger so robots accept cheap work, Aegis Watch = 1.22× courage,
Survey First = 1.5× consider range). One save slot; autosaves every 20 s; the save stores
stockpile, population and the Lego campus per body, but **not flags, fauna, or robot state.**

# MY DIAGNOSIS — argue with it

I think the control model is genuinely good and correctly enforced, and the progression skeleton
is complete. I think the game currently fails for one root reason, plus a set of broken promises.

**Root cause: nothing can ever be lost, so no decision costs anything.** Robots don't die, revival
is free and unlimited, resource shortfalls just pay out less, the deadline is off, and losing your
whole roster can't even trigger a fail state. If a bounty can't lead to a loss, then pricing a
bounty is not a decision — it's a formality. Every other depth problem is downstream of this.

Ranked, the problems I see:

1. **No consequence.** Above.
2. **The two economies never trade off.** MET spent on bounties evaporates. There's no reason to
   care whether a robot is rich or broke, except `greedHunger`, which the player can't see.
3. **Refusal is invisible.** The best mechanic in the game is buried in a text log.
4. **Broken promises.** Defense Battery doesn't shoot. Wonders do nothing. Explore does nothing.
   Defend Area secures nothing. Build flags do nothing without a construction order nearby.
5. **Parties are a trap** — followers don't work.
6. **No work stacking**, so a crisis can't be answered by throwing money at it. One flag, one
   worker, fixed duration.
7. **Research is a queue** that picks for you, with cross-run global unlocks.
8. **Ten classes, one behaviour set.** Class identity is a stat spread and a flavour string.
9. **Fauna are chip damage, not threat.** They steal 1 unit per 0.8 s and abort raids if a robot
   stands nearby. Building more economy is what spawns more pests, which is backwards.
10. **The sustain gate is a checklist,** so every run has the same optimal build order.
11. **Space is nearly meaningless.** Lego docking is a topology puzzle with no mechanical payoff.
    The one exception is genuinely good and under-exploited: a flag within 14 m of a matching
    workshop gets a real pull bonus.
12. **Failure of legibility.** Naming collisions (Commons vs Guild vs CMD-1, Mine vs OPS-1), ICE
    silently doubling as life support, wonder-tech vs wonder-building, tutorial 6/6 firing when
    you merely open a panel.

I may be wrong about priorities. If you think something else is the root cause, lead with that
and defend it.

# WHAT I WANT FROM YOU

Produce a design overhaul document with the following sections, in this order.

### 1. Verdict (≤200 words)
What kind of game this currently is versus what it is trying to be, and the single highest-leverage
change. Name the root cause. Disagree with me if warranted.

### 2. The consequence layer
Design the failure spine. I need real stakes that do not turn the game into a punishing
micro-manager's nightmare, given the player cannot directly intervene. Specify:
- What is permanently lost, and what the recovery path costs.
- What the actual game-over is, per body and per campaign.
- How the player sees a crisis coming with enough time to *bid* their way out of it, since bidding
  is their only verb.
- Exact numbers: HP thresholds, death chances, revival costs, timers, grace periods.
Treat "the player's only crisis response is money" as a design opportunity, not a limitation.

### 3. Closing the economic loop
Make bounty money circulate. I want the player to feel that a rich robot is an asset and a broke
robot is a liability. Consider hero taxation, robot-funded upgrades, wage inflation, a real
marketplace/shop building, robots refusing to work when the colony is visibly insolvent, or
robots that buy their own gear and thereby become veterans worth protecting. Give me the loop as a
diagram in text, plus exact rates.

### 4. Making refusal the star
Redesign the feedback around a robot saying no, using only text, colour, simple VFX and the
existing IMGUI HUD. I want the player to *feel* haggled with. Specify the exact strings, where they
appear, how long they persist, and how a player learns the greed gate within their first 3 minutes
without a tutorial popup telling them.

### 5. Fixing the broken promises
For each of: Defense Battery, the 3 wonders, Explore flags, Defend Area flags, Build flags, and
parties — either give it a real mechanic or **delete it.** I want subtractive design to be on the
table and I want you to actually recommend deletions where deletion is right. Note that an
auto-firing Defense Battery is *not* a violation of the no-direct-control rule (the player is not
issuing the order), but argue whether it is right for this game at all.

### 6. Depth without new systems
Find depth in what already exists. Specifically address:
- **Spatial payoff.** The workshop-within-14 m pull bonus is the one good spatial rule. Build a
  real placement game out of that family of ideas — adjacency, coverage radii, haul distances,
  danger gradients — without adding a new pathing graph and without breaking the Lego grid.
- **Work stacking.** Should multiple robots be able to advance one flag? What does that do to the
  crowd penalty and to bidding? Give the formula.
- **Flag pricing as the core minigame.** Dynamic pay, expiry, escalating bids, competing flags,
  robots negotiating, bounty decay. Make the price the interesting decision.
- **Class identity.** Give each of the 10 classes one distinctive, *autonomous* behaviour that
  needs no player order and no new art. Cut or merge classes that can't earn one.

### 7. Threat that escalates
Redesign fauna and dens so pressure builds toward a climax instead of chip-damaging forever. Fix
the backwards incentive where growing your economy is what spawns your pests. Fix raiders aborting
when any robot is within 4 m. Specify spawn curves, wave timing, and what a "bad night" looks like
when the player can only respond with money.

### 8. The run arc
Reshape a single body from "checklist of 3 gates" into a 15-minute story with an opening, a
mid-game pivot, and a climax. Say exactly what changes about the sustain gate. Say what the player
should be worrying about at minute 2, minute 8, and minute 14.

### 9. Legibility fixes
A concrete rename/clarify table for every confusing name and hidden mechanic listed above, plus any
you spot. Include the exact replacement strings.

### 10. Cut list
What to delete outright to make the game better. Be aggressive. Include anything from my own
diagnosis you think is not worth fixing.

### 11. Implementation plan
Ordered work packages. For each: the change, which of the systems named in ARCHITECTURE FACTS it
touches, whether it is pure C# or MonoBehaviour work, rough size (hours), and its dependencies.
Sequence it so the game is playable and better after *every* package, not only at the end.

### 12. `REQUIRES DESIGN SIGN-OFF`
Anything that touches `SpecialistBrain` scoring, adds a player verb, or contradicts a hard
constraint. State the change, why the softer alternative is insufficient, and the risk.

### 13. Tuning appendix
A single table of every number you changed or added, old value → new value, with a one-line
rationale each. I will implement from this table, so it must be complete and internally consistent.

# HOW I WILL JUDGE YOUR ANSWER

- **Numbers or it didn't happen.** "Add more tension" is worthless. "Downed robots roll a 35%
  scrap chance; a scrapped robot's workshop must re-fabricate over 45 s at 60% of original cost"
  is useful. Every proposal needs values I can type into C#.
- **Respect the constraints.** One click-to-move suggestion and I stop reading.
- **Prefer changing existing numbers and adding small systems** over new subsystems. The best
  answer makes the game twice as deep by touching a dozen constants and adding three small rules.
- **Show your work on second-order effects.** If you make robots die, tell me what that does to
  the Engineer's 79 MET greed gate, to the 8 s recovery window, to the letter grade, and to a
  player who lost their only Engineer at minute 12.
- **Kill your darlings and mine.** A cut list with nothing on it is a failed answer.
- **No hedging.** Pick one design. If you present alternatives, rank them and commit to a
  recommendation.

Do not summarise this brief back to me. Start at section 1.
