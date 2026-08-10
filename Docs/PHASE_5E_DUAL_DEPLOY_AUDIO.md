# Phase 5E — Dual Deploy + Campus Ambient Beds

Optional Campus B Scout detachment and dual ambient beds — still one `SpecialistBrain`.

## Play Mode

| Control | Action |
|---------|--------|
| **F6** / HUD **A** | Focus Campus A (ambient bed A) |
| **F7** / HUD **B** | Focus Campus B (ambient bed B) |
| **F9** | Seed high Explore ($160) at Campus B plaza + focus B |

## Deploy

- Campus A: full party (Scout / Engineer / Defense) — unchanged
- Campus B: optional Scout detachment (`spawnCampusBDetachment`, default on with second body)
- Shared brain, flags, stockpile; local `bodyDanger` from Phase 5D keeps B fauna local

## Audio

- `AmbientA` + `AmbientB` sources; F6/F7 swaps volumes
- Clips: `Resources/Audio/sfx_ambient.wav` / `sfx_ambient_b.wav` (procedural fallback)
- One-shots stay on a separate SFX source

## Non-goals

No SpecialistBrain rewrite, click-to-move, dual stockpiles, or Imagine mesh refine (Tuesday+).
