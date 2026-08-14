# Solar Majesty — Phase 1 smoke test

Run this before showing the demo. Unity **Game** tab, not Scene view. Menu **Solar Majesty → Play Demo** (or Open Demo Scene → Play).

Continue restores **campus + stockpile + research + body**. Flags and fauna respawn. Campus is per-world (Earth keep stays on Earth).

---

## 10-minute boot (must pass)

- [ ] Title over a frozen drop. WASD pans. Tagline names the Overseer fantasy.
- [ ] **New Game** (confirm wipe if a save exists) → Earth empty drop, orange claim disc, no starter robots.
- [ ] Yield labels on resource nodes (`REG` / `MET` / `ICE`) — no `MissingComponentException`.
- [ ] Tutorial 1/6: **B**, key **1**, Palace on the claim.
- [ ] 2/6: Airlock Junction on a Palace face socket.
- [ ] 3/6: HAB on that airlock (humans indoor).
- [ ] 4/6: Engineer (or Scout / Defense) workshop → robot fabricates when the site finishes.
- [ ] Empty ground click does **not** repath the robot.
- [ ] 5/6: **G**, post a flag. Pole shows tempted class or *ignored — raise $*.
- [ ] Engineer ignores default Build **$70**; **+** to ~**$90** and they take it.
- [ ] 6/6: **T** opens research. **SKIP** dismisses the bar. Settings → **Replay tutorial** brings it back.
- [ ] **Esc** pause → Resume / Settings / Title / Quit. Title **Continue** shows body + MET + module count.
- [ ] Build Palace + airlock, Title → Continue: keep and airlock are back; workshop robots refabricate.

## 20-minute Earth loop

- [ ] Farm + Mine docked; command chips flash if ICE/MET/REG/PWR are short.
- [ ] Power Node: gen covers draw. **Watt Leech** → **F2 Clear Threat**.
- [ ] Farm: **Soil Creeper** (olive millipede, **CREEP STEAL**) → **F5 Defend Area**. Regolith mites may still hit extractors.
- [ ] **F4 Extract** on a node updates remaining yield. Near a matching Mine/Farm the HUD shows `via … · ~100%`; far from campus it reads `loose haul · ~40%`.
- [ ] No Landing Pad: ship countdown tagged `(no pad)`; when the timer hits, toast **waved off** and stockpile does not jump.
- [ ] After a pad: Earth package docks (Luna+ spends a MET fee).
- [ ] Cyan disc NE of the keep (Campus B): after Palace, place a Mine/Farm/Pad/Power/Defense **or Harvester/Defense workshop** on it without airlocks. HUD **OUTPOST**; PWR draw ticks up. **F7** — hurt robots rest at the cyan beacon, not the A inn.
- [ ] **T** research: Guild Charter → Guild Hall (HUD **GUILD**). Select the hall and assign **SCOUT / ENG / DEF / MED** — flags nearby pull that class (inherits nearest workshop if you skip). Harvest Doctrine unlocks Harvester Workshop; Survey Doctrine unlocks Surveyor. **Terraform Charter** / **Freight Doctrine** unlock Terraformer / Courier. **Core Sampling** / **Perimeter Doctrine** unlock Geologist / Sentinel (Blender silhouettes).
- [ ] Flag **I** Research Site feeds the active tech. **O** on the cyan disc claims OUTPOST. **U** Terraform greener farms.
- [ ] ★ Secret Projects spend stockpile on complete: Anvil (mines), Skyhook (freight), Gene Vault (beds), **Climate Loom** (farms — place 6×6 landmark), **Aegis Spire** (draw + rim pressure — landmark), **Deep Archive** (lab ticks — landmark).
- [ ] Dens checkbox falls as **F2** lairs go quiet.
- [ ] Pop toward 8 with Palace + farm + mine; sustain hint is readable.
- [ ] LAB ticks Field Survey toward **Lunar Rocket**. Place a **Landing Pad** — craft stages, camera glance.
- [ ] Win banner **TO LUNA** (or gates clearly incomplete). **RATING** letter on the banner.

## Replay (Weeks 8–14)

- [ ] Title and command strip show **CAMPAIGN** (or **ENDLESS** plus challenge/stance). Settings chips cycle Mode / Challenge / Stance.
- [ ] Doctrine applies live: Open Hands takes cheaper flags (default $70 Build); Aegis Watch hunts/shops more; Survey First considers farther. Stockpile/fauna need **New Game** (Austere / Swarm). Tight Purse ship rules apply when you leave Settings.
- [ ] **Continue** does not re-scale a saved stockpile after Austere.
- [ ] Austere New Game: Palace + airlock + HAB + one workshop still affordable.
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
- [ ] Palace + airlock: orange-framed square hub and corrugated tube cladding. Extra tubes are dressing — pathing is still the Lego grid.
- [ ] While a module is building: yellow gantry crane + incomplete cladding; site clears on complete.
- [ ] HUD: top strip REG / ICE / MET / PWR / BEDS; gold-carbon chrome; bounty log on conquest gates; roster is IDLE/WORK/REST status (click inspects only). Minimap click pans camera, never path-commands.
- [ ] Dock squares BLD / FLG / TEC / CAM / MENU map to B, G, T, campus focus, Esc — not unit orders.

## Phase 4 Week 2 (hero kits)

- [ ] HAB: octagonal white pressurized dome, orange mid-stripe, cyan viewports — living module, not a box. Same 4×4 footprint.
- [ ] Palace Keep: larger octagonal citadel (flat cap, double orange bands, comms mast, cardinal dock collars). Same 6×6 footprint.
- [ ] Landing Pad: circular grey disc, yellow outer ring, radial marks, parked white/black Starship stack (visual only). Launch gate still needs the pad + tech.
- [ ] Farm = tall **water-ice** extractor (vertical tanks, cyan ice bands, scaffold tower). Regolith Camp = low **regolith** kit (horizontal orange/yellow pipes, hopper). Mine stays twin-silo ore, not a HAB.
- [ ] Ghosts in **B** show the same silhouettes. Empty ground click still does not repath robots.
- [ ] Shift+F10 Mars: same kits with a warm white hull grade; tubes still dressing.

## Phase 4 Week 3 (turrets + solar)

- [ ] Airlock Junction: dual-barrel white/orange turret with cyan lenses on the square hub. Dressing only — no click-to-fire, not a selectable unit.
- [ ] Power Node: **field** of tilted blue-cyan panels on the 4×4 footprint (inverter hut + glow bus), not a single panel beside the building. Ghost in **B** matches.
- [ ] Command / Defense: angular bunker + roof turret, not a Palace keep-dome. Week 1 shield bubble still wraps it.
- [ ] Engineer reads as a small white biped; Geologist as a six-wheel rover; Scout as a hovering probe with rotors. Defense mech is still the bulky tracked guardian (not a biped walker).
- [ ] Empty ground click still does not repath robots.

## Fail if

- Play Mode throws on world gen (yield labels, NavMesh, missing GameLoop).
- A specialist walks to an empty-ground click.
- New Game does not land on Earth, or Continue silently does nothing with a save present.
- Launch gate completes from tech alone (Landing Pad required).

Debug: **Shift+F10** unlocks all bodies. **F8** score HUD. **Y** revive / dismiss win.
