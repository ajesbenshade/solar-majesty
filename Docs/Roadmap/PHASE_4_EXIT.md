# Phase 4 — Exit review

**Date:** 2026-08-15  
**Status:** **EXIT BLOCKED.** Not complete. **Not ready for Phase 5.**

Phase 4 pushed the Mars campus into engine: atmosphere, square-dock tube campus, hero kits / FBX, Imagine-sheet units, carbon HUD chrome. Core systems were not rewritten: `SpecialistBrain` scoring is unchanged; the player still never path-commands units. Colony Commons is the civic name (never Palace).

**Look redirect (Aaron + Chief of Staff):** `SM_MarsCampaign_VisualTarget.png` is **retired**. Phase 4 EXIT look is a **spaced campus** — empty dirt is OK, fun cartoon Majesty-2 overseer feel, room for rocks / foliage / creatures and discovery. Do **not** chase that PNG’s fill-every-dirt-patch packing. still21 / still22 do not have to match that density. Dream Loop concept for this pass: [`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png) — **not an exit stamp**. GD still steps: [`DREAM_LOOP_MARS_LOOK.md`](DREAM_LOOP_MARS_LOOK.md).

Nine real stills now exist. The latest archived Game-tab shot is **`SM_MarsCampaign_PlayModeCampusStill6.png`** — Mars Sol 1 HUD, empty start, pad in the corner. `SM_MarsCampaign_PlayModeCampusStill5.png` also exists (Commons + HAB Game-tab). Packed editor still is **`SM_MarsCampaign_PackedCampusStill.png`** (`Camera.Render`, not Game-tab HUD) — archive only, not the look claim. None of these stamp exit. GD will reshoot a spaced overseer still after merge. Leftovers below are Phase 4 look gaps, not Phase 5 ship polish.

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
| **Game-tab Play Mode (campus v5)** | [`SM_MarsCampaign_PlayModeCampusStill5.png`](SM_MarsCampaign_PlayModeCampusStill5.png) — **real PNG**, human Game-tab capture. Commons dome + HAB + airlock on Mars Sol 1. Tutorial **4/6 Workshop**. **POP 2/16**, **BEDS 2/3**. Unused Commons cardinal still shows an orange port ring in this PNG. `spawnShowcaseColony` stayed false. **Not** a mockup match. |
| **Game-tab Play Mode (latest — empty Sol 1)** | [`SM_MarsCampaign_PlayModeCampusStill6.png`](SM_MarsCampaign_PlayModeCampusStill6.png) — **real PNG**, human Game-tab capture. **Latest Game-tab still.** Mars · Sol 1 · CAMPAIGN. **POP 0/16**, **BEDS 0/0**, tutorial **1/6 Commons**. Pad in the corner; not a packed campus. HUD chips **REG / ICE / MET / PWR / BEDS**. Arrival log: Dust wisps on Power, Dust creepers on the farm. Clear dens 10 left. `spawnShowcaseColony` stayed false. **Not** a campus sign-off. |
| **Editor Camera.Render (packed campus)** | [`SM_MarsCampaign_PackedCampusStill.png`](SM_MarsCampaign_PackedCampusStill.png) — **real PNG**, `DemoContentBuilder.CapturePackedMarsStill` (`Camera.Render`, **not** Game-tab HUD). Packed Commons + HAB + pad/Starship + extractors. No IMGUI HUD. **Not** a Game-tab still. |

Do not treat the editor PNG, the empty Play Mode PNG, campus v1–v5, **still6**, or the packed `Camera.Render` PNG as the Phase 4 campus sign-off shot. Still6 is the latest Game-tab still and it is an empty Sol 1 drop, not a mockup campus. Exit stays blocked.

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
| This review | Editor Mars still + empty-Sol-1 Game-tab still + campus v1–v5 + **still6** (latest Game-tab: empty Sol 1, pad in corner) + packed editor `Camera.Render` still. Look-kit hide pass for still5 unused `CommonsPort` rings (code only). Gameplay remains Overseer-only. **Not exited.** |

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
- `RefreshTubes` **hides every** `Dress_TubeArm` / `DockSleeve` / `CommonsStub` / **hull drum port** (`CommonsPort` / `HabPort` / `LabPort` / `PwrPort` and aliases) first, then enables **only** docked faces. `CommonsStub` stays off. still5 unused orange rings were `CommonsPort_*` starting active — live groups now spawn hidden; only group roots toggle so `_Ring` children do not stick
- No fourth `CampusTubeRoot` corridor — stacked orange collars in the 0.3 m gap were the HAB-join orange box
- Short white joint = airlock stub (~0.64 m) + module sleeve lip (~0.30 m outset, 0.28 m inset — no punch-through)
- Skip `IndustrialArtDressing` orange mapping on `airlock` names; `CommonsStub` / dock sleeves skipped
- Scene `minZoom` was **6** and clamped CampusOrthoSize 5.5. Runtime + scene now **4.5**. Snap still 5.5. `GlanceAt` never passes a zoom-out once pieces exist
- Mars `BindBody` is sheet-white (no dirt lerp). WhiteHull albedo dirt reduced
- `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite

**Play Mode fixes after still16 (code only — do not stamp exit)**
- Removed wrap-around `Dress_HubDoor` carbon plates that painted the 2×2 as a dark box
- Hub is a smaller white paneled square (`AirlockHubSide` 1.56 m) with white roof, thin carbon corners, small inset hatches
- One orange collar per docked `Dress_TubeArm` at the Lego face; unused arms stay hidden so unused faces stay clean
- Short white stub + white hub lip is the HAB join (not a flush black box)
- `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite

**Play Mode fixes after still18 (code only — do not stamp exit)**
- still18 signed pad / Starship / solar / extractors in the Game tab; the remaining miss is **empty-dirt framing** at play `CampusOrthoSize` 10 plus a **cube-ish** airlock (Lego-face collar hidden in the HAB join)
- CaptureStill / `StampPhase4DenseCampus` now `SnapStillCampusCamera` — fit-to-AABB iso ortho clamped 7.25–9 (play snap stays 10 so the player can still place yards)
- Docked `Dress_TubeArm` keeps the Lego-face collar and adds a proud `_HubCollar` on the white square; unused faces stay clean plates; no wrap doors
- Workshop hangar / Inn / 6×6 wonders **CanFit** south of Commons but `PlanLeftovers` skips them (`leftover=skip-frame`) — they grow the AABB past the tight still cap
- `StillCaptureHold` stays. `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` / FlagManager / Narrative / W2 toast edits
- **Needs a new Game-tab still from GD after merge. Do not stamp exit.**

**Play Mode fixes after still19 (code only — do not stamp exit)**
- still19 shuttered clean (Mars, Scale 1x, hold=True, pad/pwr/water/regolith=True, leftover=skip-frame, ortho≈9). GD signed empty-dirt framing + cube-ish airlock + leftover kits skipped
- `PlanLeftovers` now stamps Workshop / Inn / wonder when they **CanFit** (no skip-frame). Still snap fits the packed AABB; ceiling is 10 (play `CampusOrthoSize` stays 10)
- Extra HAB chain (new airlock + HAB) + workshop docked on a free airlock face so the hub reads as a multi-face joint with orange collars / `_FaceFrame`
- Extra solar bank + Defense Battery + under-construction HAB socket when they CanFit
- `RefreshTubes` finds `VillageRing` airlocks (still-chain hub) so docked arms enable
- `StillCaptureHold` stays. `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite
- **Needs a new Game-tab still from GD after merge. Do not stamp exit.**

**Play Mode fixes after still20 (code only — do not stamp exit)**
- still20 shuttered `workshop=True inn=False wonder=False leftover=workshop extraHab=True extraSolar=False defense=False ortho=10 hold=True` plus **W2 campaign cut / narrative text covering the campus**
- `PlanLeftovers` no longer stamps a second hangar that eats leftover sockets — Village Inn + a wonder still place when they **CanFit**
- Extra solar bank + Defense Battery + a second under-construction HAB socket when they CanFit (packed pockets; `MaxCenterSeparationCells` 12)
- `StillCaptureHold` suppresses arrival/mid-act cut modals, ArrivalLog / VictoryLog modal UI, and travel-toast overlays for the shutter (fail banner stay). Re-arms from editor SessionState before `BeginNarrativeSession`. `PrepareStillCaptureWorld` clears pending cuts + toast. Disarm on play exit unchanged
- Play `CampusOrthoSize` stays 10. `spawnShowcaseColony` stays false. Square airlocks stay. No click-to-move. No `SpecialistBrain` rewrite
- **Reshoot still21+ after merge. Do not stamp exit.** Phase 4 EXIT stays blocked on look

**Play Mode fixes after still21 (code only — do not stamp exit)**
- still21 shuttered `leftover=inn+wonder extraHab=True extraSolar=True defense=True hold=True` Mars Scale 1x. That pass chased packed density vs the now-retired visual-target PNG. **Do not treat still21 / still22 as a density-match gate.**
- Interior HAB sockets no longer fill every empty 4×4 inside the AABB. Empty dirt between pad / extractors / HAB is OK — room for rocks, foliage, creatures, discovery
- Leftover Inn / wonder / workshop **can** stamp when they CanFit; they are not required to max-pack the frame
- `RefreshTubes` still enables docked Lego arms only; `CampusDress_TubeRuns` may dress cardinal neighbors that are **not** airlock-linked (no `CampusTubeRoot` in the HAB gap)
- Still camera uses play `CampusOrthoSize` **10** (readable spaced campus). No packed-AABB inset / tight crop. `StillCaptureHold` stays. Mars Scale 1x stays
- `spawnShowcaseColony` stays false. Square airlocks stay. Five HUD chips stay. No click-to-move. No `SpecialistBrain` / FlagManager / Narrative / economy rewrite
- **Needs a new Game-tab still from GD after merge. Do not stamp exit.** Phase 4 EXIT stays blocked on look

**Dream Loop pass 2 — Capture → concept look pass (code only — do not stamp exit)**
- Judged Capture stills against the **locked** [`SM_MarsCampus_SpacedOverseer_Concept.png`](SM_MarsCampus_SpacedOverseer_Concept.png): flat ground, no haze, hard aprons inside hulls, smooth Commons sphere, dust-drum extractor, carbon canopy, raking 20° sun with up-left shadows
- Mars key light **36° / yaw 148** (shadows down-right ~1.4×), fill opposite the key; **linear** pale haze anchored past the camera focal depth; dusty grade + soft vignette
- `PlanetGround` pebble speckle layer + 84 `Dress_MarsPebble` rocks between yards; `SolarMajesty/Hull` triplanar `SM_Mat_*` grit + ground-dust skirt (Mars strongest)
- `Dress_Apron` → soft radial yard decal sized to the footprint (Commons included); Commons **geodesic strut lattice** + orange cupola collar; canvas awning on Workshop / Inn; regolith extractor **spherical tanks + column**; PWR-1 lattice comms mast
- Checked-in URP asset matches **Configure URP For Look Target** (MSAA 4 / 4 cascades / 120 m / 4096)
- Spaced layout, play ortho 10, square airlocks, docked-collar-only orange, `spawnShowcaseColony` false all unchanged. No click-to-move. No `SpecialistBrain` rewrite
- Deltas table + GD still checklist: [`DREAM_LOOP_MARS_LOOK.md`](DREAM_LOOP_MARS_LOOK.md). **Needs a new Game-tab still from GD after merge. Do not stamp exit.**

---

## Phase 4 EXIT look (spaced overseer — VisualTarget PNG retired)

`SM_MarsCampaign_VisualTarget.png` is no longer the look north star. Honest split: HUD + Mars ground vs campus v4, judged against a **spaced Majesty-2 overseer** campus — not that PNG’s packed density.

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
- Camera closer than v3; empty dirt around the cluster is now **allowed** (do not pack to hide it)
- Landmark kits (pad + Starship, solar, extractors) may appear when they CanFit; they are not a fill-every-cell requirement
- Mockup circular HAB cluster vs our square-dock graph (placement model stays square)
- IMGUI carbon/gold vs the mockup’s painted HUD
- Flat albedo + scatter vs mockup crater **heightmap** (Phase 4 non-goal)

**Still leftover (not a packed-PNG scorecard)**
- Latest archived Game-tab is still6 (empty Sol 1), not a spaced overseer campus sign-off. Still5/packed exist; exit stays blocked.
- **still5 failure modes this look-kit pass targets (code only — no new Game-tab still):** unused Commons cardinal orange **port ring** (`CommonsPort_*` hull-drum collar, not covered by the old `Dress_TubeArm` / `DockSleeve` / `CommonsStub` hide list); v4 orange box airlock + unused ribbed stub. Airlock hub stays a white paneled square; orange only at docked collars; no fourth `CampusTubeRoot` in the HAB gap. Needs a **fresh** Game-tab campus still before anyone restamps this review.
- **still16 look fail (signed capture gate, code pass only):** the 2×2 joint still read as a **dark rectangular box** (wrap `Dress_HubDoor` carbon plates + carbon roof), not a white paneled square. This pass removes wrap doors, keeps a smaller white hub, puts **one orange collar at the Lego face** on docked `Dress_TubeArm` only, and leaves unused faces as clean white plates. **Do not stamp exit.** A spaced Commons+airlock+HAB Game-tab still is still required.
- Tracked Defense Guardian vs the mockup’s bulky **biped walker**
- Construction cranes are runtime dressing, not authored FBX
- Status pips / aprons / dust-devils are primitive dressing
- Built-campus **readability** (pad / solar / extractors visible when placed, units optional) still needs a later Play Mode still — not a packed-density match
- Commons hero FBX skipped until a stub-free re-export; procedural geodesic is the live kit

---

## Leftovers (stay in Phase 4 — not Phase 5 polish)

- **Campus Game-tab still (spaced overseer)** — latest archived Game-tab is still6 (empty Sol 1). still21 leftover=inn+wonder extraHab/solar/defense is **not** a VisualTarget density gate. This pass is **code only**: CaptureStill prefers play ortho 10; leftover kits may stamp; interior dirt is not force-filled. `StillCaptureHold` stays. **Needs a new Game-tab still from GD after merge. Do not stamp exit.**
- Landmark kits (pad + Starship, solar, extractors, leftover Workshop / Inn / wonder) may appear when they CanFit. Do not fill every dirt patch. Units remain optional (do not invent new unit systems).
- Defense PNG **biped walker** (live mesh stays the Imagine **tracked** guardian so it does not clone Engineer)
- Circular HAB cluster vs square docks (placement model stays square; tubes are dressing)
- IMGUI HUD vs painted mockup chrome (material language shipped; painted fidelity is leftover)
- Heightmap terrain (explicit Phase 4 non-goal unless iso readability fails)
- Titan / external 45–90 min playtest (same leftovers as Phase 2 / 3). Continue now restores flags / fauna / specialist HP; dens / node yield / mission timer still omitted.

---

## How to smoke

1. `Docs/SMOKE_TEST.md` Phase 4 sections (Earth meadow New Game, then **Shift+click MARS?** or Shift+F10 Mars). Empty Mars should show boulder/dune/crater vista + node outcrops + dens, not a tiled plane of cubes. `spawnShowcaseColony` stays false.
2. On Mars: **B**, key **1**, Colony Commons on the orange claim → airlock on a face socket → HAB. Look for a **white paneled square hub** with **round white tubes + orange collars on docked faces only**, **no unused CommonsPort / CommonsStub rings** on undocked cardinals, HAB cylinder + Commons dome that stay **readable white** against the red ground, **no grey hex slabs**, camera snapping to campus ortho **10** (hopper spawn must not pan or zoom out). Hopper should not wear a giant idle **DUST HOPPER** chip. Empty ground click still must not repath robots.
3. Menu **Solar Majesty → Capture Mars Still** (or `-executeMethod SolarMajesty.EditorTools.DemoContentBuilder.CaptureMarsStill`) regenerates the editor PNG only — not a HUD still.

---

## Ready for Phase 5

**No.** Latest archived Game-tab still is `SM_MarsCampaign_PlayModeCampusStill6.png` (empty Mars Sol 1). Still5 exists; packed editor still is `SM_MarsCampaign_PackedCampusStill.png` (`Camera.Render`, no HUD) — not the look claim. None of these stamp exit. Remaining work is the spaced overseer still, not ship polish. Next work stays [`05_PHASE_4_VISUAL_TARGET.md`](05_PHASE_4_VISUAL_TARGET.md). Do not start Phase 5 as the main slice. After a true exit: [`06_PHASE_5_PRODUCTION_VALUES_SHIP.md`](06_PHASE_5_PRODUCTION_VALUES_SHIP.md).
