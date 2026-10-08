# Intro camera v2: title against the dusk sky (3D Artist, 2026-10-08)

Re-key of the end of `Intro Camera (Authored)` after the title slot moved up 5 m (`IntroShot.TitleLift`)
to (180.736, 21.5, 178.736), rotation `Euler(30, 45, 0)`.

- `intro_camera_v2_1920x1080_30fps.mp4`: the whole intro plus the crossfade, 200 frames on a fixed
  30 fps clock (preview dev player, Timeline playback, fade composited from `IntroSequence.OverlayAlpha`).
- `01_t0.8_earth_terminator_1920x1080.jpg`: the Earth pass with the lit `SM_IntroEarth` material.
- `02_t3.4_title_hold_16x9_1920x1080.jpg` and `03_t3.4_title_hold_16x10_1920x1200.jpg`: the hold.
- `camera_samples_capture_v2.csv`: per-frame camera pose and the distance to the nearest renderer bounds.

Hold pose: camera (173.05, 19.78, 171.05), pitch 9° up, yaw 45, FOV 35, 11 m from the slot. It is settled
at 3.35 s and holds until the 4.4 s fade. Title is centred against the mauve sky with the horizon below it.
At 16:9 it is about 61% of the width and fully in frame at 16:10. Peak turn rate 51°/s (v1: 57°/s), peak
speed 45 m/s, lowest point 18.3 m, at least 9.9 m from any renderer's bounds (the ground plane), and
nothing clips.

`IntroBuilder.BuildAll` left `IntroTimeline.playable` and the scene byte-identical (SHA-256 before/after).

Capture caveat: the preview harness renders through `RenderPipeline.SubmitRenderRequest`. The far ground
just below the horizon renders black here, while Unity Engineer's live capture (intro-v4) shows brown
haze there. The v1 evidence has the same band, so it is probably the capture path, not this change.
