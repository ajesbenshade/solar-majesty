# Grok Imagine — Copilot 3D clean hero shots

Turnaround sheet **crops do not work well** with Copilot 3D — leftover headers, labels, multi-view layout, and grey ground confuse monocular reconstruction.

Generate **one clean hero image per asset** instead: single subject, plain background, three-quarter studio angle. Save to `ConceptSheets/Copilot3D_Input/` and upload **that** to [Copilot 3D](https://copilot.microsoft.com).

Turnaround sheets in `ConceptSheets/` stay the art bible for Blender sheet-matching. These hero shots are **Copilot 3D input only**.

---

## Rules (every prompt)

Paste this block **first**, then the subject block:

```
Single 3D product hero render, ONE subject only, centered, full object visible with margin. Pure seamless white studio background, no ground texture, no environment, no props, no other objects. Three-quarter view from slightly above (about 30 degrees), soft even studio lighting, one faint contact shadow directly under the subject only. No text, no labels, no UI, no watermark, no turnaround panels, no front/side views, no scale bars, no material swatches, no carbon header, no collage. Hard-surface sci-fi game asset with readable silhouette. isometric-friendly angle, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, slightly exaggerated proportions for clarity, high detail 3D render style
```

**After Imagine:**

1. Save as `ConceptSheets/Copilot3D_Input/SM_Unit_{Name}_Hero.png` (or `.jpg`)
2. Optional: copy to `Downloads/SolarMajesty_Copilot3D_Input/` for upload convenience
3. Copilot 3D → download GLB → `Blender/imports/copilot3d/SM_Unit_{Name}.glb`
4. Run `sm_import_copilot3d.py` (see [`BLENDER_WORKFLOW.md`](BLENDER_WORKFLOW.md))

**If Copilot still struggles:** try pure **#808080** mid-grey background, or a **slight front-three-quarter** instead of side-three-quarter. Avoid busy shadows and cropped limbs.

---

## Priority order

| Order | Asset | Output filename |
|------:|-------|-----------------|
| 1 | Regolith Mite | `SM_Unit_RegolithMite_Hero.png` |
| 2 | Ice Wisp | `SM_Unit_IceWisp_Hero.png` |
| 3 | Watt Leech | `SM_Unit_WattLeech_Hero.png` |
| 4 | Courier Bot | `SM_Unit_CourierBot_Hero.png` |
| 5 | Scout Drone | `SM_Unit_ScoutDrone_Hero.png` |
| 6 | Medic | `SM_Unit_Medic_Hero.png` |
| 7 | Harvester | `SM_Unit_HarvesterBot_Hero.png` |
| 8 | Surveyor | `SM_Unit_SurveyorBot_Hero.png` |
| 9 | Sentinel | `SM_Unit_SentinelMech_Hero.png` |
| 10 | Geologist | `SM_Unit_GeologistBot_Hero.png` |
| 11 | Soil Creeper | `SM_Unit_SoilCreeper_Hero.png` |
| 12 | Ash Hopper | `SM_Unit_AshHopper_Hero.png` |
| 13 | Rock Tick | `SM_Unit_RockTick_Hero.png` |
| 14+ | Engineer, Defense, Terraformer, Dust Stalker | defer — heavy cleanup |

---

## Specialist prompts

### Scout Drone — LO-SCT-1

```
Tall thin autonomous scout drone, whip antenna, small cyan sensor head, white thermal shell with black structural bands and one high-vis orange beacon, four small hover rotors on arms. No guns, no human face, clean hard-surface robotics.
```

### Engineer Bot

```
Squat industrial engineer robot, toolbox hip module, cyan visor strip, white and black Starship plating, orange service stripe, chunky builder proportions, modular chest docking ports, treaded boots. No weapons, no human face.
```

### Defense Mech — Guardian Class

```
Wide low tracked combat mech, continuous black treads, sloping white ceramic hull, black armor banding, large dark-red viewport (only red on hull), massive shoulder pods with red ports, small roof turret. Not a biped walker.
```

### Medic — LO-MED-1

```
Compact field-medic hover stretcher robot, low white ceramic ambulance hull, black carbon belly, orange hazard stripe on nose, cyan medical cross on top, steel side rails, thin IV pole with cyan bag at rear, four low graphite thruster discs with cyan glow. No rotors, no biped legs, no guns.
```

### Harvester Bot — LO-HAR-1

```
Squat tracked industrial harvester, white ceramic belly, black carbon chassis band, orange front scoop blade, steel rear ore hopper with orange lip, white cab, cyan visor, continuous black treads. Not a dozer, not a six-wheel truck.
```

### Surveyor Bot — LO-SRV-1

```
Tall thin surveyor robot, white cylindrical body on three splayed graphite legs with pad feet, cyan sensor lens, steel mast with white shallow dish and orange ring, orange beacon on mast. No hover rotors, no six wheels.
```

### Terraformer Bot — LO-TRF-1

```
Slow heavy tracked terraformer dozer, white ceramic chassis, black carbon belly, wide orange front blade, orange rear rake tiller, cyan visor, continuous black treads. Not a scoop hopper.
```

### Courier Bot — LO-COU-1

```
Rugged six-wheel freight hauler robot, white ceramic chassis, black carbon belly, large white steel crate on rear bed with orange hazard corners, cyan visor, steel whip antenna with orange tip, orange roof beacon, six black wheels. No drill, no sample vials.
```

### Geologist Bot — LO-GEO-1

```
Compact six-wheel science rover, white ceramic chassis, black carbon belly, orange nose stripes, vertical core-drill arm with orange collar and steel bit, sample vial rack with cyan and orange caps, sensor mast with cyan lens. Not a freight crate truck.
```

### Sentinel Mech — LO-SEN-1

```
Squat perimeter turret on tracked chassis, white ceramic hull, black carbon treads, orange V chevron on top, twin-barrel turret with cyan tips, cyan visor strip. No red viewport, no huge shoulder pods, not the Defense Guardian.
```

---

## Fauna prompts

### Dust Stalker

```
Low elongated lunar predator creature, dark carapace, serrated dorsal fins, four legs, four paired orange glowing eyes, white forearm armor plates, three-toed talons, thick tail. Alien fauna, not a robot. No blood, no gore.
```

### Ice Wisp

```
Hovering ice-crystal creature, translucent white-cyan body, cyan glowing core, seven radial ice spikes like a snowflake, hex hub, black ridges with orange sensor nubs, faint cyan glow beneath. No propellers, not a drone.
```

### Rock Tick

```
Wide low crab creature, dark graphite carapace wider than long, six splayed black legs, orange pincer tips, paired cyan eyes, one dorsal spike. Not a pillbug, not a robot.
```

### Soil Creeper

```
Segmented armored millipede creature, six overlapping graphite dorsal plates with one olive-brown segment, many stub black legs, orange head nubs, cyan eyes, orange tail cerci. Long low body, not a ribbon leech.
```

### Ash Hopper

```
Tall insectoid hopper creature, ash-grey segmented carapace held high on six spindly black X-legs with orange knees, graphite abdomen, paired cyan eyes, orange brow stripe. Shrimp-flea silhouette, not a robot.
```

### Watt Leech

```
Flat white ray-beetle creature, low elongated body, cyan dorsal groove, two orange front nubs, white mandibles, four rear fin-flippers, six black circular discs per side. Not a millipede, not a robot.
```

### Regolith Mite

```
Compact pillbug scavenger creature, dust-brown carapace longer than wide, four overlapping graphite top plates, six pointed black legs, central cyan eye, two orange nubs, downward mandibles. Not a wide crab.
```

---

## Buildings (optional — usually skip Copilot 3D)

Modular buildings need exact docking dimensions. Prefer `sm_hero_building_kits.py`. If experimenting, use the same hero-shot prefix plus:

### HAB-1

```
Horizontal white habitat cylinder module on landing skids, black end bands, orange docking rings, large side access hatch, 8 meter diameter 12 meter long, SpaceX Starship aesthetic. Single module only, no campus context.
```

---

## Related docs

- Turnaround sheets (Blender reference): [`GROK_IMAGINE_UNIT_PROMPTS.md`](GROK_IMAGINE_UNIT_PROMPTS.md)
- Import pipeline: [`BLENDER_WORKFLOW.md`](BLENDER_WORKFLOW.md) — Copilot 3D section
- Pilot notes: [`COPILOT3D_PILOT_EVAL.md`](COPILOT3D_PILOT_EVAL.md)
