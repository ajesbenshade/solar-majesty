# Solar Majesty — Phase 1 smoke test

Run this before showing the demo. Unity **Game** tab, not Scene view. Menu **Solar Majesty → Play Demo** (or Open Demo Scene → Play).

Continue restores **campus + stockpile + research + body**. Flags and fauna respawn. Campus is per-world (Earth Commons stays on Earth).

---

## 10-minute boot (must pass)

- [ ] Title over a frozen drop. WASD pans, **Q** zooms out, **E** zooms in. Mouse does not pan or zoom. Tagline names the Overseer fantasy.
- [ ] **New Game** (confirm wipe if a save exists) → Earth empty drop: **produced meadow**, **cobalt sky**, grass/trees/pond + cumulus around the orange claim disc, HUD REG/ICE/MET/PWR/BEDS, carbon/gold chrome. No starter robots. Not the old grey-tan plane. Not Mars orange.
- [ ] Yield labels on resource nodes (`REG` / `MET` / `ICE`) — no `MissingComponentException`.
- [ ] Tutorial 1/6: **B**, key **1**, Colony Commons on the claim — **domed command-hub citadel** (FBX or procedural hero kit), not a grey box. Console `[HeroKit] Attached SM_Hero_Commons` if the FBX imported. HUD **COMMONS**, never Palace.
- [ ] 2/6: Airlock Junction on a Commons face socket — **panel-lined** white square hub (smaller than the 2×2 cell so short white tubes read) + dual-barrel turret; unused Commons / airlock stubs stay hidden. Module docks are white round tubes with **one orange collar at the Lego face** (no punch-through, no stacked orange box).
- [ ] 3/6: HAB on that airlock (humans indoor) — **horizontal white/black/orange cylinder** on skids (HAB-1 living module) with carbon rings + a spine seam, tube to Commons. Dock collar meets the airlock at the HAB face (no gap). Not a box.
- [ ] 4/6: Engineer (or Scout / Defense) workshop → hangar FBX (tall hangar if Defense) → robot fabricates when the site finishes. Console `[HeroKit] Attached SM_Hero_Workshop` (or `WorkshopTall`).
- [ ] Empty ground click does **not** repath the robot.
- [ ] 5/6: **G**, post a flag. Pole shows tempted class or *ignored — raise $*.
- [ ] Engineer ignores default Build **$70**; **+** to ~**$90** and they take it.
- [ ] 6/6: **T** opens research. **SKIP** dismisses the bar. Settings → **Replay tutorial** brings it back.
- [ ] **Esc** pause → Resume / Settings / Title / Quit. Title **Continue** shows body + MET + module count.
- [ ] Build Colony Commons + airlock, Title → Continue: Commons and airlock are back; workshop robots refabricate.

## 20-minute Earth loop

- [ ] Farm + Mine docked; command chips flash if ICE/MET/REG/PWR are short.
- [ ] Power Node: gen covers draw. **Watt Leech** → **F2 Clear Threat**.
- [ ] Farm: **Soil Creeper** (olive millipede, **CREEP STEAL**) → **F5 Defend Area**. Regolith mites may still hit extractors.
- [ ] **F4 Extract** on a node updates remaining yield. Near a matching Mine/Farm the HUD shows `via … · ~100%`; far from campus it reads `loose haul · ~40%`.
- [ ] No Landing Pad: ship countdown tagged `(no pad)`; when the timer hits, toast **waved off** and stockpile does not jump.
- [ ] After a pad: Earth package docks (Luna+ spends a MET fee).
- [ ] Cyan disc NE of Commons (Campus B): after Colony Commons, place a Mine/Farm/Pad/Power/Defense **or Harvester/Defense workshop** on it without airlocks. HUD **OUTPOST**; PWR draw ticks up. **F7** — hurt robots rest at the cyan beacon, not the A inn.
- [ ] **T** research: Guild Charter → Guild Hall (HUD **GUILD**). Select the hall and assign **SCOUT / ENG / DEF / MED** — flags nearby pull that class (inherits nearest workshop if you skip). Harvest Doctrine unlocks Harvester Workshop; Survey Doctrine unlocks Surveyor. **Terraform Charter** / **Freight Doctrine** unlock Terraformer / Courier. **Core Sampling** / **Perimeter Doctrine** unlock Geologist / Sentinel. Workshops still fabricate these robots (Blender silhouettes).
- [ ] Flag **I** Research Site feeds the active tech. **O** on the cyan disc claims OUTPOST. **U** Terraform greener farms.
- [ ] ★ Secret Projects spend stockpile on complete: Anvil (mines), Skyhook (freight), Gene Vault (beds), **Climate Loom** (farms — place 6×6 landmark), **Aegis Spire** (draw + rim pressure — landmark), **Deep Archive** (lab ticks — landmark).
- [ ] Dens checkbox falls as **F2** lairs go quiet.
- [ ] Pop toward 8 with Colony Commons + farm + mine; sustain hint is readable.
- [ ] LAB ticks Field Survey toward **Lunar Rocket**. Place a **Landing Pad** — craft stages, camera glance.
- [ ] Win banner **TO LUNA** (or gates clearly incomplete). **RATING** letter on the banner.

