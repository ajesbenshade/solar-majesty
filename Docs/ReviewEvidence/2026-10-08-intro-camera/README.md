# Intro camera shots: review evidence (2026-10-08)

The new authored camera, `Intro Camera (Authored)`, on top of PR #72 at `ccc9446`. It was captured on
Basement-1 from a Windows development player built under a separate product name ("Solar Majesty Intro
Preview"), so the real game's saves and PlayerPrefs were never touched. It is Earth, campaign start, cold title.
The capture replays the intro on a fixed 30 fps clock, so each frame is exactly 1/30 s apart (a frame-perfect
render, not a screen recording). The Timeline director was stepped from the intro's own clock.

- `intro_camera_1920x1080_30fps.mp4` covers the whole intro, 0–5.0 s, then the crossfade into the title
  screen (6.7 s, 200 frames). The 3D camera view only: the OnGUI fade is composited from
  `IntroSequence.OverlayAlpha`, and the title-screen IMGUI buttons are not in the capture.
- `01_t0.0_earth_dusk`: the opening. The Americas face the lens against the dusk sky, and the lit colony is lower right.
- `02_t0.8_earth_pass`: drifting past Earth.
- `03_t1.6_colony_at_dusk`: the aim has moved to the lit citadel, which is crossing the plain.
- `04_t4.0_title_hold`: settled. The title is centred and about 57% of the frame width at 16:9.
- `05_t4.3_title_hold_1920x1200`: the same hold at 16:10, where the title is about 63% of the width.
- `camera_samples_capture.csv`: the camera pose, FOV and fade value for every captured frame, plus the
  nearest renderer-bounds clearance. Minimum clearance is 13.6 m (ground plane bounds). The camera was never
  inside any renderer, never below 22 m, and never closer than 13 m to the Earth globe's surface.

EditMode: 632/632 passed, including the new `IntroAuthoredCameraTests` (2 tests) and every existing
`IntroSequenceTests` case. This needed a local, uncommitted `using UnityEngine.Rendering;` in `IntroDusk.cs`,
because `ccc9446` doesn't compile without it (`AmbientMode`). That fix belongs in #72.

Builder check: after baking, `IntroBuilder.BuildAll` logged
`Camera track 'Intro Camera (Authored)' is authored. Left untouched.`, and the authored clip's curve hash
was identical before and after the rebuild.
