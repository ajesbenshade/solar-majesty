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
| `SM_CMD-1_OPS-1_CommandOps_Turnaround.jpg` | Present (includes modular connector) — Guild Hall = CMD-1 dress; Mining = OPS-1 annex |
| `SM_CommandDome_CentralHub_Turnaround.jpg` | Present — Colony Commons citadel |
| `SM_PWR-1_PowerNode_SolarArrays.jpg` | Present |
| `SM_Starship_LandingPad_UprightStack.jpg` | Present |
| `SM_MarsColony_BaseOverview_Isometric.jpg` | Present — Mars apron/clutter reference |

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
│   │   ├── sm_setup_and_blockout.py
│   │   ├── sm_unit_blockouts.py
│   │   └── sm_hero_building_kits.py
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

## Unit hero blockouts (Blender CLI)

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python Blender/scripts/sm_unit_blockouts.py
```

Creates `Blender/SolarMajesty_Units.blend`, exports `SM_Unit_*.fbx` to `Blender/exports/` and copies into `Assets/Resources/Units/`.

| Mesh | Silhouette |
|------|------------|
| `SM_Unit_ScoutDrone` | Hovering probe + four rotors + antenna (~2.8 m) |
| `SM_Unit_EngineerBot` | Small white biped + toolbox (~1.7 m) |
| `SM_Unit_DefenseMech` | Wide tracked chassis + shield (~2.0 m) |
| `SM_Unit_DustStalker` | Long spined predator (~2.6–3.7 m) |
| `SM_Unit_Medic` | Hover capsule + cyan cross + IV pole (~1.7 m long) |
| `SM_Unit_HarvesterBot` | Tracked hopper + orange blade + side arm (~1.55 m) |
| `SM_Unit_SurveyorBot` | Tripod mast + dish (~2.55 m) |
| `SM_Unit_TerraformerBot` | Tracked dozer, orange blade + rear rake (~2.5 m class) |
| `SM_Unit_CourierBot` | Six-wheel white-crate hauler (~1.45 m tall) |
| `SM_Unit_GeologistBot` | Six-wheel rover + vertical drill + vials (~1.35 m) |
| `SM_Unit_SentinelMech` | Squat turret + continuous treads (~1.55 m; not Defense) |
| `SM_Unit_RegolithMite` | Compact pillbug scavenger (~0.95 m long) |
| `SM_Unit_WattLeech` | White ray + cyan dorsal groove (~1.5 m) |
| `SM_Unit_IceWisp` | Seven-point ice-star (~1.15 m hover) |
| `SM_Unit_RockTick` | Wide crab + orange pincer tips (~1.15 m wide) |
| `SM_Unit_SoilCreeper` | Graphite isopod, one olive segment (~2 m long) |
| `SM_Unit_AshHopper` | Arched shrimp/flea, six stilt legs (~1.7 m) |

Then in Unity: **Solar Majesty → Build Demo Content Assets** to refresh `Unit_*` prefabs.

Scout / Engineer / Defense / Stalker / remaining classes + leftover fauna are sheet-matched against `ConceptSheets/` JPGs (same white/black/orange lock). Imagine scale bars that swapped length/height are ignored for Soil Creeper (~2 m) and Ash Hopper (~1.7 m).


### Hero building kits (Phase 4)

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python Blender/scripts/sm_hero_building_kits.py
```

Creates `Blender/SolarMajesty_HeroBuildings.blend`, exports `SM_Hero_*.fbx` to `Blender/exports/` and copies into `Assets/Resources/Buildings/`.

| Mesh | Silhouette (footprint) |
|------|------------------------|
| `SM_Hero_HAB` | HAB-1 horizontal cylinder on skids (4×4 / 6 m) |
| `SM_Hero_Commons` | Command-dome citadel (6×6 / 9 m). Radial stubs are Unity DockSleeves, not baked into the FBX. |
| `SM_Hero_Power` | PWR-1 node + solar field (4×4 / 6 m) |
| `SM_Hero_Farm` | Water-ice tanks + scaffold (4×4 / 6 m) |
| `SM_Hero_Camp` | Low regolith hopper + pipes (4×4 / 6 m) |
| `SM_Hero_Mine` | Twin silos + headframe (4×4 / 6 m) |
| `SM_Hero_Defense` | Angular bunker + roof turret (4×4 / 6 m) |
| `SM_Hero_LandingPad` | Circular pad (orange rings + H) + Starship stack (6×6 / 9 m) |
| `SM_Hero_GuildHall` | Plaza steps + banner mast (4×4 / 6 m) |
| `SM_Hero_LAB` | LAB-1 cylinder + dish (4×4 / 6 m) |
| `SM_Hero_ClimateLoom` | Lattice + spray boom (6×6 / 9 m) |
| `SM_Hero_AegisSpire` | Tall shaft + shield rings (6×6 / 9 m) |
| `SM_Hero_DeepArchive` | Stacked vaults + dish (6×6 / 9 m) |
| `SM_Hero_Workshop` | Hangar bay + orange door tracks (4×4 / 6 m) |
| `SM_Hero_WorkshopTall` | Tall hangar + roof turret (4×4 / 6 m) |
| `SM_Hero_Inn` | Porch-lantern rest hall (4×4 / 6 m) |

