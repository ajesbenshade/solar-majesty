# Steam store screenshots

> **Captured 2026-11-02** on a current release build of this branch (AI-core Commons, charging
> pods, Colony Lifter). Only no-HUD shots for now: the live HUD shows a red "BOTS 0" chip next to
> a second "BOTS" chip and labels the minimap "MAJESTY COLONY". Fix those, then add a HUD shot and
> a den shot (the raw captures include both) before uploading; Steam wants at least five.

Captured from the release build (no development watermark) at 1920×1080 on Ultra by
`Tools/qa/store_screenshots.ps1`, which runs the game's `-storeshots` mode against a throwaway
profile (`-saveDir` and `-profileDir` point at a temp folder). By default it shoots Earth, Luna and
Mars; name other worlds with `-Bodies`. Each world gets a working colony (the full campus layout
and one robot of every class),
bounties on the nearest dens, and a minute of play at 2× before the camera frames its shots.

| File | Shows |
|---|---|
| 01_earth_colony_wide.jpg | Earth colony on the meadow: AI-core Commons, charging pod, solar yard, the Colony Lifter on its pad |
| 02_mars_colony_wide.jpg | Mars colony beside a mesa: the Commons, mining scaffold and the Colony Lifter |
| 03_luna_colony_wide.jpg | Moon colony among craters, with a den to the right |
| 04_earth_colony.jpg | Earth colony, closer |
| 05_mars_colony.jpg | Mars colony, closer |
| 06_luna_colony.jpg | Moon colony, closer |

Upload at least five; Steam shows the first four on the store page and in search results, so put a
HUD shot and a den shot among them. Re-capture after visual changes:

    powershell -ExecutionPolicy Bypass -File Tools\qa\store_screenshots.ps1 -GameDir Builds\SteamRC\Windows

The raw captures (four or five per world, with and without HUD) land in `Builds\StoreShots`.
Keep the desktop unlocked and the game window in front while it runs.
