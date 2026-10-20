# Art: colony HOUSES → charging pods (CHARGERS)

## ⛔ HOLD MERGE — Aaron reviews the before/after shots first.

Restyles the colony house (`BuildingCategory.Habitat`, `HeroBuildingKits.BuildHabitat` + `HeroBuildingDetail.DetailHabitat`) from the tan HAB box into a charging pod for the worker bots, matching the AI core palette from #93: warm ceramic white shell, graphite/carbon seams and frames, warm amber charge lights (yellow-gold `ChargeAmber` 0.95/0.74/0.26, emit 0.80/0.56/0.12: dimmer than CyanEmit and distinct from the orange CommonsStripe 0.96/0.42/0.08). Cyan stays only on the existing accents (door light bar, wall lamps, nav lights, front light bar, one status LED).

Shots in `Docs/ReviewEvidence/2026-10-20-charging-pods/` (`contact_sheet.png`: before/after, overseer / close-up / night). **They are rough matplotlib mock-ups, not Unity captures.** Please replace them with real Play-mode captures before merge.

## What changed
- Shell `Dress_HabShell` tan → ceramic white (`HabTan` value; field name kept). Belt and seams → graphite.
- Portholes `Dress_HabPorthole(B)_0..2` orange → amber charge indicators. Fit-out window glass warm → amber. The green status LED → amber.
- Hatch `Dress_HabHatch` orange → graphite, inside the same carbon frame. Street door on -X keeps its position and size, with a graphite leaf. Wall lamps warm → cyan.
- Roof PV frames orange → graphite. PV cells, deck and cell lines are unchanged.
- Added two bot charging cradles on the front (-Z) face (floor pad, back plate, two arms, amber contact strip) and a wrap-around amber charge strip at 78% height. All boxes.
- Removed the 3 roof HVAC units, the white roof tank, the dish and the orange conduit. Yellow railing → graphite, red nav light → cyan.
- Wall cladding uses a new HAB-only `CeramicPanel` helper. `Panel` (tan) stays as-is for other kits.

## Unchanged (hard constraints)
- All `Dress_Hab*` names, positions and scales, plus `HabBoxFill`, the 2.28 m height, the `Key("Hab",…)` batch key and the stage assignments (Foundation/Frame/Shell/FitOut). Footprint, pivot, bounds, colliders and construction stages are therefore the same.
- No renames. No save, HUD or text changes. `HeroCitadel.cs` untouched. `PlanetArchitectureDresser` regrade is unchanged; `Dress_` prims keep their colours as before.
- Triangles: ~−930 per house (removed ≥6 cylinders plus ~20 boxes, about 1,100 tris; added 14 boxes, 168 tris).

## Test change
- `CampusDressingTests.LiveHab_BoxyTanHull_RoofSolar_NoGraphiteRim` → `LiveHab_CeramicChargingPod_RoofSolar_NoGraphiteRim`. The tan colour asserts become warm-ceramic asserts. The box-shape, solar-name and no-graphite-rim asserts stay, and a `Dress_HabHatch` presence assert is added.

## Unity checks needed (not compiled on the box)
1. Compile, then run the full EditMode suite (CampusDressing, ConstructionStages, PlanetArchitecture, BuildZone).
2. PlayMode `VillagePersistenceTests` / `DevelopedColonySaveTests` (save IDs should be unaffected).
3. Visual check on Earth, Luna and Mars: day + night, overseer cam and close-up. Watch the amber charge glow: it must read gold, not as Commons orange. Check the bloom strength and possible z-fighting between the front cradle back plates and the front LightBar/cladding.
4. Watch a villager build a house: site → frame → shell → fit-out still stage correctly.
5. Confirm the -X door is still the visible entry and that workers path to it.

Note: the old test and class comment called the tan HAB a "locked still". This PR replaces that look on Aaron's new canon (houses = charging stations). Aaron should confirm.
