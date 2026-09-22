# Economy — Majesty 2 gold loop

CRED is Majesty gold. ICE (life support) and PWR (grid) stay constraints, never shop currency.
All numbers live in `Assets/Scripts/Systems/MajestyEconomy.cs`.

## The loop

```
treasury ──► buildings · flag rewards · hire fees · yard bills · research
   ▲                         │
   │                    heroes earn (bounties, loot, extract)
   │                         │  50% guild tax ──► workshop / guild till
   │                         ▼
   │            heroes spend ──► Market · Blacksmith · Guild · Inn · Aid · Workshop tills
   │            daily tax    ──► Commons 50 · Market 250 · Farm 50 · houses 20+10/resident (≤50)
   │            trade        ──► ship caravans 300–1,000 + ICE/REG sales ──► Market till
   │            mines        ──► Mine till
   │                         │
   └──── tax collectors (2 per Commons, 1 per Watchtower) walk tills home
```

Gold dropped in a Commons or Watchtower till is already home and goes straight to the treasury.

## Majesty 2 rules applied

| Majesty 2 | Solar Majesty |
|---|---|
| Half of every hero earning is taxed to the guild | `SpecialistAgent.EarnCredits` → workshop/guild till (Commons if none) |
| Hero purchases land in the shop's coffer | `SpendAt(vendor)` for Market, Blacksmith, Guild kits, Inn, Aid, Workshop repair |
| Palace houses tax collectors (2 at L1) | Commons spawns 2 levy drones; each Watchtower (Guardhouse) adds 1, max 8 |
| Collector settings: min to collect / min to return | `CollectMinimum` 40, `ReturnAt` 300 |
| Collectors can be robbed | A pest within 2.6 m snatches half the bag |
| Palace 50/day, Marketplace 250/day, houses 20–50/day | `DailyTax`, `HousesDailyTax`; one day = 60 s |
| Trading-post caravans 300–1,000 by distance | Pad landing pays `CaravanGold(pad→market m)` into the Market till |
| Extra building of a type = 150% of the last, round up to 10 | `BuildingPlacer.CostFor` (housing, power, farms, mines, camps, junctions stay flat) |
| Hire cost per hero | Workshop charges `HireCost` when it fabricates (waits if the treasury is short) |
| Resurrection = hire + ½ hire per level gained | `OverseerRules.YardBill(level, class)` |
| No hero wages | MET upkeep removed; only PWR upkeep remains |
| Houses are free | Village HAB growth costs regolith only |

## Prices (first building)

Commons 500 · HAB 150 · Power 150 · Pad (Trading Post) 200 · Market 500 · Blacksmith 500 ·
Watchtower (Guardhouse) 150 · Inn 250 · Fobot Yard 400 · Aid Station 300 · Defense Battery 300 ·
Lab 1,000 · workshops 250–1,000 (Warriors-tier 500, Rangers-tier 350, Rogues-tier 250, Dwarf-tier 1,000) ·
wonders 3,000.

Hire: Scout 150 · Engineer 200 · Defense 500 · Medic 400 · Harvester 100 · Surveyor 300 ·
Terraformer 350 · Courier 100 · Geologist 250 · Sentinel 600.

Flags: default 400–800, range 50–5,000, ±50 per key press. Loot: pests 20, Stalkers 75, junk-bots 10.
Shop: potions 20/40, regen necklace 300, guild kits 150–400, blacksmith gear 300–480.
Start: 2,600–3,800 CRED per body.

## Scale note

Gold moved to the Majesty scale (×10). The flag brain was tuned on the old scale, so it reads
bounties through `MajestyEconomy.ToBrain` (÷10) — hero judgement is unchanged. Saves below v6 are
migrated (stockpile, tills, flag rewards/escrow, hero purses, roster) on load.

Sources: Prima *Majesty 2* guide; Paradox *Engine of Commerce* mission eGuide.