Play Mode (`ModularBuildingFactory`) prefers these FBX, then falls back to `HeroBuildingKits`. Cardinal **square airlocks** still attach in Unity. Do **not** `SetTintOverlay` the whole kit (that replaced orange/cyan with a flat hull). HAB / Commons / LAB / Power / pad are sheet-matched to `ConceptSheets/`. Workshops / Inn stay hangar / porch-lantern kits.


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
| `SM_PWR1_SolarArray` | farm of PV wafers on pylons (~10×12 m) | (28, -18) |
| `SM_Crater_Small` | Ø ~5 m rim + pit | Environment |
| `SM_Crater_Medium` | Ø ~9 m | Environment |
| `SM_Crater_Large` | Ø ~14 m | Environment |
| `SM_LandingPad` | 41 × 40 × 1.3 | (-50, 0) |
| `SM_Starship_Placeholder` | 4.6 × 7.1 × 44 | on pad |
| `SM_LAB1_LaboratoryModule` | ~9.6 × 4.9 × 6.7 (Ø4.5 × L8.7 target) | (0, 22) |

Add / refresh LAB-1 only:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  Blender/SolarMajesty_Modules.blend \
  --python Blender/scripts/sm_add_lab1_blockout.py
```

Regenerate remaining modules (does not touch HAB / LAB / connector):

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  Blender/SolarMajesty_Modules.blend \
  --python Blender/scripts/sm_add_remaining_blockouts.py
```

Copy new FBX into `Assets/Resources/Buildings/` (or Environment) after export.

Craters + rebuilt solar array:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  Blender/SolarMajesty_Modules.blend \
  --python Blender/scripts/sm_craters_and_solar.py
```

Writes `SM_Crater_Small/Medium/Large` to `Assets/Resources/Environment/` and replaces `SM_PWR1_SolarArray`.

## In-engine hero kits (Phase 4 Weeks 2–4)

Play Mode HAB / Colony Commons / landing pad / Farm (greenhouse + ice) / Regolith Camp / **Power solar field** / **Defense bunker** / **Guild Hall (CMD-1)** / **OPS-1 annex** / **LAB** / **wonders** / **workshop hangar** / **tall hangar** / **Inn** prefer `SM_Hero_*` FBX via `BuildingVisualCatalog.LoadHeroKit`, with `HeroBuildingKits` procedural fallback. Sized to the existing square footprints. HAB / Commons / LAB / Power / pad match ConceptSheets at RTS scale (not sheet meters). HAB / Commons / LAB / CMD-1 / OPS-1 hulls carry geometric panel lines (bevelled boxes + carbon seams). Junction dual-barrel turrets sit on square airlock hubs (`ColonyVisualUtility` → `BuildJunctionTurret`) — dressing only, no fire. Dock sleeves and plus-arms **mate flush at the Lego cell face**. The HAB-1 cylinder FBX remains in the catalog for other modules. Square airlocks stay; do not change footprints.

Parked pad ship is authored on `SM_Hero_LandingPad` (white/black stack). Launch still uses existing `LaunchSite` / `MissionController` logic.

## Next modeling session

1. ~~Subdivide/bevel HAB-1 / LAB-1 / dome / CMD-1 / OPS-1 panel lines~~ (in — carbon rings, spine seams, civic wrap bands; box hulls bevelled)
2. ~~Mate docking sockets to **connector** flush~~ (in — plus-arms end on the 2×2 cell face; module sleeves + orange collars sit on the footprint face; Commons cardinal stubs / Guild E/W ports reach that face)
3. Human Mars Game-tab still vs `SM_MarsCampaign_VisualTarget.png`  

---

## Git note

Consider tracking:

- `Blender/scripts/`
- `Docs/BLENDER_WORKFLOW.md`
- optionally `Blender/exports/*.fbx` if small enough  

Usually **do not** commit huge `.blend` binaries or raw concept JPGs if you already host art elsewhere—or use Git LFS.
