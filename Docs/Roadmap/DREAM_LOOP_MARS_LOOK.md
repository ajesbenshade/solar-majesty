# Dream Loop — Spaced Mars look target

**Status:** Aaron look brief 2026-09-07 locked. **Not a Phase 4 EXIT.** Do not stamp exit. Do not start Phase 5.

Bake-off PRs **#26 / #27 / #28** (Sol / Fable / Opus Captures) were **rejected**. They are **not** the EXIT claim. EXIT = Aaron look-clear vs the locked concept under this brief.

Working files (retries, judge notes) live in `.dream-loop/` and are gitignored. Reuse the skill at [`.cursor/skills/dream-loop/SKILL.md`](../../.cursor/skills/dream-loop/SKILL.md) ([achimala/dream-loop](https://github.com/achimala/dream-loop), MIT).

## LOCKED concept (north star — keep this file)

[`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — in-engine-style isometric overseer still. **ITS language**, not the prior tube-web campus.

Judge stills against **this** image, not the retired packed `SM_MarsCampaign_VisualTarget.png`, and not bake-off `SM_Capture.png` frames from #26 / #27 / #28. Do **not** regenerate, replace, or overwrite this PNG.

### Aaron look brief (2026-09-07)

1. **Forgo interconnect tubes** between buildings. `SpawnTubeRuns` / between-yard tube web are **gone** (not a `StampTubeRuns` flag). Leftover `CampusDress_TubeRuns` roots are destroyed. Square Lego airlocks may remain as building **ports**.
2. **More space** between buildings — empty dirt is intentional; do not pack AABB or fill dirt with leftover sockets.
3. **Distant haze** toward the horizon (concept language). Existing `DemoAtmosphere` exponential-squared fog is the live haze; do not wash the campus itself.
4. **Polyhedron / geodesic Commons** silhouette — not a soft sphere-only kit if we can dress it.
5. Colonists / specialists crossing open ground should **read as spacesuited** (vulnerable between buildings). Docs + still dressing notes only. Do **not** invent new `FlagTypes` or rewrite `SpecialistBrain`.

### What the concept is

- High isometric / Majesty-2 overseer camera
- **Spaced campus pads** with empty red regolith between yards — no tube corridors linking pads
- Hero cluster: Commons **geodesic / polyhedron** dome, HAB-1 cylinder on a short square airlock port, circular pad + Starship, small solar field, one industrial / extractor yard, optional canvas porch
- Distant Mars haze toward the horizon, strong key light, long readable shadows
- Dusty metal / white thermal / carbon / orange / solar glass / canvas — practical colony, not toy, not Elden Ring clutter
- Open-ground crossings read as **spacesuited** (isolation between pads)

### What it is NOT

- Do **not** stamp interconnect tube webs (`CampusDress_TubeRuns`, `CampusTubeRoot` corridors)
- Do **not** pack every dirt patch
- Do **not** fill interior 4×4 sockets or leftover Inn / wonder / extra HAB / extra solar / Defense to chase density
- Do **not** zoom `FitStillOrtho` inside play ortho 10 to crop out empty ground
- Do **not** treat bake-off PRs #26 / #27 / #28 as EXIT or as a new north star

---

## Game Designer / Mac Unity — still vs concept

Unity Editor is required (this Cloud VM cannot run Play Mode). Shoot the same way every time:

1. Open the project in **Unity 6000.5.x** (Mac).
2. Scene: `Assets/Scenes/LunarOutpost_Sandbox.unity`.
3. **Solar Majesty → Render → Configure URP For Look Target** (once per machine).
4. **Solar Majesty → Render → Capture Mars Still**.
   - Interactive editor only (batch mode refuses).
   - Menu writes `Docs/Roadmap/SM_Capture.png`.
   - Uses play campus **ortho 10**, shutter hold, `spawnShowcaseColony` stays **false**. Mars Game view **Scale 1x**.
   - Commons → airlock port → HAB plus CanFit pad / PWR / extractors. Leftovers do **not** stamp. Interior dirt is **not** force-filled. **No** `CampusDress_TubeRuns`.
   - `FitStillOrtho` stays at play ortho 10 — it must not crop into a packed look.
5. Game view: **Scale 1x**. Do not free-orbit or zoom-to-pack.
6. Compare `SM_Capture.png` to `SM_MarsCampus_SpacedOverseer_Concept.png` at the same aspect (concept is 16:9; Game tab is short-wide — match **layout language**, not pixel crop).

### Judge ladder (when you have a still)

Use the Dream Loop tiers in the skill (shape → light → materials → detail). Cap the score if a lower tier fails. Empty dirt is a **pass**, not a miss. A tube-web or packed-AABB frame **fails** shape against this concept.

This Cloud pass could not run an in-engine still (no Unity / Blender). **Do not invent a score.**

### After a still

- If hulls wash orange: do not raise Mars dust on `IndustrialArtDressing.BindBody` (Mars dust amount stays low so white reads).
- If ground looks flat: confirm `Assets/Resources/Environment/Textures/SM_Ground_Mars_*` imported (albedo Default, normal = Normal map, Repeat) and `PlanetGround` `_DetailTexAmount` is non-zero.
- If the horizon does not recede: confirm Mars `DemoAtmosphere` fog is on. Mars uses a **Linear** ramp (`FogStart`/`FogEnd` on the body), other bodies Exp2. The ortho Game tab never shows sky (top of frame is ground ~45 m from the camera), so haze must come from ground fog; backdrop quads were removed. Custom shaders (`SM_PlanetGround`, `SM_Hull`) compute fog from **view depth** (`SM_FogCoord`) because URP's clip-space `ComputeFogFactor` is ~0 under orthographic cameras.
- If a batch (`Camera.Render`) still shows every hull one colour (black / brown / orange): that is the SRP Batcher leaking one material's constants in edit mode. `DemoContentBuilder.RenderWithoutSrpBatcher` disables the batcher around the capture; Play Mode is unaffected.
- If the pad deck or carbon bands read as salmon plates: dark prims must stay dielectric (`HeroBuildingKits.Tint` metallic 0.12); the LandingPad FBX is skipped for the procedural pad for the same reason.
- If interconnect tubes appear: `SpawnTubeRuns` must stay **gone**; `RefreshTubes` only enables docked Lego ports and destroys leftover `CampusDress_TubeRuns` roots.
- If docks step / gap: kits must seat via `SnapToGroundKeepingDockAxis` so arms stay on `ColonyVisualUtility.DockY`.
- If Commons reads soft-sphere / cyan waist: live kit now has `CommonsGeo_*` facets + orange cupola/equator bands; cyan waist visors are removed.

### Local Mac Unity (preferred)

Interactive CaptureStill works on Mac Unity 6000.5.x (batchmode refused for Game-tab). Sequence:

```bash
# GPU editor still (no -nographics)
Unity -batchmode -quit -projectPath . \
  -executeMethod SolarMajesty.EditorTools.DemoContentBuilder.CapturePackedMarsStill

# Game-tab (interactive — do not use -batchmode)
Unity -projectPath . \
  -executeMethod SolarMajesty.EditorTools.CaptureStill.Run
```

Latest `SM_Capture.png` is the dream-loop Game-tab still (ortho 10, leftover=spaced). Judge vs locked concept. Local rounds so far (fresh judge each): r2 3, r4–5 3 (haze blocked), r6 4 (haze landed; pad/dirt/shadow/solar palette blocked), r7 **5** (pad, shadows, crossings landed; fog reach / dirt hue / beige blocks still block Tier 2). **Do not stamp EXIT** until Aaron look-clear.

### Cloud agents (no Unity)

Cloud VMs cannot run Play Mode. Prefer local CaptureStill. **Do not invent a judge score** from archived stills alone when claiming EXIT.

### Bake (no Imagine required)

```bash
python Blender/scripts/sm_bake_ground_textures.py          # skips existing authored tiles
python Blender/scripts/sm_bake_look_materials.py          # hull / steel / solar / canvas
# python Blender/scripts/sm_bake_ground_textures.py --force  # only if replacing Imagine tiles
```

Then in Unity: **Solar Majesty → Verify Environment Assets**.
