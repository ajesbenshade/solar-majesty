# Phase 1 — Friction notes (Week 1)

Captured from the current Earth → Luna → Mars slice (code + last play sessions). Not a substitute for an external playtest.

## Flow
- Empty drop is correct but feels sparse until Palace + first airlock click. Tutorial beats help; briefing toast now names the body goal.
- Launch gate used to fire on tech alone, so the pad felt unused. **Fix:** tech + placed Landing Pad; craft stages with an orange beacon and camera focus.
- Sustain could complete without a Palace. **Fix:** Palace required. Hints now split housing vs population vs farm vs mine vs stockpile.
- Win/fail banners were generic. **Fix:** per-body VictoryLog / FailLog + TO LUNA / TO MARS copy.
- Body hops had no voice. **Fix:** travel log queued across reload + arrival line in the command panel.

## Still open (Week 5+)
- Continue is stockpile + body, not a full snapshot.
- Unit FBX are still Phase 0 blockouts (Imagine sheets exist; Blender remesh not run).
- External 45–90 min playtest not yet run.

## Presentation (Week 4)
Transparent construction / extract / launch VFX. Ambient beds crossfade and retune per body. Camera **glances** at Palace, pad, and first mite/leech. Earth / Luna / Mars get distinct sun angle + color grade; placed modules sprout crate/pylon scatter. Specialist cards use class color tabs and action tints. Flag popup shows posted/claimed counts.

## Economy & threat (Week 3)
Command panel flashes REG/ICE/MET/PWR when below the sustain floor (10 / 8 / 12 / 8) or when grid **draw > gen**. BEDS line alarms when full or short of the pop goal. `PWR gen/draw · upkeep · ship` plus last extract/camp tick.

Power Nodes generate **6** (Solar Array **8**) each upkeep; modules and robots draw. Robots have modest upkeep (Scout 1 PWR, Engineer 1 MET, Defense 2 PWR, Medic 1 ICE per minute).

Nodes show remaining yield (`MET 24`). Farm/Mine ticks print on the command strip.

| Fauna | Attracted by | Steals / does | Counter |
|-------|--------------|---------------|---------|
| Dust Stalker | Lairs / dens | Hunts specialists, raids village HABs | **F2 Clear Threat** (Defense hunts) |
| Regolith Mite | Farm / Mine (up to 3) | Steals ICE/MET from camps | **F5 Defend Area** |
| Watt Leech | Power Node (up to 2) | Drains POWER | **F2 Clear Threat** |

Campus piece count raises ambient threat. Extra fauna cap is 4.

## Specialists (Week 2 lock)
| Class | Greed | Wants | Ignores |
|-------|-------|-------|---------|
| Scout | low | Explore (~$40+) | Fights |
| Engineer | high | Build once pay ≥ ~$80 | Cheap flags, dens |
| Defense | low | Clear Threat / Defend | Explore / Build |
| Medic | low | Defend | Dens |

Flag poles show **tempted classes** or **ignored — raise $**. Default Build ($70) is ignored by a healthy Engineer until you nudge bounty with **+**.

## Gate summary (current)
| Body | Dens | Sustain | Launch |
|------|------|---------|--------|
| Earth | 3 lairs | Palace + pop 8 + farm + mine + stockpile, 25s | Lunar Rocket + Landing Pad |
| Luna | 8 lairs | pop 12, 40s | Mars Ship + Landing Pad |
| Mars | 10 lairs | pop 16, 50s | Mars Ship + Landing Pad (finale) |
