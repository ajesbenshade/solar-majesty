# Release checklist

Tracks what is genuinely ready to ship and what is still open. Update it as things land; the value
is in it being honest, not in it being all ticked.

## Build and pipeline

- [x] Assembly definitions split (`SolarMajesty.Systems` / `.Runtime` / `.Editor` / `.Tests.EditMode`)
- [x] Automated EditMode tests (919 passing, 2026-10-20)
- [x] CI on push and nightly ([.github/workflows/ci.yml](../.github/workflows/ci.yml))
- [x] Headless build command ([BuildCommands.cs](../Assets/Scripts/Editor/BuildCommands.cs))
- [x] Standalone player builds (macOS verified)
- [x] Bundle identifier set (`com.solarmajesty.solarmajesty`)
- [x] Application icon: 1024 px master plus separately drawn 16–512 px sizes in [Assets/Art/Icon](../Assets/Art/Icon), assigned to the Default Icon and every Standalone slot by [AppIconSetup.cs](../Assets/Scripts/Editor/AppIconSetup.cs) (re-asserted before each build, guarded by `AppIconTests`); Windows `.ico` (16–256 px); Steam client `.ico` and 184 px community icon in [Docs/StoreArt](StoreArt/README.md). Checked in the Windows exe; macOS and Linux players use the same Standalone set but were not built
- [x] Steamworks: API startup, achievements, overlay pause, rich presence ([STEAM_RELEASE.md](STEAM_RELEASE.md)) — on test app 480 until the real App ID exists
- [x] SteamPipe staging and upload script ([upload_to_steam.ps1](../Tools/steam/upload_to_steam.ps1)), dry-run verified
- [x] Achievement icons, 256 px achieved + locked ([make_achievement_icons.py](../Tools/steam/make_achievement_icons.py))
- [x] Build stamp (`build_info.txt`), version 0.9.0, single-instance player
- [ ] Real App ID in `SteamConfig.AppId`, Auto-Cloud rules, achievements and rich presence uploaded on the partner site
- [ ] Code signing and notarisation (macOS), Authenticode (Windows)
- [ ] `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` secrets added to the GitHub repo so CI can run

## Systems

- [x] Fixed 20 Hz simulation tick ([SimClock.cs](../Assets/Scripts/Systems/SimClock.cs))
- [x] Game speed: hold, 1x, 2x, 3x ([SimSpeed.cs](../Assets/Scripts/Systems/SimSpeed.cs))
- [x] Versioned full-world save with atomic writes ([SaveSystem.cs](../Assets/Scripts/Systems/SaveSystem.cs))
- [x] Save covers flags, escrow, robot health and purse, fauna — the gaps the old continue slot had
- [x] Restore of live robots and fauna positions, den scouted/cleared, node remaining, mission hold, and formed parties
- [x] Alert feed with dedupe, severity, and jump-to ([AlertFeed.cs](../Assets/Scripts/Systems/AlertFeed.cs))
- [x] Run statistics and end-of-run verdict ([RunStats.cs](../Assets/Scripts/Systems/RunStats.cs)); the tallies are recorded since 2026-10-20 (before, only four were ever written: the summary read zeros and every verdict was "You posted nothing")
- [x] Achievements, persisted ([Achievements.cs](../Assets/Scripts/Systems/Achievements.cs)); evaluated every 2 s of play; Solar Conquest is campaign-wide (it was unreachable); per-world stats survive Continue; Terms Accepted, Hard Bargain, Skinflint, Full Crew, Den Breaker and Rebuilder read tallies nothing recorded and could not be earned, and Clean Sheet came free with any conquest (fixed 2026-10-20)
- [ ] Homesteader ("Reach a population of 20") reads a population the gold-only economy removed, so it cannot be earned: redefine or retire it before the achievements go up on the partner site
- [x] Named robots with service records ([SpecialistIdentity.cs](../Assets/Scripts/Systems/SpecialistIdentity.cs))
- [x] Campaign progress and settings in a file ([GamePrefs.cs](../Assets/Scripts/Systems/GamePrefs.cs)) so Steam Cloud syncs the whole campaign; registry values migrate on first read
- [x] Developer cheats (F10 hop, Shift+click unlock, F8/F9/R) gated to editor, dev builds and `-devtools` ([DevTools.cs](../Assets/Scripts/Systems/DevTools.cs))
- [x] Persistent error log with Settings → Open Logs Folder ([ErrorLog.cs](../Assets/Scripts/Systems/ErrorLog.cs))
- [x] Live-AI content guard on chat input, streaming text and replies ([AiContentGuard.cs](../Assets/Scripts/Systems/Narration/AiContentGuard.cs))
- [ ] Hardcoded catalogs moved to ScriptableObject databases (`TechCatalog`, `ShopCatalog`, `CelestialBodyCatalog`)
- [x] Ironman wired to the run configuration (`ReplayRules.Ironman` chip, latched at New Game / Continue; blocks manual saves and body restarts; No Second Draft evaluates on body conquest)

