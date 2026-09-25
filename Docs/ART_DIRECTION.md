# Art Direction

**Integration note — 2026-09-22:** The later Commons lock is the smooth command-dome citadel (2026-09-17); older geodesic instructions and screenshots below are historical. The boxy tan HAB, four-cell landmark yards, and animated units are retained. Phase 4 exit remains blocked pending a fresh visual review.


**Hero roster v2 — 2026-09-22:** The ten specialist heroes are armoured humanoid knights (LO-COU/DEF/ENG/GEO/HAR/MED/SCT/SEN/SRV/TRF-1) per [`ConceptSheets/Heroes_v2/`](../ConceptSheets/Heroes_v2/). Drones, tracked chassis and rovers are retired for heroes. The pipeline, rig and clips are documented in [Docs/HERO_ROSTER_V2.md](HERO_ROSTER_V2.md).

**Mandatory keywords in every prompt:**

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

**Phase 4 EXIT look (extends the lock, does not replace it):** Production checklist lives in [Docs/Roadmap/05_PHASE_4_VISUAL_TARGET.md](Roadmap/05_PHASE_4_VISUAL_TARGET.md). The claim is Aaron’s **2026-09-07 spaced ITS campus** vs [`Docs/Roadmap/SM_MarsCampus_SpacedOverseer_Concept.png`](Roadmap/SM_MarsCampus_SpacedOverseer_Concept.png) — empty dirt is OK, no interconnect tube webs, geodesic Commons, distant horizon haze, spacesuited crossings. Fun cartoon Majesty-2 overseer feel; room for rocks / foliage / creatures and discovery. `SM_MarsCampaign_VisualTarget.png` is **retired**. Bake-off PRs **#26 / #27 / #28** are **not** the EXIT claim. Do **not** stamp Phase 4 EXIT. Append the Phase 4 keyword extensions when briefing Imagine / Blender. Squad-command chrome is HUD inspiration only.

## Dream Loop look target (spaced Mars — Aaron 2026-09-07)

Locked north star: [`Docs/Roadmap/SM_MarsCampus_SpacedOverseer_Concept.png`](Roadmap/SM_MarsCampus_SpacedOverseer_Concept.png) — ITS language, not the prior tube-web campus. Capture protocol and “what NOT to do” live in [`Docs/Roadmap/DREAM_LOOP_MARS_LOOK.md`](Roadmap/DREAM_LOOP_MARS_LOOK.md). Skill tree: [`.cursor/skills/dream-loop/`](../.cursor/skills/dream-loop/) (vendored [achimala/dream-loop](https://github.com/achimala/dream-loop); Pro by default).

**Aaron look brief (2026-09-07)**

1. **No tubes, airlocks or ports** between buildings. Every building stands alone on open ground (airlocks were removed from the game 2026-09-25).
2. **More space** between buildings — empty dirt is intentional; do not pack AABB or fill dirt with leftover sockets.
3. **Distant haze** toward the horizon (concept language).
4. **Colony Commons** is the locked smooth command-dome citadel (plinth + drum + orange band + sphere + cupola) — not geodesic lattice, not a grey box.
5. Colonists / specialists crossing open ground should **read as spacesuited** (vulnerable between buildings). Docs + still dressing notes only. Do **not** invent new `FlagTypes` or rewrite `SpecialistBrain`.

Phase 4 keyword extensions (append, do not replace the lock):

> reddish Mars regolith and hazy orange sky, distant horizon haze, long low-angle shadows, white/black/orange industrial SpaceX-adjacent campus, **no pressurized interconnect tubes**, separate dirt pads with empty yards between buildings, large central white **geodesic / polyhedron** command dome with orange trim, free-standing buildings (no airlocks or tubes), blue-glow solar arrays, circular landing pad with white Starship-like rocket, spacesuited figures on open ground, spacious campus pads, negative space, overseer strategy view

Do not add “packed city / fill every dirt patch / tube-web campus” to Imagine or Blender briefs.

## Still dressing notes (spacesuit crossings)

Outdoor workshop robots stay the live specialist meshes. When a still or dressing note shows a **human / colonist crossing open dirt** between pads, they must read as **spacesuited** — the concept’s isolation language (no pressurized tube to hide in). Do not add a new unit class, `FlagType`, or `SpecialistBrain` path for this. HAB interiors remain the human living space.
