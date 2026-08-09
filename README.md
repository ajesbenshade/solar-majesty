# Solar Majesty

**SpaceXAI Colonization Protocol** — Majesty 2–style indirect control, rethemed as solar-system colonization.

You never control units. You build infrastructure, post flags/bounties, and manage the economy. Autonomous specialists accept or ignore work based on personality and greed.

This repo is a **Unity 6 URP project** with a playable greybox demo scene.  
See **[Docs/DEMO.md](Docs/DEMO.md)** for open → Play → 60-second demo script.

## Namespace

All code: `SolarMajesty`

## Layout

See **[Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)** for systems design, utility AI, data flow, and the full `Assets/` folder tree.

### Scripts (what matters)

| Path | Purpose |
|------|---------|
| `Assets/Scripts/Core/SpecialistTypes.cs` | Context, decision, FlagHandle |
| `Assets/Scripts/Data/*Data.cs` | ScriptableObject definitions |
| `Assets/Scripts/Systems/SpecialistBrain.cs` | **Hero decision core** (utility AI) |
| `Assets/Scripts/Systems/FlagManager.cs` | Bounty flag registry |
| `Assets/Scripts/Systems/BuildingPlacer.cs` | Placement + construction orders |
| `Assets/Scripts/Systems/ResourceManager.cs` | Stockpile |
| `Assets/Scripts/Systems/SimpleEconomy.cs` | Upkeep + Earth resupply |
| `Assets/Scripts/Systems/GameTypes.cs` | Enums + ConstructionOrder |
| `Assets/Scripts/Runtime/*` | Phase-1 MonoBehaviour drivers (agent, grid, camera, input, HUD) |

Vertical slice setup: **[Docs/VERTICAL_SLICE_PHASE1.md](Docs/VERTICAL_SLICE_PHASE1.md)**

---

## How to open (Unity 6)

1. **Unity Hub → Open** this folder (**Unity 6000.5.x**).
2. Open **`Assets/Scenes/LunarOutpost_Sandbox.unity`**.
3. Press **Play**.

If the scene is missing: **Solar Majesty → Build Demo Scene**.

Demo script & checklist: **[Docs/DEMO.md](Docs/DEMO.md)**.

### What not to do

- Do not add player click-to-move or any command that bypasses `SpecialistBrain`.

---

## Art (later only)

Every Grok Imagine prompt must include:

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

See `Docs/ART_DIRECTION.md`.
