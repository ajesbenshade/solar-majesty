# Unit Blockouts (Blender)

Interim hero meshes built via CLI while Imagine credits refresh. Prefer these over primitive placeholders.

## Rebuild

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python Blender/scripts/sm_unit_blockouts.py

/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python Blender/scripts/sm_hero_building_kits.py
```

Unity: **Solar Majesty → Build Demo Content Assets**

## Assets

| Resources path | Role |
|----------------|------|
| `Units/SM_Unit_ScoutDrone` | Scout mesh |
| `Units/SM_Unit_EngineerBot` | Engineer mesh |
| `Units/SM_Unit_DefenseMech` | Defense mesh |
| `Units/SM_Unit_DustStalker` | Stalker mesh (long predator) |
| `Units/SM_Unit_Medic` | Hover stretcher |
| `Units/SM_Unit_HarvesterBot` | Tracked orange-blade hopper |
| `Units/SM_Unit_SurveyorBot` | Tripod mast |
| `Units/SM_Unit_TerraformerBot` | Terraformer orange-blade dozer + rear rake |
| `Units/SM_Unit_CourierBot` | Six-wheel freight hauler |
| `Units/SM_Unit_GeologistBot` | Six-wheel drill rover |
| `Units/SM_Unit_SentinelMech` | Squat turret chassis (continuous treads) |
| `Units/SM_Unit_RegolithMite` | Compact pillbug mite |
| `Units/SM_Unit_WattLeech` | White ray + cyan groove |
| `Units/SM_Unit_IceWisp` | Seven-point ice-star |
| `Units/SM_Unit_AshHopper` | Tall six-leg hopper |
| `Units/SM_Unit_SoilCreeper` | Graphite isopod creeper |
| `Units/SM_Unit_RockTick` | Wide crab tick |
| `Buildings/SM_Hero_*` | Hero building kits (HAB / Commons / Power / extractors / Defense / pad / guild / LAB / wonders) |
| `Units/Unit_*` | Play prefabs (agent + mesh) |

`UnitPlaceholderFactory` / `UnitMeshCatalog` load FBX first, fall back to primitives.
