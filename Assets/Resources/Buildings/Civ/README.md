# Civ building art (phase 2)

Twenty game-ready FBXs: Inn, Guild Hall, HAB, Power and Defense for Lumen Concord, Tether Exchange, Bastion Mandate and Pathfinder Accord.

Naming: `SM_{Civ}_{Building}.fbx` with civs `Lumen`, `Exchange`, `Mandate`, `Pathfinder`.

Each file holds `{name}_LOD0` / `_LOD1` / `_LOD2`, plus empties `Gate` (door, faces Blender −Y / Unity −Z), `Core_Socket` (the small cyan Core motif), and `GunPivot` on Defense.

Material slots, in this order, mapped by name: Trim, Carbon, TeamColour, Accent, plus CoreEmission and (HAB only) ChargerAmber. They share the citadel 2048 trim sheet at `Assets/Resources/Citadels/Textures/`. TeamColour and Accent stay neutral in the file for runtime tinting. Cyan is only CoreEmission; amber is only HAB charger pods.

Plot is 6 × 6 m (4×4 cells at 1.5 m). Pivot at ground centre. Import at 1:1, FBX axis_forward=−Z, axis_up=Y. Heights stay under 10 m (the shortest castle, the Dome, is 12.59 m). Mandate buildings are deliberately low and wide (2.2–3.9 m).

Regenerate with `blender -b -P Blender/scripts/sm_civ_buildings.py`. Poly budget is in `BUDGET.md`. These files are additive — they do not replace `SM_Hero_*`. No C# in this change; hookup is Unity Engineer's prefab routing.
