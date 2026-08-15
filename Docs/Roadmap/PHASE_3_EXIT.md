# Phase 3 — Exit review

**Date:** 2026-08-14  
**Status:** Complete (Weeks 1–14 in). External 45–90 min playtest, Imagine→Blender remesh, Titan, and Continue snapshot gaps remain leftovers — same bar as Phase 2.

Phase 3 filled the five-body sandbox with roster, fauna, guilds, wonders, doctrines, challenge modes, endless, scoring, then a desktop balance + flavor pass. Core systems were not rewritten: `SpecialistBrain.ScoreFlag` is unchanged; the player still never path-commands units.

---

## What shipped

| Slice | In |
|-------|-----|
| Weeks 1–4 | 10-class roster (Terraformer / Courier / Geologist / Sentinel + existing six); body-native fauna (creeper / hopper / mite / tick / leech / wisp) on Earth / Luna / Mars / Belt / Europa |
| Weeks 5–7 | Guild Hall class pull; Climate Loom / Aegis Spire / Deep Archive landmarks; body-tinted greybox hulls; Imagine prompt sheets for remaining roster/fauna (remesh is Phase 4) |
| Weeks 8–10 | Doctrines (Open Hands / Aegis Watch / Survey First); challenges (Austere / Swarm / Tight Purse); Endless; Overseer rating |
| Weeks 11–14 | ReplayRules readability bump; guild/wonder place costs; specialist voice; body briefing / endless copy; S-letter dens+roster+pace gate; this review |

## Content catalog

### Specialists (10)

| Class | Callsign / guild | Wants | Ignores |
|-------|------------------|-------|---------|
| Scout | Horizon Lodge | Explore | Fights |
| Engineer | Anvil Compact | Build (greedy) | Cheap flags, dens |
| Defense | Aegis Lodge | Clear Threat / Defend | Explore / Build |
| Medic | Triage Compact | Defend | Dens |
| Harvester | Strip Guild | Extract | Dens / tubes |
| Surveyor | Chart Lodge | Explore / Research Site | Fights |
| Terraformer | Bloom Compact | Terraform | Dens |
| Courier | Haul Lodge | Explore / Outpost | Dens |
| Geologist | Core Lodge | Extract / Research Site | Dens |
| Sentinel | Rim Watch | Defend Area | Explore / Build |

No 11th/12th class. Surveyor / Geologist / Courier already cover research and scout-variant roles.

### Fauna (by body)

| Body | Defend (F5) | Clear Threat (F2) |
|------|-------------|-------------------|
| Earth | Soil creeper, mite, dust tick | Watt leech, stalker |
| Luna | Dust tick | Ash hopper, leech, stalker |
| Mars | Dust creeper, mite | Dust wisp, hopper, stalker |
| Belt | Rock mite, rock tick | Shard hopper, stalker |
| Europa | Ice creeper | Fissure leech, ice wisp, stalker |

### Replay (Weeks 11–14 numbers)

| Control | Effect |
|---------|--------|
| **Open Hands** | Hunger +0.26 (spawn 0.55 → 0.81, past the 0.75 cheap-flag greed bypass); courage ×0.90 |
| **Aegis Watch** | Courage ×1.22; workshop bonus +0.18; hunger −0.08 |
| **Survey First** | Consider range ×1.50 (~84 m vs ~75); workshop bonus +0.10 |
| **Austere** | Start stockpile ×0.55 (Earth 187 MET — Colony Commons + airlock + HAB + workshop = 164) |
| **Swarm** | Fauna cap ×1.50; weights ×1.22; ambient ×1.28; spawn interval ×1.35 |
| **Tight Purse** | Resupply interval ×1.55; extra dock fee 8 MET |
| **Endless** | Body `EndlessLog`; no **TO {next}** |
| **S rating** | Letter S requires dens cleared, gates met, ≥3 robots, mean HP ≥0.55, elapsed ≤12 min |

Tune in `ReplayRules` / catalogs only. Do not retune `SpecialistBrain.ScoreFlag`.

### Three mid-game strategies (desktop)

1. **Extract / haul rush** — Harvest Doctrine + Harvester/Geologist shops, F4 on nodes, Planetary Anvil / Orbital Skyhook. Tight Purse hurts this path (ship + fee); Swarm does not, until farms/mines exist.
2. **Guild + workshop pull** — Guild Charter, assign Anvil/Horizon/Aegis/Triage, FLAG HERE on the hall. Survey First lengthens consider range so distant flags still pull.
3. **Aegis / defense hold** — Aegis Watch stance, Perimeter Doctrine / Sentinel, Aegis Spire, F5/F2. Swarm + Open Hands does **not** make extract dominate: Open Hands cheapens flags for everyone, but Defense/Sentinel still take Clear Threat / Defend at low greed, and campus pests steal ICE/MET until F5/F2 lands.

If a live 45–90 min session finds one of these dead, note it in `PHASE_1_FRICTION.md` rather than rewriting the brain.

## How to smoke

1. `Docs/SMOKE_TEST.md` 10-minute boot + Earth loop + Replay section.
2. Settings: cycle Stance — Open Hands should take a default $70 Build within ~5 minutes (Engineer hunger past 0.75). Aegis Watch hunts/shops harder. Survey First considers farther.
3. Austere New Game: Colony Commons + airlock + HAB + one workshop still affordable (~187 MET start). Farm waits on extract or the ship.
4. Swarm New Game: more F5/F2 after farm/power/HAB, not a wipe on the empty drop (pests still gate on buildings).
5. Tight Purse: ship timer slower; +8 MET fee. Drop pile unchanged.
6. Win banner: rating letter + breakdown. Endless: body endless line, no **TO {next}**.
7. Select a robot: card shows voice copy, not `pursue_Build`. Claim logs a class line.

## Known leftovers (not Phase 3 blockers)

- Titan / outer system (Phase 2 stretch).
- Imagine→Blender unit remesh (production look is [Phase 4 – Visual Target](05_PHASE_4_VISUAL_TARGET.md)). Prompt sheets are in `Docs/GROK_IMAGINE_UNIT_PROMPTS.md`.
- Continue still omits flags, fauna, and specialist HP.
- External 45–90 min playtest not yet run (same as Phase 1 / 2). Desktop balance pass only.
- Full combat sim / multiplayer / heightmap terrain.

## Ready for Phase 4

Yes. No more core systems are required for the visual-target art slice. Next: `Docs/Roadmap/05_PHASE_4_VISUAL_TARGET.md` — campus / units / HUD toward the Mars mockup. Mockup squad bars stay HUD chrome; never click-to-move. After Phase 4: [ship](06_PHASE_5_PRODUCTION_VALUES_SHIP.md).