## Replay (Weeks 8–14)

- [ ] Title and command strip show **CAMPAIGN** (or **ENDLESS** plus challenge/stance). Settings chips cycle Mode / Challenge / Stance.
- [ ] Doctrine applies live: Open Hands takes cheaper flags (default $70 Build); Aegis Watch hunts/shops more; Survey First considers farther. Stockpile/fauna need **New Game** (Austere / Swarm). Tight Purse ship rules apply when you leave Settings.
- [ ] **Continue** does not re-scale a saved stockpile after Austere.
- [ ] Austere New Game: Colony Commons + airlock + HAB + one workshop still affordable.
- [ ] Swarm: more F5/F2 after farm/power/HAB, not a wipe on the empty drop.
- [ ] Endless win: body keep-colonizing line, rating letter + breakdown, no **TO {next}**. Shift+F10 still hops bodies.
- [ ] Select a robot: card shows voice copy. Claim logs a class line (Anvil / Aegis / Horizon…).
- [ ] Non-default rules log `Replay: …` on play start.

## 45–90 minute arc (when you have the time)

- [ ] Luna: crater lighting, pop 12, Mars Ship research, **TO MARS**. **Ash hoppers** on the HAB (**F2**). Dust ticks on mines (**F5**).
- [ ] Mars: red grade, pop 16, **Belt Hauler**, **TO BELT**. Dust wisps on Power (**F2**). Dust creepers on farms (**F5**).
- [ ] Belt: dark islets, fat MET nodes, farms starve, **Icebreaker**, **TO EURO**. Seed line shows **LOW-G**. Rock mites and ticks on mines (**F5**). Shard hoppers on HABs (**F2**).
- [ ] Europa: ice plates, ICE-heavy nodes, PWR draw high, **SOLAR CONQUEST COMPLETE**. Seed line shows **RAD**. Walk a robot off campus — cyan flash and HP drain. Fissure leeches and ice wisps on Power (**F2**). Ice creepers on farms (**F5**).
- [ ] Clear all dens: overseer log that campus pests scatter by name (creepers, hoppers, mites, ticks, leeches, wisps); they walk off and despawn.
- [ ] Launch hop spends freight MET/ICE (or a 12% jettison line if the stockpile cannot pay).
- [ ] Construction sparks + complete burst; extract ping; each body sun/grade reads different.
- [ ] No click-to-move. Outdoor units are workshop robots only.

## Phase 4 Week 1 (visual)

- [ ] Mars: reddish cratered ground, hazy orange sky, long shadows; distant dust-devil dressing (not a new threat).
- [ ] Earth New Game (not Mars): lush meadow albedo, cobalt sky, long shadows, distant cumulus, carbon/orange claim chevrons. Must not look like the pre-Phase-4 olive plane. Dust-devils stay Mars-only.
- [ ] Colony Commons + airlock: **panel-lined** orange-framed square hub (not a flat cube), junction turret, corrugated tube cladding on the joint (not through the dome). Extra tubes are dressing — pathing is still the Lego grid. Commons is a white/orange **command-dome citadel** (hero FBX or procedural), not a pale grey box. Orange collars meet module faces flush.
- [ ] While a module is building: yellow gantry crane + incomplete cladding; site clears on complete.
- [ ] HUD: top strip REG / ICE / MET / PWR / BEDS with rates and color swatches; Sol counter; gold-carbon chrome; bounty log on conquest gates; roster is IDLE/WORK/REST status (click inspects only). Minimap titled MAJESTY COLONY, click pans camera, never path-commands.
- [ ] Dock squares BLD / FLG / TEC / CAM / PTY / MENU map to B, G, T, campus focus, party, Esc — not unit orders.

