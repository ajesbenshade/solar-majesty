# Solar Majesty — Developer Handoff

**Repo:** https://github.com/ajesbenshade/solar-majesty  
**Local path (origin machine):** `/Users/aaronesbenshade/solar-conquest`  
**Unity:** 6000.5.x (tested 6000.5.6f1) · URP 17.5 · namespace `SolarMajesty`

---

## Paste-ready pickup prompt (for the next developer / agent)

```
You are picking up Solar Majesty (repo: ajesbenshade/solar-majesty).

## What this is
Majesty 2–style RTS homage: player is Overseer AI (never direct unit control).
Build infrastructure, post bounty flags, manage economy. Autonomous specialists
(Scout / Engineer / Defense / Medic) accept or reject work via SpecialistBrain.
Campaign spine: Earth tutorial → Luna → Mars → Belt → Europa with dens / sustain / launch gates
(tech + Landing Pad) and an Alpha Centauri-style tech tree (TECH · T).
Roadmap: Docs/Roadmap/ (Phase 3 complete: exit in PHASE_3_EXIT.md). Dust Stalkers raise ThreatPressure.
After Phase 3: Phase 4 visual target (Mars mockup fidelity — Weeks 1–4 continued in, including Imagine-sheet unit refine, Earth New Game produced biome, Workshop/Inn FBX, remaining-class Imagine JPGs + sheet-matched remesh, a Mars dressing close, hero-building sheet-match, **CMD-1 Guild / OPS-1 annex / wonder + extractor remesh**, **airlock panel-line polish**, **HAB/LAB/Commons/CMD/OPS panel bevels**, and **flush docks**; editor still + Game-tab empty-Sol-1 still in; exit **blocked** in PHASE_4_EXIT.md — **not** exited). Then Phase 5 ship.
Never add click-to-move to match mockup squad UI.

## Non-negotiables
- Do NOT add click-to-move or any player command that bypasses SpecialistBrain.
- Keep Assets/Scripts/Systems/ pure C# (no MonoBehaviours). Runtime drivers only
  under Assets/Scripts/Runtime/.
- Namespace: SolarMajesty.
- Prefer thin visual/runtime wiring over architecture rewrites.

## Open & verify first (mandatory)
1. Unity Hub → Open the repo root (needs Unity 6000.5.x).
2. Open Assets/Scenes/LunarOutpost_Sandbox.unity → Play (title screen; New Game is Earth if prefs fresh).
3. If scene missing: menu Solar Majesty → Build Demo Scene.
4. Run Docs/SMOKE_TEST.md (10-minute boot, then Earth loop). DEMO.md has the talk-track.
5. Confirm: no free starter robots; HAB = humans; workshops fabricate outdoor robots; empty drop + conquest gates + research.

## Architecture map
- Pure systems: Assets/Scripts/Systems/ (SpecialistBrain, FlagManager, BuildingPlacer,
  ResourceManager, SimpleEconomy, GameTypes)
- Decision types: Assets/Scripts/Core/SpecialistTypes.cs
- Data SOs: Assets/Scripts/Data/*Data.cs
- Runtime: Assets/Scripts/Runtime/ (GameLoop boots everything; SpecialistAgent;
  Flag/Building placement; Threat/; BuildingVisualCatalog)
- Building meshes: Assets/Resources/Buildings + Environment (FBX from Blender)
- Blender source: Blender/SolarMajesty_Modules.blend + Blender/scripts/
- Docs: SMOKE_TEST.md, DEMO.md, DEVELOPER_HANDOFF.md, VERTICAL_SLICE_PHASE1.md, PHASE_1_6_THREAT.md,
  PHASE_2A_BITE_AND_BOUNTY.md, PHASE_2B_NAVMESH_AND_JUICE.md,
  PHASE_3A_PRESENTATION_AND_MISSION.md, PHASE_3B_UNITS_VOLUME_WAVES.md,
  PHASE_4A_MISSION_STAKES.md, PHASE_4B_CONTENT_SCALE.md,
  PHASE_5A_MAP_DEADLINE_AMBIENT.md, PHASE_5B_UNIT_BLOCKOUTS.md,
  PHASE_5C_MULTI_BODY.md, PHASE_5D_BODY_FRAMING.md, PHASE_5E_DUAL_DEPLOY_AUDIO.md, ARCHITECTURE.md,
  BLENDER_WORKFLOW.md, ART_DIRECTION.md, NEXT_STEPS.md

## Suggested next work (priority order)
1. Phase 4 visual target — Week 1–4 continued are in (`Docs/Roadmap/05_PHASE_4_VISUAL_TARGET.md`), including flush docks. Exit is **blocked** ([PHASE_4_EXIT.md](Roadmap/PHASE_4_EXIT.md)): editor still `SM_MarsCampaign_EditorStill.png` exists; next is a **human Game-tab** Mars still. Do not add click-to-move. Do not start Phase 5. Square airlocks stay; tubes/domes/kits/turrets/solar are dressing. Defense stays tracked.
2. Shift+F10 smoke: Earth soil creepers (**F5**), Luna ash hoppers (**F2**), Belt (`LOW-G`, rock mites/ticks **F5**, shard hoppers **F2**), Europa (`RAD`, fissure leeches / ice wisps **F2**, ice creepers **F5**). **T** Guild Charter → assign hall class (Horizon / Anvil / Aegis / Triage). ★ Climate Loom / Aegis Spire / Deep Archive landmarks. Settings: Mode / Challenge / Stance — Open Hands should take a $70 Build.
3. Imagine turnarounds from `Docs/GROK_IMAGINE_UNIT_PROMPTS.md`. All ten specialists + seven fauna Blender meshes are sheet-matched blockouts against `ConceptSheets/` JPGs. Defense stays tracked (PNG walker still open). Building FBX pipeline (`SM_Hero_*`) is in.

## Controls reference
Esc pause · G flag · B build · Tab cycle · WASD pan · Q zoom out · E zoom in · F1 Explore · F2 ClearThreat · F3 Build ·
F4 Extract · F5 Defend · +/- bounty · I Research Site · O Outpost · U Terraform · LMB post/place · RMB cancel flag · 1–7 buildings ·
P party (selection or inn) · R force fatigue Rest · F8 debug · Y revive.

When done with a change set: commit on main (or PR) with a clear message and
update Docs/NEXT_STEPS.md if milestones moved.
```

