# Flag-Hero Local LLM — implementation packet

**Status:** Parked stretch. **Do not start during Phase 4.** Do not treat this as Phase 5 ship work.  
**After:** Phase 4 EXIT is stamped. Then this is an optional systems experiment, not the visual-target or ship checklist.  
**Does not:** stamp Phase 4 EXIT, start Phase 5, add click-to-move, or rewrite `SpecialistBrain.ScoreFlag` without explicit design sign-off.

Grok-the-advisor stays a **scripted catalog** ([`MARS_COLONY_MAJESTY_LOOPS.md`](../MARS_COLONY_MAJESTY_LOOPS.md), Phase 5). This packet is a different idea: specialists **read typed flag text** with a **bundled local LLM** (no cloud). Flavor speech on cards stays `SpecialistFlavor`.

---

## How this maps onto Solar Majesty today

Inspect the repo before implementing. Do **not** create a parallel `Assets/_Game/` tree or a second unit AI.

| Packet name | Already in this repo | Hook, don't replace |
|-------------|----------------------|---------------------|
| Flag planting | `FlagPlacementInput`, `FlagManager`, `FlagData` (ScriptableObject + typed `FlagType`) | Add optional `rawText` / `version` on the planted actor. Keep typed flags (Explore / Build / …) as the deterministic fallback. |
| Heroes | 10 `SpecialistClass` robots via workshops; `SpecialistAgent` + `SpecialistBrain` | LLM is an optional **intent overlay**. Legs stay NavMesh + existing actions. Never bypass the brain with click-to-move. |
| Personality | `SpecialistPersonality`, `SpecialistFlavor`, guild halls (Horizon / Anvil / Aegis / Triage) | `personalityPrompt` can wrap existing callsigns. Do not invent Majesty 2 / Paradox names. |
| NavMesh | Campus NavMesh on place; specialists already path | Reuse. Stub capsules only if a throwaway scene is needed. |
| Player verbs | Buildings, flags, bounties, research, camera, parties | Typed NL is an extra input on a flag, not a move command. |
| File layout | `Assets/Scripts/Systems/` (pure C#) + `Runtime/` (MonoBehaviours) | Put parser / snapshot / queue in Systems. `LocalLLMService` and flag text UI in Runtime. Namespace `SolarMajesty`. |

**Fallback (non-negotiable):** if JSON parse fails, timeout, or the model is cold, run the existing `SpecialistBrain` decision. Never freeze a specialist waiting on a model.

**First implementation slice (when this packet is un-parked):** Milestone 0 + mocked JSON so one typed flag can push one robot, then swap mock → LLMUnity. Do not start with art, multiplayer, or a 7B model.

---

## Cursor / Grok prompt (drop-in)

Copy everything inside the fence into a new agent when this packet is un-parked.

~~~~text
# Flag-Hero Local LLM RTS — Unity Implementation Plan

You are implementing a Majesty-2-style overseer RTS in Unity.
The player never directly clicks units. The player plants FLAGS with typed natural-language orders.
Heroes are autonomous agents with personality prompts. They READ nearby flags, interpret the text with a bundled local LLM (NO cloud APIs), and act.

Work incrementally. Keep the game playable after every milestone. Do not invent cloud calls.

## Non-negotiable constraints
- Engine: Unity (C#). Assume Unity 6 + URP unless the project already differs.
- LLM: 100% local via LLMUnity (llama.cpp). Package: https://github.com/undreamai/LLMUnity.git
- No OpenAI / Grok / HTTP inference at runtime.
- One shared model instance. Many heroes share it.
- Inference NEVER blocks the main thread / render loop.
- Heroes only query the LLM on discrete decision points (flag enter/change, combat shock, order complete, periodic rethink). Not every frame.
- LLM output MUST be structured JSON the game can execute. Flavor speech is optional and secondary.
- If JSON parse fails, fall back to a deterministic behavior tree. Never freeze a unit waiting on a model.
- Keep model small enough to ship: start with Llama 3.2 1B or Qwen 2.5 0.5B–1.5B Q4_K_M GGUF. 3B only if the machine has headroom.

API note: LLMUnity versions use either `LLMCharacter` or `LLMAgent`. Detect what the installed package actually exports and use THAT. Do not mix names.

## Architecture (hybrid brain)

```
Player types text on a Flag
        │
        ▼
Flag (world object: text, faction, radius, priority, expiry)
        │  heroes poll on cooldown / trigger
        ▼
HeroBrain
  ├─ Perception snapshot (cheap, C#)
  ├─ Decision gate (should I ask the LLM?)
  ├─ LocalLLMService (queued, one-at-a-time)
  ├─ IntentParser (JSON + schema + repair)
  └─ CommandExecutor → NavMesh / combat / gather / defend / idle
        │
        ▼
Behavior tree / state machine does the actual walking and fighting
```

The LLM is the sergeant. The behavior tree is the legs.

## Milestone 0 — Package + dummy loop
1. Add LLMUnity from git URL.
2. Create empty GameObject `LLMRuntime` with `LLM` component.
3. Load a small Q4 GGUF into StreamingAssets (or Inspector Load Model).
4. Settings: `numThreads = -1`, `numGPULayers` conservative (8–16 if GPU, else 0).
5. Warmup once at boot. Show a "Heroes briefing…" spinner until ready.
6. Prove a single `Chat`/`Completion` call from a test button that logs a reply. Stop here if that fails.

## Milestone 1 — Data model (no LLM yet)
Create these types. Use ScriptableObjects where designers will edit them.

### HeroDefinition : ScriptableObject
- id, displayName
- personalityPrompt (string, 80–200 words)
- role tags: Scout / Engineer / Gunner / Harvester / Officer
- traits: bravery, obedience, greed, curiosity (0–1)
- moveSpeed, health, attack
- preferredActions[]

### FlagData (MonoBehaviour on a planted flag prefab)
- ownerFaction
- rawText (what the player typed)
- worldPosition
- influenceRadius
- priority (int)
- createdAt, expiresAt (optional)
- claimedByHeroId (optional)
- version (increment when text changes so heroes re-read)

### WorldSnapshot (plain C# struct/class, built in code, NOT sent as a novel)
Keep it tiny. Example fields:
- heroId, heroRole, hpPct, ammoPct
- position (grid or rounded meters)
- currentJob
- nearbyFlagText, flagDistanceM, flagPriority
- threats: count + nearestDistance
- alliesNearby
- resourcesVisible
- lastOrderResult ("reached_flag" | "lost_target" | "under_fire" | "idle")

Cap snapshot to ~400–600 tokens. Never dump the whole scene.

## Milestone 2 — Command schema (the contract)
The model may ONLY emit this JSON. Put a GBNF / JSON-schema grammar on the agent if the package supports `grammar` / `json_schema`.

```json
{
  "intent": "move|attack|defend|harvest|repair|scout|flee|idle|claim_flag|abandon_flag",
  "target": {
    "type": "flag|enemy|resource|ally|point|none",
    "id": "string-or-empty",
    "x": 0,
    "z": 0
  },
  "stance": "aggressive|cautious|hold|work",
  "speech": "optional 8-word bark",
  "confidence": 0.0
}
```

Rules:
- `speech` max 12 words. No paragraphs.
- Coordinates must be near something in the snapshot. If not, CommandExecutor snaps to nearest valid thing or rejects.
- Unknown intent → idle.
- Multiple actions in one reply → take the first valid one.

Write `IntentParser`:
1. Extract first `{...}` block from the raw model text.
2. JsonUtility / Newtonsoft deserialize into `HeroIntent`.
3. Validate enum + numbers.
4. If fail, try a tiny repair (trim markdown fences). If still fail → `HeroIntent.Idle`.

## Milestone 3 — LocalLLMService (singleton)
One service owns the model.

Responsibilities:
- Queue of `InferenceRequest { heroId, systemPrompt, userPrompt, onComplete }`
- Process ONE request at a time (llama.cpp slots exist, but start single-file to keep FPS stable).
- Cancellation when the hero dies or the flag version changes mid-flight.
- Timeout (e.g. 4s). On timeout, complete with Idle.
- Token budget: max_tokens ~80–120. This is an order slip, not a short story.
- Cache: hash(heroId + flagText + coarse snapshot). If same hash within N seconds, reuse last intent.
- Metrics: queue depth, ms to first token, parse-fail rate. Log them.

Heroes call:
`LocalLLMService.RequestIntent(hero, snapshot, flag, callback)`

Never call Chat from Update.

## Milestone 4 — Flag planting (player verb)
This is the Majesty hook.

- Hotkey / toolbar: Plant Flag.
- Click ground → place prefab.
- Immediate UI: input field focused. Player types order, Enter commits.
- Examples the system must handle:
  - "hold this ridge, don't chase"
  - "strip the ore then fall back"
  - "keep the harvester alive"
  - "scout the crater but run from anything bigger than you"
- Flag shows the text on a world-space label.
- Drag to move. Delete key removes. Edit text bumps `version`.
- Optional: colored flag types (Attack / Guard / Work) as a hint bit in the snapshot. Text still wins.

## Milestone 5 — Hero read loop
On each hero, a `HeroBrain` tick (0.25–0.5s, staggered):

```
if dead: return
if inferenceInFlight: return
if current command still valid AND flag.version unchanged AND not in shock: return

build snapshot
if nearest flag in radius AND (flag.version != lastSeenVersion OR job complete OR under_fire):
    enqueue LLM request
else:
    run behavior tree on last intent (or Idle)
```

Shock events that force a rethink: first damage in 5s, ally death nearby, flag deleted, target lost.

After intent arrives:
- CommandExecutor sets BT blackboard (MoveTo, AttackTarget, DefendRadius, HarvestNode, …)
- Play `speech` as a floating bark once
- Store lastIntent + lastFlagVersion

## Milestone 6 — Prompts
Two layers.

### Shared system prompt (on the LLM agent or injected every request)
You are a field hero in a sci-fi industrial colony RTS.
You receive a compact world snapshot and one flag order written by your commander.
Reply with ONLY the JSON schema. No markdown. No extra keys.
Obey the flag unless it is suicidal given hp and threats.
Stay in character, but character affects stance and speech, not JSON shape.

### Per-hero personality (HeroDefinition.personalityPrompt)
Short. Concrete. Examples:
- "You are Rook, a cautious surveyor. You hate fair fights. You complete scans then leave."
- "You are Vesper, a loud gunner. You interpret 'hold' as 'hold and shoot anything that moves.'"

User message template:

```
FLAG: "{rawText}" (priority {n}, {distance}m)
YOU: {role} hp={hp%} job={job}
SEE: threats={n} nearest={m} allies={n} resource={yes/no}
LAST: {lastOrderResult}
Emit JSON now.
```

Keep this template in one place so we can tune it.

## Milestone 7 — CommandExecutor + BT
Map intents to existing or new unit AI:

| intent     | behavior |
|------------|----------|
| move       | NavMesh to point / flag |
| attack     | acquire enemy id or nearest in radius |
| defend     | leash around flag, engage hostiles in radius, don't chase past leash |
| harvest    | path to resource, work loop, dropoff |
| repair     | path to ally/building with missing hp |
| scout      | visit point, linger 2s, return or next |
| flee       | move opposite nearest threat to cover |
| idle       | wander small radius / emote |
| claim_flag | set claimedByHeroId if free |
| abandon_flag | clear claim, idle |

Reject impossible commands (repair with no target, attack with no enemy) → next-best from snapshot, else idle.

## Milestone 8 — Multi-hero contention
- A flag can be read by many heroes.
- If flag text implies one body ("you, Rook"), only that hero claims.
- If it implies a job ("mine this"), first N heroes of matching role claim.
- Officers can broadcast a derived sub-order to nearby units WITHOUT extra LLM calls (copy intent, cheaper).
- Never let 12 heroes slam the queue because one flag dropped. Cap: 1 rethink per hero per 3s, global queue max 4.

## Milestone 9 — Playable slice (definition of done)
A scene with:
- 1 map, NavMesh
- 3 heroes with different personalities
- Player plants 2 flags and types orders
- Heroes walk, bark one line, and roughly obey
- Combat dummy so "defend" and "flee" are visible
- FPS stays near 60 while one inference runs
- Works offline after first model load

## File layout (create these)
```
Assets/_Game/
  AI/
    LocalLLMService.cs
    HeroBrain.cs
    HeroIntent.cs
    IntentParser.cs
    CommandExecutor.cs
    WorldSnapshotBuilder.cs
    PromptLibrary.cs
  Flags/
    FlagActor.cs
    FlagPlacer.cs
  Heroes/
    HeroAgent.cs
    HeroDefinition.cs
  Data/
    Grammars/hero_intent.gbnf
  UI/
    FlagTextPrompt.cs
    HeroBark.cs
```

In Solar Majesty, prefer `Assets/Scripts/Systems/` + `Assets/Scripts/Runtime/` over `Assets/_Game/`. Match existing names (`FlagData`, `SpecialistAgent`, `SpecialistBrain`) instead of a second hero stack.

## Implementation rules for you (Cursor)
- Match existing project style. If the repo already has units/NavMesh, hook there. Don't rewrite the game.
- Prefer composition over giant MonoBehaviours.
- All public inspector fields documented with tooltips.
- No `async void` except Unity event methods; use Task + cancellation tokens.
- Add a debug overlay: current flag text, last JSON, parse ok/fail, queue length.
- If LLMUnity API differs from this prompt, adapt to the package. Don't stall.
- First commit-worthy milestone is Milestone 0+1+2 with a mocked LLM that returns canned JSON. Then swap in the real model.
- After the mocked loop works, wire LocalLLMService.

## First thing to do now
1. Inspect the Unity project: version, existing unit AI, input, NavMesh.
2. Report what already exists in 10 lines.
3. Implement Milestone 0 and a mocked HeroIntent loop so flags already move a cube.
4. Then swap mock → LLMUnity.

Do not start with art, multiplayer, or a 7B model.
~~~~

---

## Notes for the next agent

- If the LLMUnity package exports `LLMCharacter` instead of `LLMAgent`, follow the installed API. Do not mix names.
- Start from a scene that already has a floor and NavMesh (Play Mode campus). Do not stub a capsule unless the live specialists are the wrong hook.
- Tighter weekend spike (optional): one specialist, one typed flag, mocked JSON, then one local GGUF. Cut milestones 8–9 until that loop is honest.
- Cloud Grok / OpenAI at runtime is disallowed. Editor-only Imagine / Copilot3D art pipelines are unrelated and stay as they are.

*Filed: 2026-09-11. Parked until Phase 4 EXIT.*
