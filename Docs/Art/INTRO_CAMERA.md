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

Checked on Basement-1: baked the track, ran `IntroBuilder.BuildAll`, and hashed every curve of the
authored clip (all 8 curves, sampled every 0.25 s) before and after. They were identical, and the builder logged
`Camera track 'Intro Camera (Authored)' is authored. Left untouched.`

Reduce Motion and a missing Timeline still use the code shot in `IntroShot.Sample`, so they are unchanged.

## The shot (0–5 s, settles at about 3.25 s)

| Time | Beat |
| --- | --- |
| 0.0–1.1 s | Low, south-west of the Earth globe. The Americas face the lens against the dusk sky, and the lit citadel sits lower right. The camera drifts in slowly, then speeds up. |
| 1.0–1.5 s | Passes under Earth's south side. Earth slides off frame left, and the aim glides onto the citadel. |
| 1.5–2.4 s | Crests over the dark plain and curves down toward the colony. The lit windows carry the dusk read. |
| 2.4–3.25 s | Ends on a 9 m push-in straight down the title axis, so the title (on at 2.85 s) only grows and stays centred. |
| 3.25–5.0 s | Locked on the settle pose: `ColonyLayout.CameraFocus + (-18, 22, -18)`, `Euler(30, 45, 0)`, FOV 35. The title is 11 m away and fills about 57% of the width at 16:9 and about 63% at 16:10. |

Motion: the path is a Hermite spline, re-parameterised by arc length. Speed starts at 22% and reaches
full by mid-path, then lands with zero velocity and zero acceleration at the end. Peak speed is about 45 m/s
and peak turn rate is about 57°/s at the Earth exit. Bank is at most 3° and returns to 0 before the settle.
FOV runs from 44 to 35. The camera never goes below 22 m. It stays at least 13 m from the Earth globe's
surface and at least 13.5 m from any colony renderer's bounds.

## The final pose is locked

The title slot is placed from `IntroShot.TitlePose`, which is derived from the code settle pose. The authored
camera must end exactly on that pose, or the title is off-centre or skewed. `IntroAuthoredCameraTests` fails
if the authored track drifts from it, for example when someone changes `IntroShot` or `ColonyLayout.CameraFocus`.
If that happens, re-key the track.

## Editing / re-keying

- **Small tweaks:** open `IntroTimeline` in the Timeline window and edit the `Intro Camera (Authored)` clip's
  curves. Keys are every 1/15 s with ClampedAuto tangents. Keep the track name suffix.
- **Re-generate:** `Docs/Art/intro_camera_shot.py` holds the control points, aim, ease and FOV. It writes a
  30 fps table (`t,px,py,pz,qx,qy,qz,qw,fov`). The one-off bake read that table and set the clip curves with
  `AnimationUtility.SetEditorCurve` (`m_LocalPosition.*` and `m_LocalRotation.*` on Transform,
  `field of view` on Camera). The clip is absolute (`removeStartOffset = false`, track in
  `ApplyTransformOffsets` with identity offsets). Then run `IntroBuilder.BuildAll`, which leaves the track alone.

Evidence: `Docs/ReviewEvidence/2026-10-08-intro-camera/`.