## Phase 4 Week 2 (hero kits)

- [ ] HAB: **horizontal cylinder** on black skids, white shell, black bands, orange access, **carbon panel rings + spine seam** — living module, not a box. Same 4×4 footprint.
- [ ] Colony Commons: **smooth command dome** on a dark mechanical ring, two-tier cupola, equatorial panel rings + drum meridians, radial tube stubs (cardinals reach the 6×6 face; square airlocks still attach). Same 6×6 footprint. HUD **COMMONS**.
- [ ] Landing Pad: dark circular disc, **orange concentric rings + H**, cardinal ticks, parked white/black Starship stack with heat-shield belly + forward flaps (visual only). Launch gate still needs the pad + tech.
- [ ] Farm = **AG-1 vaulted greenhouse** + ice tanks/scaffold (water-ice extractor, not a HAB). Regolith Camp = low **drum + hopper**. Mine = twin silos + **A-frame** headframe.
- [ ] Ghosts in **B** show the same silhouettes. Empty ground click still does not repath robots.
- [ ] Shift+F10 Mars: same kits with a warm white hull grade; tubes still dressing.

## Phase 4 Week 3 (turrets + solar)

- [ ] Airlock Junction: dual-barrel white/orange turret with cyan lenses on the **panel-lined** square hub (inset carbon hatches, orange collars at docked joints only). Dressing only — no click-to-fire, not a selectable unit. Cardinal module sleeves read as white tubes + orange collars **flush to the hub** (no gap / overlap through the hull).
- [ ] Power Node: **PWR-1 hut** plus a **field** of tilted blue-cyan panels with orange corner brackets on the 4×4 footprint, not a single panel beside the building. Ghost in **B** matches.
- [ ] Command / Defense: **Defense Battery** angular bunker + roof turret, not a Commons citadel-dome. Week 1 shield bubble still wraps it. B menu says Defense Battery, never Command / CMD-1.
- [ ] Engineer reads as a small white biped with backpack; Geologist as a six-wheel rover; Scout as a hovering probe with rotors and a boxy cyan-lens head. Defense mech is still the bulky tracked guardian (red viewport, not a biped walker).
- [ ] Empty ground click still does not repath robots.

## Phase 4 Week 4 (Commons + dress)

- [ ] Tutorial 1/6 and drop manifest say **COMMONS** / **Colony Commons**, never Palace.
- [ ] Guild Hall: **CMD-1** stepped civic (dark skirt, orange door columns, roof sensor, banner mast, **hull panel bands**). LAB: **horizontal cylinder** (LAB-1) with carbon rings + spine. Climate Loom = lattice + cooling towers; Aegis Spire = tapered rings; Deep Archive = buried silos — not generic boxes. OPS-1 Mining annex has wrap bands + roof seams. Ghosts in **B** match.
- [ ] Medic reads as a hover capsule (cyan cross, IV pole, four hover discs); Harvester as a tracked orange-blade hopper + side arm; Surveyor as a tripod mast; Courier as a six-wheel white-crate hauler; Sentinel as a squat dual-barrel turret on **continuous treads** (not the Defense Guardian, no red viewport). Engineer/Geologist/Scout unchanged from sheet-matched meshes.
- [ ] Terraformer reads as a **tracked dozer** with an **orange front blade** and **orange rear rake** — not a biped Engineer, not a Harvester hopper. Geologist reads as a compact 6×6 with a **vertical** core-drill, not a Courier freight bed.
- [ ] Empty ground click still does not repath robots.

## Phase 4 fauna silhouettes

