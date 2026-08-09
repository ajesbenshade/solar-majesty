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
(Scout / Engineer / Defense) accept or reject work via SpecialistBrain utility AI
(personality + greed + distance + risk). Dust Stalkers raise ThreatPressure so
courage matters. Greybox demo is playable in Unity 6 URP.

## Non-negotiables
- Do NOT add click-to-move or any player command that bypasses SpecialistBrain.
- Keep Assets/Scripts/Systems/ pure C# (no MonoBehaviours). Runtime drivers only
  under Assets/Scripts/Runtime/.
- Namespace: SolarMajesty.
- Prefer thin visual/runtime wiring over architecture rewrites.
- No NavMesh, save/load, multi-body campaign, or new flag types unless explicitly asked.

## Open & verify first (mandatory)
1. Unity Hub → Open the repo root (needs Unity 6000.5.x).
2. Open Assets/Scenes/LunarOutpost_Sandbox.unity → Play.
3. If scene missing: menu Solar Majesty → Build Demo Scene.
4. Run the 60-second script and checklist in Docs/DEMO.md.
5. Confirm: mesh colony showcase, three specialists self-sort, stalkers raise
   threat, F2 ClearThreat attracts Defense, cheap far Explore → Idle, R → Rest.

## Architecture map
- Pure systems: Assets/Scripts/Systems/ (SpecialistBrain, FlagManager, BuildingPlacer,
  ResourceManager, SimpleEconomy, GameTypes)
- Decision types: Assets/Scripts/Core/SpecialistTypes.cs
- Data SOs: Assets/Scripts/Data/*Data.cs
- Runtime: Assets/Scripts/Runtime/ (GameLoop boots everything; SpecialistAgent;
  Flag/Building placement; Threat/; BuildingVisualCatalog)
- Building meshes: Assets/Resources/Buildings + Environment (FBX from Blender)
- Blender source: Blender/SolarMajesty_Modules.blend + Blender/scripts/
- Docs: DEMO.md, VERTICAL_SLICE_PHASE1.md, PHASE_1_6_THREAT.md, ARCHITECTURE.md,
  BLENDER_WORKFLOW.md, ART_DIRECTION.md, NEXT_STEPS.md

## Suggested next work (priority order)
1. Run Docs/DEMO.md Play Mode checklist (camera/LAB-1/ghost polish landed).
2. Optional: real ScriptableObject assets under Assets/Data/ instead of runtime
   CreateInstance factories in GameLoop.
3. Optional: specialist/stalker mesh placeholders (keep brain loop identical).
4. Only then: combat HP for specialists, NavMesh, construction UI, multi-body.

## Controls reference
G flag · B build · Q none · Tab cycle · F1 Explore · F2 ClearThreat · F3 Build ·
+/- bounty · LMB post/place · 1–4 buildings · R force fatigue Rest.

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

### Known gaps

- Building footprints vs real mesh size are still approximate (improved, not exact)  
- Landing Pad placeable footprint is compact vs Ø40 showcase mesh  
- Specialists/stalkers are primitives (intentional for this demo)  
- No multiplayer / save / campaign  

---

## Clone & run

```bash
git clone https://github.com/ajesbenshade/solar-majesty.git
# Unity Hub → Open → solar-majesty
# Open Assets/Scenes/LunarOutpost_Sandbox.unity → Play
```

Do **not** commit `Library/`, `Temp/`, `Logs/`, or `UserSettings/` (see `.gitignore`).
