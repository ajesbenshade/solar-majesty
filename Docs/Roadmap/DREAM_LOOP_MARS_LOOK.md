# Dream Loop — Spaced Mars look target

**Status:** Aaron look brief 2026-09-07 locked. **Not a Phase 4 EXIT.** Do not stamp exit. Do not start Phase 5.

Bake-off PRs **#26 / #27 / #28** (Sol / Fable / Opus Captures) were **rejected**. They are **not** the EXIT claim. EXIT = Aaron look-clear vs the locked concept under this brief.

Working files (retries, judge notes) live in `.dream-loop/` and are gitignored. Reuse the skill at [`.cursor/skills/dream-loop/SKILL.md`](../../.cursor/skills/dream-loop/SKILL.md) ([achimala/dream-loop](https://github.com/achimala/dream-loop), MIT).

## LOCKED concept (north star — keep this file)

[`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — in-engine-style isometric overseer still. **ITS language**, not the prior tube-web campus.

Judge stills against **this** image, not the retired packed `SM_MarsCampaign_VisualTarget.png`, and not bake-off `SM_Capture.png` frames from #26 / #27 / #28. Do **not** regenerate, replace, or overwrite this PNG.

### Aaron look brief (2026-09-07)

1. **Forgo interconnect tubes** between buildings. `SpawnTubeRuns` / between-yard tube web are **gone** (not a `StampTubeRuns` flag). Leftover `CampusDress_TubeRuns` roots are destroyed. Square Lego airlocks may remain as building **ports**.
2. **More space** between buildings — empty dirt is intentional; do not pack AABB or fill leftover sockets. Landmark yards use `MinYardGapCells` **4**. Leftover Inn / wonder / extra packing stays skipped on `StampPhase4*`.
3. **Distant haze** toward the horizon. Live haze is Mars exp2 fog 0.014 plus `MarsHazeRoot` cards; do not wash campus hulls.
4. **Polyhedron / geodesic Commons** — faceted `SM_CommonsGeodesic` + carbon lattice, not a smooth hemisphere-only.
5. Colonists crossing open ground **read as spacesuited** (`CampusDress_Colonists` dressing). Do **not** invent new `FlagTypes` or rewrite `SpecialistBrain`.

### What the concept is

- High isometric / Majesty-2 overseer camera
- **Spaced campus pads** with empty red regolith between yards — no tube corridors; landmark yards use `MinYardGapCells` 4
- Hero cluster: Commons **geodesic / polyhedron** dome (not a smooth hemisphere), HAB-1 cylinder on a short square airlock port, circular pad + Starship, small solar field, one extractor, optional canvas porch
- **Distant orange haze** toward the horizon (fog 0.014 + `MarsHazeRoot`), strong key light, long readable shadows
- **Spacesuited colonists** crossing open dirt between buildings (`CampusDress_Colonists` dressing — not SpecialistBrain, not FlagTypes)
- Dusty metal / white thermal / carbon / orange / solar glass / canvas — practical colony, not toy, not Elden Ring clutter
- Open-ground crossings read as **spacesuited** (isolation between pads)

### Aaron brief after #29 (keep)

`StillCaptureHold`, Mars Scale 1x, docked Lego airlock arms OK, `spawnShowcaseColony` false, Colony Commons name, five HUD chips. Bake-off `#26` / `#27` / `#28` stay parked. Do not reintroduce between-yard tubes. **Not a Phase 4 EXIT.**

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
   - Commons → airlock port → HAB plus CanFit pad / PWR / extractors on **island gaps** (`MinYardGapCells` 4). Leftovers do **not** stamp. Interior dirt is **not** force-filled. **No** `CampusDress_TubeRuns`.
   - `FitStillOrtho` stays at play ortho 10 — it must not crop into a packed look.
5. Game view: **Scale 1x**. Do not free-orbit or zoom-to-pack.
6. Compare `SM_Capture.png` to `SM_MarsCampus_SpacedOverseer_Concept.png` at the same aspect (concept is 16:9; Game tab is short-wide — match **layout language**, not pixel crop).

### Judge ladder (when you have a still)

Use the Dream Loop tiers in the skill (shape → light → materials → detail). Cap the score if a lower tier fails. Empty dirt is a **pass**, not a miss. A tube-web or packed-AABB frame **fails** shape against this concept.

This Cloud pass could not run an in-engine still (no Unity / Blender). **Do not invent a score.**

### After a still

- If hulls wash orange: do not raise Mars dust on `IndustrialArtDressing.BindBody` (Mars dust amount stays low so white reads).
- If ground looks flat: confirm `Assets/Resources/Environment/Textures/SM_Ground_Mars_*` imported (albedo Default, normal = Normal map, Repeat) and `PlanetGround` `_DetailTexAmount` is non-zero.
- If the horizon does not recede: confirm Mars `DemoAtmosphere` fog is on (exponential-squared, campus unfogged, distant haze).
- If interconnect tubes appear: `SpawnTubeRuns` must stay **gone**; `RefreshTubes` only enables docked Lego ports and destroys leftover `CampusDress_TubeRuns` roots.

### Bake (no Imagine required)

```bash
python Blender/scripts/sm_bake_ground_textures.py          # skips existing authored tiles
python Blender/scripts/sm_bake_look_materials.py          # hull / steel / solar / canvas
# python Blender/scripts/sm_bake_ground_textures.py --force  # only if replacing Imagine tiles
```

Then in Unity: **Solar Majesty → Verify Environment Assets**.
