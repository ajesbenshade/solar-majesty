# Release checklist

Tracks what is genuinely ready to ship and what is still open. Update it as things land; the value
is in it being honest, not in it being all ticked.

## Build and pipeline

- [x] Assembly definitions split (`SolarMajesty.Systems` / `.Runtime` / `.Editor` / `.Tests.EditMode`)
- [x] Automated EditMode tests (129 passing)
- [x] CI on push and nightly ([.github/workflows/ci.yml](../.github/workflows/ci.yml))
- [x] Headless build command ([BuildCommands.cs](../Assets/Scripts/Editor/BuildCommands.cs))
- [x] Standalone player builds (macOS verified)
- [x] Bundle identifier set (`com.solarmajesty.solarmajesty`)
- [x] Application icon (the crowned, ringed planet that is also the "O" of the logo): `Assets/Art/Icons/SM_AppIcon_*`, a 1024 px master plus separately drawn 16–512 px sizes and a Windows `.ico` (16–256 px), made by [make_store_art.py](../Tools/steam/make_store_art.py) `icon`. Committed as the Default Icon and every Standalone slot in ProjectSettings; `ReleaseStamp.ApplyIcons` re-asserts it before each build (writes only on drift), guarded by `AppIconTests`. Steam client `.ico` and 184 px community icon in [Docs/StoreArt](StoreArt/README.md)
- [ ] Icon checked in a fresh Windows build (exe resource and taskbar) after the #92 port; macOS and Linux use the same Standalone set
- [ ] Code signing and notarisation (macOS), Authenticode (Windows) — pipeline steps exist in `release.yml` and run once the certificates are added as secrets ([RELEASE_PIPELINE.md](RELEASE_PIPELINE.md)); not yet exercised
- [ ] `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` secrets added to the GitHub repo so CI can run — CI now fails fast with instructions until they are ([RELEASE_PIPELINE.md](RELEASE_PIPELINE.md))
- [x] Compile check that needs no Unity licence, on every push and PR ([Tools/compile_check](../Tools/compile_check/README.md))
- [x] Versioned builds (tag `vX.Y.Z` or `0.0.0-dev+sha`) with `build-info.json`, tagged GitHub Releases, optional itch.io / Steam upload ([release.yml](../.github/workflows/release.yml))

## Systems

- [x] Fixed 20 Hz simulation tick ([SimClock.cs](../Assets/Scripts/Systems/SimClock.cs))
- [x] Game speed: hold, 1x, 2x, 3x ([SimSpeed.cs](../Assets/Scripts/Systems/SimSpeed.cs))
- [x] Versioned full-world save with atomic writes ([SaveSystem.cs](../Assets/Scripts/Systems/SaveSystem.cs))
- [x] Save covers flags, escrow, robot health and purse, fauna — the gaps the old continue slot had
- [x] Restore of live robots and fauna positions, den scouted/cleared, node remaining, mission hold, and formed parties
- [x] Alert feed with dedupe, severity, and jump-to ([AlertFeed.cs](../Assets/Scripts/Systems/AlertFeed.cs))
- [x] Run statistics and end-of-run verdict ([RunStats.cs](../Assets/Scripts/Systems/RunStats.cs))
- [x] Achievements, persisted ([Achievements.cs](../Assets/Scripts/Systems/Achievements.cs))
- [x] Named robots with service records ([SpecialistIdentity.cs](../Assets/Scripts/Systems/SpecialistIdentity.cs))
- [x] Hardcoded catalogs moved to ScriptableObject databases (`TechCatalog`, `ShopCatalog`, `CelestialBodyCatalog`): optional `Resources/Data` overrides, code tables stay the default ([DATA_CATALOGS.md](DATA_CATALOGS.md))
- [x] Ironman wired to the run configuration (`ReplayRules.Ironman` chip, latched at New Game / Continue; blocks manual saves and body restarts; No Second Draft evaluates on body conquest)

