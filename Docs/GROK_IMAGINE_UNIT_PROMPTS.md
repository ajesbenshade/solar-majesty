# Grok Imagine — Hero Unit Turnarounds

Generate **one unit per run**. Paste the **style lock** into every prompt (replace `[STYLE LOCK]`).

When images are ready, drop them here:

| File | Unit | Status |
|------|------|--------|
| `ConceptSheets/SM_Unit_ScoutDrone_Turnaround.jpg` | Scout | **In** — Imagine [post](https://grok.com/imagine/post/e4dfd56c-f412-4106-9d59-d8959f4ebac6); Blender blockout refined |
| `ConceptSheets/SM_Unit_EngineerBot_Turnaround.jpg` | Engineer | **In** — v2 habitat-builder sheet (backpack + chest dock); Blender rebuilt |
| `ConceptSheets/SM_Unit_DefenseMech_Turnaround.jpg` | Defense | **In** — tracked Guardian Class sheet; Blender blockout refined |
| `ConceptSheets/SM_Unit_DustStalker_Turnaround.jpg` | Stalker | **In** — creature sheet; Blender blockout refined |

Optional second pass (scale): same unit isometric next to a white HAB module → `*_Scale.jpg`.

Then tell the agent: **“turnarounds are in ConceptSheets — refine Blender blockouts.”**

---

## Style lock (every prompt)

```
isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style
```

**Tip:** ask Imagine for **orthographic turnaround on pure mid-grey** first.

---

## 1) Scout Drone

```
Character turnaround sheet, three-quarter + front + side, single tall thin autonomous scout drone for a lunar overseer RTS. Whip antenna, small cyan sensor head, white thermal shell with black structural bands and one high-vis orange beacon. Readable from isometric distance, no human face, no guns, clean hard-surface robotics. Neutral grey lunar ground, soft harsh sunlight. isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style
```

## 2) Engineer Bot

```
Character turnaround sheet, three-quarter + front + side, squat industrial engineer robot with toolbox hip module and cyan visor strip. White/black Starship plating, orange service stripe, chunky proportions for Majesty readability. Builder silhouette, no weapons, modular docking ports. Neutral grey lunar ground, soft harsh sunlight. isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style
```

## 3) Defense Mech

```
Character turnaround sheet, three-quarter + front + side, wide low combat chassis with left shield plate and right shoulder block. White shell, black armor banding, orange hazard accent, red-tinted hull panels for class identity. Shielded guardian silhouette, no gore, readable from isometric camera. Neutral grey lunar ground, soft harsh sunlight. isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style
```

## 4) Dust Stalker

```
Creature turnaround sheet, three-quarter + front + side, low elongated lunar predator fauna (not a robot). Dark carapace, spine ridges, four legs, paired orange glowing eyes. Alien-industrial threat silhouette for a Majesty-like RTS, readable at distance, no blood, no human features. Neutral grey lunar ground, soft harsh sunlight. isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style
```

---

## After sheets land

1. Save JPGs under `ConceptSheets/` with the names above.
2. Agent refines `Blender/scripts/sm_unit_blockouts.py` against the sheets.
3. Re-export FBX → `Assets/Resources/Units/`.
4. Unity: **Solar Majesty → Build Demo Content Assets**.
