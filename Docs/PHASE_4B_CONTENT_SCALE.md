# Phase 4B — Content Scale

Wire unused FlagTypes and Blender meshes into the playable demo. SpecialistBrain scoring constants unchanged (`GetPreference` already covers Extract / DefendArea).

## Success criteria (Play Mode)

| Test | Expected |
|------|----------|
| **F4** Extract | Green flag; Engineer prefers; completion grants Regolith/Metals/Ice |
| **F5** DefendArea | Purple flag; Defense prefers; claimed Defend lowers HUD Threat |
| **1–7** buildings | Pad · HAB · PWR · OPS · LAB · CMD · Solar Array |
| Campus | Denser showcase (extra HAB spur, connector, second solar) |
| Resupply | Toast when Earth package lands (~90s) |

## Files

- `FlagPlacementInput` F4/F5 · `BuildingPlacementInput` 1–7
- `DemoContentBuilder` / `DemoContentCatalog` / `GameLoop` content
- `BuildingCategory.Laboratory` · Extract yield · Defend pressure dampen
- `ColonyLayout` densify · OverseerHud toast

## Rebuild assets

**Solar Majesty → Build Demo Content Assets** (or batch `-executeMethod …DemoContentBuilder.Build`)
