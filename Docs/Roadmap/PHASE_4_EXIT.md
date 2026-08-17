# Phase 4 — Exit review

**Date:** 2026-08-15  
**Status:** **EXIT BLOCKED.** Not complete. **Not ready for Phase 5.**

Phase 4 pushed the Mars-campaign mockup into engine: atmosphere, square-dock tube campus, hero kits / FBX, Imagine-sheet units, carbon HUD chrome. Core systems were not rewritten: `SpecialistBrain` scoring is unchanged; the player still never path-commands units. Colony Commons is the civic name (never Palace).

Six real stills now exist. The latest Game-tab **campus** shot (`SM_MarsCampaign_PlayModeCampusStill4.png`) is the follow-up after the post-v3 Play Mode pass (hidden unused stubs, larger square hub, snap zoom 7, no fauna zoom-out). It still does **not** stamp exit. Code after v4 is not in that PNG. Leftovers below are visual-target gaps, not Phase 5 ship polish.

---

## Still captured

| Shot | Path / result |
|------|----------------|
| **Editor Camera.Render** | [`SM_MarsCampaign_EditorStill.png`](SM_MarsCampaign_EditorStill.png) — **real PNG**, not invented. `DemoContentBuilder.CaptureMarsStill` (Unity 6000.5.6f1 `-executeMethod`, avgLum 90.6 `CAPTURE_OK`). Mars albedo + long shadows; **Colony Commons** dome + square airlock + HAB cylinder. No IMGUI HUD. Hulls in this edit-mode path read **dark Mars-grade** with bright dock ports, not the sheet’s white/orange. Pad / solar / extractors / units / fauna were not spawned. |
| **Game-tab Play Mode (empty)** | [`SM_MarsCampaign_PlayModeStill.png`](SM_MarsCampaign_PlayModeStill.png) — **real PNG**, human Game-tab capture. **SOLAR MAJESTY \| Mars · Sol 1 · CAMPAIGN · AEGIS WATCH.** Tutorial **1/6 COMMONS**. **POP 0/16**, **BEDS 0/0**, no campus. HUD chrome is live. `spawnShowcaseColony` stayed false. Archived; not the campus sign-off. |
| **Game-tab Play Mode (campus v1)** | [`SM_MarsCampaign_PlayModeCampusStill.png`](SM_MarsCampaign_PlayModeCampusStill.png) — **real PNG**, human Game-tab capture (1024×428). Built campus on the orange claim. `spawnShowcaseColony` stayed false. **Not** a mockup match. Before-shot for the first Play Mode lighting/tube pass. |
| **Game-tab Play Mode (campus v2)** | [`SM_MarsCampaign_PlayModeCampusStill2.png`](SM_MarsCampaign_PlayModeCampusStill2.png) — **real PNG**, human Game-tab capture (1024×419). Follow-up after re-entering Play Mode for white hub / joint tubes / white hulls / closer camera. Tutorial **4/5 Workshop**. `spawnShowcaseColony` stayed false. **Not** a mockup match. |
| **Game-tab Play Mode (campus v3)** | [`SM_MarsCampaign_PlayModeCampusStill3.png`](SM_MarsCampaign_PlayModeCampusStill3.png) — **real PNG**, human Game-tab capture (1024×418). After re-entering Play Mode for the post-v2 pass. Tutorial **4/6 Workshop**. **POP 3/16**, **BEDS 3/3**. `spawnShowcaseColony` stayed false. **Not** a mockup match. |
| **Game-tab Play Mode (campus v4)** | [`SM_MarsCampaign_PlayModeCampusStill4.png`](SM_MarsCampaign_PlayModeCampusStill4.png) — **real PNG**, human Game-tab capture (1024×421). After re-entering Play Mode for white square hub / tubes on docked faces only / closer camera that fauna does not yank out. Tutorial **4/5 Workshop**. **POP 3/16**, **BEDS 3/3**. `spawnShowcaseColony` stayed false. **Not** a mockup match. |

