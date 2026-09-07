# Dream Loop — Spaced Mars look target

**Status:** look pass 3, read against a real Capture from Aaron. **Not a Phase 4 EXIT.** Do not stamp exit. Do not start Phase 5.

Working files (retries, judge notes, local stills) live in `.dream-loop/` and are gitignored.
Reuse the skill at [`.cursor/skills/dream-loop/SKILL.md`](../../.cursor/skills/dream-loop/SKILL.md)
([achimala/dream-loop](https://github.com/achimala/dream-loop), MIT).

## Concept (north star — locked, do not regenerate)

[`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — in-engine-style
isometric overseer still.

Judge stills against **this** image, not the retired packed `SM_MarsCampaign_VisualTarget.png`.

### What the concept is

- High isometric / Majesty-2 overseer camera
- **Spaced campus pads** with empty red regolith between yards
- Hero cluster: Commons geodesic dome, HAB-1 cylinder, short tube + airlock, circular pad +
  Starship, small solar field, one extractor, canvas porch
- Thin Mars haze, strong key light, long readable shadows
- Dusty metal / white thermal / carbon / orange / solar glass / canvas — practical colony, not toy,
  not Elden Ring clutter

### What it is NOT

- Do **not** pack every dirt patch
- Do **not** fill interior 4×4 sockets to chase density
- Do **not** zoom the still camera inside play ortho 10 to crop out empty ground
- Do **not** treat leftover Inn / wonder / extra HAB as a density gate

---

## Game Designer / Mac Unity — how to shoot the still

Unity Editor is required; the Cloud VM cannot run Play Mode. Shoot the same way every time so
successive stills are comparable.

1. Open the project in **Unity 6000.5.x** (Mac).
2. Open scene `Assets/Scenes/LunarOutpost_Sandbox.unity`.
3. **Solar Majesty → Render → Configure URP For Look Target** — once per machine.
   Sets depth + opaque textures, MSAA 4×, HDR, 4 shadow cascades at 120 m, SSAO (blue noise,
   intensity 1.1, radius 0.35), decals, and adds `SolarMajesty/Hull` + `SolarMajesty/PlanetGround`
   to always-included shaders. The repo's URP asset ships MSAA 1, so **skipping this step is why
   hull edges alias** in a fresh clone.
4. Optional, only if the ground or look tiles look wrong: rebake them (see *Bake* below), then
   **Solar Majesty → Verify Environment Assets** and confirm `OK` for Mars albedo / Mars normal /
   white hull / steel / solar / canvas / dusty metal.
5. **Solar Majesty → Render → Capture Mars Still**.
   - Interactive editor only — batch mode refuses (`CaptureStill.cs`).
   - Unlocks the campaign, sets the Mars body seed, enters Play, waits ~120 frames, stamps the
     still campus, waits ~90 more, snaps the camera, then writes **`Docs/Roadmap/SM_Capture.png`**.
   - Uses play campus **ortho 10**, holds the shutter so cut / narrative modals stay suppressed,
     and leaves `spawnShowcaseColony` **false**.
   - Commons → airlock → HAB plus CanFit pad / PWR / extractors. Leftovers may stamp; interior
     dirt is **not** force-filled.
6. Game view: **Scale 1×**, Free Aspect. Do not free-orbit and do not zoom to pack the frame.
7. Rename the result into the archive series (`SM_MarsCampaign_PlayModeCampusStillN.png`) and
   compare against the concept.

### Reading the comparison

The concept is a **perspective** frame with a horizon and a sky band. Both capture paths are
**orthographic** and pitched ~30°, so the ground plane fills the shot and there is no sky in
frame at all. Match the **layout language and surface quality**, not the crop. Empty dirt is a
**pass**, not a miss.

### Judge ladder (when you have a still)

Use the Dream Loop tiers in the skill: shape → light → materials → detail. Cap the score if a
lower tier fails. **Do not invent a score without a still** — this Cloud pass could not run one.

---

## Read of Aaron's Capture (pass 3 input)

Aaron shot `SM_Capture.png` (1024×667, Mars · Sol 1 · CAMPAIGN, POP 2/16, tutorial "Colony Commons
is down"). **HUD chrome passes**: five chips REG/ICE/MET/PWR/BEDS with rates, OVERSEER panel with
the body switcher, ACTIVE BOUNTIES with the three campaign beats, FLAG LOG, MAJESTY COLONY minimap,
BLD/FLG/TEC/CAM/PTY/MENU dock, THREAT 23%. That part of the sheet is done.

The world read did not, and the causes were code faults rather than missing art:

1. **The frame was a close-up, not a campus.** `FitStillOrtho` discarded the fit math it already
   had and returned `PlayCampusOrthoSize` outright. The rule is "do not zoom **in** to pack the
   AABB" — but pinning the ortho while the campus grows past ~20 m of frame does not preserve empty
   dirt, it crops the campus and squeezes the dirt out. The pad and rocket fell off the left edge
   behind the OVERSEER panel. Now the ortho fits the AABB × `StillDirtHeadroom`, floored at the play
   ortho; the floor is what enforces the rule.
2. **The campus was one contiguous mass.** Every candidate list in `CollectCandidates` leads with
   gap 0, so pad, power, and both extractors butted straight onto the dome. Free-standing yards now
   try a spaced ring first (`TryNextSpaced`, `SpacedYardGapCells`). HAB chains still dock flush —
   those are tube-linked.
3. **Orange glowed.** Safety orange carried an emissive in *both* material paths (1.4 HDR red on
   the lit path) and on every dock collar, so each tube joint read as a hot ring where the concept
   has thin matte trim. Orange is paint now; collars are slimmer.
4. **A blown-out white dome sat over the campus.** The shield bubble at alpha 0.16 with 0.82
   smoothness caught a broad specular sheet that clipped to solid white — the brightest thing in
   frame. Fainter and matte now, and the status pips drop from 1.6× HDR.
5. **Black baseplates under every building.** Hero-kit foundation slabs ran to ~0.94 of the Lego
   cell. Inset to `PlinthFill` so the graded dust apron shows around the footing; the concept has
   no slab at all.

Not yet addressed, and worth a decision: **the HUD covers roughly a third of the frame** (OVERSEER
panel left, BOUNTIES right, minimap and dock bottom) and the left panel occludes whatever landmark
sits west of the Commons. The concept has no HUD. Widening the frame helps, but if the look still is
meant to sell the campus, a still-mode HUD dim or a panel-free variant is the lever.

## Capture vs concept — where the gap stands

Pass 2 changes are code and asset level, verified by measurement and by reading the bake output
directly. None of them have been seen in engine yet.

### Closed in pass 3 (from the Capture read above)

| Concept read | What was wrong | Change |
|---|---|---|
| Campus sitting in open regolith | `FitStillOrtho` returned the play ortho outright, so a campus wider than ~20 m of frame got cropped and the dirt squeezed out | Fits the AABB × `StillDirtHeadroom`, floored at `PlayCampusOrthoSize` |
| Six separate pads with dirt between | Every candidate list leads with gap 0, so yards butted onto the dome as one mass | `TryNextSpaced` tries a spaced ring first for pad / power / extractors / Inn / wonder / solar / Defense |
| Orange as thin matte trim | Safety orange was emissive in both material paths (1.4 HDR red on the lit path) and on every collar | Orange is paint; collars slimmer |
| Clean hulls | Shield bubble at alpha 0.16 / smoothness 0.82 clipped to a solid white dome | Fainter, matte; status pips down from 1.6× HDR |
| Buildings on graded dirt | Foundation slabs ran to ~0.94 of the Lego cell and read as black baseplates | Inset to `PlinthFill` (0.82) |

### Closed in pass 2

| Concept read | What was wrong | Change |
|---|---|---|
| Wall-to-wall pebbles with shadow between them | Ground sampled the tile once at 8.5 m, so only broad ripple survived; nothing darkened the crevices | `SM_PlanetGround` takes a second tap at ~2.3 m, level-preserved against a near-mean mip of itself, and derives a cavity occlusion term from it |
| Regolith, not a repeating pattern | The Mars tile's noise lattice did not wrap: seam delta **16.1** against an interior adjacency of **10.8**, i.e. a visible join every 8.5 m | Tile rebaked periodic; seam now **at or below** interior adjacency on both axes |
| Stones you can pick out | Tile "pebbles" were single-pixel hash noise — sub-millimetre at 120 px/m, gone after one mip | Two sparse stone fields (~10-20 cm cobbles, ~3-7 cm chips), lit from the Mars key-light side with cast shadows. Pebble-scale contrast **7.5 → 11.0** |
| Loose litter between the pads | 24 ring boulders + 6 outcrops against bare dirt | 96 gravel chips on a jittered spiral, `MarsGravelRoot` |
| Panelled, weathered pad / extractor / ship | Those kits are named `Dress_*`, which `IndustrialArtDressing` skips, so they never reached `SolarMajesty/Hull` and rendered flat | Skipped prims with real surface area build a hull material directly, keeping their authored colour |
| Grimy near the ground, clean up high | Hulls were uniform top to bottom; Mars dust is pinned at 0.04 to stop the orange wash | `SolarMajesty/Hull` gains a **neutral** splash-back grime term on vertical faces, fading out with height |
| Near-black PV glass | `SolarEmit` was a 2.15 HDR blue — neon strips, not cells | Emissive cut ~70%, panels carry the glow through smoothness instead |
| Swept dirt under each pad | One hard cylinder lerped 0.18 *toward* GroundLight read as a grey slab | Apron darkened and a wider faint skirt added so the edge fades |
| Enterable dome | Blank drum, no skirt | Orange base skirt plus orange-framed entries with stoop, tread, and rail on the two free diagonals (cardinals belong to the dock ports) |
| Raised HAB you can reach | No external access | Four-step stair with steel rails off the side door |
| Ship standing on the pad | Two fins | Four splayed landing legs with feet |
| Pad approach circle | Four rim lights | Eight rim posts with lamps |
| Vertical read on the solar field | Four flat rows | Lattice comms mast with bands and a dish |
| Sphere among the extractor pipes | All cylinders | Pressure sphere on a four-leg cradle with a riser |
| Canvas | Nothing in engine hit the Canvas slot; `InnCanopy` was carbon | `IceCanvasShade` / `InnCanvasShade` on steel posts over a railed work deck, deliberately un-prefixed so the slot remap reaches the authored fabric tile |

### Still open

- **No sky, horizon, or aerial-perspective gradient in frame.** The concept's hazy sky band and
  distant mesas need a **perspective** camera; both capture paths are orthographic pitched ~30° and
  see ground and no sky at any zoom. Pulling the still ortho back widens the ground — it does not
  bring a horizon into frame. An **Aaron decision**, not a bug: either accept the layout-language
  match, or authorise a second perspective hero shot alongside the ortho still. The Mars haze
  multiplier and sky-lifted fog colour landed anyway, so they pay off in any perspective capture.
- **The HUD covers roughly a third of the Capture**, and the left OVERSEER panel occludes whatever
  landmark sits west of the Commons. The concept has no HUD. Widening the frame helps; if the look
  still has to sell the campus, the lever is a still-mode HUD dim or a panel-free capture variant.
  Also an Aaron call — the roadmap wants the chrome verified in the same shot.
- **Commons is a latitude/longitude dome, not a geodesic one.** The concept's triangulated facets
  are a mesh change (`sm_hero_building_kits.py`), not a dressing change.
- **Ground relief is albedo + scatter, not a heightmap.** Unchanged Phase 4 non-goal.
- **Earth ground tile is still the authored one** and still non-periodic. Only Mars was rebaked;
  Earth is out of scope for this pass. See the `--force` note below.
- **Nothing here is verified in engine.** Every claim above is a code or bake-level measurement.

---

## After a still

- If hulls wash orange: do not raise Mars dust on `IndustrialArtDressing.BindBody` (Mars stays at
  0.04 so white reads). Reach for `_GrimeAmount` instead — it is neutral by design.
- If the ground looks flat: confirm `Assets/Resources/Environment/Textures/SM_Ground_Mars_*`
  imported (albedo Default, normal = Normal map, Repeat) and that `PlanetGround` has non-zero
  `_DetailTexAmount` **and** `_GritAmount`. Both are set in
  `PlanetaryMapDressing.BindAuthoredGroundDetail`.
- If the ground looks noisy or busy: lower `_GritCavity` before `_GritAmount` — the cavity term is
  what carries the contrast.
- If solar or canvas look plastic: confirm `Assets/Resources/Art/Materials/SM_Mat_*` imported Repeat.
- If the awning renders as a white box: the Canvas slot is name-driven. Run the
  `CanvasPorchNames_MapToTheFabricSlot` EditMode test — a token-order change in
  `IndustrialArtDressing.TryFromToken` breaks it silently.

## Bake (no Imagine required)

```bash
# Mars regolith: vectorised, periodic, needs numpy. Safe to force.
python Blender/scripts/sm_bake_ground_textures.py --mars --force

# Hull / steel / solar / canvas look tiles.
python Blender/scripts/sm_bake_look_materials.py
```

Then in Unity: **Solar Majesty → Verify Environment Assets**.

`--force` is **scoped per body** (`--mars` / `--earth`) on purpose. A global `--force` replaced the
authored Earth meadow tile with flat scalar noise from `earth_pixel`, which is not what produced
the committed tile. Do not force Earth unless you intend to replace it.
