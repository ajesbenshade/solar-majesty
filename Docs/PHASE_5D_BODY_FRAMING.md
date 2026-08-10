# Phase 5D — Per-Body Framing + Authored Audio/Volume

Spatial threat honesty and light economy framing across Campus A/B — without touching `SpecialistBrain`.

## Play Mode

| Control | Action |
|---------|--------|
| **F6** | Focus Campus A — camera + ambient pitch + HUD local threat |
| **F7** | Focus Campus B — thinner ambient bed |
| **G / B** | Flag / Build menus (unchanged from HUD pass) |

## Threat

- Each specialist’s `bodyDanger` is sampled from **nearby** Dust Stalkers (`LocalThreatRadius` 16 m), not the global peak.
- Campus B fauna no longer spooks the Campus A party until they get close.
- DefendArea calm is also **local** (claimed Defend near the agent).
- HUD shows Threat A / B / global + stalker counts per campus.
- Wave 2 reinforcements spawn at the **focused** campus.

## Economy framing

- Still **one shared stockpile**.
- Resupply toast: “Earth resupply → Campus A pad”.
- Extract yield differs slightly by nearest campus (B = more regolith, leaner metals/ice).

## Audio / Volume

- `Resources/Audio/sfx_*.wav` one-shots + ambient (procedural fallback if missing).
- `DemoAtmosphere` prefers `Resources/Atmosphere/DemoVolumeProfile` (bake via **Solar Majesty → Bake Demo Volume Profile**), else runtime profile.

## Non-goals

No SpecialistBrain rewrite, dual stockpiles, dual parties, or combat sim rewrite.
