# Look-material tiles (Dream Loop)

Seamless 512² PNGs for URP Lit fallback + `IndustrialArtDressing`. Bake:

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
| `SM_Mat_MarsRock_Albedo.png` / `_Normal.png` | Neutral fractured relief; runtime body tint supplies Mars rust |

Unity: albedo = Default, normal = Normal map, Wrap = Repeat. Name Blender materials
`SM_Canvas` / `SM_DustyMetal` / `SM_Steel` / `SM_White` / `SM_Solar` /
`SM_Rock`.
