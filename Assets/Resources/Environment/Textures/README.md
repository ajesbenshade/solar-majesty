# Environment ground textures

Place seamless tiled PNGs here (1024² recommended, Repeat wrap in Unity):

| File | Body |
|------|------|
| `SM_Ground_Earth_Albedo.png` | Meadow grass / soil |
| `SM_Ground_Earth_Normal.png` | Soft bump |
| `SM_Ground_Mars_Albedo.png` | Orange regolith (ripples / grit / stones — no large craters) |
| `SM_Ground_Mars_Normal.png` | Dust / rock bump, derived from the regolith height field |

Look tiles (hull / steel / solar / canvas / dusty metal) live in `Assets/Resources/Art/Materials/`.

**Seamless is a hard requirement, not a recommendation.** `PlanetGround` repeats the Mars tile every
8.5 m, so a non-periodic lattice reads as a grid at overseer range. The previous Mars tile was not
periodic (seam delta 16.1 against an interior adjacency of 10.8). Detail also has to be authored at
real scale: at 1024² over 8.5 m the tile is ~120 px/m, so per-pixel hash "pebbles" are
sub-millimetre and vanish in the mip chain.

See `Docs/GROK_IMAGINE_ENVIRONMENT_PROMPTS.md` and `Docs/Roadmap/DREAM_LOOP_MARS_LOOK.md`. Generate
locally with:

```powershell
python Blender/scripts/sm_bake_ground_textures.py --mars --force
python Blender/scripts/sm_bake_look_materials.py
```

`sm_bake_ground_textures.py` **skips existing PNGs** so it does not stomp Imagine/authored tiles.
`--force` is scoped per body (`--mars` / `--earth`) — the Mars path is the vectorised periodic
regolith generator and is safe to force, while the committed **Earth** tile is authored and a global
force replaces it with flat scalar noise.
