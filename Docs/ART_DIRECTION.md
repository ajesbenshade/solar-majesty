# Art Direction

**Mandatory keywords in every prompt:**

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

**Phase 4 EXIT look (extends the lock, does not replace it):** Production checklist lives in [Docs/Roadmap/05_PHASE_4_VISUAL_TARGET.md](Roadmap/05_PHASE_4_VISUAL_TARGET.md). The claim is a **spaced campus** — empty dirt is OK, fun cartoon Majesty-2 overseer feel, room for rocks / foliage / creatures and discovery. `SM_MarsCampaign_VisualTarget.png` is **retired**; do not chase that sheet’s packed density. Append the Phase 4 keyword extensions when briefing Imagine / Blender. Squad-command chrome is HUD inspiration only.

## Dream Loop look target (spaced Mars)

In-engine stills are judged against [`Docs/Roadmap/SM_MarsCampus_SpacedOverseer_Concept.png`](Roadmap/SM_MarsCampus_SpacedOverseer_Concept.png) — an isometric overseer campus with **breathing room**. Capture protocol, bake commands, and “what NOT to do” live in [`Docs/Roadmap/DREAM_LOOP_MARS_LOOK.md`](Roadmap/DREAM_LOOP_MARS_LOOK.md). Skill copy: [`.cursor/skills/dream-loop/SKILL.md`](../.cursor/skills/dream-loop/SKILL.md).

Phase 4 keyword extensions (append, do not replace the lock):

> reddish Mars regolith and hazy orange sky, long low-angle shadows, white/black/orange industrial SpaceX-adjacent campus, pressurized corridor tubes linking habs, large central white command dome with orange trim, blue-glow solar arrays, circular landing pad with white Starship-like rocket, spacious campus pads, negative space, overseer strategy view

Do not add “packed city / fill every dirt patch” to Imagine or Blender briefs.

### Surface language (pass 2)

- **One hull surface across the campus.** Every module, pad, extractor, and ship prim renders
  through `SolarMajesty/Hull`: world-space panel seams, blotchy wear, settled dust on up-faces, and
  a **neutral** splash-back grime that climbs the lower hull. Kits whose prims are named `Dress_*`
  (and so skipped by the slot remap) build that material themselves via
  `IndustrialArtDressing.BuildKitHullMaterial`. Do not add flat URP Lit surfaces to a hero kit.
- **Grime is neutral warm grey, never the body's ground colour.** Tinting hulls with `GroundLight`
  is what turned the Mars stills orange; Mars dust stays pinned at 0.04 for that reason.
- **Ground is two taps of one tile.** `SM_PlanetGround` samples the authored `SM_Ground_*` map at
  metre scale for ripple and again at pebble scale for grit, and darkens the crevices from the
  tight tap. Authored ground tiles must be **seamless** — the tile repeats every 8.5 m and a
  non-periodic lattice reads as a grid at overseer range.
- **Solar is near-black PV glass with a sheen**, not an emissive strip. The blue-glow keyword is
  carried by smoothness and a dim emissive, not HDR brightness.
- **Canvas is the campus's one soft material.** The Canvas slot is name-driven: put `canvas`,
  `awning`, `tarp`, or `fabric` in the mesh or material name and keep it off the `Dress_` prefix.
