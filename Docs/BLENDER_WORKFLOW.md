# Solar Majesty — Blender Asset Workflow

## Confirmed image locations

All concept sheets currently live under:

```
/Users/aaronesbenshade/solar-conquest/ConceptSheets/
```

| File | Status |
|------|--------|
| `SM_HAB-1_HabitatModule_Turnaround.jpg` | Present |
| `SM_LAB-1_LaboratoryModule_Turnaround.jpg` | Present |
| `SM_CMD-1_OPS-1_CommandOps_Turnaround.jpg` | Present (includes modular connector) |
| `SM_CommandDome_CentralHub_Turnaround.jpg` | Present |
| `SM_PWR-1_PowerNode_SolarArrays.jpg` | Present |
| `SM_Starship_LandingPad_UprightStack.jpg` | Present |
| `SM_MarsColony_BaseOverview_Isometric.jpg` | **Missing** — drop into `ConceptSheets/` when ready |

Duplicates also exist in `~/Downloads/` (safe to ignore once project copies are in `ConceptSheets/`).

---

## Modeling priority (simplest → complex)

| Order | Asset | Source sheet | Sheet scale |
|------:|-------|--------------|-------------|
| a | Modular tube connector | CMD/OPS sheet (inset) | ~4 m long, dock OD ~2.8 m |
| b | **HAB-1** | HAB-1 turnaround | **Ø 8.0 m × L 12.0 m** |
| c | LAB-1 | LAB-1 turnaround | Ø 4.5 m × L 8.7 m |
| d | CMD-1 | CMD/OPS sheet | ~building scale, ~6–8 m |
| e | OPS-1 | CMD/OPS sheet | similar to CMD |
| f | Command Dome | Dome turnaround | large hub |
| g | PWR-1 + arrays | PWR sheet | power node |
| h | Landing pad | Starship/pad sheet | pad first; Starship placeholder |

**Style lock (from sheets):** white thermal shell · black structural bands · high-vis orange access/service · Majesty-readable silhouettes · modular docking interface shared across habitat modules.

---

## Project layout

```
solar-conquest/
├── ConceptSheets/                 # 2D reference only (not Unity assets)
├── Blender/
│   ├── SolarMajesty_Modules.blend # working file (generated)
│   ├── scripts/
│   │   └── sm_setup_and_blockout.py
│   ├── exports/                   # FBX + GLB for Unity
│   └── references/                # optional copies of JPGs
└── Assets/                        # Unity (later: import FBX here)
```

### Collection organization (in the .blend)

| Collection | Contents |
|------------|----------|
| `01_References` | Image Empties (Add → Image → Reference) |
| `02_ModularKit` | Tube connector (shared docking kit) |
| `03_HAB1` | HAB-1 blockout / final |
| `04_LAB1_WIP` … `08_LandingPad_WIP` | Empty WIP slots |
| `09_ExportReady` | Objects ready for FBX |

---

## First concrete Blender actions

### Option A — Auto setup (recommended)

