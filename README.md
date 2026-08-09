# Solar Majesty

**SpaceXAI Colonization Protocol** — Majesty 2–style indirect control, rethemed as solar-system colonization.

You never control units. You build infrastructure, post flags/bounties, and manage the economy. Autonomous specialists accept or ignore work based on personality and greed.

This repo is a **pure C# + documentation scaffold**. It is **not** a full Unity project yet (no `ProjectSettings/`, `Library/`, or `Packages/`).

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

## How to drop this into Unity

1. **Create** a new Unity 6 (or latest LTS) project (3D / URP recommended).
2. **Copy** this repo’s `Assets/` folder contents into the Unity project’s `Assets/` folder  
   (merge with the default `Assets/` — keep Unity’s default folders if present).
3. **Copy** `Docs/` anywhere you like (optional; not required to compile).
4. Open the project and wait for script compilation. You should see menu items under  
   **Create → Solar Majesty →** Building / Specialist / Flag / Resource / Monster.
5. Create sample assets under `Assets/Data/...` (right-click → Create → Solar Majesty → …).
6. Later session: add thin MonoBehaviour drivers that own instances of:
   - `ResourceManager` + `SimpleEconomy`
   - `FlagManager`
   - `BuildingPlacer`
   - `SpecialistBrain` (one per agent, or shared stateless instance)
7. Wire input so the player can only:
   - call `BuildingPlacer.TryPlace`
   - call `FlagManager.Post`
   - adjust economy-facing UI  
   **Never** set specialist destinations from click-to-move.

### Minimal runtime wiring (future)

```csharp
using SolarMajesty;

var resources = new ResourceManager();
var economy   = new SimpleEconomy(resources);
var flags     = new FlagManager();
var placer    = new BuildingPlacer(resources);
var brain     = new SpecialistBrain();

// Each think tick for an agent:
var decision = brain.Evaluate(specialistContext, flags.Flags, bodyDanger: 0.35f);
// decision.Action: Idle | Rest | PursueFlag (TargetFlag)
// Execute on the agent — player never injects PursueFlag.
```


### What not to do

- Do not generate `ProjectSettings` / package manifests inside this scaffold repo until you intentionally promote it to a full Unity project.
- Do not add player commands that bypass `SpecialistBrain`.

---

## Art (later only)

Every Grok Imagine prompt must include:

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

See `Docs/ART_DIRECTION.md`.
