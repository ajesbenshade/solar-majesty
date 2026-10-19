# Release checklist

Tracks what is genuinely ready to ship and what is still open. Update it as things land; the value
is in it being honest, not in it being all ticked.

## Build and pipeline

- [x] Assembly definitions split (`SolarMajesty.Systems` / `.Runtime` / `.Editor` / `.Tests.EditMode`)
- [x] Automated EditMode tests (775 passing, 2026-10-19)
- [x] CI on push and nightly ([.github/workflows/ci.yml](../.github/workflows/ci.yml))
- [x] Headless build command ([BuildCommands.cs](../Assets/Scripts/Editor/BuildCommands.cs))
- [x] Standalone player builds (macOS verified)
- [x] Bundle identifier set (`com.solarmajesty.solarmajesty`)
- [ ] Application icons for every platform size
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
- [x] Run statistics and end-of-run verdict ([RunStats.cs](../Assets/Scripts/Systems/RunStats.cs))
- [x] Achievements, persisted ([Achievements.cs](../Assets/Scripts/Systems/Achievements.cs)); evaluated every 2 s of play; Solar Conquest is campaign-wide (it was unreachable); per-world stats survive Continue
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
- [ ] Scripted camera beat for launch and victory

## Audio

- [x] Channel buses with ducking ([SoundBus.cs](../Assets/Scripts/Systems/SoundBus.cs))
- [x] Pooled 3D positional one-shots ([SpatialAudio.cs](../Assets/Scripts/Runtime/Audio/SpatialAudio.cs))
- [x] Adaptive four-stem generative score ([AdaptiveMusic.cs](../Assets/Scripts/Runtime/Audio/AdaptiveMusic.cs))
- [x] Synthesised Overseer voice and robot chirps ([OverseerVoice.cs](../Assets/Scripts/Runtime/Audio/OverseerVoice.cs))
- [ ] Existing one-shots migrated from 2D `DemoAudio` to `SpatialAudio`
- [ ] Missing clips authored: `sfx_extract`, `sfx_build_complete`, `sfx_research` (currently fall through to synthesis)

## Interface

- [x] UI Toolkit foundation with a USS design system ([SolarMajesty.uss](../Assets/Resources/UI/SolarMajesty.uss))
- [x] Alert feed rendered in UI Toolkit ([OverseerAlertView.cs](../Assets/Scripts/Runtime/UI/OverseerAlertView.cs))
- [x] Procedural icon set ([UiIcons.cs](../Assets/Scripts/Runtime/UI/UiIcons.cs)), wired into the resource chips
- [x] Map overlays for power, threat, and defence coverage ([MapOverlay.cs](../Assets/Scripts/Runtime/UI/MapOverlay.cs))
- [ ] Remaining IMGUI panels migrated to UI Toolkit (`OverseerHud.cs` is still ~2,100 lines)
- [ ] Tooltips on every panel
- [x] Controls screen generated from `InputBindings`: Settings → CONTROLS, click to rebind, conflict and reserved-key refusal, reset

## Accessibility

- [x] Colourblind palettes: deuteranopia, protanopia, tritanopia ([Accessibility.cs](../Assets/Scripts/Systems/Accessibility.cs))
- [x] Reduce motion (suppresses camera shake)
- [x] HUD scale
- [x] Severity carried by icon and text prefix as well as colour
- [x] Frame cap selector
- [x] Rebindable keys with conflict rejection ([InputBindings.cs](../Assets/Scripts/Systems/InputBindings.cs))
- [x] Runtime input routed through `InputBindings` (33 actions; hints and tutorial copy follow rebinds via `{key:Action}` tokens). Fixed by design: Esc, number keys 0–9, modifiers, mouse, dialog Enter/Space
- [ ] Gamepad support
- [ ] Independent UI text scaling

## Localization

- [x] Lookup with English fallback ([Localization.cs](../Assets/Scripts/Systems/Localization.cs))
- [ ] Strings extracted into a table (~2,360 literals still inline)
- [ ] Machine translation pass and community correction
- [ ] Font fallback for non-Latin scripts

## Before Early Access

- [ ] **The 45–90 minute external playtest** ([PLAYTEST_PROTOCOL.md](PLAYTEST_PROTOCOL.md)) — still the single
      largest open risk; nobody outside the project has played this
- [x] Benchmark/soak mode (`-benchmark`, [run_benchmark.ps1](../Tools/qa/run_benchmark.ps1)): all five worlds run 30–40 s at 3× with 0 errors; the ~120 ms NavMesh hitch on campus growth is fixed (async rebuild)
- [x] Measured on a high-end PC (Ryzen 9 7900X, RTX 3080, 1080p Ultra, 2026-10-19): 450–574 fps average, worst frame ≤ 26 ms on every world after removing the autosave (~35 ms + GC) and terrain-grading (77 ms) main-thread work
- [ ] 60 fps on minimum-spec hardware: run [run_benchmark.ps1](../Tools/qa/run_benchmark.ps1) on a low-end PC with the desktop unlocked; 200-agent stress still unmeasured
- [x] Real graphics tiers: Low (80% + FSR, no SSAO, 1 shadow cascade to 60 m), Medium (no MSAA/SSAO), High (2x MSAA, 2 cascades), Ultra unchanged; regenerate with **Solar Majesty → Render → Create Quality Tiers**. First launch picks a tier from the GPU ([QualityAutoDetect.cs](../Assets/Scripts/Systems/QualityAutoDetect.cs)). Screenshots checked on Earth and Mars at Low/Medium/Ultra
- [ ] Low tier measured on an actual integrated GPU
- [ ] Full playthrough on all five bodies without an exception
- [ ] Steam page, capsule art, screenshots
- [ ] Steam content survey incl. AI disclosure (draft text in [STEAM_RELEASE.md](STEAM_RELEASE.md))
- [ ] Trademark check on "Solar Majesty"; GitHub repository made private (Asset Store packages are committed)
- [x] Landing-pad rocket redesigned as the original Colony Lifter (legs, strap-on boosters, domed capsule; no flaps or ogive nose); the Starship placeholder model is out of the build ([render](ReviewEvidence/colony-lifter-pad.png))
- [ ] Trailer
- [ ] Demo build for a Next Fest
- [ ] Phase 4 exit review re-run against a fresh still
