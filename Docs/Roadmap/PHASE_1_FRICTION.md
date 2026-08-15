# Phase 1 — Friction notes (Week 1)

Captured from the current Earth → Luna → Mars slice (code + last play sessions). Not a substitute for an external playtest.

## Flow
- Empty drop is correct but feels sparse until Colony Commons + first airlock click. Tutorial beats help; briefing toast now names the body goal.
- Launch gate used to fire on tech alone, so the pad felt unused. **Fix:** tech + placed Landing Pad; craft stages with an orange beacon and camera focus.
- Sustain could complete without Colony Commons. **Fix:** Commons required. Hints now split housing vs population vs farm vs mine vs stockpile.
- Win/fail banners were generic. **Fix:** per-body VictoryLog / FailLog + TO LUNA / TO MARS copy.
- Body hops had no voice. **Fix:** travel log queued across reload + arrival line in the command panel.

## Still open (Phase 1 exit)
- Continue is campus + stockpile + research per body (not flags, fauna, or specialist HP).
- Unit FBX are still Phase 0 blockouts (Imagine sheets exist; Blender remesh not run).
- External 45–90 min playtest not yet run.

## Presentation (Week 4)
Transparent construction / extract / launch VFX. Ambient beds crossfade and retune per body. Camera **glances** at Colony Commons, pad, and first campus pest (creeper / hopper / mite / leech). Earth / Luna / Mars get distinct sun angle + color grade; placed modules sprout crate/pylon scatter. Specialist cards use class color tabs and action tints. Flag popup shows posted/claimed counts.

## Economy & threat (Week 3)
Command panel flashes REG/ICE/MET/PWR when below the sustain floor (10 / 8 / 12 / 8) or when grid **draw > gen**. BEDS line alarms when full or short of the pop goal. `PWR gen/draw · upkeep · ship` plus last extract/camp tick.

Power Nodes generate **6** (Solar Array **8**) each upkeep; modules and robots draw. Robots have modest upkeep (Scout 1 PWR, Engineer 1 MET, Defense 2 PWR, Medic 1 ICE per minute).

Nodes show remaining yield (`MET 24`). Farm/Mine ticks print on the command strip.

| Fauna | Attracted by | Steals / does | Counter |
|-------|--------------|---------------|---------|
| Dust Stalker | Lairs / dens | Hunts specialists, raids village HABs | **F2 Clear Threat** (Defense hunts) |
| Soil Creeper (Earth) | Farm | Steals ICE from greenhouses | **F5 Defend Area** |
| Dust Creeper (Mars) | Farm | Steals ICE | **F5 Defend Area** |
| Ice Creeper (Europa) | Farm | Steals ICE | **F5 Defend Area** |
| Ash Hopper (Luna) | HAB | Raids habitats, steals ICE | **F2 Clear Threat** |
| Dust Hopper (Mars) | HAB | Raids airlocks | **F2 Clear Threat** |
| Shard Hopper (Belt) | HAB | Raids Commons / HABs | **F2 Clear Threat** |
| Regolith Mite | Farm / Mine | Steals ICE/MET from camps | **F5 Defend Area** |
| Rock Mite (Belt) | Mines first | Steals MET | **F5 Defend Area** |
| Rock Tick (Belt) | Mines | Fast swarm steal | **F5 Defend Area** |
| Dust Tick (Earth/Luna) | Mines | Camp steal | **F5 Defend Area** |
| Watt Leech | Power Node | Drains POWER | **F2 Clear Threat** |
| Fissure Leech (Europa) | Power Node | Drains POWER | **F2 Clear Threat** |
| Ice Wisp (Europa) | Power / open ice | Drains POWER, hovers | **F2 Clear Threat** |
| Dust Wisp (Mars) | Power Node | Drains POWER | **F2 Clear Threat** |

Campus piece count raises ambient threat (body `AmbientThreat` + `ExpansionThreat`). Extra fauna cap is per-body (Earth/Luna 5, Mars/Belt/Europa 6, +2 with outpost). Cleared dens scatter leftover campus pests by name.

## Specialists (Week 2 lock)
| Class | Greed | Wants | Ignores |
|-------|-------|-------|---------|
| Scout | low | Explore (~$40+) | Fights |
| Engineer | high | Build once pay ≥ ~$80 | Cheap flags, dens |
| Defense | low | Clear Threat / Defend | Explore / Build |
| Medic | low | Defend | Dens |
| Harvester | mid | Extract | Dens / tubes |
| Surveyor | low | Explore / Research Site | Fights |
| Terraformer | low | Terraform (cheap) | Dens |
| Courier | low | Explore / Outpost | Dens |
| Geologist | low-mid | Extract / Research Site | Dens |
| Sentinel | low | Defend Area | Explore / Build |

Flag poles show **tempted classes** or **ignored — raise $**. Default Build ($70) is ignored by a healthy Engineer until you nudge bounty with **+**. Open Hands (hunger +0.26) crosses the 0.75 greed bypass at spawn so that Engineer takes cheaper flags without a brain rewrite.

## Phase 3 Weeks 11–14 — strategy notes (desktop)

Three mid-game paths should all work. Do not retune `ScoreFlag` if one feels ahead — note it here after a live session.

| Path | How | Pressure |
|------|-----|----------|
| Extract / haul | Harvest Doctrine, Harvester/Geologist, F4, Anvil / Skyhook | Tight Purse slows the ship and adds a MET fee. Swarm is quiet until farms/mines exist. |
| Guild + workshop | Guild Charter, assign Horizon/Anvil/Aegis/Triage, FLAG HERE on the hall | Survey First lengthens consider range. Open Hands cheapens flags for whoever the hall pulls. |
| Aegis / defense | Aegis Watch, Perimeter / Sentinel, Aegis Spire, F5/F2 | Swarm wants this path. Open Hands does not make extract dominate: Defense/Sentinel still take Clear Threat / Defend at low greed, and pests steal ICE/MET until flags land. |

S rating cannot be bought with a fat stockpile while dens stand (letter S requires dens + gates + ≥3 robots + HP + pace).

## Gate summary (current)
| Body | Dens | Sustain | Launch |
|------|------|---------|--------|
| Earth | 3 lairs | Colony Commons + pop 8 + farm + mine + stockpile, 25s | Lunar Rocket + Landing Pad |
| Luna | 8 lairs | pop 12, 40s | Mars Ship + Landing Pad |
| Mars | 10 lairs | pop 16, 50s | Belt Hauler + Landing Pad |
| Belt | 7 lairs | pop 10, 35s | Icebreaker + Landing Pad |
| Europa | 9 lairs | pop 14, 45s | pad only (spine finale) |
