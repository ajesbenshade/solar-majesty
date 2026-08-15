# Phase 4 — Exit review

**Date:** 2026-08-15  
**Status:** **EXIT BLOCKED.** Not complete. **Not ready for Phase 5.**

Phase 4 pushed the Mars-campaign mockup into engine: atmosphere, square-dock tube campus, hero kits / FBX, Imagine-sheet units, carbon HUD chrome. Core systems were not rewritten: `SpecialistBrain` scoring is unchanged; the player still never path-commands units. Colony Commons is the civic name (never Palace).

A real editor still and a real Game-tab Play Mode still now both exist. The Game-tab shot is **empty Mars Sol 1** (tutorial 1/6 COMMONS, POP 0, no buildings). That is **not** a campus-vs-mockup sign-off. Leftovers below are visual-target gaps, not Phase 5 ship polish.

---

## Still captured

| Shot | Path / result |
|------|----------------|
| **Editor Camera.Render** | [`SM_MarsCampaign_EditorStill.png`](SM_MarsCampaign_EditorStill.png) — **real PNG**, not invented. `DemoContentBuilder.CaptureMarsStill` (Unity 6000.5.6f1 `-executeMethod`, avgLum 90.6 `CAPTURE_OK`). Mars albedo + long shadows; **Colony Commons** dome + square airlock + HAB cylinder. No IMGUI HUD. Hulls in this edit-mode path read **dark Mars-grade** with bright dock ports, not the sheet’s white/orange. Pad / solar / extractors / units / fauna were not spawned. |
| **Game-tab Play Mode** | [`SM_MarsCampaign_PlayModeStill.png`](SM_MarsCampaign_PlayModeStill.png) — **real PNG**, human Game-tab capture. **SOLAR MAJESTY \| Mars · Sol 1 · CAMPAIGN · AEGIS WATCH.** Tutorial **1/6 COMMONS**. **POP 0/16**, **BEDS 0/0**, no campus. HUD chrome is live. `spawnShowcaseColony` stayed false. |

Do not treat either still as the Phase 4 campus sign-off shot. The editor PNG has a three-piece spine and no HUD. The Play Mode PNG has HUD and empty ground.

---

## What shipped

| Slice | In |
|-------|-----|
| Week 1 | Mars albedo / hazy orange sky / long shadows / dust-devil dressing; corrugated tubes + orange square airlock hubs; yellow gantry cranes; Overseer HUD carbon/gold chrome |
| Week 2 | HAB-1 cylinder / Colony Commons command dome / pad+Starship / water vs regolith extractor hero kits on the square Lego grid |
| Week 3 | Junction turrets; PWR-1 + solar-field landmark; Defense Battery bunker (not Commons) |
| Week 4 | Commons rename; guild/lab/wonder dress; all ten specialists + seven fauna sheet-matched; Terraformer dozer |
| Week 4 continued | Earth New Game meadow + cobalt sky; Workshop / Inn FBX; remaining Imagine JPGs; HAB/Commons/LAB/Power/pad sheet-match; **CMD-1 Guild / OPS-1 Mining**; airlock panel lines; HAB/LAB/Commons/CMD/OPS **panel bevels**; **dock sockets flush** at the Lego face |
| This review | Editor Mars still + **Game-tab empty-Sol-1 still** + this blocked exit. Empty-Mars scatter dressing (nodes / dens / vista boulders) so the drop is not a tiled plane + greybox cubes. Gameplay remains Overseer-only |

---

## Match vs `SM_MarsCampaign_VisualTarget.png`

Compared to the mockup. Code/dressing was already documented in [`05_PHASE_4_VISUAL_TARGET.md`](05_PHASE_4_VISUAL_TARGET.md). Honest split: HUD + Mars grade vs campus (the Play Mode still has no Commons/HAB).

