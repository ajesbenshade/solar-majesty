# Grok Imagine — Environment textures and props

Earth + Mars landscape assets for Copilot 3D / tiled ground. Units stay in [`GROK_IMAGINE_COPILOT3D_PROMPTS.md`](GROK_IMAGINE_COPILOT3D_PROMPTS.md).

Flat `GroundPlane` stays — these are **textures + scatter props**, not heightmaps.

---

## Ground textures (seamless tiles)

Save to `Assets/Resources/Environment/Textures/`. Prefer **seamless / tileable** 1024×1024 PNG.

Or bake locally (no Imagine required):

```powershell
python Blender/scripts/sm_bake_ground_textures.py
```

### Earth meadow albedo

```
Seamless tileable top-down ground texture, 1024x1024, soft meadow grass with patches of soil and faint dirt tracks, muted green and warm soil brown, no buildings, no trees, no text, no UI, even lighting, photoreal but game-ready, seamless wrap on all edges
```

### Mars regolith albedo

```
Seamless tileable top-down Mars regolith texture, 1024x1024, orange-red dusty soil with subtle darker mottling, wind ripples, and tiny pebble noise, no buildings, no craters as large features, no text, even lighting, game-ready PBR albedo, seamless wrap on all edges. Spacious campus pads sit on this dirt — do not paint buildings into the tile.
```

### Normal maps

Ask Imagine for “normal map only, purple-blue tangent space, seamless tile” matching each albedo, **or** derive from albedo height in the bake script (recommended).

Unity import: Texture Type **Default** for albedo; **Normal map** for `*_Normal.png`. Wrap Mode **Repeat**.

---

## Prop hero shots (Copilot 3D)

Paste **Rules** first, then one subject. Same clean-studio rules as unit heroes.

```
Single 3D product hero render, ONE subject only, centered, full object visible with margin. Pure seamless white studio background, no ground texture, no environment, no props, no other objects. Three-quarter view from slightly above (about 30 degrees), soft even studio lighting, one faint contact shadow directly under the subject only. No text, no labels, no UI, no watermark. Readable silhouette for an isometric RTS. high detail 3D render style
```

| Save as | Subject prompt | Import targets |
|---------|----------------|----------------|
| `SM_Tree_Broadleaf_A_Hero.png` | Broadleaf deciduous tree, short thick trunk, rounded leafy canopy, exaggerated RTS proportions, ~2.4 m tall game asset | `--name SM_Tree_Broadleaf_A --target-height 2.4 --category environment` |
| `SM_Tree_Broadleaf_B_Hero.png` | Slightly taller broadleaf tree, asymmetric canopy, darker trunk, RTS readable | `--name SM_Tree_Broadleaf_B --target-height 2.6 --category environment` |
| `SM_Rock_Boulder_A_Hero.png` | Angular Mars boulder, rough orange-brown rock, about 1 m across | `--name SM_Rock_Boulder_A --target-width 1.0 --target-height 0.55 --category environment` |
| `SM_Rock_Boulder_B_Hero.png` | Lower flatter Mars rock cluster, graphite and rust tones | `--name SM_Rock_Boulder_B --target-width 1.2 --target-height 0.45 --category environment` |
| `SM_Crater_Vista_Hero.png` | Shallow impact crater bowl with raised rim, diameter about 10 m, grey-orange regolith, top-downish three-quarter | `--name SM_Crater_Vista --target-width 10 --target-height 1.2 --category environment` |
| `SM_Dune_Low_Hero.png` | Low elongated sand dune ridge, soft Mars orange, about 6 m long 1.2 m tall | `--name SM_Dune_Low --target-width 6 --target-height 1.2 --category environment` |

### After Copilot GLB download

```powershell
Copy-Item "$env:USERPROFILE\Downloads\Tree A.glb" Blender\imports\copilot3d\SM_Tree_Broadleaf_A.glb
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background `
  --python Blender/scripts/sm_import_copilot3d.py -- `
  --name SM_Tree_Broadleaf_A --target-height 2.4 --category environment
```

### Stand-in without Copilot

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background `
  --python Blender/scripts/sm_environment_blockouts.py
```

Exports tree/rock/dune/vista-crater FBX into `Assets/Resources/Environment/`.

---

## Landmark dressing (Copilot 3D)

Same hero-shot rules. These are visual dressing only — gameplay footprints stay unchanged.

| Save as | Subject prompt | Import targets |
|---------|----------------|----------------|
| `SM_Starship_Stack_Hero.png` | White vertical Starship-style rocket, black heat-shield belly stripe, forward flaps, standing upright, SpaceX industrial aesthetic, about 40 m tall | `--name SM_Starship_Placeholder --target-height 44 --category environment` |
| `SM_Cloud_Cumulus_Hero.png` | Soft white cumulus cloud puff, stylized low-poly game asset, gentle rounded lobes | `--name SM_Cloud_Cumulus --target-width 12 --target-height 5 --category environment` |

## Better ground tiles (replace the baked ones)

The current PNGs are procedurally baked stand-ins. For a big visual jump, generate these in Imagine and overwrite the files in `Assets/Resources/Environment/Textures/`:

1. `SM_Ground_Earth_Albedo.png` — use the Earth meadow prompt above; ask for "seamless tileable, no visible repetition, subtle clover and wildflower flecks"
2. `SM_Ground_Mars_Albedo.png` — Mars regolith prompt above; add "fine ripple texture, scattered small pebbles"

Keep 1024×1024. Unity reimports automatically; normal maps can stay baked.

## Related

- Runtime catalog: `EnvironmentMeshCatalog`
- Ground prefer/fallback: `PlanetaryMapDressing.DressGround`
- Unit Copilot prompts: [`GROK_IMAGINE_COPILOT3D_PROMPTS.md`](GROK_IMAGINE_COPILOT3D_PROMPTS.md)
