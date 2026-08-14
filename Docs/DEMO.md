# Solar Majesty — Greybox Demo (campaign)

Shareable overseer-loop sandbox: post flags, place mesh buildings, watch a party of up to **four** specialists self-sort under Dust Stalker **bite** pressure across a **campaign spine** (Earth → Luna → Mars) with research, sustain gates, and a real Overseer HUD.

## Open & Play

1. Install **Unity 6** (project targets **6000.5.x**; URP via `Packages/manifest.json`).
2. **Unity Hub → Open →** select this repo folder  
   (clone of `ajesbenshade/solar-majesty` / `solar-conquest`).
3. First open may take several minutes (package resolve + FBX import).
4. Open scene via menu **Solar Majesty → Open Demo Scene**  
   (or double-click `Assets/Scenes/LunarOutpost_Sandbox.unity` in the Project window).  
   Hierarchy must show **GameLoop** + **Main Camera** + **Directional Light**.
5. Select the **Game** tab (not only Scene view) and press **Play**. You should see the title screen over a frozen drop.

If you only see empty sky with no GameLoop in Hierarchy, you are in the wrong scene — use **Open Demo Scene**.  
If the scene asset is missing: **Solar Majesty → Build Demo Scene**, then Play.

No Inspector wiring is required: `GameLoop` bootstraps grid, camera, party, stalkers, economy, HUD, and loads demo ScriptableObjects from `Resources/DemoContent` (factories remain as fallback). Building meshes load from `Resources/Buildings` + `Environment`.

Regenerate authored content (SOs + unit prefabs): **Solar Majesty → Build Demo Content Assets**.

---

Play opens on the **title screen** (frozen drop behind the menu). **New Game** wipes campaign prefs and starts the Earth empty drop. **Continue** reloads the last stockpile on the saved body. **Esc** pauses in play.

## Controls

