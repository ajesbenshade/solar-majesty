# Colonist long-stride walk + grounding: review evidence (2026-10-08)

Unity 6000.5.10f1, Basement-1, branch `art/colonists-stride` (from `main` f2459f1).

| File | What it shows |
|---|---|
| `colonist_stride_old_vs_new.mp4` / `.gif` | 5 s (GIF: 4 s) at 30 fps, both suits moving at 2.4 m/s. Left: Kevin Iglesias Walk01 at 2.5x (the cap). Right: the new colony stride at 1.14x. Ground ticks are 1 m apart. |
| `colonist_ground_contact.jpg` | Level side view with the ground top at y = 0. Panels: Idle01, stride touchdown, stride mid-stance, old Walk01 contact. Each label gives the lowest skinned vertex. |
| `measure-report.txt` | Raw measurement log: speed, slide and ground gap for each variant, plus the terrain spawn checks. |

## Numbers (2.4 m/s, toe joint used as the contact point)

| | Old Walk01 | New colony stride |
|---|---|---|
| Planted-foot speed at 1x | 1.03–1.05 m/s | 2.098 m/s (authored 2.1) |
| Playback at 2.4 m/s | 2.50x (pinned at the cap) | 1.143x |
| Steps per second | 6.25 | 1.90 |
| Planted-foot slide | 0.21–0.32 m/s (8.8–13.3 %) | 0.066 m/s (2.8 %) |
| Lowest vertex while planted | -0.022 to -0.028 m (sinks) | -0.003 m |

## Float

- **Terrain.** `VillagerAgent.Spawn` placed suits at y = 0, and nothing resampled the height while they walked. On the Earth bake (256 m, seed 7), terrain heights run from -7.97 to +7.90 m, and 92 % of cells are more than 0.3 m from 0.
- **Bounds padding.** Snapping by the skinned bounds added another +0.050 m on flat ground.
- **After the fix,** the ground gap is -0.002 to -0.004 m at spawn and after walking 3 m.

EditMode: 692/692 passed.