## Look

- [x] ACES tonemapping and per-body white balance
- [x] SSAO and decal renderer features enabled
- [x] `PostProcessData` assigned — bloom and vignette were configured but never rendering
- [x] Exponential fog replacing the linear fog that washed the campus one colour
- [x] `SM_Hull` procedural panel/wear/dust shader
- [x] `SM_PlanetGround` slope-aware ground shader
- [x] Displaced terrain with level campus pads ([TerrainMeshBuilder.cs](../Assets/Scripts/Runtime/World/TerrainMeshBuilder.cs))
- [x] Scatter props share cached instanced materials instead of one material each
- [ ] Fresh Mars still captured as a spaced overseer campus (run **Solar Majesty > Render > Capture Mars Still** in the editor; batch mode cannot drive play mode). Do not score vs the retired VisualTarget PNG.
- [ ] Campus landmark pass: pad, Starship, solar, extractors readable when placed — empty dirt OK, not fill-every-patch
- [ ] LOD chains on the hero building meshes

## Motion

- [x] Velocity-driven locomotion: gait bob, lean, bank, squash ([UnitMotion.cs](../Assets/Scripts/Runtime/Motion/UnitMotion.cs))
- [x] Two-bone IK legs for walkers and arthropod fauna ([ProceduralLegs.cs](../Assets/Scripts/Runtime/Motion/ProceduralLegs.cs))
- [x] Tread scrolling on tracked classes
- [x] Camera shake on impacts, module loss, and construction
- [x] Edge scroll and mouse wheel zoom
- [x] Scripted camera beat for launch and victory ([CameraBeatDirector.cs](../Assets/Scripts/Runtime/Motion/CameraBeatDirector.cs); every duration, distance and curve in `CameraBeatTuning` in [CameraBeat.cs](../Assets/Scripts/Systems/CameraBeat.cs)). Launch (7.3 s): ease in to a clear 3/4 view of the pad, ignition hold with flame, pad dust and rumble, crane up as the Colony Lifter climbs out of frame, fade to the next world. Victory (6.9 s, on every body conquest including the last): slow orbit and pull-back over the colony, then the verdict. Skip with Esc, click, or pad A/B/Menu; HUD and name tags hidden, world input blocked (Esc skips instead of pausing), camera restored exactly; the world runs at 1x during a beat and the player's speed or victory hold comes back after; a destroyed lifter or a scene change ends the beat without a softlock. Checked in a release build with `-beatshots` on Mars and Earth, including the real win → verdict → CONTINUE → next world path ([frames](ReviewEvidence/launch-beat/)). The painted sky panorama (`Skybox/Panoramic`) was stripped from release builds; a keeper material now ships it (see [GOLDEN_HOUR_AND_SKY.md](GOLDEN_HOUR_AND_SKY.md))

## Audio