Do not treat the editor PNG, the empty Play Mode PNG, or campus v1–v3 as the Phase 4 campus sign-off shot. Campus v4 **is** the requested post-v3 still; it still fails the mockup bar (see below). Code after v4 is not in that PNG.

---

## What campus v4 actually shows

Read from the PNG pixels (not captions). 1024×421 Unity editor Game-tab grab. Sampled every 2nd pixel: **93** pixels with R,G,B > 220 (v3: 54; v2: 19; v1: 8; mockup: 726). Near-white > 180: **634** (v3: 272; mockup: 2956). Hulls are the whitest Play Mode campus yet and still far from the sheet. Near-white cluster bbox **216×152** (v3: **129×90**) — closer than v3, still a short-wide Game tab with dirt on all sides.

**World**
- Orange-red cratered Mars, isometric Game tab, long shadows
- **Colony Commons:** geodesic dome on a packed-dust disc — right civic silhouette, **not** a white square hub. Cyan equatorial band + cupola beacon read
- **HAB-1:** white horizontal cylinder with a carbon band, docked on the left — hero kit reads
- **Airlock:** the 2×2 joint reads as a **bright orange rectangular box** (wrap frames + proud orange doors), not a white paneled square. A second orange box / unused **ribbed stub** sticks off the right of the dome. Junction turret sits on the orange box
- **Tubes:** HAB join is flush orange box-to-cylinder, not a mockup-length white corridor. Unused face still shows an orange stub — last pass’s “docked faces only” did not land
- **Pads:** **no grey hex slabs**
- **Hopper:** small dark multi-leg fauna at the HAB apron; **no idle DUST HOPPER chip**
- Camera closer than v3 (white cluster ~1.7×) but still empty-drop-wide vs the mockup’s packed campus. Post-v3 ortho 7 did not fully fill this Game tab (aspect ~2.4)

**HUD (readable on this PNG)**
- Header: **SOLAR MAJESTY** · Mars · Sol 1 · CAMPAIGN · AEGIS WATCH
- Top chips: **REG 110 / ICE 0 / MET 170 (+4) / PWR 77** (deficit) **/ BEDS 3**
- Left: **OVERSEER ACTIVE**; MARS seed 29311; planet chips **EARTH / LUNA / MARS / BELT / EURO**; Drop Manifest lists Colony Commons, Hab Module (HAB-1), Airlock Junction, Engineer Workshop
- Right: **ACTIVE BOUNTIES** — Clear dens (10/10 left), Sustain colony (pop 3/16), Launch craft
- Tutorial **4/5 Workshop** — dock Scout / Engineer / Defense
- Dock: **BLD / PLC / TEC / CAM / PTY / MENU**; **THREAT** ~50%
- Minimap title **MAJESTY COLONY**

---

## Did the last pass land?

| Ask | In campus v4? |
|-----|----------------|
| White square hub | **No** — geodesic Commons is the silhouette; the 2×2 joint is an orange box |
| Round tubes on docked faces only | **No** — orange box join + unused orange ribbed stub on the right |
| Closer camera / fauna does not yank out | **Partial** — ~1.7× closer than v3; Game tab still shows a dirt vista; hopper is in frame without a nameplate |
| No grey hex slabs | **Yes** |
| Whiter hulls | **Partial** (93 bright samples vs v3’s 54 vs mockup 726) |
| No giant idle hopper chip | **Yes** |

---

## What shipped

| Slice | In |
|-------|-----|
| Week 1 | Mars albedo / hazy orange sky / long shadows / dust-devil dressing; corrugated tubes + orange square airlock hubs; yellow gantry cranes; Overseer HUD carbon/gold chrome |
| Week 2 | HAB-1 cylinder / Colony Commons command dome / pad+Starship / water vs regolith extractor hero kits on the square Lego grid |
| Week 3 | Junction turrets; PWR-1 + solar-field landmark; Defense Battery bunker (not Commons) |
| Week 4 | Commons rename; guild/lab/wonder dress; all ten specialists + seven fauna sheet-matched; Terraformer dozer |
| Week 4 continued | Earth New Game meadow + cobalt sky; Workshop / Inn FBX; remaining Imagine JPGs; HAB/Commons/LAB/Power/pad sheet-match; **CMD-1 Guild / OPS-1 Mining**; airlock panel lines; HAB/LAB/Commons/CMD/OPS **panel bevels**; **dock sockets flush** at the Lego face |
| This review | Editor Mars still + empty-Sol-1 Game-tab still + campus v1 + v2 + v3 + **campus v4** + Play Mode fixes aimed at v4 (see below). Gameplay remains Overseer-only |

