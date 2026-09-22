# Art Lock — Anthropomorphic Epic Heroes (2026-09-22)

The vehicle / rover / hover-sled specialist look is **retired as the hero target**.
Specialists remain robots (humans stay in HABs). They are now **bipedal, anthropomorphic, epic**.

## Why

Outdoor heroes were reading as industrial vehicles (tracked hoppers, 6-wheel haulers, hover stretchers, tripod masts). That kills Majesty 2 readability: heroes need a *person-shaped* silhouette you can pick out of a campus at isometric distance.

## New lock

- Bipedal humanoid chassis, Tesla-Optimus language, slightly exaggerated Majesty 2 proportions.
- Helmet + cyan visor face. **No human skin. No mouth.** Defense keeps the red viewport slit.
- White ceramic / black carbon Starship armor, orange high-vis accents only.
- Class identity lives in **silhouette + kit**, not in a different locomotion type.
- Old rover/dozer/hover meshes stay in-repo as legacy blockouts until Blender is sheet-matched to these heroes.

## Roster (unchanged — still ten classes)

| Class | Model | Kit that reads at ISO | File |
|-------|-------|------------------------|------|
| Scout | LO-SCT-1 | Lean runner, whip antenna, rangefinder, shoulder beacon | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_ScoutDrone_Anthropomorphic_Hero.jpg` |
| Engineer | LO-ENG-1 | Stocky builder, weld pack, hip toolbox, HAB panel | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_EngineerBot_Anthropomorphic_Hero.jpg` |
| Defense | LO-DEF-1 | Knight-mech, tower shield, shoulder block, red visor | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_DefenseMech_Anthropomorphic_Hero.jpg` |
| Medic | LO-MED-1 | Slim medic, cyan cross, folded stretcher, kit sphere | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_Medic_Anthropomorphic_Hero.jpg` |
| Harvester | LO-HAR-1 | Miner-mech, orange scoop forearm, ore hopper pack | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_HarvesterBot_Anthropomorphic_Hero.jpg` |
| Surveyor | LO-SRV-1 | Lanky chartist, dish mast pack, measuring staff | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_SurveyorBot_Anthropomorphic_Hero.jpg` |
| Terraformer | LO-TRF-1 | Builder-knight, dual seed tanks, plow + soil rake | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_TerraformerBot_Anthropomorphic_Hero.jpg` |
| Courier | LO-COU-1 | Athletic runner, crate pack, orange-corner freight | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_CourierBot_Anthropomorphic_Hero.jpg` |
| Geologist | LO-GEO-1 | Crust-reader, vial rack, handheld core drill | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_GeologistBot_Anthropomorphic_Hero.jpg` |
| Sentinel | LO-SEN-1 | Squat guardian, dual-barrel shoulder turret, side shield | `ConceptSheets/AnthropomorphicHeroes/SM_Unit_SentinelMech_Anthropomorphic_Hero.jpg` |

Do **not** add an 11th class. Do **not** change `SpecialistBrain`.

## Style keywords (every Imagine / Blender brief)

> isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style

Append for this pass:

> anthropomorphic bipedal Optimus-inspired specialist robot, epic heroic stance, cyan visor face, no human skin

## Imagine prompt template (one class per run)

```
Isometric three-quarter hero portrait of a single anthropomorphic bipedal [CLASS] specialist robot named [MODEL] for a Majesty-style space colony RTS. Humanoid Optimus-inspired body, [KIT], cyan visor face (no human skin, no mouth). White ceramic and black carbon Starship armor with orange high-vis accents. Epic heroic stance on cratered grey lunar ground, readable silhouette from isometric distance. isometric view, Majesty 2 inspired readable silhouettes, SpaceX industrial aesthetic, clean white and black Starship materials with orange accents, modular habitat design, slightly exaggerated proportions for clarity, vibrant but grounded sci-fi lighting, high detail 3D render style
```

## Blender next

Sheet-match remaining-class meshes to these hero JPGs. Origins at ground contact. Scale 1 unit = 1 m. Do not reshape HAB-1 / tube connector.