In Terminal:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python /Users/aaronesbenshade/solar-conquest/Blender/scripts/sm_setup_and_blockout.py
```

This will:

1. Create metric scene (1 unit = 1 m)
2. Load all available concept sheets as **Reference** empties
3. Create SM material palette (Principled BSDF)
4. Build **Modular Tube Connector** + **HAB-1** blockout
5. Save `Blender/SolarMajesty_Modules.blend`
6. Export FBX/GLB to `Blender/exports/`

Then open:

```bash
open /Users/aaronesbenshade/solar-conquest/Blender/SolarMajesty_Modules.blend
```

### Option B — Manual reference load (if you prefer GUI)

1. Open Blender → **File → New → General**
2. **Scene Properties → Units → Metric**, Unit Scale `1.0`, Length `Meters`
3. Delete default cube
4. For each sheet: **Add → Image → Reference**, pick file from `ConceptSheets/`
5. Rotate references so they face the camera you model against (typically 90° on X for “wall” sheets)
6. Lock references: select empty → **Sidebar (N) → Item → lock transforms** (optional)
7. Model only in collections `02_ModularKit` / `03_HAB1`

---

## Material palette (Principled BSDF)

| Name | Base color (approx) | Metallic | Roughness | Use |
|------|---------------------|----------|-----------|-----|
| `SM_White` | 0.85, 0.86, 0.88 | 0.12 | 0.42 | Thermal shell |
| `SM_Black` | 0.03, 0.03, 0.035 | 0.35 | 0.48 | Structural bands |
| `SM_Graphite` | 0.12, 0.13, 0.14 | 0.40 | 0.40 | Systems / trim |
| `SM_Orange` | 0.95, 0.38, 0.05 | 0.08 | 0.35 | Access / warning |
| `SM_Steel` | 0.45, 0.47, 0.50 | 0.70 | 0.32 | Hatches / collars |
| `SM_Glass` | 0.55, 0.62, 0.70 | 0.00 | 0.08 | Viewports later |

Do **not** invent new accent colors; stick to the sheet legend.

---

## HAB-1 blockout fidelity notes (from sheet)

- Horizontal cylinder, **docking axis = X**
- **Diameter 8 m**, **length 12 m**
- Black bands at ends + mid waist
- Orange docking rings + large side access hatch
- Front circular hatch plate; rear docking collar
- Dual skid pairs / feet — **origin at ground pads**
- Top utility box slightly off-center (sheet)

Refine from blockout by matching orthographic front/side/rear panels on the HAB sheet—do not freehand new proportions.

---

## Unity export settings

### FBX (primary)

| Setting | Value |
|---------|--------|
| Path Mode | Auto |
| Apply Scalings | **FBX All** |
| Forward | **-Z** |
| Up | **Y** |
| Apply Unit | On |
| Smoothing | Face |
| Leaf bones | Off |
| Selection only | On (export one building at a time) |

### Import in Unity

1. Drop FBX into `Assets/Art/Buildings/` (create as needed)
2. Model scale **1** (we modeled in meters)
3. Generate Lightmap UVs if using baked lighting
4. Replace ScriptableObject `BuildingData.prefab` references when ready

### GLB

Also exported for quick look in other viewers; FBX is preferred for Unity workflows.

---

## Blockout inventory (current)

| Object | Approx size (W×D×H m) | Collection / park |
|--------|------------------------|-------------------|
| `SM_ModularTubeConnector` | 4.4 × 2.8 × 2.8 | origin |
| `SM_HAB1_HabitatModule` | 13.1 × 8.3 × 8.4 | Y=10 |
| `SM_CMD1_CommandBuilding` | 11.4 × 11.1 × 6.5 | (20, 0) |
| `SM_OPS1_OperationsUnit` | 9.7 × 9.4 × 5.7 | (20, 14) |
| `SM_CommandDome_CentralHub` | 20.8 × 20.8 × 9.9 | (45, 0) |
| `SM_PWR1_PowerNode` | 8.0 × 9.4 × 6.3 | (20, -18) |
| `SM_PWR1_SolarArray` | 10.4 × 12.4 × 0.1 | (28, -18) |
| `SM_LandingPad` | 41 × 40 × 1.3 | (-50, 0) |
| `SM_Starship_Placeholder` | 4.6 × 7.1 × 44 | on pad |

**Missing from blend:** `SM_LAB1_LaboratoryModule` (Ø4.5 × L8.7) — add with a dedicated script next if needed.

Regenerate only remaining modules (does not touch HAB / connector):

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  Blender/SolarMajesty_Modules.blend \
  --python Blender/scripts/sm_add_remaining_blockouts.py
```

## Next modeling session

1. Add LAB-1 blockout if still missing  
2. Subdivide/bevel HAB-1 / modules to match panel lines on sheets  
3. Mate docking sockets to **connector** flush  
4. Replace Starship placeholder with refined stack when ready  

---

## Git note

Consider tracking:

- `Blender/scripts/`
- `Docs/BLENDER_WORKFLOW.md`
- optionally `Blender/exports/*.fbx` if small enough  

Usually **do not** commit huge `.blend` binaries or raw concept JPGs if you already host art elsewhere—or use Git LFS.
