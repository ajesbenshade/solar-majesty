# Unit Blockouts (Blender)

Interim hero meshes built via CLI while Imagine credits refresh. Prefer these over primitive placeholders.

## Rebuild

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python Blender/scripts/sm_unit_blockouts.py
```

Unity: **Solar Majesty → Build Demo Content Assets**

## Assets

| Resources path | Role |
|----------------|------|
| `Units/SM_Unit_ScoutDrone` | Scout mesh |
| `Units/SM_Unit_EngineerBot` | Engineer mesh |
| `Units/SM_Unit_DefenseMech` | Defense mesh |
| `Units/SM_Unit_DustStalker` | Stalker mesh |
| `Units/Unit_*` | Play prefabs (agent + mesh) |

`UnitPlaceholderFactory` / `UnitMeshCatalog` load FBX first, fall back to primitives.
