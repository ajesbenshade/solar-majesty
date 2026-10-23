# Intro camera (authored)

The first-launch intro camera is a hand-authored Timeline track, **`Intro Camera (Authored)`**, inside
`Assets/Resources/Intro/IntroTimeline.playable`. It drives the Main Camera's position, rotation and
field of view for the 5 s intro.

## Why it survives `IntroBuilder.BuildAll`

`IntroBuilder` (PR #72) leaves any camera track whose name ends in ` (Authored)` alone
(`IntroAssets.AuthoredCameraSuffix`). It doesn't touch the track, its clip, curves or offsets, and it only
rebuilds the other tracks. When an authored track exists, the builder (`WireScene`) and the runtime
(`IntroSequence.Bind`) bind only the authored track to the camera Animator. The generated `Intro Camera`
track stays in the asset unchanged, because `IntroSequenceTests` still checks it against the code fallback.
Its scene binding is cleared, so it drives nothing.

Checked on Basement-1 (v2 re-key): ran `IntroBuilder.BuildAll` and compared the SHA-256 of
`IntroTimeline.playable` before and after: byte-identical. The builder logged
`Camera track 'Intro Camera (Authored)' is authored. Left untouched.`

## The shot (0–5 s, settles at 3.35 s)

| Time | Beat |
| --- | --- |
| 0.0–1.1 s | Low, south-west of the Earth globe. The Americas face the lens against the dusk sky, and the lit citadel sits lower right. The camera drifts in slowly, then speeds up. |
| 1.0–1.5 s | Passes under Earth's south side. Earth slides off frame left, and the aim glides onto the citadel. |
| 1.5–2.3 s | Crests low (about 30 m) over the dark plain and comes down toward the colony, citadel centred. |
| 2.3–3.35 s | Drops to about 19 m and tilts up off the citadel onto the title slot, ending on a push-in down the title axis. The title (on at 2.85 s) grows in the centre of the frame against the sky. |
| 3.35–5.0 s | Held: camera 11 m in front of the title slot on yaw 45, pitch 9° **up**, FOV 35 (position about (173.05, 19.78, 171.05)). The title fills about 61% of the width at 16:9 and about 68% at 16:10, centred, with the horizon about 0.14 frame-heights below its lower edge, so the letters read against the mauve sky. The hold is 1.05 s before the 4.4 s fade. |

Motion: the path is a Hermite spline, re-parameterised by arc length. Speed starts at 22% and reaches
full by mid-path, then lands with zero velocity and zero acceleration at the end. Peak speed is about 45 m/s
and peak turn rate is about 51°/s (v1: 57°/s). The final tilt up runs on the clock (t / 3.35 s) rather than
path progress, so it spreads over the slow landing instead of bunching mid-descent. Bank is at most 3° and
returns to 0 before the settle. FOV runs from 44 to 35. The camera never goes below 18 m, stays at least 20 m
from the Earth globe's surface and never closer than 11 m to the title slot.

## The final pose is tied to the title slot

The title slot is placed by code: `IntroShot.TitlePose` = code settle pose + 11 m forward + `TitleLift` (5 m)
up = (180.736, 21.5, 178.736), rotation `Euler(30, 45, 0)`. Since the lift, the authored camera no longer
ends on the code settle pose (`ColonyLayout.CameraFocus + (-18, 22, -18)`, pitch 30): from there the title
sits above the top of the frame. It ends square to the lifted slot instead, looking slightly up.

`IntroAuthoredCameraTests` checks the slot position, that the camera holds for 1 s before the fade, that the
title is centred 11 m down the view axis with no yaw skew or roll, that all four title corners are in frame at
16:9 and 16:10, the 16:9 width (55–63%), and that the letters sit above the horizon. If `IntroShot` or
`ColonyLayout.CameraFocus` move the slot, re-key the track.

**Reduce Motion / no Timeline:** those still use the code shot in `IntroShot.Sample`, which ends on the
code settle pose. With `TitleLift = 5` the title is above the frame there (centre at about 1.6 in NDC, i.e.
about 0.3 frame-heights above the top edge). That is code, not this track.

## Earth globe (asset only)

`IntroSequence.EnsureEarth` keeps a renderer material named `SM_IntroEarth` instead of creating its unlit
one. `Assets/Art/Intro/SM_IntroEarth.mat` (URP Lit, the orrery Earth texture as base map, smoothness 0.3, the
same texture as a dim blue-tinted emission, about 0.3, so the night side keeps a faint read) is assigned to `Intro/IntroEarth`
in `LunarOutpost_Sandbox`. The low dusk sun now gives it a day/night terminator. The orrery texture is not changed.

## Editing / re-keying

- **Small tweaks:** open `IntroTimeline` in the Timeline window and edit the `Intro Camera (Authored)` clip's
  curves. Keys are every 1/15 s with ClampedAuto tangents. Keep the track name suffix.
- **Re-generate:** `Docs/Art/intro_camera_shot.py` holds the control points, aim, ease, end pitch and FOV. It
  writes a 30 fps table (`t,px,py,pz,qx,qy,qz,qw,fov`). The one-off bake read that table and set the clip
  curves with `AnimationUtility.SetEditorCurve` (`m_LocalPosition.*` and `m_LocalRotation.*` on Transform,
  `field of view` on Camera). The clip is absolute (`removeStartOffset = false`, track in
  `ApplyTransformOffsets` with identity offsets). Then run `IntroBuilder.BuildAll`, which leaves the track alone.

Evidence: `Docs/ReviewEvidence/2026-10-08-intro-camera/` (v1) and
`Docs/ReviewEvidence/2026-10-08-intro-camera-v2/` (this re-key).