**Play Mode fixes after campus v1 (visible in v2 only as geodesic dome + HUD; tubes/pads/hulls still failed)**
- Stop overlaying greybox `SM_ModularTubeConnector` on the paneled airlock hub
- Cardinal dock sleeves renamed `DockSleeve_*`; IndustrialArtDressing no longer maps them to solid orange
- Mars `BindBody` / `HeroHull` warmer white; grade/fill lifted
- Dressing tubes slightly longer; campus ortho 11

**Play Mode fixes after campus v2 (visible in v3 as: no hex slabs, no hopper chip, HAB cylinder, somewhat whiter hulls)**
- Airlock cube plus → four round dock stubs on a square white hub
- Connected dock sleeves are cylinders; unused cardinal sleeves + unused Commons cardinal stubs **meant** to hide (did not fully land — see v3 unused orange stubs)
- Grey cube apron slabs removed
- Hopper scaled; fauna world labels only when aggro/raid/scatter
- Campus ortho 8.5 — **not in v3 PNG** (fauna glance reset zoom to 16)
- `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite

**Play Mode fixes after campus v3 (visible in v4 as: whiter hulls, no hex pads, no hopper chip, somewhat closer camera; hub/tubes/unused stub still failed)**
- Fauna / minimap `GlanceAt` no longer forces empty-drop ortho 16; Commons/HAB/workshop **snap** to campus ortho **7**
- Live dock sleeves and airlock `Dress_TubeArm` start **hidden**; RefreshTubes enables docked faces only
- Procedural Commons (FBX skipped — joined mesh baked unused radial stubs). No diagonal stubs. Cardinal stubs start off
- Larger white square airlock hub (~2.1 m) in the 2×2 cell
- Dressing corridor is white + carbon ribs (orange collars at the ends only)
- FindPieceGo skips construction `Site_` props; prefers `Bld_` / airlock
- Commons packed-dust apron smaller; Mars fill/grade slightly cooler so hulls can read white
- `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite

