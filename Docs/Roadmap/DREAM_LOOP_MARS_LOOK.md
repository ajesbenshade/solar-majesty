# Dream Loop — Spaced Mars look target

**Status:** second look pass (code only — shaders / materials / dressing / atmosphere / silhouettes). **Not a Phase 4 EXIT.** Do not stamp exit. Do not start Phase 5. The concept PNG is **locked** — do not regenerate or replace it.

Working files (retries, judge notes) live in `.dream-loop/` and are gitignored. Reuse the skill at [`.cursor/skills/dream-loop/SKILL.md`](../../.cursor/skills/dream-loop/SKILL.md) ([achimala/dream-loop](https://github.com/achimala/dream-loop), MIT).

## Concept (north star for this pass)

[`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — in-engine-style isometric overseer still.

Judge stills against **this** image, not the retired packed `SM_MarsCampaign_VisualTarget.png`.

### What the concept is

- High isometric / Majesty-2 overseer camera
- **Spaced campus pads** with empty red regolith between yards
- Hero cluster: Commons geodesic dome, HAB-1 cylinder, short tube + airlock, circular pad + Starship, small solar field, one extractor, optional canvas porch
- Thin Mars haze, strong key light, long readable shadows
- Dusty metal / white thermal / carbon / orange / solar glass / canvas — practical colony, not toy, not Elden Ring clutter

### What it is NOT

- Do **not** pack every dirt patch
- Do **not** fill interior 4×4 sockets to chase density
- Do **not** zoom the still camera inside play ortho 10 to crop out empty ground
- Do **not** treat leftover Inn / wonder / extra HAB as a density gate

---

## Pass 2 — Capture → concept deltas (code only, no still on this VM)

Judged against still9 (`SM_MarsCampaign_PlayModeCampusStill9.png`, Earth meadow campus) and the last Mars Game-tab stills, which read **flat**: no grit or haze, hard 4 m discs hiding inside hulls, smooth-sphere Commons, dust drums for extractors, carbon slab instead of canvas, 20° sun throwing 2.7× shadows up-left.

| Concept cue | What changed | Where |
|-------------|--------------|-------|
| Key from screen upper-left, shadows **down-right** ~1.4× height | Mars `SunEuler` 20/-62 → **36/148**; intensity 1.52 → 1.28; sun colour less yellow. Fill light now always sits ~175° from the key. | `CelestialBodyCatalog.BuildMars`, `DemoAtmosphere.EnsureFillLight` |
| Pale orange haze on the far ground, crisp campus | Mars fog is **Linear**, anchored at camera focal depth + 4 m, full at +96 m (≈15 % at frame top, ortho 10). Fog colour → pale dust `(0.86, 0.60, 0.38)`. | `DemoAtmosphere.ConfigureAmbientAndFog`, `FocalGroundDepth` |
| Dusty, slightly desaturated grade | Mars contrast 11 → 9, saturation 4 → −3, exposure 0.22 → 0.12 (higher sun puts ~1.7× more light on the ground). Soft vignette 0.16. | `DemoAtmosphere.GradeVolume`, `EnsureVignette` |
| Regolith peppered with dark stones | `PlanetGround` **pebble layer**: hashed jittered discs per 0.36 m cell, lit crescent facing the key, density 0.34 on Mars (0 on Earth). | `SM_PlanetGround.shader`, `PlanetaryMapDressing.BindPebbleField` |
| Fist-sized rocks between yards that cast shadows | **84** `Dress_MarsPebble` low-poly rocks (Boulder_A only), golden-angle spiral 4.5–21 m from the claim. | `PlanetaryMapDressing.SpawnPebbleField` |
| Hulls landed on dirt (dirty sill, textured panels) | `SolarMajesty/Hull` samples the PR #24 `SM_Mat_WhiteHull / Steel / DustyMetal` tiles **triplanar** (they only reached the Lit fallback before) and adds a **ground-dust skirt** over the lowest 0.85 m. Mars skirt 0.42, Earth 0.25×. | `SM_Hull.shader`, `IndustrialArtDressing.BindHullDetail` |
| Wide soft packed-dust yards | `Dress_Apron` is now a **soft radial decal quad** sized to the footprint (Mars 1.75× span, pad 1.9×; Commons gets one too) instead of a hard 4.2 m cylinder. | `CampusDressing.SpawnApron`, `YardDiameter` |
| Geodesic Commons dome | Frequency-2 icosphere strut lattice baked to one mesh (`CommonsGeoSteelLattice`), struts above the drum only; world-grid panel seams muted on the dome; orange cupola collar. | `HeroBuildingKits.BuildGeodesicLattice` |
| Canvas awning on a small module | Four steel poles + pitched khaki sheet + hem on **Workshop** (front-left corner, clear of the dock) and **Inn** (replaces carbon canopy). Canvas tint → sun-bleached khaki. | `HeroBuildingKits.BuildCanvasAwning` |
| Chemical-plant extractor with spherical tanks | Regolith extractor: two white **pressure spheres** on ring stands, steel pipe, tall white **column** with orange band + ladder (dust drums removed). | `HeroBuildingKits.BuildRegolithExtractor` |
| Tall lattice comms mast by the solar field | Three-strut steel mast (5.6 m) with ring braces, dish, whip, orange beacon on the PWR-1 yard corner. | `HeroBuildingKits.BuildLatticeMast` |
| MSAA / shadow crispness | Checked-in `URP-SolarMajesty.asset` now matches **Configure URP For Look Target** (MSAA 4, 4 cascades, 120 m, 4096 shadowmap) so a fresh clone shoots the same. | `Assets/Settings/URP-SolarMajesty.asset` |

Kept from the lock: spaced layout, play ortho 10, `spawnShowcaseColony` false, square airlocks, white hub + orange collars on docked faces only, Overseer-only control. No `SpecialistBrain` / FlagManager / economy edits.

### Capture read (Sep 7 Game-tab `SM_Capture.png`, 1024×667, Mars Sol 1, Defense + pad + solar + 2 HAB)

First real Capture against the concept. Tier-1 (shape) fails first, so those come first:

| What the Capture shows | Why | Fix in this pass |
|------------------------|-----|------------------|
| Hulls touch every frame edge; no dirt, no rocks in shot | still at play ortho 10 on a 1.5 Game tab = 30 m wide; concept frames ~60 m | `FitStillOrtho` is a **concept fit**: campus fills ~55 % of the frame, clamped **10–15**. Play ortho stays 10; the still never zooms in past it |
| Huge pale-cyan glowing disc + white flare in the foreground | Defense **shield bubble** (glossy translucent sphere, smoothness 0.82) catching the key + status pip at ×1.6 emission | `Dress_Shield` is a faint matte **ground ring** (α 0.10, no specular); pip emission ×0.55 |
| Fat white tube from Commons running off-frame to the pad | still21 `CampusDress_TubeRuns` linked any cardinal neighbours, pad included | Runs only between **pressurized** modules (Commons / HAB / LAB / Guild / Inn / airlock) — pad, solar, extractors, workshop stand free |
| Tubes ~40 % of HAB diameter, oversized orange collars | `DockBore` 1.42 m | `DockBore` **1.10** (≈30 % of HAB, concept). Every sleeve / port / collar keys off it |
| Pad reads as one bright orange plate | ring "discs" were solid cylinders stacked on graphite | Each orange disc capped by a graphite disc → thin annuli, dark deck between |
| Workshop / Inn read as black slabs; black square under Defense | carbon full-roof caps; Graphite slot at 0.26 | White roofs with carbon edge band + orange roof stripe; Graphite slot lifted to 0.40 concrete |
| Cyan visors / solar / lenses bloom into white-cyan blobs | HDR emissives 1.6–2.2 under bloom | `CyanEmit` / `SolarEmit` / `IceEmit` and the Cyan / Solar art slots cut to dark-glass levels |
| Dome and HAB sides go concrete grey | low Mars ambient + fill | AmbientSky / Equator lifted, fill 0.58; hub white emission 0.34 → 0.14 so it holds white without glowing |

Save the next Game-tab shot as `SM_MarsCampaign_PlayModeCampusStill10.png` when reshooting (the Sep 7 Capture was not committed).

### GD still checklist for pass 2 (what to look for in `SM_Capture.png`)

0. Campus sits in the middle ~55 % of the frame with regolith, stones and yards visible on every side (console: `SnapStillCampusCamera ortho=` between 10 and 15). No shield disc, no tube to the pad, pad shows dark deck between orange rings.
1. Shadows fall **down-right**, roughly 1.4× the HAB height. If they fall up-left, `DemoAtmosphere.Apply` did not run (check `[Atmosphere] Mars` in the console).
2. Far ground at the frame top is a shade paler than the near ground; hulls at frame centre are still white. If the whole frame is orange, check `RenderSettings.fogStartDistance` ≈ camera height / sin(pitch) + 4.
3. Ground shows dark specks at ~0.3 m and a few real stones with shadows between yards. No specks → `PlanetGround._PebbleDensity` is 0 (material not rebuilt) or the shader failed to compile (check `_PebbleCell` in the inspector).
4. Every module sits on a soft oval yard that fades into the pebbles; none end at a hard circle.
5. Commons reads as a **triangulated** dome with grey struts and an orange collar under the cupola.
6. Workshop / Inn show a khaki awning on four poles. Regolith extractor shows two white spheres and a tall column. PWR-1 has a thin mast with an orange beacon.
7. White hulls carry a faint dusty sill at the base and subtle panel grit — **not** an orange wash. If hulls wash orange, lower `IndustrialArtDressing.BindHullDetail` skirt (0.42) before touching `BindBody` dust.

Then run the judge ladder below. A score is only valid from a real `SM_Capture.png`.

---

## Game Designer / Mac Unity — still vs concept

Unity Editor is required (this Cloud VM cannot run Play Mode). Shoot the same way every time:

1. Open the project in **Unity 6000.5.x** (Mac).
2. Scene: `Assets/Scenes/LunarOutpost_Sandbox.unity`.
3. **Solar Majesty → Render → Configure URP For Look Target** (once per machine).
4. **Solar Majesty → Render → Capture Mars Still**.
   - Interactive editor only (batch mode refuses).
   - Menu writes `Docs/Roadmap/SM_Capture.png`.
   - Still ortho is a **concept fit** (campus ≈ 55 % of frame, clamped 10–15; play snap stays 10), shutter hold, `spawnShowcaseColony` stays **false**.
   - Commons → airlock → HAB plus CanFit pad / PWR / extractors. Leftovers may stamp; interior dirt is **not** force-filled.
5. Game view: **Scale 1x**. Do not free-orbit or zoom-to-pack.
6. Compare `SM_Capture.png` to `SM_MarsCampus_SpacedOverseer_Concept.png` at the same aspect (concept is 16:9; Game tab is short-wide — match **layout language**, not pixel crop).

### Judge ladder (when you have a still)

Use the Dream Loop tiers in the skill (shape → light → materials → detail). Cap the score if a lower tier fails. Empty dirt is a **pass**, not a miss.

This Cloud pass could not run an in-engine still (no Unity / Blender). **Do not invent a score.**

### After a still

- If hulls wash orange: do not raise Mars dust on `IndustrialArtDressing.BindBody` (Mars dust amount stays low so white reads).
- If ground looks flat: confirm `Assets/Resources/Environment/Textures/SM_Ground_Mars_*` imported (albedo Default, normal = Normal map, Repeat) and `PlanetGround` `_DetailTexAmount` is non-zero.
- If solar / canvas look plastic: confirm `Assets/Resources/Art/Materials/SM_Mat_*` imported Repeat.

### Bake (no Imagine required)

```bash
python Blender/scripts/sm_bake_ground_textures.py          # skips existing authored tiles
python Blender/scripts/sm_bake_look_materials.py          # hull / steel / solar / canvas
# python Blender/scripts/sm_bake_ground_textures.py --force  # only if replacing Imagine tiles
```

Then in Unity: **Solar Majesty → Verify Environment Assets**.
