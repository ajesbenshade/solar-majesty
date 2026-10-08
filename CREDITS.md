# Credits and third-party assets

This file lists the third-party content checked into this repo, its license, and what we use it for. Everything else (code, Blender-scripted models, generated audio and textures) is original to Solar Majesty. If you add a third-party asset, add an entry here and keep its license file next to it.

**Before this repo is made public:** the Unity Asset Store packages below are covered by the Standard Unity Asset Store EULA. It allows using them inside the game, but not redistributing them on their own, and a public source repo could count as redistribution. Review them (remove them, or keep them out of the public tree) before changing the repo's visibility.

## Fonts

### Cinzel (Cinzel Black)
- **Author:** Natanael Gama / The Cinzel Project Authors
- **Source:** https://github.com/NDISCOVER/Cinzel, commit `8271e16` (`fonts/ttf/Cinzel-Black.ttf`)
- **License:** SIL Open Font License 1.1. Full text is in `Blender/fonts/Cinzel-OFL.txt`. "Cinzel Decorative" is the only Reserved Font Name, and we don't use it.
- **In repo:** `Blender/fonts/Cinzel-Black.ttf`, outside `Assets/`.
- **Use:** source lettering for the 3D title logo (`Blender/scripts/sm_title_logo.py`). Builds ship only the mesh geometry, never the font file.

## Audio

### Intro sting

| | |
|---|---|
| Clip | `Assets/Resources/Intro/IntroSting.ogg` |
| Source file | `jingles_STEEL07.ogg` from Kenney's *85 Short Music Jingles* |
| Author | Kenney (Kenney Vleugels), [kenney.nl](https://kenney.nl) |
| Download | [OpenGameArt: 85 Short music jingles](https://opengameart.org/content/85-short-music-jingles) |
| Pack page | [Kenney Music Jingles](https://kenney.nl/assets/music-jingles) |
| License | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |

Attribution is not required by the license. The rest of the soundtrack and sound effects are generated in `Tools/audio/` (see [Docs/AUDIO.md](Docs/AUDIO.md)).

## Unity Asset Store packages (Standard Unity Asset Store EULA)

### Kevin Iglesias: Human Animations ("Human Basic Motions FREE")
- **Publisher:** Kevin Iglesias (https://www.keviniglesias.com)
- **Source:** Unity Asset Store, "Human Basic Motions FREE", package 154271 (https://assetstore.unity.com/packages/3d/animations/human-basic-motions-free-154271). The store listing is at v2.4.2; the imported version isn't recorded in the repo.
- **License:** Standard Unity Asset Store EULA (free package). We may use and modify it inside the game, but may not redistribute it on its own.
- **In repo:** `Assets/Kevin Iglesias/Human Animations/`, a subset:
  - `HumanM@`/`HumanF@` `Idle01` and `Walk01_Forward` clips
  - the two reference dummy models
  - avatar masks
  - `SpineProxy.cs`
  - the Human Basic Motions demo controller and dummy prefabs
- **Use:**
  - **Colonists (`Assets/Art/Colonists`, `Assets/Resources/Colonists`):** play the `Idle01`/`Walk01_Forward` humanoid clips through Unity Humanoid retargeting. Their skeleton reuses Kevin Iglesias's joint names and hierarchy (`B-hips` … `B-toe.R`), re-proportioned in `Blender/scripts/sm_colonists.py`. **No Kevin Iglesias mesh is used in the colonists.**
  - **`SuitCrossingWalker` / `VendorDressingKit`:** also reference these clips.
- **Notes:** no standalone redistribution. **Review before the repo is made public** (see the note at the top).

### BOXOPHOBIC: Terrain Data Baker
- **Publisher:** BOXOPHOBIC (https://boxophobic.com)
- **Source:** Unity Asset Store, "Terrain Data Baker", package 205053 (free) (https://assetstore.unity.com/packages/tools/terrain/terrain-data-baker-205053)
- **License:** Standard Unity Asset Store EULA.
- **In repo:** `Assets/BOXOPHOBIC/` (Terrain Data Baker plus BOXOPHOBIC Utils).
- **Use:** the demo terrain albedo tiles (Sand / Grass / Snow) are used as terrain splat layers (`TerrainSplatLayers`, `TerrainSplatLayersBuilder`).
- **Notes:** review before the repo is made public.

### Polytope Studio: Lowpoly Environment (Nature)
- **Publisher:** Polytope Studio
- **Source:** Unity Asset Store, Polytope Studio's Lowpoly Environment – Nature series (`PT_*` prefabs). The repo doesn't record which edition was imported. The free edition, "Low Poly Environment - Nature Free", is package 187052 (https://assetstore.unity.com/packages/3d/environments/low-poly-environment-nature-free-lowpoly-medieval-fantasy-series-187052).
- **License:** Standard Unity Asset Store EULA.
- **In repo:** `Assets/Polytope Studio/Lowpoly_Environments/`, a subset of trees, shrubs, flowers, plants and rocks.
- **Use:** Earth nature dressing (`EnvironmentMeshCatalog`, `VendorDressingKit`).
- **Notes:** review before the repo is made public.

## Open-source shaders (MIT)

### Uber Stylized Water
- **Author:** MatrixRex, © 2025
- **Source:** https://github.com/MatrixRex/Uber-Stylized-Water
- **License:** MIT. See `Assets/Shaders/Uber Stylized Water/LICENSE.txt`.
- **In repo:** `Assets/Shaders/Uber Stylized Water/`, imported without the demo/test scenes or planar reflection scripts. Notes are in `README_SOLAR_MAJESTY.md`.
- **Use:** lakes, rivers and ponds (`StylizedWaterVisual`, `Assets/Resources/Environment/SM_Water.mat`).

### Shader Graph Custom Lighting and Shader Graph Variables (bundled with Uber Stylized Water)
- **Author:** Cyanilux. Custom Lighting © 2020, Variables © 2021.
- **Source:**
  - https://github.com/Cyanilux/URP_ShaderGraphCustomLighting
  - https://github.com/Cyanilux/ShaderGraphVariables
- **License:** MIT. See `Assets/Shaders/Uber Stylized Water/Third Party/*/LICENSE`.
- **Use:** the water shader includes `CustomLighting.hlsl`.

## Development tooling (not shipped in builds)

### dream-loop agent skill
- **Author:** Anshu Chimala (@anshuc), © 2026
- **License:** MIT. See `.cursor/skills/dream-loop/LICENSE`.
- **In repo:** `.cursor/skills/dream-loop/`
