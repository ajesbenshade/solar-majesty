# Phase 3B — Unit Silhouettes, Volume & Waves

Readable industrial unit placeholders, light URP post, and a two-wave stalker mission. Blender hero FBX still deferred.

## Success criteria (Play Mode)

| Test | Expected |
|------|----------|
| Party silhouettes | Scout tall+antenna, Engineer toolbox, Defense shield; white/black/orange accents |
| Stalkers | Low predator with orange eyes + legs |
| Post FX | Mild bloom / vignette / contrast (DemoVolume) |
| HUD | Wave 1 objective; after clear → “Reinforcements inbound…” → Wave 2 |
| Clear both waves | OUTPOST SECURED |
| Overwhelm | Lose / revive still works |

## Implementation

- `UnitPlaceholderFactory` — SpaceX shell/band/accent parts + URP Lit mats
- `DemoAtmosphere.EnsureVolume` — ColorAdjustments + Bloom + Vignette
- `MissionController` — wave 1 clear → spawn reinforcements → wave 2 clear → win
- `GameLoop.SpawnStalkerWave` — shared stalker spawn for waves
- Rebuild unit prefabs: **Solar Majesty → Build Demo Content Assets**

## Still deferred

- True Blender hero meshes for units/stalkers
- Authored VolumeProfile asset (runtime SO is fine for demo)
