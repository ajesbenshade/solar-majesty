# Solar Majesty — Greybox Demo (2–3 min)

Shareable overseer-loop sandbox: post flags, place mesh buildings, watch three specialists self-sort under Dust Stalker pressure.

## Open & Play

1. Install **Unity 6** (project targets **6000.5.x**; URP via `Packages/manifest.json`).
2. **Unity Hub → Open →** select this repo folder  
   (clone of `ajesbenshade/solar-majesty` / `solar-conquest`).
3. First open may take several minutes (package resolve + FBX import).
4. Open scene: **`Assets/Scenes/LunarOutpost_Sandbox.unity`**
5. Press **Play**.

If the scene is missing, in the Editor menu run **Solar Majesty → Build Demo Scene**, then Play.

No Inspector wiring is required: `GameLoop` bootstraps grid, camera, party, stalkers, economy, HUD, and loads building meshes from `Resources/`.

---

## Controls (unchanged)

| Input | Action |
|-------|--------|
| **WASD** / edge pan / MMB–RMB drag / scroll | Isometric camera |
| **G** / **B** / **Q** / **Tab** | Flag tool / Build tool / None / cycle |
| **F1** Explore · **F2** ClearThreat · **F3** Build | Flag type |
| **+/-** | Adjust bounty |
| **LMB** | Post flag or place building (active tool) |
| **1–4** | Select building: Landing Pad · HAB-1 · PWR-1 · OPS-1 |
| **R** | Debug: force high fatigue → Rest (all specialists) |

**No click-to-move on specialists.** They only act via `SpecialistBrain`.

---

## 60-second demo script

Speak while playing:

1. **Show the colony** — mesh HAB-1, PWR, dome, landing pad + Starship placeholder already in the sandbox. Capsule specialists (cyan Scout, orange Engineer, red Defense). Dark-red Dust Stalkers wander.
2. **Explore (Scout)** — **G**, **F1**, bounty **~100+**, place **near the cyan Scout**. He should **Pursue** and work the flag.
3. **Build (Engineer)** — **F3**, bounty **~120+**, place **near the orange Engineer**. High-greed builder should prefer it over combat.
4. **Threat** — Note HUD **`threat=`** rise when stalkers aggro. **F2 ClearThreat**, bounty **~80+**, drop **on/near a stalker**. **Defense Mech** should engage; Engineer should stay reluctant.
5. **Greed reject** — Post **Explore** with **low bounty far away**. Expect **Idle** / `no_attractive_flag`.
6. **Rest** — Press **R**. Specialists should show **Rest** (blue status orbs).
7. **Build placement** — **B**, keys **1–4**, place a mesh building; stockpile deducts on the HUD.

---

## Success criteria checklist

### Phase 1 / 1.5

- [ ] Low bounty far → Idle / `no_attractive_flag`
- [ ] High Explore near Scout → walk + work
- [ ] High Build near Engineer → Engineer accepts
- [ ] High ClearThreat → Defense prefers combat
- [ ] **R** → Rest
- [ ] Click empty ground → specialists do **not** repath to click
- [ ] OnGUI HUD shows decisions, scores, fatigue, bounty

### Phase 1.6

- [ ] Stalker near party → HUD threat / `danger=` rises
- [ ] Defense more willing to take ClearThreat under pressure
- [ ] Engineer more cautious under pressure
- [ ] ClearThreat worked near stalker → stalker dies → threat falls

### Demo visuals

- [ ] Showcase colony meshes visible on Play
- [ ] Placed buildings use Blender FBX kit (not only cubes)
- [ ] Specialists remain capsules; stalkers remain spheres (by design for this greybox)

---

## What this demo is / isn’t

| Is | Isn’t |
|----|--------|
| Playable overseer loop | Full game / campaign |
| Personality + greed + threat | NavMesh, save/load, multi-body |
| Mesh building kit greybox | Final art / animation |

See also: `Docs/VERTICAL_SLICE_PHASE1.md`, `Docs/PHASE_1_6_THREAT.md`, `Docs/ART_DIRECTION.md`.
