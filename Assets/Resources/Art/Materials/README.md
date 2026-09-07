# Look-material tiles (Dream Loop)

Seamless 512² PNGs for `IndustrialArtDressing`. Hull-shader slots (`SolarMajesty/Hull`: white / steel / graphite / carbon / orange) project the albedo + normal **triplanar** in world space (`_DetailAlbedo` / `_DetailNormal`, pass 2); Lit-fallback slots (solar / canvas) sample them by UV. Bake:

```
python Blender/scripts/sm_bake_look_materials.py
```

| File | Slot |
|------|------|
| `SM_Mat_WhiteHull_Albedo.png` / `_Normal.png` | White thermal panels |
| `SM_Mat_Steel_Albedo.png` / `_Normal.png` | Brushed steel |
| `SM_Mat_DustyMetal_Albedo.png` | Dust-filmed metal (extractor / pylon) |
| `SM_Mat_Solar_Albedo.png` | PV cells + bus bars |
| `SM_Mat_Canvas_Albedo.png` / `_Normal.png` | Tan awning / Inn porch |

Unity: albedo = Default, normal = Normal map, Wrap = Repeat. Name Blender materials `SM_Canvas` / `SM_DustyMetal` / `SM_Steel` / `SM_White` / `SM_Solar`.
