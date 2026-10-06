# Majesty 2 hero AI — what the data files say

Design reference only: numbers and structure observed in the Majesty 2 data files (extracted copy on `E:\Majesty2_Extracted\merged`). Nothing is copied into the game; every value in our tuning is our own.

Interpretations below are marked **(inferred)** where the data gives names and numbers but the engine code is binary.

## Where it lives
| File | What it holds |
|---|---|
| `set/ai_hero_allure_factors.set` | global flag-allure weights |
| `properties/units/behaviour/hero_default.inc` | motive matrix, safety modifiers, purchase priorities |
| `entity/units/heroes/<hero>/extenders/ai.inc` | per-class allure multipliers, shopping, motive task budgets, health reflex |
| `gameData/units/rpg_params.xml` | hero stats, wander (search adventure) ranges |
| `gameData/spawn/global_spawn_settings.set` | enemy spawn tables by kingdom value |
| `set/caravans.set` | trade-caravan payout by distance |

## Motives
Three motives, each 0-100: **work** (starts 31), **safety** (100), **relaxation** (100).
- Influence matrix: working lowers relaxation fast (-1), relaxing lowers work (-0.5) and raises itself (+0.5).
- Safety modifiers: *Health* (fall coefficient 0.8) and *SafetyField* (fall 0.5, rise 2.0). **(inferred)** safety drops slowly in danger and recovers four times faster when safe.
- Task budget per class (`max_tasks`): warrior and rogue and cleric 12 work / 3 relaxation, mage 8 / 4. **(inferred)** a hero does up to N work tasks, then M relaxation tasks.
- `HealthReflex health_percent`: warrior 20, mage 20, rogue 50, cleric 5. **(inferred)** below this health the hero retreats.
- `sleep_in_guild time 7`.

## Flag allure
Global: distance 0.1, loot 1, danger 30 / 20, attack flag 2, protect flag 1.5, explore flag 1, theft 1.
Per class multipliers (examples): warrior dist 6, attack 1.9, protect 1.5, explore 0.9; mage dist 8, attack 2.2, protect 0.8, explore 0.6; rogue dist 3, danger 0.6, attack 1.3, explore 1.4; cleric dist 5, attack 1.32, protect 1.5, explore 0.6.

Takeaway: fear (danger) dwarfs everything and distance barely matters; attack flags beat protect beat explore.

## Shopping priority (warrior / mage / rogue / cleric)
Armour: max / high / high / high. Weapon: high / max / max / high. Health potion: high / high / max / medium. Mana potion: min / max / min / max.

## Spawns
Entries carry `city_value` bands (e.g. skeletons at 1-5, zombies at 8+), spawn time, count and cap. Graveyards appear only once a hero has died. Enemy pressure scales with kingdom value, not just time.

## Kingdom value ("city_summ")
Not a building count: each building carries `building_value` and the spawn table compares the sum.
Palace 1 (+1 per upgrade), guilds 0.5 (+0.35 per upgrade), market 0.5 (+0.5), tavern / temples 0.5, smithy 0.2 (+0.2 per upgrade), guard tower / trading post / statue / hall of lords 0.2, houses and mills 0.
Spawn bands run on this scale: town sewers at 1, 3, 5, 7, 10, 12; graveyard undead tiers 1-5, 5-10, 8+, 15+.

## Safety fields
Buildings emit `safety_emitter {power, radius}`: palace 10 / 30 m, most guilds 5 / 20 m (warrior-type 10), market 5 / 10 m, houses 3 / 10 m, mills 2 / 10 m, magic tower 7 / 20 m, lairs 1 / 10 m. This is the "SafetyField" the hero safety motive rises toward.

## Other mechanics seen
- Lairs: `guard_point` zone radius 20, guard radius 25. Spawn contents are set per map, not in the building.
- Heroes carry tax in a `TaxCashCard` and only walk it to their guild once it reaches `SatietyBarrier` 250 (the "pay duty" bark).
- Hero animation moods: default, laziness, panic, fight. These match the motive states.
- Hero levels: max level 100, but stat growth is computed in engine code ("designer formulas") and is not in the data.
- Market `TreasuresGenerator` 250 per day; upgrades +125 / +225 per day for 1,500 / 3,000.
- Boss raiders (dragon, ogre, vampire, elementals, wolfman, bearman) each have their own spawn entry with 180-360 s timers and a cap of 1.

## Caravans
Payout rises linearly with distance: 200 at 25, 2100 at 500 (about +100 per 25).

