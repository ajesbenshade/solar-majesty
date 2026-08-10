# Solar Majesty — Greybox Demo (2–3 min)

Shareable overseer-loop sandbox: post flags, place mesh buildings, watch three specialists self-sort under Dust Stalker **bite** pressure with a real Overseer HUD.

## Open & Play

1. Install **Unity 6** (project targets **6000.5.x**; URP via `Packages/manifest.json`).
2. **Unity Hub → Open →** select this repo folder  
   (clone of `ajesbenshade/solar-majesty` / `solar-conquest`).
3. First open may take several minutes (package resolve + FBX import).
4. Open scene via menu **Solar Majesty → Open Demo Scene**  
   (or double-click `Assets/Scenes/LunarOutpost_Sandbox.unity` in the Project window).  
   Hierarchy must show **GameLoop** + **Main Camera** + **Directional Light**.
5. Select the **Game** tab (not only Scene view) and press **Play**.

If you only see empty sky with no GameLoop in Hierarchy, you are in the wrong scene — use **Open Demo Scene**.  
If the scene asset is missing: **Solar Majesty → Build Demo Scene**, then Play.

No Inspector wiring is required: `GameLoop` bootstraps grid, camera, party, stalkers, economy, HUD, and loads demo ScriptableObjects from `Resources/DemoContent` (factories remain as fallback). Building meshes load from `Resources/Buildings` + `Environment`.

Regenerate authored content (SOs + unit prefabs): **Solar Majesty → Build Demo Content Assets**.

---

## Controls

| Input | Action |
|-------|--------|
| **WASD** / edge pan / MMB–RMB drag / scroll | Isometric camera |
| **G** / **B** / **Q** / **Tab** | Flag tool / Build tool / None / cycle |
| **F1** Explore · **F2** ClearThreat · **F3** Build · **F4** Extract · **F5** Defend | Flag type |
| **F6** / **F7** | Camera → Campus A / Campus B |
| **+/-** | Adjust bounty |
| **LMB** | Post flag or place building (active tool) |
| **1–7** | Pad · HAB · PWR · OPS · LAB · CMD · Solar |
| **R** | Debug: force high fatigue → Rest (all specialists) |
| **F8** | Toggle deep debug score HUD |
| **Y** | Revive party when outpost is overwhelmed |

**No click-to-move on specialists.** They only act via `SpecialistBrain`.

---

## 60-second demo script

Speak while playing:

1. **Show the colony** — one campus: dome core, HAB–LAB spine with connectors, CMD/OPS north, power/solar south, pad+ship east. Specialists spawn in the plaza with **distinct silhouettes** (tall Scout / squat Engineer / shielded Defense). Dust Stalkers use a low predator placeholder. Meshes use Majesty-readable scale (not raw Blender meters).
2. **Explore (Scout)** — **G**, **F1**, bounty **~100+**, place **near the cyan Scout**. He should **Pursue** and work the flag.
3. **Build (Engineer)** — **F3**, bounty **~120+**, place **near the orange Engineer**. High-greed builder should prefer it over combat.
4. **Threat** — Note HUD **Threat** / HP bars when stalkers aggro and **bite**. **F2 ClearThreat**, bounty **~80+**, drop **on/near a stalker**. **Defense Mech** should engage; Engineer should stay reluctant. HP should drop on bitten specialists.
5. **Greed reject** — Post **Explore** with **low bounty far away**. Expect **Idle** / `no_attractive_flag`.
6. **Rest** — Press **R**. Specialists should show **Rest** (blue status orbs).
7. **Build placement** — **B**, keys **1–7**, place a mesh building (try LAB or Solar); green/red ghost + footprint pad shows validity; **campus tiles stay red** (showcase footprints reserved). Stockpile deducts on the HUD.
8. **Extract / Defend** — **F4** near Engineer for regolith yield; **F5** near Defense to calm Threat while claimed.

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

### Phase 2A

- [ ] Stalker aggro bites → specialist HP drops on OverseerHud cards
- [ ] HP → 0 → incapacitated, then recovers
- [ ] ClearThreat worked near stalker → stalker dies
- [ ] Construction site shows world progress bar + HUD line
- [ ] All specialists down → Outpost Overwhelmed → **Y** revives
- [ ] **F8** toggles debug scores

### Phase 2B

- [ ] Specialists path around campus buildings (NavMesh), not through them
- [ ] Bite → hit flash; incap/stalker death → burst spheres
- [ ] Flag claim → ring pulse; audio chords (not single beeps only)
- [ ] Fail state shows red veil + revive UI

### Phase 3A

- [ ] Soft sun shadows + cool fill; lunar ground; distance fog
- [ ] HUD shows mission objective + stalker remaining count
- [ ] Clear stalkers → OUTPOST SECURED win banner
- [ ] Overwhelm still loses; **Y** revives and resumes mission

### Phase 3B

- [ ] Specialists show industrial shell/band/accent silhouettes
- [ ] Stalkers show predator legs + orange eyes
- [ ] Mild bloom/vignette from DemoVolume
- [ ] Wave 1 clear → reinforcements → Wave 2 clear (combat stake)

### Phase 4A

- [ ] HUD shows Combat / Hold / Build checklist
- [ ] Waves alone do not win — need hold ~60s + finish 1 construction
- [ ] All three → OUTPOST SECURED
- [ ] Overwhelm pauses hold until revive

### Phase 4B

- [ ] **F4** Extract → Engineer works; stockpile gains Regolith
- [ ] **F5** DefendArea → Defense works; Threat drops while claimed
- [ ] **5–7** place LAB / CMD / Solar meshes
- [ ] Campus shows denser spur + second solar

### Phase 5A

- [ ] Wider map / camera; deadline countdown on stakes panel
- [ ] Miss deadline → MISSION TIME EXPIRED → Restart mission
- [ ] Quiet ambient hum under SFX

### Demo visuals

- [ ] Showcase colony meshes visible on Play
- [ ] Placed buildings use Blender FBX kit (not only cubes)
- [ ] Specialists show distinct class silhouettes (Scout / Engineer / Defense)
- [ ] Stalkers use predator placeholder (not anonymous spheres)
- [ ] Cannot place buildings overlapping showcase campus footprints
- [ ] Content loads from `Resources/DemoContent` (or factory fallback)

---

## What this demo is / isn’t

| Is | Isn’t |
|----|--------|
| Playable overseer loop with multi-stake win/lose + deadline | Full game / campaign |
| Personality + greed + threat + NavMesh campus pathing | Save/load, multi-body |
| Mesh building kit + lunar lighting greybox | Final Blender hero unit art / animation |

See also: `Docs/VERTICAL_SLICE_PHASE1.md`, `Docs/PHASE_1_6_THREAT.md`, `Docs/PHASE_2A_BITE_AND_BOUNTY.md`, `Docs/PHASE_2B_NAVMESH_AND_JUICE.md`, `Docs/PHASE_3A_PRESENTATION_AND_MISSION.md`, `Docs/PHASE_3B_UNITS_VOLUME_WAVES.md`, `Docs/PHASE_4A_MISSION_STAKES.md`, `Docs/PHASE_4B_CONTENT_SCALE.md`, `Docs/PHASE_5A_MAP_DEADLINE_AMBIENT.md`, `Docs/ART_DIRECTION.md`.
