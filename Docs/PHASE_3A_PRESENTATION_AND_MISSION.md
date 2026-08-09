# Phase 3A — Presentation & Mission Stakes

Lunar lighting pass + a clear sandbox win condition. `SpecialistBrain` unchanged. Blender hero unit meshes deferred to 3B.

## Success criteria (Play Mode)

| Test | Expected |
|------|----------|
| Boot | Soft sun shadows on campus; cool fill; lunar ground; distant fog |
| Camera | Deep navy clear color (not default skybox blue) |
| HUD top bar | Mission line: Clear all Dust Stalkers (N/N left) |
| ClearThreat both stalkers | Green “OUTPOST SECURED” + victory chord |
| Continue overseeing / **Y** | Win banner dismisses; sandbox continues |
| All specialists down | Red overwhelm veil (lose); **Y** revives → mission Active again |
| Click ground | Still no unit repath |

## Implementation

- `DemoAtmosphere` — sun soft shadows, fill light, trilight ambient, linear fog, ground tint
- URP asset: soft shadows on, shadow distance 80, 2 cascades
- `MissionController` — win when stalkers cleared; lose when outpost overwhelmed
- `OverseerHud` — mission status + win banner
- `DemoAudio.PlayVictory`

## Deferred (3B+)

- Blender hero meshes for Scout / Engineer / Defense / Stalker
- Richer URP Volume (bloom / color grading asset)
- Timed hold / multi-wave missions