| Input | Action |
|-------|--------|
| **Esc** | Pause / resume (Settings from pause or title) |
| **WASD** / edge pan / **LMB–MMB–RMB drag** / scroll | Isometric camera |
| **G** / **B** / **Q** / **Tab** | Open Flag popup / Build popup / close tool / cycle (also bottom dock) |
| **F1** Explore · **F2** ClearThreat · **F3** Build · **F4** Extract · **F5** Defend | Flag type (in Flag popup when open) |
| **1–9 / 0** | Select building (Palace · HAB · PWR · OPS · LAB · Pad · CMD · …; Airlock + workshops in Build popup) |
| **LMB** | Inspect: select specialist or building (info / workers / FLAG HERE). Flag/Build: empty ground places · drag pans |
| **RMB** on a flag | Cancel that bounty and **refund escrowed MET** |
| **P** | Form a party from the current selection (2+), else 2+ heroes at the inn (max 4) |
| **[** | Disband selected party (or the last party) |
| **T** | Toggle research / tech tree panel |
| **F6** / **F7** | Camera → Campus A / Campus B (HUD A/B chips on bottom dock) |
| **F9** | Seed Explore attractor at Campus B + focus B |
| **F10** | Debug: cycle any body (reload). **Shift+F10** also unlocks all campaign worlds |
| **+/-** | Adjust bounty |
| **Shift+LMB** | Add/remove specialist from selection (up to 4) |
| **R** | Debug: force high fatigue → Rest (all specialists) |
| **F8** | Toggle deep debug score HUD |
| **Y** | Revive party when outpost is overwhelmed / dismiss win banner |

**Procedural worlds / campaign:** fresh play starts on **Earth** (tutorial). Body chips show unlocked worlds only (`EARTH?` while locked). Conquest win unlocks the next body — win banner **TO LUNA** / **TO MARS**. Each body keeps its own seed on a **384 m** sandbox. **F4 Extract** near a node harvests that deposit; **F2 ClearThreat** clears lair fauna. **Shift+F10** unlocks all bodies for debug. Campus A landing stays fixed. Same body+seed → same map.

**No click-to-move on specialists.** Outdoor units are **robots** fabricated when their **workshop** finishes building (Scout / Engineer / Defense / Medic). **Humans live only in HABs** (tax, births, beds) — never as outdoor agents. Raise the **Palace Keep** first (Majesty castle). **Airlock Junctions** snap only to module face midlines (symmetry axes); every other module must Lego-dock onto an airlock end. Robots take bounties they want, **flee to the rest beacon** when hurt, **hunt** nearby fauna if brave, and hang out at workshops when idle. Post flags near a workshop — or **FLAG HERE** on a selected building — to pull that class. **P** parties the current selection or inn. Bounties escrow **MET** from the stockpile; robots keep **$**. **Conquest gates** (HUD): clear all dens · sustain pop goal with palace+farm+mine · research **Lunar Rocket** (TECH · **T**) **and place a Landing Pad**. Labs tick science into one active tech. Flag/Build menus are popups above the bottom dock — click the dock button again (or **Q**) to close. The command panel shows the last Overseer log lines (drop, dens, sustain, launch, travel).

---

## 60-second demo script (Earth tutorial)

Speak while playing (fresh prefs / Earth):

1. **Title** — New Game. Empty drop: map + dens only; orange claim disc at Campus A. Starter stockpile is loaded. First-run beats (Palace → HAB → workshop robot → flag → TECH) are skippable.
2. **Build (B)** — **Palace Keep** (key 1) on the claim → **Airlock Junction** snapped to a face socket → dock **HAB** / workshops onto airlock ends (Lego campus).
3. **TECH · T** — once the LAB is up, Field Survey ticks; completions auto-queue toward **Lunar Rocket**.
4. **Threat** — **F2 ClearThreat** on a den; dens checkbox fills as lairs go quiet.
5. **Economy** — place **Farm** + **Mine**, grow POP toward goal 8; sustain holds when stockpile is healthy. Command chips flash when ICE/MET/REG/PWR are short. Dock a **Power Node** so gen covers draw.
6. **Ecology** — a **Regolith Mite** shows up at the farm (**F5 Defend Area**); a **Watt Leech** at the Power Node (**F2 Clear Threat**).
7. **Rocket** — finish tree to **Lunar Rocket** (pays metals/ice). **Place a Landing Pad** — craft stages with an orange beacon; Launch gate checks.
8. **Win** — OUTPOST SECURED → **TO LUNA**.

## Luna excerpt (after Earth)

1. Cratered grey world; dens harder; pop goal 12.
2. Research continues toward **Mars Ship** (unlocks persist).
3. Clear dens + farm/mine sustain + Mars Ship → **TO MARS**.
4. Mars finale banner: **SOLAR CONQUEST COMPLETE**.

## Classic overseer beat (any body)

1. **Explore (Scout)** — **G**, **F1**, bounty **~40+**. Pole should read **SCOUT** tempted (or *ignored — raise $* if no Scout robot yet).
2. **Build (Engineer)** — **F3**, default **$70** is ignored by a greedy Engineer. **+** to **~$90**, then they take it.
3. **Hunt / flee** — Defense **HUNT**; hurt heroes **FLEE** to the rest beacon and stay until recovered. **P** parties the current selection or inn; followers rest/hunt with the leader.
4. **Extract** — **F4** on a metal/ice/fissile node (world label shows remaining yield).
5. **Mites / leeches** — Farm attracts ochre **MITE STEAL** (Defend). Power Node attracts cyan **LEECH DRAIN** (Clear Threat).

---

## Success criteria checklist

### Phase 1 / 1.5

- [ ] Low bounty far → Wander (SCOUT / TOWN / PATROL), not a cheap flag
- [ ] High Explore near Scout → walk + work
- [ ] High Build near Engineer → Engineer accepts
- [ ] High ClearThreat → Defense prefers combat
- [ ] Hurt Defense / Scout → FLEE to waystation inn
- [ ] Defense near stalker, no flag → HUNT
- [ ] **R** → walk to inn, REST
- [ ] Village HABs appear as POP grows; stalkers chew outer HABs first
- [ ] **P** at inn → party follows leader; **[** disbands
- [ ] Farm / Mine / Regolith camp produce; HABs pay metals tax
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

### Phase 6A (procedural world)

- [ ] HUD shows body code + seed + node count + uncleared/total lairs
- [ ] Same body+seed → same crater / node / lair layout
- [ ] Restart / win → NEW LUNA or NEW MARS advances seed → fresh layout
- [ ] **F4** Extract near metal / ice / fissile / regolith node yields that resource
- [ ] **F2** ClearThreat worked on a lair → den clears; fauna gone; lair count drops
- [ ] Campus A footprints stay clear of procedural props
- [ ] **F10** / body chips switch Luna ↔ Mars without advancing that body's seed

### Week 1 campaign gates

- [ ] Building card has **no Upgrade** button / LVL chip
- [ ] Mission panel shows Clear dens · Sustain colony · Launch craft
- [ ] Hard 3‑minute deadline is **off** by default
- [ ] Clear all dens + farm/mine + pop goal held ~40s → sustain gate fills
- [ ] Win banner mentions dens / sustain / launch

### Week 2 tech tree

- [ ] **T** / TECH dock opens research panel
- [ ] Showcase LAB (and placed labs) raise science rate
- [ ] Pick Field Survey → branch toward Lunar Rocket
- [ ] Completing Lunar Rocket spends metals/ice and checks Launch gate
- [ ] Dens + sustain + Lunar Rocket → OUTPOST SECURED

### Week 3 campaign

- [ ] Fresh prefs start on **Earth** (blue sky, fewer dens, pop goal 8)
- [ ] Luna/Mars chips locked until prior body conquered
- [ ] Win on Earth → **TO LUNA** unlocks and travels
- [ ] Research unlocks persist across bodies (Mars Ship available after Lunar Rocket)
- [ ] **Shift+F10** unlocks all bodies for debug

### Week 3 economy & threat

- [ ] Resource chips flash when REG < 10, ICE < 8, MET < 12, or PWR short / draw > gen
- [ ] Command strip shows `PWR gen/draw`, upkeep countdown, Earth resupply countdown
- [ ] Extract near a node updates remaining yield label + last-extract line
- [ ] Docking a Farm/Mine attracts a **Regolith Mite** — **F5 Defend Area** kills it
- [ ] Docking a Power Node attracts a **Watt Leech** — **F2 Clear Threat** kills it
- [ ] Campus growth raises ambient threat (THREAT meter on the dock)

### Week 4 polish

- [ ] Launch tech stages departure craft on the pad + plume VFX
- [ ] Sustain hint shows pop / farm+mine / stockpile needs
- [ ] Village HAB expansion rebuilds NavMesh
- [ ] Mars win shows **SOLAR CONQUEST COMPLETE**
- [ ] Advance campaign plays launch plume before reload
- [ ] Construction sites spark; completed modules burst orange
- [ ] Extract flags ping green; Earth/Luna/Mars lighting reads different
- [ ] Camera glances at Palace / first mite or leech

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
| Playable overseer loop + Earth → Luna → Mars campaign | Finished commercial game |
| Personality + greed + **local** threat + NavMesh campus pathing | Full save (continue is stockpile + body + research prefs) |
| Mesh building kit + Luna/Mars lighting + Campus A/B framing | Final Blender hero unit art / animation |

See also: `Docs/VERTICAL_SLICE_PHASE1.md`, `Docs/PHASE_1_6_THREAT.md`, `Docs/PHASE_2A_BITE_AND_BOUNTY.md`, `Docs/PHASE_2B_NAVMESH_AND_JUICE.md`, `Docs/PHASE_3A_PRESENTATION_AND_MISSION.md`, `Docs/PHASE_3B_UNITS_VOLUME_WAVES.md`, `Docs/PHASE_4A_MISSION_STAKES.md`, `Docs/PHASE_4B_CONTENT_SCALE.md`, `Docs/PHASE_5A_MAP_DEADLINE_AMBIENT.md`, `Docs/PHASE_5C_MULTI_BODY.md`, `Docs/PHASE_5D_BODY_FRAMING.md`, `Docs/ART_DIRECTION.md`.
