# Hero Roster v2 — Humanoid Knights (2026-09-22)

The ten specialist classes are now armoured humanoid "knights", following Aaron's 2026-09-22 concept art in
[`ConceptSheets/Heroes_v2/`](../ConceptSheets/Heroes_v2/). The earlier drones, tracked chassis and rovers are retired.
Fauna are unchanged.

| Class | Code | Build | Signature kit |
|---|---|---|---|
| Courier | LO-COU-1 | slim | Cargo cube on the back, orange rotating beacon, whip antenna |
| Defense | LO-DEF-1 | heavy | Tower shield (right), gatling cannon on the left forearm, red slit visor |
| Engineer | LO-ENG-1 | medium | Orange chest band, cable-reel pack, hip tool boxes, right-arm drill, HAB wall panel |
| Geologist | LO-GEO-1 | medium | Chest sample-vial rack, crystal beacon mast, core drill, orange helmet crest |
| Harvester | LO-HAR-1 | medium | Dark limbs, ore hopper full of rock on the back, orange scoop |
| Medic | LO-MED-1 | slim | Dome helmet with wraparound visor, cyan cross, folded stretcher, hip kit sphere with IV line |
| Scout | LO-SCT-1 | slim | Shoulder beacon, chest range-finder camera, antenna |
| Sentinel | LO-SEN-1 | heavy | Twin back-mounted cannons on a turret yoke, octagonal shield (left), orange chevron |
| Surveyor | LO-SRV-1 | slim | Three-lens dome head, lattice-mast dish pack, survey staff |
| Terraformer | LO-TRF-1 | heavy | Twin seed tanks, spade-blade (right), soil rake (left), tabard |

## Pipeline

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python Blender/scripts/sm_animated_roster.py -- --export          # all 17 units
# add  --only Engineer,Scout   to rebuild a few;  --render / --still for the Mars lineup
```

* `Blender/scripts/sm_hero_humanoids.py` — shared skeleton, class builds/kits, all hero clips.
* `Blender/scripts/sm_animated_roster.py` — fauna rigs, export, and `Assets/Resources/Units/UnitClipMeta.json`.
* Output: `Blender/exports/units/SM_Unit_*.fbx`, copied to `Assets/Resources/Units/`. File names, `.meta` GUIDs and the
  `SM_*` material tokens are unchanged, so nothing in Unity needs rewiring.

### Skeleton

One humanoid rig for every hero: `Root › Hips › Spine › Chest › Neck › Head`, `Pack`, and per side
`Shoulder › UpperArm › Forearm › Hand`, `Thigh › Shin › Foot`, plus a few prop bones (Shield, Cannon/Barrels, Bit,
CoreDrill, Scoop, Hopper, Cargo, Beacon, Antenna, Dish, Staff, Blade, Rake, Turret, GunL/R, Reel, Kit, Mast).
22–25 bones, rigid skinning (hard-surface parts), 6.3k–8k triangles per hero.

### Clips

| Clip | Frames | Loop | Notes |
|---|---|---|---|
| Idle | 48 | yes | Breathing, weight shift, look-around, prop secondary motion |
| Walk | 12–14 | yes | Planted-foot jog solved with 2-bone IK. Stride speed written to `UnitClipMeta.json` |
| Strike | 24 | once | Class attack: gatling burst, twin-cannon volley, drill thrust, overhead chop, scoop, staff thrust … |
| Work | 32 | yes | Class labour: drilling, scooping, raking, tending, scanning, guard sweep |
| Down | 30 | once | Collapse to a kneel-slump. UnitClipPlayer holds the last frame until revived |

Every clip keys every bone on every frame. Earlier exports left un-keyed bones at the previous clip's last pose, so
for example Engineer's Idle shipped with Walk's legs.

## Unity runtime

* **`UnitClipPlayer`** matches clips on the text after `|`. Blender names takes `SM_Unit_X_Rig|Idle`, and Unity keeps
  that verbatim. The old exact-match lookup never found a clip. It also left the dead component attached, which
  switched `UnitMotion` off, so heroes glided around unanimated. The player now:
  * cross-fades between states;
  * scales Walk to ground speed ÷ authored stride speed (0.55–1.9×);
  * restarts Strike on a new swing;
  * plays Work while the agent's status is labour (`working_*`, `repairing`, `workshop_repair`, `party_work`);
  * plays Down while incapacitated;
  * randomises loop phase per unit;
  * skips evaluation off-screen.
* **`UnitMotion`** adds only a small root lean and bank on clip-driven units. It sags the body only when a unit has no
  Down clip. Its hero fallback gait is always `Walker`.
* **`IndustrialArtDressing` / `SM_Hull`**: skinned renderers get an object-space hull variant (`_PanelSpace = 1`,
  finer 0.32× panel grid), so panel seams ride with the body instead of swimming across it. `SM_VisorRed` maps to a
  new emissive red visor slot for the Defense guardian.
* **Tests**: `Assets/Scripts/Tests/EditMode/HeroAnimationTests.cs`.

## Stride speeds (m/s at 1× sim speed)

Authored to each class's `moveSpeed` in `Resources/DemoContent/Specialists`. If a class's move speed changes a lot,
re-export so the jog cadence still matches. The player's 0.55–1.9× rate clamp covers normal buffs.