- [x] Channel buses with ducking ([SoundBus.cs](../Assets/Scripts/Systems/SoundBus.cs))
- [x] Pooled 3D positional one-shots ([SpatialAudio.cs](../Assets/Scripts/Runtime/Audio/SpatialAudio.cs))
- [x] Adaptive four-stem generative score ([AdaptiveMusic.cs](../Assets/Scripts/Runtime/Audio/AdaptiveMusic.cs))
- [x] Synthesised Overseer voice and robot chirps ([OverseerVoice.cs](../Assets/Scripts/Runtime/Audio/OverseerVoice.cs))
- [x] World-located one-shots play through `SpatialAudio`, with a rolloff that follows the overseer camera; UI, alerts, outcomes and research stay 2D ([SfxRouting.cs](../Assets/Scripts/Systems/SfxRouting.cs), [AUDIO.md](AUDIO.md#2d-or-3d)). The 2D fallback covers a missing or non-finite position. One position-less world call remains: caravan gold in `OverseerHud`, which plays 2D.
- [x] `sfx_extract`, `sfx_build_complete`, `sfx_research`: 3 rendered takes each from `Tools/audio/sfx_events.py`. They were already shipping and the checklist was stale. `DemoAudioRoutingTests` now fails if any event has no authored clip.
- [ ] Listen pass in a build: 3D balance at default and max zoom, in both the ortho and diorama lenses. The tuning is numeric and hasn't been checked by ear. Positioned SFX at screen centre are now ~6 dB louder than before (their 2D level), so check them against the music and voices.

## Interface

- [x] UI Toolkit foundation with a USS design system ([SolarMajesty.uss](../Assets/Resources/UI/SolarMajesty.uss))
- [x] Alert feed rendered in UI Toolkit ([OverseerAlertView.cs](../Assets/Scripts/Runtime/UI/OverseerAlertView.cs))
- [x] Procedural icon set ([UiIcons.cs](../Assets/Scripts/Runtime/UI/UiIcons.cs)), wired into the resource chips
- [x] Map overlays for power, threat, and defence coverage ([MapOverlay.cs](../Assets/Scripts/Runtime/UI/MapOverlay.cs))
- [ ] Remaining IMGUI panels migrated to UI Toolkit (`OverseerHud.cs` is still ~2,100 lines)
- [x] Tooltips on every panel: one system ([HudTooltip.cs](../Assets/Scripts/Runtime/UI/HudTooltip.cs), placement and hover delay in [HudTooltipMath.cs](../Assets/Scripts/Systems/HudTooltipMath.cs)): 0.45 s rest, follows the cursor, flips and clamps on screen, wraps, works with the gamepad's virtual cursor (it moves the OS cursor), hidden while typing, rebinding, dragging or during camera beats. Every tip's words live in [HudTips.cs](../Assets/Scripts/Systems/HudTips.cs): stockpile chips, treasury, levy, clock/threat, speed controls, dock verbs, build/flag/orbital/research rows, bounty −/+, orders, panel headers, minimap, alerts, overlay legend, building and robot cards, title, pause and every Settings option. Labels cut to fit ("…") show their full text as a tooltip. French complete; EditMode tests for placement, delay and the catalog
- [ ] Visual pass of tooltips and TEXT SIZE in a build (Deck 1280×800 `-handheld`, 1080p, 4K × en/fr/qps × 100 %/150 %): `Tools/qa/hud_text_scale_shots.ps1` with a development player. Not done yet: the capture session's desktop was disconnected, so the player rendered blank frames
- [x] Controls screen generated from `InputBindings`: Settings → CONTROLS, click to rebind, conflict and reserved-key refusal, reset

## Accessibility

- [x] Colourblind palettes: deuteranopia, protanopia, tritanopia ([Accessibility.cs](../Assets/Scripts/Systems/Accessibility.cs))
- [x] Reduce motion (suppresses camera shake; the launch and victory beats become short still shots)
- [x] HUD scale
- [x] Severity carried by icon and text prefix as well as colour
- [x] Frame cap selector
- [x] Rebindable keys with conflict rejection ([InputBindings.cs](../Assets/Scripts/Systems/InputBindings.cs))
- [x] Runtime input routed through `InputBindings` (33 actions; hints and tutorial copy follow rebinds via `{key:Action}` tokens). Fixed by design: Esc, number keys 0–9, modifiers, mouse, dialog Enter/Space
- [x] Gamepad support: virtual cursor with real OS clicks (works with the IMGUI HUD), button actions through `InputBindings`, stick pan, trigger zoom, controller-mode hints; PlayMode-tested with a virtual pad
- [x] Steam Deck: detection, controller-first start, 100 % HUD at 1280×800 (`-handheld` to preview), Medium tier, Steam floating keyboard for text fields
- [ ] Play an hour on a real controller and a real Steam Deck before claiming more than Partial Controller Support
- [x] Independent UI text scaling: Settings → TEXT SIZE, 90–150 % in 10 % steps, separate from UI SCALE, saved in GamePrefs (`SM_Set_TextScale`; `-textScale 1.5` for one QA run). Applied in `HudSkin.Text` (display type capped at +6 px), the alert feed, placement refusals and cutscene captions; the dock, chips, catalogs, cards, dialogs and Settings rows grow with it ([HudTextScale.cs](../Assets/Scripts/Systems/HudTextScale.cs), `HudLayout.DockHeight` / `ChipHeight`), long labels fall back to `FitText` with an ellipsis. Identical layout at 100 %. Visual pass pending (see Interface)

## Localization

- [x] Source-keyed lookup with English fallback, JSON tables, Settings → Language, pseudo-locale for QA ([Localization.cs](../Assets/Scripts/Systems/Localization.cs), [LOCALIZATION.md](LOCALIZATION.md))
- [x] Strings extracted into a table: 1,400+ player-facing strings wrapped and collected by [extract.py](../Tools/loc/extract.py); what remains is mostly model prompts, ids and debug text ([status](LOCALIZATION_STATUS.md))
- [x] French table, 100 % (`Resources/Locale/fr.json`), checked in game
- [ ] Native French review before listing French on Steam; more languages and community correction
- [x] Font fallback for non-Latin scripts on Windows (CJK, Indic, Thai); bundle an open font before shipping CJK on Steam Deck

## Before Early Access

- [ ] **The 45–90 minute external playtest** ([PLAYTEST_PROTOCOL.md](PLAYTEST_PROTOCOL.md)) — still the single
      largest open risk; nobody outside the project has played this
- [x] Benchmark/soak mode (`-benchmark`, [run_benchmark.ps1](../Tools/qa/run_benchmark.ps1)): all five worlds run 30–40 s at 3× with 0 errors; the ~120 ms NavMesh hitch on campus growth is fixed (async rebuild)
- [x] Measured on a high-end PC (Ryzen 9 7900X, RTX 3080, 1080p Ultra, 2026-10-19): 450–574 fps average, worst frame ≤ 26 ms on every world after removing the autosave (~35 ms + GC) and terrain-grading (77 ms) main-thread work
- [ ] 60 fps on minimum-spec hardware: run [run_benchmark.ps1](../Tools/qa/run_benchmark.ps1) on a low-end PC with the desktop unlocked; 200-agent stress still unmeasured
- [x] Real graphics tiers: Low (80% + FSR, no SSAO, 1 shadow cascade to 60 m), Medium (no MSAA/SSAO), High (2x MSAA, 2 cascades), Ultra unchanged; regenerate with **Solar Majesty → Render → Create Quality Tiers**. First launch picks a tier from the GPU ([QualityAutoDetect.cs](../Assets/Scripts/Systems/QualityAutoDetect.cs)). Screenshots checked on Earth and Mars at Low/Medium/Ultra
- [ ] Low tier measured on an actual integrated GPU
- [x] Full playthrough on all five bodies without an exception — automated, on release builds: [run_autoplay.ps1](../Tools/qa/run_autoplay.ps1) ([SMOKE_TEST.md](SMOKE_TEST.md)) plays New Campaign through Earth, Luna, Mars, Belt and Europa to SOLAR CONQUEST COMPLETE with 0 errors or exceptions, checking settings, save → load and title → Continue on every world and a quit + relaunch on Mars (2026-10-20: 10–14 real-min a run, ~2.7–3.9 sim-hours; 4 of the last 5 full runs passed). The 14 bugs it turned up are fixed, with EditMode regressions for all but the NavMesh pocket hop. It is a bot, not a person: the human end-to-end pass is the external playtest above
- [x] Europa balance (decided 2026-10-20: keep the junk-bot spiral as the final world's intended difficulty spike; confirm in the external playtest): in 4 of those 5 runs the bot lost its first Europa colony (won after Restart Mission) or, once, stalled with 0 EU. Every robot down raises a junk bot beside it and a wreck raises one every 12 s (up to 3); each takes 10 EU per 2.4 s from the robot it bites or the treasury (~250 EU/min), more than a young Europa colony earns, and robots rest and flee to the inn in the middle of them.
- [x] Store screenshots: 9 at 1920×1080 across all five worlds ([Docs/StoreScreenshots](StoreScreenshots/README.md)); re-capture with [store_screenshots.ps1](../Tools/qa/store_screenshots.ps1). They predate the AI-core Commons (#93): re-capture before uploading
- [x] Steam capsule and library art at the current Steamworks sizes (header, small, main and vertical capsules, page background, library capsule, hero and logo), composed from in-game renders with a Cinzel (OFL) wordmark: [Docs/StoreArt](StoreArt/README.md), rebuilt by [make_store_art.py](../Tools/steam/make_store_art.py) from `store_screenshots.ps1 -Keyart` renders
- [ ] Steam store page assembled on the partner site: upload the art, check it in Steamworks' capsule preview, answer the "Fill in" questions in [STORE_PAGE.md](STORE_PAGE.md)
- [ ] Human artist pass on the key art, logo and icon before launch: today's art is programmer art composed from game renders, and the logo is a 2D take on the 3D intro title
- [ ] Steam content survey incl. AI disclosure (draft text in [STEAM_RELEASE.md](STEAM_RELEASE.md))
- [ ] Trademark check on "Solar Majesty"; GitHub repository made private (Asset Store packages are committed)
- [x] Landing-pad rocket redesigned as the original Colony Lifter (legs, strap-on boosters, domed capsule; no flaps or ogive nose); the Starship placeholder model is out of the build ([render](ReviewEvidence/colony-lifter-pad.png))
- [ ] Trailer
- [x] Demo build for a Next Fest: `BuildCommands.BuildWindowsDemo` makes `SolarMajestyDemo.exe` ("Solar Majesty Demo") with the `SM_DEMO` define for that build only (ProjectSettings untouched; checked that the full build's `SolarMajesty.Systems.dll` has `CompiledAsDemo=false` and the demo's `true`). Earth only, guided start locked on, Settings switches that open the campaign hidden, Solar Conquest / Ironman left out, own `DemoSaves` / `DemoProfile`. Earth's win ends on an end-of-demo screen (Wishlist on Steam via the overlay or browser / Keep playing Earth / Quit). `run_autoplay.ps1 -Demo` plays it from New Campaign through a quit + relaunch to the end screen and on (passed 2026-10-20, 0 errors); `upload_to_steam.ps1 -Demo` stages the demo depot (dry-run verified). See [DEMO.md](DEMO.md#steam-demo-build-next-fest) and [STEAM_RELEASE.md](STEAM_RELEASE.md#7-next-fest-demo)
- [ ] Look at the end-of-demo screen in a build (English and French) and press Wishlist with Steam running: every automated run so far had the desktop locked, so the screen was reached and driven but never seen
- [ ] Demo app created on Steamworks, its App ID in `SteamConfig.DemoAppId` (480 placeholder today), the real `FullGameAppId` so Wishlist opens our page, demo store presence and build reviewed, Next Fest registration
- [ ] Full-game import of demo progress (sketched in STEAM_RELEASE.md, not built)
- [ ] Phase 4 exit review re-run against a fresh still
