# Steam store screenshots

Captured from the release build (no development watermark) at 1920×1080 on Ultra by
`Tools/qa/store_screenshots.ps1`, which runs the game's `-storeshots` mode against a throwaway
profile. Each world gets a working colony (the full campus layout and one robot of every class),
bounties on the nearest dens, and a minute of play at 2× before the camera frames its shots.

| File | Shows |
|---|---|
| 01_earth_colony.jpg | Earth colony: citadel Commons, landing pad with the Colony Lifter, solar and farm yards |
| 02_mars_colony_gameplay.jpg | Mars colony with the full HUD: objectives, treasury, robot tags |
| 03_europa_colony.jpg | Europa colony on the ice, robots at work |
| 04_earth_den_bounty.jpg | A den and its creatures under a live 1,800 EU Clear Threat bounty |
| 05_luna_den.jpg | A Moon den with its creatures |
| 06_mars_colony.jpg | Mars colony, closer, no HUD |
| 07_belt_wide.jpg | The Asteroid Belt colony and its surroundings |
| 08_europa_den.jpg | A Europa den on the ice |
| 09_luna_colony_gameplay.jpg | Moon colony with the full HUD |

Upload at least five; Steam shows the first four on the store page and in search results, so put a
HUD shot and a den shot among them. Re-capture after visual changes:

    powershell -ExecutionPolicy Bypass -File Tools\qa\store_screenshots.ps1 -GameDir Builds\SteamRC\Windows

The raw captures (four or five per world, with and without HUD) land in `Builds\StoreShots`.
Keep the desktop unlocked and the game window in front while it runs.
