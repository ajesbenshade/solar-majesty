# Copilot 3D pilot evaluation — Courier (`SM_Unit_CourierBot`)

**Date:** 2026-08-29  
**Pilot status:** Pipeline validated; crop generated for Copilot 3D upload. Real Copilot 3D GLB still requires manual upload to Copilot Labs.

## What was tested

| Step | Result |
|------|--------|
| Folder layout (`ConceptSheets/Copilot3D_Input/`, `Blender/imports/copilot3d/`) | Created with README |
| `sm_crop_turnaround.py` | Runs; exits cleanly when no `*_Turnaround.jpg` in `ConceptSheets/` (requires `pip install Pillow`) |
| `sm_import_copilot3d.py` | Import → scale → SM_White stub → FBX → `Assets/Resources/Units/` — **pass** |
| Unity Play Mode visual | Not run (Unity Editor not detected on this machine); FBX name matches `UnitMeshCatalog.CourierPath` |

### Pipeline run (stand-in GLB)

Concept turnaround JPG **is** in repo. Crop completed:

- Input: `ConceptSheets/SM_Unit_CourierBot_Turnaround.jpg`
- Output: `ConceptSheets/Copilot3D_Input/SM_Unit_CourierBot_ThreeQuarter.png`

Because Copilot 3D is browser-only (no API), the pilot used the procedural blockout GLB as a stand-in to validate the Blender import → FBX → Unity path:

1. `sm_unit_blockouts.py -- SM_Unit_CourierBot` → `Blender/exports/SM_Unit_CourierBot.glb`
2. Copied to `Blender/imports/copilot3d/SM_Unit_CourierBot.glb`
3. `sm_import_copilot3d.py --name SM_Unit_CourierBot --target-height 1.45 --target-width 2.0 --category units`

**Dimensions after import script:**

| | W (m) | D (m) | H (m) |
|---|------:|------:|------:|
| Blockout (before scale) | 1.08 | 1.71 | 1.81 |
| After import script | 2.00 | 1.37 | 1.45 |
| Target (workflow doc) | ~2.0 | — | ~1.45 |

Saved blend: `Blender/SolarMajesty_Copilot3D_SM_Unit_CourierBot.blend`  
Shipped FBX: `Assets/Resources/Units/SM_Unit_CourierBot.fbx`

### Remaining manual step (real Copilot 3D)

When ready for the real Copilot 3D mesh:

1. Upload `ConceptSheets/Copilot3D_Input/SM_Unit_CourierBot_ThreeQuarter.png` to [Copilot Labs → Copilot 3D](https://copilot.microsoft.com)
3. Replace `Blender/imports/copilot3d/SM_Unit_CourierBot.glb` with the download
4. Re-run import script; open `.blend` for material remap + silhouette polish
5. Unity: **Solar Majesty → Build Demo Content Assets**; compare Play Mode at campus ortho 10 against the spaced overseer look (`05_PHASE_4_VISUAL_TARGET.md`)

## Comparison: Copilot path vs `sm_unit_blockouts.py`

| Criterion | Python blockout | Copilot 3D + import script |
|-----------|-----------------|---------------------------|
| Time to first mesh | ~10 s (automated) | ~1 min generate + ~15–45 min cleanup (estimated) |
| Style lock (SM palette) | Built-in | Requires manual remap after import |
| Silhouette fidelity | Sheet-matched primitives | Potentially higher if Copilot captures Imagine detail |
| RTS dimensions | Authored in code | `--target-height/width` + manual fix |
| Docking / hard-surface precision | Good for simple hauler | Unknown until real GLB tested |
| Fallback cost | Low (already shipped) | Discard GLB if cleanup > rebuild time |

**Pilot conclusion:** The **tooling path works**. Copilot 3D is worth trying on Courier when the turnaround JPG is local; compare cleanup time against the existing blockout before rolling to second-wave units.

## Second-wave recommendation

Proceed tier-by-tier after one **real** Copilot 3D Courier attempt:

| Tier | Assets | Go / wait |
|------|--------|-----------|
| **Pilot (try next)** | Regolith Mite, Ice Wisp, Scout Drone | Go if Courier cleanup ≤ 30 min |
| **Second wave** | Medic, Harvester, Surveyor, Sentinel, Geologist | Go if pilot silhouette beats blockout in Unity |
| **Heavy Blender** | Engineer, Defense, Terraformer, Stalker, Rock Tick | Wait — limbs/treads likely need more manual work than Copilot saves |
| **Buildings (mood only)** | HAB-1, Command Dome, Landing Pad | Do not replace `sm_hero_building_kits.py`; optional reference mesh only |

## Success criteria checklist

- [x] `SM_Unit_CourierBot` FBX produced via import script path
- [x] FBX copied to `Assets/Resources/Units/` with correct name for `UnitMeshCatalog`
- [x] Dimensions scaled to documented RTS targets (H 1.45 m, W 2.0 m)
- [ ] Silhouette verified in Unity Play Mode (pending Editor run)
- [ ] Real Copilot 3D GLB tested (pending turnaround JPG + Copilot Labs upload)
- [ ] Total time less than blockout + polish (pending real Copilot run)