**Directionally in (this Game-tab still)**
- Orange-red Mars ground, isometric camera, long shadows
- Carbon/gold HUD: **REG / ICE / MET / PWR / BEDS** chips (110 / 70 / 340 / 119 / 0/0)
- Header **SOLAR MAJESTY \| Mars · Sol 1 · CAMPAIGN · AEGIS WATCH**
- Planet chips EARTH / LUNA / **MARS** / BELT / EURO
- Bounties panel (Clear dens 10/10, Sustain colony pop 0/16, Launch craft) + FLAG LOG
- Drop manifest (Colony Commons / Hab Module / Airlock Junction / Engineer Workshop)
- Tutorial **1/6 COMMONS — B, key 1. Raise Colony Commons on the orange claim.**
- Minimap titled **MAJESTY COLONY**; dock **BLD / FLG / TEC / CAM / PTY / MENU** (PTY = party)
- Overseer readout: seed, **nodes 12 · lairs 10/10**, POP 0, PWR 0 gen / 0 draw

**Missing because this still is empty (not because the kits are unshipped)**
- Colony Commons command dome, HAB cylinder cluster, square airlock + tubes
- Landing pad + Starship, solar field, extractors, yellow cranes
- Units / fauna in frame (dens exist off the claim; fauna may be out of ortho 16)
- Mockup’s built-campus density. Kits are in engine; this shot never placed them.

**Not the PNG (leftovers even after a campus still)**
- Mockup circular HAB cluster vs our square-dock graph
- Tracked Defense Guardian vs the mockup’s bulky **biped walker**
- IMGUI carbon/gold vs the mockup’s painted HUD
- Flat albedo + scatter vs mockup crater **heightmap** (Phase 4 non-goal)
- Construction cranes are runtime dressing, not authored FBX
- Status pips / aprons / dust-devils are primitive dressing
- Editor still’s hulls read dark Mars-grade (Play Mode hero kits are meant to keep white/black/orange — verify on a **placed** Commons+HAB Game-tab shot)

---

## Leftovers (stay in Phase 4 — not Phase 5 polish)

- **Campus Game-tab still** vs the PNG (human: Play Demo → New Game or Continue → **Shift+click MARS?** while Playing, or **Shift+F10** then **F10** if macOS does not steal it → **B**, key **1**, place Colony Commons on the orange claim, then airlock + HAB → Game-tab screenshot). Empty Sol 1 is archived; it does not stamp exit.
- Defense PNG **biped walker** (live mesh stays the Imagine **tracked** guardian so it does not clone Engineer)
- Circular HAB cluster vs square docks (placement model stays square; tubes are dressing)
- IMGUI HUD vs painted mockup chrome (material language shipped; painted fidelity is leftover)
- Heightmap terrain (explicit Phase 4 non-goal unless iso readability fails)
- Titan / Continue snapshot gaps / external 45–90 min playtest (same leftovers as Phase 2 / 3)

---

## How to smoke

1. `Docs/SMOKE_TEST.md` Phase 4 sections (Earth meadow New Game, then **Shift+click MARS?** or Shift+F10 Mars). Empty Mars should show boulder/dune/crater vista + node outcrops + dens, not a tiled plane of cubes. `spawnShowcaseColony` stays false.
2. Menu **Solar Majesty → Capture Mars Still** (or `-executeMethod SolarMajesty.EditorTools.DemoContentBuilder.CaptureMarsStill`) regenerates the editor PNG only — not a HUD still.
3. Empty ground click still must not repath robots.

---

## Ready for Phase 5

**No.** Empty campus is not an exit stamp. Remaining work is still visual-target (a placed Commons+airlock+HAB Game-tab still, then square-vs-circular / tracked Defense / IMGUI / heightmap leftovers), not ship polish (audio, accessibility, save, first-hour, packaging). Next work stays [`05_PHASE_4_VISUAL_TARGET.md`](05_PHASE_4_VISUAL_TARGET.md). Do not start Phase 5 as the main slice. After a true exit: [`06_PHASE_5_PRODUCTION_VALUES_SHIP.md`](06_PHASE_5_PRODUCTION_VALUES_SHIP.md).
