# Environment ground textures

Place seamless tiled PNGs here (1024² recommended, Repeat wrap in Unity):

| File | Body |
|------|------|
| `SM_Ground_Earth_Albedo.png` | Meadow grass / soil |
| `SM_Ground_Earth_Normal.png` | Soft bump |
| `SM_Ground_Mars_Albedo.png` | Orange regolith (ripples / grit / pebbles — no large craters) |
| `SM_Ground_Mars_Normal.png` | Dust / rock bump |

Look tiles (hull / steel / solar / canvas / dusty metal) live in `Assets/Resources/Art/Materials/`.

See `Docs/GROK_IMAGINE_ENVIRONMENT_PROMPTS.md` and `Docs/Roadmap/DREAM_LOOP_MARS_LOOK.md`. Generate locally with:

```powershell
python Blender/scripts/sm_bake_ground_textures.py
python Blender/scripts/sm_bake_look_materials.py
```

`sm_bake_ground_textures.py` **skips existing PNGs** so it does not stomp Imagine/authored tiles. Pass `--force` only when you intend to replace them.
