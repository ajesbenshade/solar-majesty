# Phase 2B — NavMesh & Juice

Specialists path around campus obstacles; bites/deaths/claims get visible + audible feedback. `SpecialistBrain` scoring is unchanged.

## Success criteria (Play Mode)

| Test | Expected |
|------|----------|
| Party walks across campus | Paths curve around showcase buildings (not through meshes) |
| Console on boot | `[CampusNavMesh] Runtime NavMesh built…` |
| Stalker bite | Hit flash sphere + low bite tone |
| Specialist incapacitated | Death-burst spheres + dark tint |
| ClearThreat kills stalker | Death burst + descending chord |
| Claim flag | Yellow claim ring + bright chord |
| All specialists down | Red screen veil + fail chord + revive UI |
| Click empty ground | Still no unit repath (indirect control) |

## Implementation notes

- Package: `com.unity.ai.navigation` **2.0.14** (required for Unity 6000.5 / EntityId)
- `CampusNavMesh` bakes a dedicated invisible ground collider after showcase spawn
- Showcase + player-placed buildings get `NavMeshObstacle` (carve)
- `SpecialistAgent` uses `NavMeshAgent` with straight-line fallback if off-mesh
- Juice: `DemoVfx` (hit / death / claim) + richer `DemoAudio` chords

## Files

- `CampusNavMesh.cs`, `DemoVfx.cs`
- `SpecialistAgent` NavMesh bind / pathing
- `GameLoop.EnsureNavMesh`, `BuildingPlacementInput` obstacles
- `DustStalkerAgent` death VFX, `OverseerHud` fail veil
