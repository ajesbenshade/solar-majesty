# Environment ground textures

Place seamless tiled PNGs here (1024² recommended, Repeat wrap in Unity):

| File | Body |
|------|------|
| `SM_Ground_Earth_Albedo.png` | Meadow grass / soil |
| `SM_Ground_Earth_Normal.png` | Soft bump |
| `SM_Ground_Mars_Albedo.png` | Orange regolith |
| `SM_Ground_Mars_Normal.png` | Dust / rock bump |

See `Docs/GROK_IMAGINE_ENVIRONMENT_PROMPTS.md`. Generate locally with:

```powershell
python Blender/scripts/sm_bake_ground_textures.py
```
