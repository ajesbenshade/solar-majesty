# Solar Majesty — Architecture

**Project:** Solar Majesty – SpaceXAI Colonization Protocol  
**Homage:** Majesty 2 systems, rethemed as solar-system colonization  
**Player role:** Overseer AI — **never** direct unit control

This document describes the **pure C# foundation** under `Assets/Scripts/`.  
Scene MonoBehaviours (camera, input, agents) will wrap these systems later when the code is dropped into Unity 6.

---

## A. Folder structure (`Assets/`)

```
Assets/
├── Art/
│   ├── Placeholders/          # temporary cubes/capsules later
│   └── UI/                    # HUD sprites later
├── Audio/                     # SFX / music later
├── Data/                      # ScriptableObject *assets* (created in Editor)
│   ├── Buildings/
│   ├── Flags/
│   ├── Monsters/
│   ├── Resources/
│   └── Specialists/
├── Materials/                 # shared materials later
├── Prefabs/
│   ├── Buildings/
│   ├── Environment/
│   ├── Flags/
│   ├── Monsters/
│   └── Specialists/
├── Scenes/                    # LunarOutpost_Sandbox.unity (later)
└── Scripts/
    ├── Core/
    │   └── SpecialistTypes.cs # SpecialistContext, BrainDecision, FlagHandle
    ├── Data/                  # ScriptableObject *definitions* (.cs)
    │   ├── BuildingData.cs
    │   ├── FlagData.cs
    │   ├── MonsterData.cs
    │   ├── ResourceData.cs
    │   └── SpecialistData.cs
    └── Systems/               # pure C# simulation (no scene assumption)
        ├── BuildingPlacer.cs
        ├── FlagManager.cs
        ├── GameTypes.cs       # enums + ConstructionOrder + ResourceAmount
        ├── ResourceManager.cs
        ├── SimpleEconomy.cs
        └── SpecialistBrain.cs # pure utility decision core
```

All C# types use namespace **`SolarMajesty`**.

---

## Design pillars

| Pillar | Code consequence |
|--------|------------------|
| Indirect control | Player APIs: place buildings, post flags, set bounties, spend economy. **No** move/attack commands on specialists. |
| Autonomous heroes | `SpecialistBrain` is the only decision authority for specialist work. |
| Data-driven content | Numbers and affinities live on ScriptableObjects, not hardcoded in systems. |
| Extensible map | Systems take positions and catalogs as arguments; nothing assumes a single forever-scene singleton. |

---

## High-level systems

### 1. Building

| Piece | Role |
|-------|------|
| `BuildingData` (SO) | Cost, build time, footprint, housing, category |
| `BuildingPlacer` | Validates placement + affordability; records pending construction orders |

The player requests a building. The placer checks resources and footprint rules, then spends cost and enqueues a `ConstructionOrder`.  
**Engineers are not ordered to build.** They may later choose a `BuildHere` flag whose position overlaps a construction site (wired when agents exist).

### 2. Specialist AI (priority system)

| Piece | Role |
|-------|------|
| `SpecialistData` (SO) | Class, personality, flag affinities, greed thresholds, work rates |
| `SpecialistBrain` | Utility scoring: accept / reject / continue work |

The brain never receives a “do this now” order from the player. Each evaluation tick it:

1. Scores **rest** (from energy / needs).  
2. Scores every known **flag** (from `FlagManager`).  
3. Picks the highest score above the class’s accept threshold.  
4. Returns a **decision** (`Rest`, `PursueFlag`, `Idle`) for a future agent/mover to execute.

Low bounties at long range intentionally score below threshold → **ignored** (Majesty greed).

### 3. Flag / Bounty

| Piece | Role |
|-------|------|
| `FlagData` (SO) | Flag type, default/min/max bounty, base risk, work required |
| `FlagManager` | Posts, lists, completes, and cancels runtime `FlagHandle`s |

The player posts a flag with a bounty. Specialists discover flags only by evaluating the manager’s list. Completing work reduces `WorkRemaining`; on complete, the brain (or caller) can grant a reward via the economy.

### 4. Economy

| Piece | Role |
|-------|------|
| `ResourceData` (SO) | Identity + presentation defaults for a resource type |
| `ResourceManager` | Stockpile: get / add / can-afford / spend |
| `SimpleEconomy` | Periodic upkeep + Earth resupply on top of `ResourceManager` |

Phase-0 resources: **Regolith, Water Ice, Metals, Power**.

### 5. Threat

| Piece | Role |
|-------|------|
| `MonsterData` (SO) | Dust Stalker (etc.) stats and native body tags |

