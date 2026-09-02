# Copilot 3D GLB imports

Download GLBs from Copilot Labs immediately — cloud copies expire in ~28 days.

## Example

1. Upload `ConceptSheets/Copilot3D_Input/SM_Unit_CourierBot_ThreeQuarter.png` to Copilot 3D.
2. Save download as `SM_Unit_CourierBot.glb` in this folder.
3. Run:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background `
  --python Blender/scripts/sm_import_copilot3d.py -- `
  --name SM_Unit_CourierBot --target-height 1.45 --category units
```

4. Open `Blender/SolarMajesty_Copilot3D_SM_Unit_CourierBot.blend` for manual cleanup, then re-export FBX.
