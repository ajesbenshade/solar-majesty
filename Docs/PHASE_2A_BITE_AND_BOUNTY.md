# Phase 2A — Bite & Bounty

Make danger hurt and the overseer UI intentional. `SpecialistBrain` scoring is unchanged.

## Success criteria (Play Mode)

| Test | Expected |
|------|----------|
| Stalker aggro near specialist | HP bar on OverseerHud drops |
| HP → 0 | Specialist incapacitated (dark tint, no work) then recovers |
| F2 ClearThreat near stalker + Defense work | Stalker dies, threat falls, death beep |
| Place building (B, 1–4, LMB) | Orange progress bar at site; HUD Construction line |
| Build flag near site + Engineer work | Construction progresses faster via ApplyLabor |
| All three specialists down | “OUTPOST OVERWHELMED” + **Y** / button to revive |
| **F8** | Debug score panel toggles |
| Click ground | Still no unit repath (indirect control) |

## Controls (additions)

| Input | Action |
|-------|--------|
| **F8** | Toggle debug HUD |
| **Y** | Revive party when overwhelmed |
| **R** | Force fatigue Rest (unchanged) |

## Files

- `SpecialistAgent.ApplyDamage` / incapacitate / revive
- `DustStalkerAgent` bite DPS
- `OverseerHud`, `ConstructionSiteVisual`, `DemoAudio`
- `BuildingPlacer.ApplyLabor` wired from Build-flag work