---

## Quick context for humans

| Topic | Detail |
|--------|--------|
| **Playable scene** | `Assets/Scenes/LunarOutpost_Sandbox.unity` |
| **Bootstrap** | Single `GameLoop` component — no Inspector content required |
| **Demo guide** | [DEMO.md](DEMO.md) |
| **Design pillars** | Indirect control, greedy autonomous heroes, data-driven SOs |
| **Do not rewrite** | SpecialistBrain scoring model without product/design sign-off |

### Current milestone status

- [x] Pure systems + SO definitions  
- [x] Runtime vertical slice (party, flags, buildings, HUD)  
- [x] Three personalities + Dust Stalker threat pressure  
- [x] Blender modular blockouts + FBX exports  
- [x] Unity 6 URP project + greybox demo scene  
- [x] Demo content pass (Data SOs, unit placeholders, campus reservation)  
- [x] Phase 2A Bite & Bounty (HP combat, OverseerHud, construction feedback)  
- [x] Phase 2B NavMesh pathing + hit/death/claim VFX + richer SFX  
- [x] Phase 3A lunar lighting + clear-stalkers mission win  
- [x] Phase 3B industrial unit silhouettes + URP volume + two-wave mission  
- [x] Phase 4A multi-stake mission (combat + hold + build)  
- [x] Phase 4B content scale (Extract/Defend, LAB/CMD/Solar, denser campus)  
- [x] Phase 5A larger map + deadline fail + ambient audio  
- [x] Blender unit FBX blockouts (Scout / Engineer / Defense / Stalker)  
- [x] Phase 5C multi-body scaffold (Campus A + B, F6/F7)
- [x] Phase 5D body framing (local threat, campus extract yield, authored audio/VolumeProfile)
- [x] Phase 5E dual deploy (B Scout detachment, F9 attractor, Ambient A/B beds)
- [x] Phase 2 solar expansion (Belt + Europa, haul, doctrines, ecology — [exit](Roadmap/PHASE_2_EXIT.md))  
- [x] Phase 3 Weeks 8–10 — replay doctrines, challenges, Endless, Overseer rating  
- [x] Phase 3 Weeks 11–14 — balance / flavor / exit ([PHASE_3_EXIT.md](Roadmap/PHASE_3_EXIT.md))  
- [x] Phase 4 Week 1 — Mars atmosphere, tube dressing, construction cranes, Overseer HUD chrome  
- [x] Phase 4 Week 2 — HAB / Commons citadel / pad+ship / water vs regolith extractor hero kits  
- [x] Phase 4 Week 3 — junction turrets + solar-field landmark (Defense bunker kit)  
- [x] Phase 4 Week 4 start — Colony Commons rename; guild/lab/wonder dress; Medic/Harvester/Surveyor/Courier/Sentinel remesh  
- [x] Phase 4 Week 4 continued — Terraformer dozer remesh; Stalker / Hopper / Creeper / Tick silhouettes; Mars mockup notes (Phase 4 still open)
- [x] Phase 4 Week 4 continued — Mite / Leech / Wisp remesh; Courier/Geologist/Medic/Sentinel tighten; hero building FBX (Phase 4 still open)
- [x] Phase 4 Imagine-sheet refine — Scout / Engineer / Defense / Stalker vs ConceptSheets; HUD/tube/pad/solar dressing (Phase 4 still open)
- [x] Phase 4 Workshop / Inn FBX + campus clutter / HUD class readout (Phase 4 still open)
- [x] Phase 4 remaining-class Imagine prompt sheets + Mars dressing/HUD/lighting close (Phase 4 still open)
- [x] Phase 4 remaining-class + leftover-fauna Imagine JPGs + sheet-matched remesh (Phase 4 still open)
- [x] Phase 4 dock sockets flush to the square connector (Phase 4 still open)
- [x] Phase 4 editor Mars still + blocked exit ([PHASE_4_EXIT.md](Roadmap/PHASE_4_EXIT.md))  