## How the game uses it (this change)
- `HeroMotives` (pure C#): safety with fast-rise / slow-fall easing and a "shaken" latch; work-task budget then relaxation break; break ends on task count or timeout.
- `SpecialistBrain`: breaks raise the acceptance bar by `relaxFlagPremium` so only a standout bounty tempts a resting hero; shaken hurt heroes stay home; per-class health reflex; flag-kind weights.
- All tunable on `SpecialistBrainTuning` (Motives section). Suggested Majesty-flavoured settings: `flagAttackWeight 1.0 / flagDefendWeight 0.75 / flagExploreWeight 0.5`, and class overrides for the mage-like and cleric-like classes.
- Defaults keep legacy tests green; set `motivesEnabled` false to compare.

## Kingdom spawn table (implemented)
`KingdomThreatTuning` on GameLoop: per-category `BuildingWorth` (Majesty values above), and a spawn table of rows with a kind, a site (in-town burrow, colony edge, scrapyard), a trigger (always / after a hero death), a value band, an interval, a count and a cap.
Defaults: mite burrow at value 2, hopper burrow at 4, leech burrow at 6, alpha stalker raider at 3 (300 s) and a second at 6 (240 s), wreck junk after a death at 4. Alpha raiders have x4 health, x1.8 bite, x1.6 size and x4 kill reward. Burrow pests keep coming after the outer dens are cleared, like Majesty sewers.
Hero safety now also rises near buildings (safety field, `safetyFieldWeight`).

## Tax carry (implemented)
Heroes keep their half-earning guild tax in a `DutyPurse`. At `taxCarryBarrier` (250) the brain sends them to their guild ("pay_duty", score 0.6) once their current flag or hunt is done; panic still comes first. Passing the guild with any tax in hand also pays it in. A downed hero drops half the carried tax, a scrapped one loses all of it. The overhead label shows `TAX n` on the walk. Saved with the agent. `taxCarryEnabled` off restores instant tax.

## Per-class flag appeal and shopping (implemented)
Class mapping (Majesty archetype -> ours): ranger -> Scout, warrior -> Defense Mech, cleric -> Medic, rogue -> Harvester, dwarf -> Sentinel, elf -> Surveyor, marksman -> Geologist, beastmaster -> Courier. Engineer and Terraformer stay neutral.

Flag appeal (`ClassAllure`, brain tuning): Majesty's raw cl_dist / cl_danger / cl_flag_* are softened into multipliers around 1 by `ClassAllure.FromMajesty`: distance penalty `0.5 + 0.5 * cl_dist / 5`, danger penalty `0.6 + 0.8 * cl_danger`, flag kinds tilted by half against the class's own mean so they adjust our per-class preferences rather than replace them. All clamped to 0.5-1.5.

Shopping (`ClassShopPriority`, economy tuning): Majesty TypeNecessity per class for armor, weapon, health potion, magic potion (Majesty mana potion) and accessory (artefact). Min never buys, Lower only with money to spare, Medium is the original behaviour, High and Max buy first and drink health potions earlier (health below 0.80 / 0.88 instead of 0.72). At the blacksmith the higher need wins between armor and weapon; equal need keeps the original rule.

Raw Majesty values per hero (dist, danger, attack, protect, explore | armour, weapon, artefact, health, mana | work/relax, reflex %):
- warrior 6, 0.1, 1.9, 1.5, 0.9 | max high high high min | 12/3, 20
- ranger (hunter) 2, 0.3, 0.6, 0.3, 1.4 | high high max max min | 12/3, 35
- rogue 3, 0.6, 1.3, 0.8, 1.4 | high max medium max min | 12/3, 50
- cleric 5, 0.6, 1.32, 1.5, 0.6 | high high medium medium max | 12/3, 5
- mage 8, 0.1, 2.2, 0.8, 0.6 | high max high high max | 8/4, 20
- dwarf 8, 1, 2.2, 0.8, 1 | max max high max min | 12/3, 10
- elf 2, 0.8, 1.7, 0.7, 1 | high high medium max high | 15/5, 30
- marksman 2, 1, 0.8, 1, 1.2 | high max medium max min | 12/3, 20
- beastmaster 3, 1, 1.3, 1, 1.3 | high high max max min | 12/3, 20
- paladin 4, 0.05, 1.2, 2, 0 | max high high max min | 15/3, 5
- priest 5, 1, 2, 3, 0.4 | max high medium medium max | 15/3, 10

## Caravan curve (implemented)
`EconomyTuning.caravanCurve` holds the Majesty table (x = Majesty distance, y = gold, linear between points, flat past the ends) and `caravanDistanceScale` 2.5 maps our metres onto it. Mines (the active trade posts) and landing-pad caravans both use it. Versus the old curve: 200 instead of 300 for routes under 10 m, the same 600 at 50 m, 1,100 at 100 m instead of the old 1,000 cap, topping out at 2,100 from 200 m.

## Per-class motives (implemented)
`ClassMotiveOverride.Defaults()` (same class mapping as flag appeal) sets each class's work / relax task budget and retreat health. Budgets are Majesty's own (12/3 for most, elf-Surveyor 15/5). Retreat health is not copied raw: our robots go down at 2% and bites reach 18%/s, so Majesty's 5-50% would get them killed. Each class keeps its place relative to the Majesty median (20%) around our 45% line: `0.45 + (pct - 20) x 0.006`, clamped to 30-60%. Rogue-Harvester 60%, ranger-Scout 54%, elf-Surveyor 51%, warrior-Defense Mech / marksman-Geologist / beastmaster-Courier 45%, dwarf-Sentinel 39%, cleric-Medic 36%. Engineer and Terraformer use the globals.

## Not yet used
Nothing from the data list above; next candidates are the unread `perks.xml`, `unit_actions.xml` and `spells.xml`. `perks.xml`, `unit_actions.xml` and `spells.xml` are unread in detail.
