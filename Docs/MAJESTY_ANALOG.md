# Majesty 2 → Compact robot campus

Player-facing names stay Compact / robot. Do not use Paradox guild or temple names in HUD.

| Majesty 2 | Solar Majesty | Toy |
|-----------|---------------|-----|
| Palace | Colony Commons | First civic dock. Levy home. |
| Houses | HAB purses | 20 + 10 CRED per resident per day (max 50). Collectors walk it. |
| Tax collectors | Levy drones (2 per Commons, 1 per Watchtower) | Walk every building's till home. Haul (Courier) still walks HAB purses. |
| Farms | Greenhouse Farm | ICE tank (lungs), not gold. |
| Marketplace | Market Stall | Potions + necklace. Surplus ICE/REG → CRED above reserve. |
| Blacksmith | Blacksmith | Lodge arms and armor. |
| Guardhouse | Watchtower | Posted guard. Levy chest. Arm lasers. |
| Temple resurrect | Fobot Yard | CRED stand-up. Level-scaled bill. Wrecks sit on the dirt. |
| Temple heal | Aid Station | Hurt robots pay CRED for a patch. |
| Inn | Waystation Inn | Rest only. |
| Warrior / Ranger / Cleric / Dwarf halls | Aegis / Horizon / Triage / Anvil | Kits, pulses, flag pull. |
| Wizard tower | Lab + TECH | Science. Secret Projects = monuments. |
| Magic tower | Defense Battery / armed Watchtower | Campus and rim lasers. |
| Trading post | Landing Pad + Market | Dock fee + siphon. |
| Dungeons | Dens (fogged until charted) | Explore disc, Horizon pulse, or a scout walking up charts them. Unscouted dens are a smudge. |
| Hero recruit | Workshops | Fabricate outdoor robots. |
| Flags | Decrees | Eight `FlagType`s. No click-to-move. |

Code: `MajestyAnalog` in `Assets/Scripts/Systems/MajestyAnalog.cs`. Gold numbers and the Majesty 2 gold loop: `MajestyEconomy` — see [`ECONOMY_MAJESTY2.md`](ECONOMY_MAJESTY2.md).

Hard locks: no click-to-move, no `SpecialistBrain.ScoreFlag` rewrite, no 11th class, no new `FlagType` for these toys.

Continue keeps charted dens, depleted nodes, the sustain timer, parties, HAB purses, and armed watchtowers. Fear/Avoid decrees stay parked.