### Known gaps

- Unit meshes are Blender blockouts. All ten specialists + seven fauna are sheet-matched against `ConceptSheets/` turnarounds (Scout keeps hover rotors; Defense stays the Imagine tracked guardian — PNG biped walker still open). Play Mode HAB / Colony Commons / pad / extractor / solar / Defense bunker / Guild / LAB / wonder / **workshop hangar / tall hangar / Inn** kits prefer `SM_Hero_*` FBX (`HeroBuildingKits` fallback). Junction turrets dress airlock hubs. Medic capsule / Harvester orange-blade hopper / Surveyor tripod / Courier white crate / Sentinel **treads** / Terraformer **orange blade + rear rake** silhouettes are in. Fauna: Stalker / six-leg Hopper / graphite Creeper / Tick / pillbug Mite / **white-ray Leech** / seven-point Wisp. Honest Mars mockup notes live in the Phase 4 doc — **Phase 4 is not exited** ([PHASE_4_EXIT.md](Roadmap/PHASE_4_EXIT.md): Game-tab still is empty Sol 1, not a campus stamp).  
- Continue slot is campus + stockpile + research per body (flags/fauna/HP are not snapshotted)  
- External Phase 1 playtest / exit review still open  
- No multiplayer  
- Roadmap: [Docs/Roadmap/00_ROADMAP_OVERVIEW.md](Roadmap/00_ROADMAP_OVERVIEW.md) — Phase 2 complete ([exit](Roadmap/PHASE_2_EXIT.md)); Phase 3 complete ([exit](Roadmap/PHASE_3_EXIT.md)); then [visual target](Roadmap/05_PHASE_4_VISUAL_TARGET.md) → [ship](Roadmap/06_PHASE_5_PRODUCTION_VALUES_SHIP.md)  

---

## Clone & run

```bash
git clone https://github.com/ajesbenshade/solar-majesty.git
# Unity Hub → Open → solar-majesty
# Open Assets/Scenes/LunarOutpost_Sandbox.unity → Play
```

Do **not** commit `Library/`, `Temp/`, `Logs/`, or `UserSettings/` (see `.gitignore`).
