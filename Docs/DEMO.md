# Solar Majesty — Greybox Demo (campaign)

Shareable overseer-loop sandbox: post flags, place mesh buildings, watch a party of up to **four** specialists self-sort under Dust Stalker **bite** pressure across a **campaign spine** (Earth → Luna → Mars → Belt → Europa) with research, sustain gates, and a real Overseer HUD.

**Smoke path (show / playtest):** [SMOKE_TEST.md](SMOKE_TEST.md). Menu **Solar Majesty → Play Demo** or **Open Smoke Test Notes**.

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

Play opens on the **title screen** (frozen drop behind the menu). **New Game** wipes campaign prefs (confirms if a save exists) and starts the Earth empty drop. **Continue** restores that body's **campus** (modules + workshop robots), stockpile, research, and population. Flags and fauna are not snapshotted. **Esc** pauses (Resume / Settings / Title / Quit). Tutorial is six skippable beats (Colony Commons → airlock → HAB → workshop → flag → TECH); Settings can replay it.

## Controls

| Input | Action |
|-------|--------|
| **Esc** | Pause / resume (Settings from pause or title) |
| **WASD** | Pan the isometric camera |
| **Q** / **E** | Zoom out / zoom in (mouse does not pan or zoom) |
| **G** / **B** / **Tab** | Open Flag / Build catalog / cycle. After picking a flag or module the list minimizes (B/G re-opens it). Click the dock button again to close |
| **F1** Explore · **F2** ClearThreat · **F3** Build · **F4** Extract · **F5** Defend · **I** Research Site · **O** Outpost · **U** Terraform | Flag type (in Flag popup when open) |
| **1–9 / 0** | Select building (COMMONS · HAB · PWR · OPS · LAB · Pad · Defense Battery · …; Airlock + workshops in Build popup) |
| **LMB** | Inspect: select specialist or building (info / workers / FLAG HERE). Flag/Build: empty ground places |
| **RMB** on a flag | Cancel that bounty and **refund escrowed MET** |
| **P** | Form a party from the current selection (2+), else 2+ heroes at the inn (max 4) |
| **[** | Disband selected party (or the last party) |
| **T** | Toggle research / tech tree panel (starts a tech, then closes) |
| **F6** / **F7** | Camera → Campus A / Campus B (HUD A/B chips on bottom dock) |
| **F9** | Seed Explore attractor at Campus B + focus B |
| **F10** | Debug: cycle any body (reload). **Shift+F10** also unlocks all campaign worlds |
| **+/-** | Adjust bounty |
| **Shift+LMB** | Add/remove specialist from selection (up to 4) |
| **R** | Debug: force high fatigue → Rest (all specialists) |
| **F8** | Toggle deep debug score HUD |
| **Y** | Revive party when outpost is overwhelmed / dismiss win banner |

**Procedural worlds / campaign:** fresh play starts on **Earth** (tutorial). Body chips show unlocked worlds only (`BELT?` while locked). Conquest win unlocks the next body — win banner **TO LUNA** / **TO MARS** / **TO BELT** / **TO EURO**. Each body keeps its own seed on a **384 m** sandbox. **F4 Extract** near a node harvests that deposit into the nearest drop-off (haul % on the HUD). **F2 ClearThreat** clears lair fauna. Cyan disc at Campus B is a forward outpost after Colony Commons. Earth ships need a Landing Pad. **Shift+F10** unlocks all bodies for debug. Campus A landing stays fixed. Same body+seed → same map.

**No click-to-move on specialists.** Outdoor units are **robots** fabricated when their **workshop** finishes building (Scout / Engineer / Defense / Medic / Harvester / Surveyor / Terraformer / Courier / Geologist / Sentinel). **Humans live only in HABs** (tax, births, beds) — never as outdoor agents. Raise **Colony Commons** first (HUD **COMMONS**). **Airlock Junctions** are panel-lined square hubs (orange frames/doors) that snap only to module face midlines (symmetry axes); every other module must Lego-dock onto an airlock end. Robots take bounties they want, **flee to the rest beacon** when hurt, **hunt** nearby fauna if brave, and hang out at workshops when idle. Post flags near a workshop — or **FLAG HERE** on a selected building — to pull that class. **Guild Hall** (after Guild Charter) is a class hall: assign SCOUT/ENG/DEF/MED (or it inherits the nearest workshop). Flags near the hall pull that class. **P** parties the current selection or inn. Bounties escrow **MET** from the stockpile; robots keep **$**. **Conquest gates** (HUD): clear all dens · sustain pop goal with Commons+farm+mine · research the body's launch tech (TECH · **T**) **and place a Landing Pad**. Labs tick science into one active tech; ★ rows are Secret Projects. Flag/Build menus are popups above the bottom dock — click the dock button again (or **B** / **G**) to close. The command panel shows the last Overseer log lines (drop, dens, sustain, launch, travel).

---

## 60-second demo script (Earth tutorial)

Speak while playing (fresh prefs / Earth):

1. **Title** — New Game. Empty drop: produced meadow + cobalt sky + carbon/orange claim chevrons; no buildings. Starter stockpile is loaded. First-run beats (Colony Commons → airlock → HAB → workshop robot → flag → TECH) are skippable.
2. **Build (B)** — **Colony Commons** (key 1, HUD **COMMONS**) on the claim → **Airlock Junction** snapped to a face socket → dock **HAB** then a **workshop** onto airlock ends (Lego campus). Humans stay in HABs; robots fabricate from workshops.
3. **TECH · T** — once the LAB is up, Field Survey ticks; completions auto-queue toward **Lunar Rocket**.
4. **Threat** — **F2 ClearThreat** on a den; dens checkbox fills as lairs go quiet.
5. **Economy** — place **Farm** + **Mine**, grow POP toward goal 8; sustain holds when stockpile is healthy. Command chips flash when ICE/MET/REG/PWR are short. Dock a **Power Node** so gen covers draw.
6. **Ecology** — a **Regolith Mite** (compact pillbug, **F5 Defend Area**) shows up at extractors; a **Watt Leech** (white ray + cyan groove, **F2 Clear Threat**) at the Power Node.
7. **Rocket** — finish tree to **Lunar Rocket** (pays metals/ice). **Place a Landing Pad** — craft stages with an orange beacon; Launch gate checks.
8. **Win** — OUTPOST SECURED → **TO LUNA**.

## Luna excerpt (after Earth)

1. Cratered grey world; dens harder; pop goal 12.
2. Research continues toward **Mars Ship** (unlocks persist).
3. Clear dens + farm/mine sustain + Mars Ship → **TO MARS**.
4. Mars: dens + sustain pop 16 + **Belt Hauler** + pad → **TO BELT**.
5. Belt: metal rush, thin farms, **Icebreaker** → **TO EURO**.
6. Europa: ice crust, heater power draw; dens + sustain → **SOLAR CONQUEST COMPLETE**.

## Classic overseer beat (any body)

1. **Explore (Scout)** — **G**, **F1**, bounty **~40+**. Pole should read **SCOUT** tempted (or *ignored — raise $* if no Scout robot yet).
2. **Build (Engineer)** — **F3**, default **$70** is ignored by a greedy Engineer. **+** to **~$90**, then they take it.
3. **Hunt / flee** — Defense **HUNT**; hurt heroes **FLEE** to the rest beacon and stay until recovered. **P** parties the current selection or inn; followers rest/hunt with the leader.
4. **Extract** — **F4** on a metal/ice/fissile node. Yield hauls through the nearest matching Mine/Farm/Camp/Power (HUD `via … · %`). Distant or no drop-off leaks ore; same-node double-taps saturate.
5. **Research / Outpost / Terraform** — **I** feeds the active tech. **O** on the cyan Campus B disc claims the outpost. **U** greens farm ticks.
6. **Campus pests** — Earth farm attracts olive **SOIL CREEPER** / **CREEP STEAL** (Defend). Power Node attracts cyan **LEECH DRAIN** (Clear Threat). Luna: **ASH HOPPER** on HAB (Clear Threat). Mars: **DUST WISP** (Clear) + **DUST CREEPER** (Defend).

---

## Success criteria (Phase 1 demo)

Run **[SMOKE_TEST.md](SMOKE_TEST.md)** (10-minute boot, then 20-minute Earth, then the full arc when you have time). Older slice checklists live in `VERTICAL_SLICE_PHASE1.md` and the `PHASE_*` docs.

### Must-pass

- [ ] Title → New Game → Earth empty drop; Continue restores that body's campus + stockpile/research
- [ ] Six skippable tutorial beats, including workshop robots
- [ ] No click-to-move; greed gate (Engineer ignores cheap Build)
- [ ] Three conquest gates: dens, sustain (Colony Commons + pop + farm + mine), launch (tech + Landing Pad)
- [ ] Body-native pest counters (**F5** Defend vs **F2** Clear Threat); resource chips flash when short
- [ ] Earth → Luna → Mars → Belt → Europa lighting and win copy (`TO LUNA` / `TO MARS` / `TO BELT` / `TO EURO`)
- [ ] Belt `LOW-G` / Europa `RAD`; Campus B rest + Harvester/Defense shops
- [ ] Pause / settings / quit; Play Mode does not throw on node yield labels

---

## What this demo is / isn’t

| Is | Isn’t |
|----|--------|
| Playable overseer loop + Earth → … → Europa campaign | Finished commercial game |
| Personality + greed + **local** threat + NavMesh campus pathing | Full save (continue is campus + stockpile + research per body; flags/fauna reset) |
| Mesh building kit + Luna/Mars lighting + Campus A/B framing | Final Blender hero unit art / animation |

See also: `Docs/VERTICAL_SLICE_PHASE1.md`, `Docs/PHASE_1_6_THREAT.md`, `Docs/PHASE_2A_BITE_AND_BOUNTY.md`, `Docs/PHASE_2B_NAVMESH_AND_JUICE.md`, `Docs/PHASE_3A_PRESENTATION_AND_MISSION.md`, `Docs/PHASE_3B_UNITS_VOLUME_WAVES.md`, `Docs/PHASE_4A_MISSION_STAKES.md`, `Docs/PHASE_4B_CONTENT_SCALE.md`, `Docs/PHASE_5A_MAP_DEADLINE_AMBIENT.md`, `Docs/PHASE_5C_MULTI_BODY.md`, `Docs/PHASE_5D_BODY_FRAMING.md`, `Docs/ART_DIRECTION.md`.