Threat **runtime agents** are out of scope for this scaffold. `MonsterData` exists so content is data-driven when monsters are added. Defense specialists will prefer `ClearThreat` flags once those flags exist in the world.

---

## How specialists evaluate flags (Utility AI)

`SpecialistBrain` is pure decision logic (no MonoBehaviour).  
Types: `Assets/Scripts/Core/SpecialistTypes.cs` · Brain: `Assets/Scripts/Systems/SpecialistBrain.cs`

Each `Evaluate(ctx, openFlags, bodyDanger)`:

1. **Hard rest** if rest score &gt; 0.78 (fatigue + injury, reduced by `workaholicBias`).
2. **Score each flag** in range:

```
score =
    greedScore(CurrentBounty, baseGreed)
  + preferenceScore(GetPreference(flagType))
  − distPenalty
  − riskPenalty(Risk, bodyDanger, courage)
  − crowdPenalty(ClaimCount)
  − fatiguePenalty
  + hysteresis if already on this flag
```

3. **Greed gate:** accept only if  
   `bestScore >= 0.38 + baseGreed * 0.25`
4. Else mild rest, else **Idle** (`no_attractive_flag` — Majesty ignore).

### Personality levers (on `SpecialistData`)

| Field | Effect |
|-------|--------|
| `baseGreed` | Raises acceptance threshold; scales bounty attractiveness |
| `courage` | Lowers risk penalty on dangerous flags |
| `workaholicBias` | Resists resting when tired |
| `explore/build/combat/extractPreference` | Task affinity via `GetPreference` |

### Accept / reject examples

| Situation | Likely result |
|-----------|----------------|
| Engineer, Build, bounty 80, nearby | Accept |
| Engineer, ClearThreat, bounty 20, far | Reject |
| Scout, Explore, moderate bounty, nearby | Accept |
| Defense, ClearThreat, bounty 60 | Accept |
| Any class, bounty 5, far, crowded | Idle (`no_attractive_flag`) |

---

## Data flow

```
                    ┌──────────────────────┐
                    │  ScriptableObjects   │
                    │  BuildingData        │
                    │  SpecialistData      │
                    │  FlagData            │
                    │  ResourceData        │
                    │  MonsterData         │
                    └──────────┬───────────┘
                               │ read-only definitions
           ┌───────────────────┼───────────────────┐
           ▼                   ▼                   ▼
   BuildingPlacer        FlagManager         SpecialistBrain
   (uses BuildingData    (uses FlagData      (uses SpecialistData
    + ResourceManager)    to create handles)  + FlagHandles)
           │                   │                   │
           │ spend             │ list flags        │ decision
           ▼                   │                   ▼
   ResourceManager ◄───────────┴────────── SimpleEconomy
   (stockpile)                     (upkeep / resupply ticks)
```

### Player → world (allowed)

1. **Build:** UI/input → `BuildingPlacer.TryPlace` → spends via `ResourceManager` → construction order.  
2. **Flag:** UI/input → `FlagManager.Post` → `FlagHandle` with player bounty.  
3. **Economy (indirect):** placement spends; `SimpleEconomy` drains/adds over time.

### World → specialist (autonomous)

1. Agent holds `SpecialistData` + live state (`SpecialistContext`: position, energy, current flag id).  
2. Each think tick: `SpecialistBrain.Evaluate(context, flags, …)` → `BrainDecision`.  
3. Agent **executes** decision (move/work/rest) — still no player pathing.

### Specialist → economy

1. On flag complete: grant reward (e.g. convert bounty → Metals) through `ResourceManager`.  
2. Upkeep: `SimpleEconomy` or caller applies specialist upkeep costs from `SpecialistData`.

---

## What this scaffold intentionally omits

- MonoBehaviour agents, camera, grid rendering, input components  
- Full combat resolution / pathfinding  
- Save/load, multi-body scenes, UI  
- Art assets  

Those wrap **these** systems later without rewriting the hero loop.

---

## File ownership (systems)

| File | Owns |
|------|------|
| `SpecialistBrain.cs` | Scoring + accept/reject decision |
| `FlagManager.cs` | Runtime flag list lifecycle |
| `BuildingPlacer.cs` | Placement validation + construction queue entry |
| `ResourceManager.cs` | Stockpile math |
| `SimpleEconomy.cs` | Timed upkeep + Earth resupply |
| `GameTypes.cs` | Enums + plain runtime records shared by systems |

---

## How to drop this into Unity

See the end of `README.md` (section **How to drop this into Unity**).
