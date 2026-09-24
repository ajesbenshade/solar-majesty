# Golden hour, sky and diorama camera

This is a presentation-only pass. Game rules are untouched, and at mission start every world looks exactly as it was tuned.

## Light cycle (on by default)

`SunPath` (pure, `Systems/World`) and `SunCycle` (`Runtime/Atmosphere`) move the sun through a cycle on mission time: dawn → golden hour → high sun → golden hour → blue-hour night → dawn. One cycle is 8 Sols (8 minutes), and it pauses when the game is paused.

- **Starts on the tuned look.** At t = 0 the sun matches `CelestialBodyProfile.SunEuler` / `SunColor` exactly (locked by `VisualsTests`). During still captures (`StillCaptureHold`) or with the setting off, the cycle holds there.
- **The sun never sets.** It bottoms out at 7° elevation. Night dims the light to 30% and cools it rather than removing it, so the colony stays readable and emissive windows and pad lights bloom (bloom rises at night).
- **Sunsets differ per world:** amber on Earth, **blue on Mars** (as on the real planet), and white on airless worlds.
- Ambient, fill, fog and clear colour are *scaled* from the body's tuned values, not replaced. Shadow strength, fog distances and the Mars fog math are unchanged.

## Cloud shadows (on by default)

`CloudShadows` puts a generated, tileable cookie on the sun. Earth gets drifting cumulus shadows; Mars gets a faint dust veil; airless worlds get none. Drift follows mission time.

## Diorama camera (opt-in: Settings → DIORAMA CAMERA, or `-diorama`)

A perspective lens on the existing rig. The orthographic pose is still the source of truth, so pan, bounds, focus-on, orbit and every caller that reads `orthographicSize` behave the same. The perspective camera is derived from that pose each frame (`DioramaRig`):

- Up close it looks like the classic iso view (32° lens, slightly steeper).
- As you zoom out past ~55%, the pitch lifts toward 6° until the **horizon and sky** come into frame.
- Fog switches to a perspective-friendly linear band: clear through the focus, then hazing to the fog colour, which is also the sky's horizon colour, so the ground melts into the sky.
- **Sky panoramas** (`SkyPainter` + `SkyPanorama`, shown via `Skybox/Panoramic`): the Earth rising over Luna (with its phase set by the Luna sun), Jupiter with the Great Red Spot over Europa, a daytime crescent Phobos in Mars's butterscotch sky, star fields plus the Milky Way on airless worlds, and a blue gradient on Earth. Planet sizes are exaggerated on purpose so they read in the narrow band of sky.
- **Tilt-shift** (Settings → TILT-SHIFT): Bokeh depth of field focused on the ground at screen centre, strongest up close and gone by mid zoom, for the miniature-diorama look. It runs on its own volume, and only with the perspective camera.

The classic orthographic camera never shows sky, so its skybox (and the reflections baked from it) are restored untouched when diorama is turned off.

Harness renders of the sky band at full zoom-out (flat fog-colour ground; not an in-engine capture): `Docs/ReviewEvidence/2026-09-24-sky-preview-*.png`.

## Build note

`Skybox/Panoramic` is reached only through `Shader.Find`. Run **Solar Majesty → Render → Configure URP For Look Target** once, which now adds it (and `Skybox/Procedural`) to Always Included Shaders. Without it, the diorama camera still works but shows the tuned sky; the game warns once.

## Tuning knobs

| What | Where |
|---|---|
| Cycle length, day share, night floor | `SunPath.CycleSeconds`, `DayShare`, `NightIntensity` |
| Sunset / night colours | `SunPath.GoldenColor`, `NightColor` |
| Lens, horizon pitch, when the lift starts | `DioramaRig.FieldOfView`, `HorizonPitch`, `LiftStart` |
| Diorama fog band | `DioramaRig.Fog` |
| Sky planets (size, placement) | `SkyPainter.PlanetFor` |
| Tilt-shift range and strength | `DioramaFocus.FullAt` / `GoneAt`, focal length and aperture in `DioramaFocus` |
| Cloud coverage and drift | `CloudShadows.Apply` (cookie thresholds, size, wind) |
