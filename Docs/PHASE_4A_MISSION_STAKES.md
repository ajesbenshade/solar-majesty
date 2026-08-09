# Phase 4A — Multi-Stake Mission

Win requires three parallel stakes: clear stalker waves, hold the outpost on a timer, and finish a player-placed construction. `SpecialistBrain` unchanged.

## Success criteria (Play Mode)

| Test | Expected |
|------|----------|
| HUD top-right | Checklist: Combat / Hold / Build |
| Clear waves only | Combat checks off; win does **not** fire yet |
| Survive ~60s without overwhelm | Hold checks off (timer runs while Active) |
| Place building + complete (Build flag / wait) | Build 1/1 checks off |
| All three complete | OUTPOST SECURED |
| Overwhelm mid-mission | Lose; hold pauses until **Y** revive |

## Implementation

- `MissionController` — `_combatCleared` + `_holdElapsed` + completed construction count
- `OverseerHud` — mission stakes panel
- Optional `metalsGoal` Inspector fallback (off by default; build stake is primary)

## Deferred

- Blender hero unit meshes
- Multi-body / campaign missions
- Hard deadline fail (timer as countdown lose)
