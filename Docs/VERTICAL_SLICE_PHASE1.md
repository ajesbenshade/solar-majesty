# Vertical Slice Phase 1 → 1.5

## Verification checklist (Play Mode)

| Test | Expected | Proves |
|------|----------|--------|
| Low-bounty Explore far from party | Idle / `no_attractive_flag` | Greed gate |
| High-bounty Explore near Scout | Scout walks, works, completes | Accept → move → work |
| High-bounty Build near Engineer | Engineer prefers it (Scout may ignore if Explore better) | Personality split |
| High-bounty ClearThreat | Defense Mech engages | Combat preference + courage |
| Press **R** | Agents Rest | Fatigue scoring |
| Click empty ground | Agents do not repath to click | No unit control |

## Phase 1 polish (done)

1. **Specialist status orb** — gray Idle / blue Rest / orange Pursue (+ WORK pulse)  
2. **Flag claim feedback** — warm tint + yellow claim badge when `ClaimCount > 0`  
3. **Work scale pulse** on `ApplyWork`  
4. **Large `$ bounty` TextMesh** on FlagMarker  

## Phase 1.5 party (done)

| Class | Greed | Courage | Prefers | Tint |
|-------|-------|---------|---------|------|
| Scout Drone | 0.40 | 0.55 | Explore 0.95 | Cyan |
| Engineer Bot | 0.85 | 0.25 | Build 0.95 | Orange |
| Defense Mech | 0.35 | 0.90 | Combat 0.95 | Red |

Spawn: `GameLoop.spawnFullParty = true` (default).

## Controls

| Input | Action |
|-------|--------|
| G / B / Q / Tab | Flag / Build / None / cycle |
| F1 Explore · F2 ClearThreat · F3 Build | Flag type |
| +/- | Bounty |
| LMB | Post / place |
| R | Force fatigue on all (debug Rest) |

## Emergent sandbox recipe

1. Post **Explore $120** near the cyan Scout.  
2. Post **Build $150** near the orange Engineer (low bounty Build may be ignored — high greed).  
3. Post **ClearThreat $60** near red Defense (low greed combat specialist still takes it).  
4. Post a cheap far Explore — watch everyone Idle.

## Next (Phase 1.5 → 2) recommendation

**B — Dust Stalker** that raises local flag Risk / auto ClearThreat so Defense reacts under pressure.  
(Systems stay pure; add `Runtime/MonsterAgent` only.)
