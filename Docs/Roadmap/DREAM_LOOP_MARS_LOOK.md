# Dream Loop — Spaced Mars look target

**Status:** first look pass. **Not a Phase 4 EXIT.** Do not stamp exit. Do not start Phase 5.

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

## Game Designer / Mac Unity — still vs concept

Unity Editor is required (this Cloud VM cannot run Play Mode). Shoot the same way every time:

1. Open the project in **Unity 6000.5.x** (Mac).
2. Scene: `Assets/Scenes/LunarOutpost_Sandbox.unity`.
3. **Solar Majesty → Render → Configure URP For Look Target** (once per machine).
4. **Solar Majesty → Render → Capture Mars Still**.
   - Interactive editor only (batch mode refuses).
   - Menu writes `Docs/Roadmap/SM_Capture.png`.
   - Uses play campus **ortho 10**, shutter hold, `spawnShowcaseColony` stays **false**.
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