- [ ] Earth: **Soil Creeper** is a long graphite isopod (one olive segment) on farms (**F5**); **Watt Leech** is a **white** flat ray with a cyan dorsal groove on Power (**F2**) — they must not look like the same sausage.
- [ ] Luna: **Ash Hopper** is a tall six-leg shrimp/flea on HABs (**F2**); **Rock Tick** is a wide crab on mines (**F5**).
- [ ] **Regolith Mite** is a compact pillbug (longer than wide, six stub legs) on extractors (**F5**) — not a Tick crab.
- [ ] **Ice Wisp** / dust wisp is a hovering seven-point ice-star (no rotors) on Power (**F2**) — not a Scout drone.
- [ ] Stalker (lair, **F2**) is a long spined predator, not a beetle. Shift+F10 Mars: dust creeper / dust wisp still use those silhouettes with body tints.
- [ ] Defense mech is still the bulky **tracked** guardian (not a biped walker).

## Phase 4 hero building FBX

- [ ] Play Mode HAB / Colony Commons / Power / Farm / Camp / Mine / Defense / pad / guild / LAB / wonders / **workshop / Inn** / **OPS-1** prefer `SM_Hero_*` FBX. HAB / Commons / LAB / Guild / OPS hulls show **panel lines** (rings, meridians, wrap bands). Console `[HeroKit] Attached …` on first place. Engineer workshop is the hangar; Defense/Sentinel workshop is the taller roof-turret hangar; Inn is the porch-lantern hall. If an FBX is missing, the procedural hero kit still appears (warning logged).
- [ ] Cardinal **square airlocks** still attach **flush at the module face** (white sleeve + orange collar, no gap / hull punch-through). Commons stays the 6×6 first landmark.
- [ ] Ghosts in **B** match the FBX silhouettes. Empty ground click still does not repath robots.

## Phase 4 remaining Imagine sheets + Mars close

- [x] Remaining-class / fauna Imagine JPGs are in `ConceptSheets/` (`Docs/GROK_IMAGINE_UNIT_PROMPTS.md` table). Blender is sheet-matched. Do not invent an 11th class.
- [x] Hero buildings sheet-matched to existing ConceptSheets: HAB-1 cylinder, command-dome Commons, LAB-1 cylinder, PWR-1 + solar field, pad + Starship stack. Units/fauna not redone this slice.
- [x] CMD-1 / OPS-1 vs `SM_CMD-1_OPS-1_CommandOps_Turnaround.jpg`: Guild Hall = CMD-1 civic dress (not Commons); Mining = OPS-1 annex (`SM_Hero_OPS`). Wonders + Farm/Camp/Mine remeshed. Defense bunker labeled **Defense Battery**.
- [ ] Shift+F10 Mars: packed-dust **plus grey paved slabs** under modules, extra crates/cones, orange rings on dock tubes, gold/cyan status pips over Commons / Power / Defense, taller far-right dust devil, warmer orange sky + long shadows. Empty Mars (no buildings) should show boulder/dune/crater vista + node outcrops + dens, not a tiled plane of cubes. HUD chips show color swatches; minimap reads **MAJESTY COLONY**; dock has PTY (party, not a move order). Earth New Game stays meadow + cobalt (no Mars grey apron).
- [ ] Empty ground click still does not repath robots. Do not treat this as Phase 4 exit — editor still `Docs/Roadmap/SM_MarsCampaign_EditorStill.png` and Game-tab empty-Sol-1 still `Docs/Roadmap/SM_MarsCampaign_PlayModeStill.png` exist; a **placed Commons+airlock+HAB** Game-tab still vs the PNG is still required. See [PHASE_4_EXIT.md](Roadmap/PHASE_4_EXIT.md).

## Fail if

- Play Mode throws on world gen (yield labels, NavMesh, missing GameLoop).
- A specialist walks to an empty-ground click.
- New Game does not land on Earth, or Continue silently does nothing with a save present.
- New Game Earth looks like the pre-Phase-4 olive/grey plane (no meadow, no cobalt sky, no claim chevrons, no HUD chips).
- Launch gate completes from tech alone (Landing Pad required).

Debug: **Shift+click MARS?** on the Earth drop (tutorial OK) hops to Mars; Game-tab empty-Sol-1 still saved at `Docs/Roadmap/SM_MarsCampaign_PlayModeStill.png`. **Shift+F10** unlocks all then cycles Earth→Luna→Mars (macOS may steal F10 — use the chip). **F8** score HUD. **Y** revive / dismiss win.
