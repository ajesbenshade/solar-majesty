# Phase 1.6 — Minimal Dust Stalker

## What was added

| Path | Role |
|------|------|
| `Runtime/Threat/ThreatPressure.cs` | Aggregates danger 0–1 (ambient + peak stalker contribution) |
| `Runtime/Threat/DustStalkerAgent.cs` | Wander, aggro → report pressure, die via ClearThreat work nearby |
| `GameLoop` | Spawns 1–2 stalkers; pushes `Threat.Current` → `SpecialistAgent.SetBodyDanger` |
| `SpecialistAgent` | `SetBodyDanger` / `BodyDanger` fed into brain each think tick |
| `DebugHud` | Shows threat line + stalker HP/aggro |

**Systems/ unchanged.**

## How pressure affects the brain

`SpecialistBrain.Evaluate(..., bodyDanger)` already uses:

```
risk = flag.Risk + bodyDanger * 0.4
riskPenalty = risk * (1.15 - courage)
```

So when stalkers aggro (pressure ~0.18 ambient + 0.55 = **~0.73**):

- **Defense** (courage 0.90) — low risk penalty, high combat preference → takes ClearThreat
- **Engineer** (courage 0.25) — high risk penalty → more Idle / avoid combat flags
- **Scout** (courage 0.55) — mixed; prefers Explore unless ClearThreat is well paid and close

## Defeat loop (minimal)

1. Player posts **F2 ClearThreat** near a dark-red stalker (set bounty high enough for Defense).
2. Defense soft-claims and works the flag.
3. While a claimed/worked ClearThreat is within ~4.5m, stalker loses HP.
4. Stalker despawns → contribution cleared → pressure falls toward ambient (~0.18).

## Success criteria

- [ ] Stalker near party → HUD `threat` rises; agents show elevated `danger=`
- [ ] Defense more willing to take ClearThreat under pressure
- [ ] Engineer more reluctant / Rest-prone under pressure
- [ ] Kill stalkers → threat drops, behavior calms
- [ ] No click-to-move specialists

## Play recipe

1. Let stalkers wander into aggro range (or wait).
2. Note HUD `threat=0.7x`.
3. **F2** ClearThreat, bounty ~80+, place on/near a stalker.
4. Watch Defense (red) pursue; Engineer may stay off combat.
5. After stalkers die, threat falls; party returns to Explore/Build sorting.