**Play Mode fixes after campus v4 (in code, not in that PNG)**
- Airlock hub is a **smaller white paneled square** (~1.68 m in the 3 m cell) with inset face plates and carbon seams. The v4 2.4 m cube filled the cell and ate the tubes
- Orange lives only on **one round collar at each docked Lego face**. Extra inset orange rings removed from `DockSleeve`
- `RefreshTubes` **hides every** `Dress_TubeArm` / `DockSleeve` / `CommonsStub` first, then enables **only** docked `Dress_TubeArm` + `DockSleeve`. `CommonsStub` stays off for the life of the kit (that was the unused orange rib)
- No fourth `CampusTubeRoot` corridor — stacked orange collars in the 0.3 m gap were the HAB-join orange box
- Short white joint = airlock stub (~0.64 m) + module sleeve lip (~0.30 m outset, 0.28 m inset — no punch-through)
- Skip `IndustrialArtDressing` orange mapping on `airlock` names; `CommonsStub` / dock sleeves skipped
- Scene `minZoom` was **6** and clamped CampusOrthoSize 5.5. Runtime + scene now **4.5**. Snap still 5.5. `GlanceAt` never passes a zoom-out once pieces exist
- Mars `BindBody` is sheet-white (no dirt lerp). WhiteHull albedo dirt reduced
- `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite

---

## Match vs `SM_MarsCampaign_VisualTarget.png`

Compared to the mockup. Honest split: HUD + Mars ground vs campus v4.

**Directionally in (campus v4)**
- Orange-red Mars ground, isometric camera, long shadows
- Carbon/gold HUD chrome with **REG / ICE / MET / PWR / BEDS**
- Commons **geodesic dome** + HAB cylinder on the claim
- Empty start; player-placed campus (`spawnShowcaseColony` false); tutorial 4/5 workshop
- No grey hex pads; no idle hopper nameplate
- Hulls whiter than v3

**Mismatch on campus v4 (why exit stays blocked)**
- **Unused orange ribbed stub** + **orange box airlock** (white square hub does not read)
- Docked HAB join is an orange box, not a white corridor with orange collars only at the joint
- Hulls still Mars-washed vs sheet white (93 bright samples vs mockup 726)
- Camera closer than v3 but still a dirt vista vs the mockup’s packed campus close-up
- Mockup density (pad + Starship, solar field, extractors, units) is not in this early campus
- Mockup circular HAB cluster vs our square-dock graph (placement model stays square)
- IMGUI carbon/gold vs the mockup’s painted HUD
- Flat albedo + scatter vs mockup crater **heightmap** (Phase 4 non-goal)

**Not the PNG (leftovers even after the post-v4 Play Mode pass)**
- **Fresh Game-tab still required** — this pass is not in campus v4
- Tracked Defense Guardian vs the mockup’s bulky **biped walker**
- Construction cranes are runtime dressing, not authored FBX
- Status pips / aprons / dust-devils are primitive dressing
- Built-campus **density** (pad / solar / extractors / units in one shot) still needs a later Play Mode still
- Commons hero FBX skipped until a stub-free re-export; procedural geodesic is the live kit

---

## Leftovers (stay in Phase 4 — not Phase 5 polish)

- **Fresh Game-tab still** after this pass (smaller paneled hub, no unused CommonsStub, no stacked orange collars, snap zoom 5.5 that actually lands). Campus v4 is the before-shot.
- Mockup **density**: pad + Starship, solar field, extractors, units in the same frame
- Defense PNG **biped walker** (live mesh stays the Imagine **tracked** guardian so it does not clone Engineer)
- Circular HAB cluster vs square docks (placement model stays square; tubes are dressing)
- IMGUI HUD vs painted mockup chrome (material language shipped; painted fidelity is leftover)
- Heightmap terrain (explicit Phase 4 non-goal unless iso readability fails)
- Titan / Continue snapshot gaps / external 45–90 min playtest (same leftovers as Phase 2 / 3)

---

## How to smoke

1. `Docs/SMOKE_TEST.md` Phase 4 sections (Earth meadow New Game, then **Shift+click MARS?** or Shift+F10 Mars). Empty Mars should show boulder/dune/crater vista + node outcrops + dens, not a tiled plane of cubes. `spawnShowcaseColony` stays false.
2. On Mars: **B**, key **1**, Colony Commons on the orange claim → airlock on a face socket → HAB. Look for a **white paneled square hub** with **round white tubes + orange collars on docked faces only**, HAB cylinder + Commons dome that stay **readable white** against the red ground, **no grey hex slabs**, camera snapping to ortho 5.5 on the campus centroid (hopper spawn must not pan or zoom out). Hopper should not wear a giant idle **DUST HOPPER** chip. Empty ground click still must not repath robots.
3. Menu **Solar Majesty → Capture Mars Still** (or `-executeMethod SolarMajesty.EditorTools.DemoContentBuilder.CaptureMarsStill`) regenerates the editor PNG only — not a HUD still.

---

## Ready for Phase 5

**No.** Campus v4 is still the latest Game-tab still and it fails the mockup (orange box airlock, unused orange stub, dirt vista). Code after v4 is not in that PNG. Remaining work is still visual-target, not ship polish. Next work stays [`05_PHASE_4_VISUAL_TARGET.md`](05_PHASE_4_VISUAL_TARGET.md). Do not start Phase 5 as the main slice. After a true exit: [`06_PHASE_5_PRODUCTION_VALUES_SHIP.md`](06_PHASE_5_PRODUCTION_VALUES_SHIP.md).
