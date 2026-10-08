# Colonists v1 (art batch 3): review evidence, 2026-10-07

In-game shots come from World Preview development players (`WorldPreviewBuild.Run`, separate product name, so saves and prefs are separate). A temp harness, not committed, spawned 8 villagers through the real `VillagerAgent.Spawn` near `ColonyLayout.PartySpawn` and captured them with the overseer camera (ortho, pitch 30°, yaw 45°).
- **Before:** `main` @ f319a0c (capsules).
- **After:** this branch.

| File | What |
|---|---|
| 01_before_after_walk_close.jpg | Walking, close zoom (ortho 5.5), before vs after |
| 02_before_after_idle_close.jpg | Idling at work spots, before vs after |
| 03_before_after_default_zoom.jpg | Default zoom (ortho 18), before vs after |
| 04_before_after_mid_zoom.jpg | Mid zoom (ortho 9), before vs after |
| 11/13/14_after_ingame_*.jpg | Full after frames |
| 20/21_unity_lineup_*.jpg | Unity editor lineups, Idle01 and Walk01 |
| 22_unity_walk_strips_facing.jpg | Side-view walk frames, walking toward +Z: visor leads |
| 30-33_blender_*.jpg | Blender 4.2.3 Cycles lineups: front, overseer, back, stride |
| setup-report.txt / inspect-report.txt | Unity import, avatar and prefab report; tri, facing and stride inspection |
| editmode-summary.txt | EditMode run: 629/629 |

To regenerate the models: `blender -b --python Blender/scripts/sm_colonists.py -- --export Assets/Art/Colonists --blend Blender/SolarMajesty_Colonists.blend [--review <dir>]`