- [x] Steam Early Access readiness, first pass (#92 port): Steamworks.NET achievements, overlay pause and rich presence ([STEAM_RELEASE.md](STEAM_RELEASE.md)); `SteamConfig.AppId` is **480, Valve's Spacewar test app**, not a real ID
- [x] Dev shortcuts (F10 world hop, Shift+click unlock, F8/F9/R debug keys) are off in release builds ([DevTools.cs](../Assets/Scripts/Systems/DevTools.cs)); `-devtools` turns them on
- [x] Prefs live in `Profile/prefs.json` (`GamePrefs`) so Steam Cloud can sync them; the old registry values migrate on first read. Per-PC keys (quality, fullscreen, resolution, frame cap, UI Scale) stay in PlayerPrefs; saved key bindings keep main's `GameAction` numbers (`PrefsMigrationTests`)
- [x] Error log, build stamp and key rebinding come from #82 (`ErrorLog`, `ReleaseStamp`, `InputBindings`); not duplicated by the #92 port
- [x] Live-AI content guard ([AiContentGuard.cs](../Assets/Scripts/Systems/Narration/AiContentGuard.cs)) for the Steam AI-disclosure form; `THIRD-PARTY-NOTICES.txt` ships with builds
- [ ] Still to land from #92: localization, demo build, benchmark / background autosave / async NavMesh, local-AI packager, QA autoplay fixes (listed in the #92 port PR). Store art, capsules, icon and store-shot harness landed in the store-art port

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
- [x] LOD chains on the hero building meshes (every spawned building gets a LODGroup: detail drops at 4 % screen height, culls at 1 %; [BuildingLod.cs](../Assets/Scripts/Runtime/BuildingLod.cs), [BuildingLodRules.cs](../Assets/Scripts/Systems/World/BuildingLodRules.cs))

## Motion

- [x] Velocity-driven locomotion: gait bob, lean, bank, squash ([UnitMotion.cs](../Assets/Scripts/Runtime/Motion/UnitMotion.cs))
- [x] Two-bone IK legs for walkers and arthropod fauna ([ProceduralLegs.cs](../Assets/Scripts/Runtime/Motion/ProceduralLegs.cs))
- [x] Tread scrolling on tracked classes
- [x] Camera shake on impacts, module loss, and construction
- [x] Edge scroll and mouse wheel zoom
- [x] Scripted camera beat for launch and victory ([CameraDirector.cs](../Assets/Scripts/Runtime/Camera/CameraDirector.cs); skipped under Reduce Motion, any input hands back control; needs an in-editor play check)
- [x] Controller support for camera, speed and pause via the legacy Input Manager ([CONTROLS.md](CONTROLS.md)); no pointer or HUD navigation by controller; needs a play check with a real pad

## Audio

- [x] Channel buses with ducking ([SoundBus.cs](../Assets/Scripts/Systems/SoundBus.cs))
- [x] Pooled 3D positional one-shots ([SpatialAudio.cs](../Assets/Scripts/Runtime/Audio/SpatialAudio.cs))
- [x] Adaptive four-stem generative score ([AdaptiveMusic.cs](../Assets/Scripts/Runtime/Audio/AdaptiveMusic.cs))
- [x] Synthesised Overseer voice and robot chirps ([OverseerVoice.cs](../Assets/Scripts/Runtime/Audio/OverseerVoice.cs))
- [x] World-located one-shots play through `SpatialAudio`, with a rolloff that follows the overseer camera; UI, alerts, outcomes and research stay 2D ([SfxRouting.cs](../Assets/Scripts/Systems/SfxRouting.cs), [AUDIO.md](AUDIO.md#2d-or-3d)). The 2D fallback covers a missing or non-finite position. One position-less world call remains: the trade-ship gold in `OverseerHud`, which plays 2D.
- [x] Missing clips authored: `sfx_extract`, `sfx_build_complete`, `sfx_research` (three rendered takes each in `Resources/Audio/Sfx/`, guarded by `SfxClipCoverageTests`; `DemoAudioRoutingTests` also fails if any event the game plays has no authored take)
- [ ] Listen pass in a build: 3D balance at default and max zoom, in both the ortho and diorama lenses. The tuning is numeric and hasn't been checked by ear. Positioned SFX at screen centre are now ~6 dB louder than before (their 2D level), so check them against the music and voices.

## Interface

- [x] UI Toolkit foundation with a USS design system ([SolarMajesty.uss](../Assets/Resources/UI/SolarMajesty.uss))
- [x] Alert feed rendered in UI Toolkit ([OverseerAlertView.cs](../Assets/Scripts/Runtime/UI/OverseerAlertView.cs))
- [x] Procedural icon set ([UiIcons.cs](../Assets/Scripts/Runtime/UI/UiIcons.cs)), wired into the resource chips
- [x] Map overlays for power, threat, and defence coverage ([MapOverlay.cs](../Assets/Scripts/Runtime/UI/MapOverlay.cs))
- [ ] Remaining IMGUI panels migrated to UI Toolkit (`OverseerHud.cs` is still ~2,100 lines)
- [ ] Tooltips on every panel
- [x] Controls screen generated from `InputBindings` (Settings → CONTROLS: grouped list, click-to-rebind with conflict and reserved-key refusal, reset to defaults, read-only controller map; [CONTROLS.md](CONTROLS.md#rebinding-keys)); needs a play check

## Accessibility

- [x] Colourblind palettes: deuteranopia, protanopia, tritanopia ([Accessibility.cs](../Assets/Scripts/Systems/Accessibility.cs))
- [x] Reduce motion (suppresses camera shake)
- [x] HUD scale
- [x] Severity carried by icon and text prefix as well as colour
- [x] Frame cap selector
- [x] Rebindable keys with conflict rejection ([InputBindings.cs](../Assets/Scripts/Systems/InputBindings.cs))
- [x] Runtime input routed through `InputBindings` (every gameplay hotkey; defaults equal the old keys; only Esc, Shift, mouse buttons and text-box Enter/Esc stay literal on purpose)
- [x] Gamepad support (see the controller line under Motion and [CONTROLS.md](CONTROLS.md#controller))
- [ ] Independent UI text scaling

## Localization

- [x] Lookup with English fallback ([Localization.cs](../Assets/Scripts/Systems/Localization.cs))
- [ ] Strings extracted into a table (~2,360 literals still inline)
- [ ] Machine translation pass and community correction
- [ ] Font fallback for non-Latin scripts

## Before Early Access

- [ ] **The 45–90 minute external playtest** ([PLAYTEST_PROTOCOL.md](PLAYTEST_PROTOCOL.md)) — still the single
      largest open risk; nobody outside the project has played this
- [ ] 60 fps with 200 agents, profiled rather than assumed
- [ ] Full playthrough on all five bodies without an exception
- [x] Steam capsule and library art at the current Steamworks sizes (header, small, main and vertical capsules, page background, library capsule, hero and logo), composed from in-game renders with a Cinzel (OFL) wordmark: [Docs/StoreArt](StoreArt/README.md), rebuilt by [make_store_art.py](../Tools/steam/make_store_art.py) from `store_screenshots.ps1 -Keyart` renders. The page background is a Europa render: swap it for Earth, Moon or Mars, since the campaign now ends at Mars
- [ ] Store screenshots: 9 at 1920×1080 in [Docs/StoreScreenshots](StoreScreenshots/README.md), but they **predate the AI-core reshape and charging stations** (and show Belt and Europa, which players no longer reach). Recapture with [store_screenshots.ps1](../Tools/qa/store_screenshots.ps1) on a current release build; needs an unlocked desktop with the game window in front
- [ ] Key-art renders recaptured on the current build (`store_screenshots.ps1 -Keyart`) and the capsules recomposed (`make_store_art.py all --shots Builds/StoreShots`)
- [ ] Steam store page assembled on the partner site: upload the art, check it in Steamworks' capsule preview, answer the bracketed Early Access questions in [PressKit/STORE_PAGE.md](PressKit/STORE_PAGE.md)
- [ ] Human artist pass on the key art, logo and icon before launch: today's art is programmer art composed from game renders, and the logo is a 2D take on the 3D intro title
- [ ] Trailer
- [ ] Demo build for a Next Fest
- [ ] Phase 4 exit review re-run against a fresh still
