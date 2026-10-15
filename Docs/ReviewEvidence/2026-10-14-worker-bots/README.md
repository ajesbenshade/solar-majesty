# Worker-bot villagers (canon): review evidence (2026-10-14)

These are editor-camera renders made in Unity 6000.5.10f1 batchmode on Basement-1, with no Play mode. Every villager was spawned through the real `VillagerAgent.Spawn` hookup on flat ground.

- `before_*`: main 3423792 with `ColonistArt.Enabled` off, which gives the capsule-and-band placeholder.
- `after_*`: this branch.

| Shot | View |
|---|---|
| 1_overseer_default_zoom | Ortho size 9, 30° pitch, 45° yaw (the IsometricCameraController defaults), 12 villagers, every third one walking |
| 2_overseer_close | Same angle at ortho size 3.6 |
| 3_lineup | The four roles at rest: Engineer, Hydroponics, Medic (Repair bot), Hauler |
| 4_night_lineup / 5_night_overseer | Night lighting: blue moonlight at 0.18 intensity plus dark ambient. Shows the cyan sensor eye and the role beacons |

`setup-report.txt` and `probe-report.txt` hold the import log: avatars, triangle counts, clips, and the idle/walk hip height and lowest vertex.

To regenerate the meshes and the idle: `blender -b --python Blender/scripts/sm_worker_bots.py -- --export <dir> [--blend Blender/SolarMajesty_WorkerBots.blend] [--review <dir>]`
