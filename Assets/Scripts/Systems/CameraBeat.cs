using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Which scripted camera beat is playing.</summary>
    public enum CameraBeatKind { Launch, Victory }

    /// <summary>
    /// Where a beat is. Intro eases off the player's view, Hold waits on the subject (ignition),
    /// Main is the move itself (climb / orbit), Return eases back to the player's view.
    /// </summary>
    public enum CameraBeatPhase { Idle, Intro, Hold, Main, Return, Done }

    /// <summary>How a beat ended. Aborted means the scene went away: no completion callback.</summary>
    public enum CameraBeatEnd { None, Completed, Skipped, TargetLost, Aborted }

    /// <summary>A camera pose for a beat: where it sits, what it looks at, its vertical FOV.</summary>
    public struct BeatPose
    {
        public Vector3 Position;
        public Vector3 LookAt;
        public float Fov;

        public BeatPose(Vector3 position, Vector3 lookAt, float fov)
        {
            Position = position;
            LookAt = lookAt;
            Fov = fov;
        }

        public Quaternion Rotation
        {
            get
            {
                Vector3 d = LookAt - Position;
                if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
                return Quaternion.LookRotation(d.normalized, Vector3.up);
            }
        }

        public static BeatPose Lerp(BeatPose a, BeatPose b, float t) => new BeatPose(
            Vector3.LerpUnclamped(a.Position, b.Position, t),
            Vector3.LerpUnclamped(a.LookAt, b.LookAt, t),
            Mathf.LerpUnclamped(a.Fov, b.Fov, t));
    }

    /// <summary>What a beat films: the player's starting view and the subject.</summary>
    public struct BeatContext
    {
        /// <summary>The player's view as a perspective pose.</summary>
        public BeatPose Start;
        /// <summary>Lifter base on the pad (launch) or the colony centre (victory).</summary>
        public Vector3 Subject;
        public float LifterHeight;
        /// <summary>Where the lifter's base is now (it moves during the climb).</summary>
        public Vector3 LifterBase;
        /// <summary>The player's flat heading; shots are placed relative to it.</summary>
        public Vector3 ViewFlat;
        /// <summary>Heading of the pad shot, degrees off <see cref="ViewFlat"/> (chosen for a clear line).</summary>
        public float LaunchYaw;
    }

    /// <summary>Timing for one beat variant (full or reduced motion). Seconds of unscaled time.</summary>
    public sealed class CameraBeatProfile
    {
        public readonly float Intro;
        public readonly float Hold;
        public readonly float Main;
        public readonly float Return;
        /// <summary>Fade to black over the last seconds (launch hands off to a scene load).</summary>
        public readonly float FadeOut;
        /// <summary>False: the camera cuts to a still framing instead of travelling (Reduce motion).</summary>
        public readonly bool CameraMoves;
        /// <summary>Engine rumble on the camera.</summary>
        public readonly bool Shake;

        public CameraBeatProfile(float intro, float hold, float main, float ret, float fadeOut, bool cameraMoves, bool shake)
        {
            Intro = Mathf.Max(0f, intro);
            Hold = Mathf.Max(0f, hold);
            Main = Mathf.Max(0.01f, main);
            Return = Mathf.Max(0f, ret);
            FadeOut = Mathf.Clamp(fadeOut, 0f, Main);
            CameraMoves = cameraMoves;
            Shake = shake;
        }

        public float Total => Intro + Hold + Main + Return;
    }

    /// <summary>
    /// Every number the launch and victory beats use, in one place. Distances are in metres or
    /// in lifter heights where noted; curves are the easing functions below.
    /// </summary>
    public static class CameraBeatTuning
    {
        // ---- timing ------------------------------------------------------------------------------
        // Launch: ease in to the pad, hold on ignition, follow the climb, fade to the next world.
        public static readonly CameraBeatProfile Launch =
            new CameraBeatProfile(intro: 1.6f, hold: 1.5f, main: 4.2f, ret: 0f, fadeOut: 0.7f, cameraMoves: true, shake: true);
        // Reduce motion: cut to a still wide shot of the pad, no shake, shorter.
        public static readonly CameraBeatProfile LaunchReduced =
            new CameraBeatProfile(intro: 0f, hold: 0.6f, main: 2.6f, ret: 0f, fadeOut: 0.5f, cameraMoves: false, shake: false);
        // Victory: ease out to a slow orbit and pull-back over the colony, then back to the player.
        public static readonly CameraBeatProfile Victory =
            new CameraBeatProfile(intro: 1.5f, hold: 0f, main: 4.5f, ret: 0.9f, fadeOut: 0f, cameraMoves: true, shake: false);
        // Reduce motion: a still wide shot of the colony, then a cut back.
        public static readonly CameraBeatProfile VictoryReduced =
            new CameraBeatProfile(intro: 0f, hold: 0f, main: 2.2f, ret: 0f, fadeOut: 0f, cameraMoves: false, shake: false);

        /// <summary>Clicks and keys this soon after the beat starts do not skip it (the click that launched).</summary>
        public const float SkipGrace = 0.25f;

        // ---- lens --------------------------------------------------------------------------------
        public const float BeatFov = 36f;
        /// <summary>The lens opens a little during the climb so the lifter stays in frame longer.</summary>
        public const float LaunchClimbFov = 42f;
        public const float VictoryFov = 38f;

        // ---- launch shot (lifter heights) ---------------------------------------------------------
        /// <summary>Camera distance from the pad, in lifter heights.</summary>
        public const float LaunchShotDistance = 3.0f;
        /// <summary>Camera height above the pad, in lifter heights: eye level with the lifter, high enough to see the deck over the pad walls.</summary>
        public const float LaunchShotHeight = 0.7f;
        /// <summary>Look point on the lifter, as a fraction of its height.</summary>
        public const float LaunchLookHeight = 0.6f;
        /// <summary>
        /// Headings tried for the pad shot, in degrees off the player's own heading, best first: a 3/4
        /// view from the player's side, then further round. The first one with a clear line to the
        /// lifter (no building, no hill) wins.
        /// </summary>
        public static readonly float[] LaunchYawCandidates = { 28f, -28f, 55f, -55f, 0f, 90f, -90f, 130f, -130f, 180f };
        public const float LaunchYawDefault = 28f;
        /// <summary>How high the lifter climbs during Main, in lifter heights.</summary>
        public const float LaunchClimbHeights = 11f;
        /// <summary>Ease-in exponent of the climb: slow off the pad, fast at the top.</summary>
        public const float LaunchClimbExponent = 2.3f;
        /// <summary>The camera cranes up this much (lifter heights) while it follows the climb.</summary>
        public const float LaunchCameraRise = 2.2f;
        /// <summary>And eases back this much (lifter heights).</summary>
        public const float LaunchCameraPullback = 1.0f;
        /// <summary>
        /// The camera never pitches up more than this while following the climb. Kept well under half
        /// the lens's vertical angle, so the horizon and the pad stay in the bottom of the frame and the
        /// lifter leaves through the top of it, rather than the shot ending up staring at empty sky.
        /// </summary>
        public const float LaunchMaxTiltUp = 12f;
        /// <summary>
        /// Reduce-motion still: a wide locked-off frame with the lifter in the lower third and room above
        /// it, so the climb plays without the camera moving.
        /// </summary>
        public const float LaunchStillDistance = 6f;
        public const float LaunchStillHeight = 1.0f;
        public const float LaunchStillLook = 1.5f;
        /// <summary>The pad's base must be visible from the shot (lifter heights above the pad).</summary>
        public const float LaunchBaseCheck = 0.05f;

        // ---- exhaust (lifter heights, unscaled seconds) -------------------------------------------
        public const float FlameLength = 0.42f;
        public const float FlameWidth = 0.16f;
        /// <summary>Trail puffs are laid every this many lifter heights of climb.</summary>
        public const float PuffSpacing = 0.07f;
        public const float PuffStartSize = 0.1f;
        public const float PuffEndSize = 0.5f;
        public const float PuffLife = 1.6f;
        /// <summary>Ground-cloud puffs per second while the engines are lit on (or just off) the pad.</summary>
        public const float GroundPuffRate = 28f;
        public const float GroundPuffSpeed = 1.0f;
        public const float GroundPuffStartSize = 0.1f;
        public const float GroundPuffEndSize = 0.4f;
        /// <summary>Ground puffs are flattened: dust rolling out along the pad, not a dome.</summary>
        public const float GroundPuffSquash = 0.6f;
        public const float SmokeAlpha = 0.5f;
        /// <summary>Fraction of Main during which the climb still kicks up the ground cloud.</summary>
        public const float GroundCloudUntil = 0.35f;
        public const int MaxPuffs = 220;
        public const float GlowRange = 3.5f;
        public const float GlowIntensity = 3f;

        // ---- victory orbit (metres) ----------------------------------------------------------------
        public const float VictoryRadiusStart = 38f;
        public const float VictoryRadiusEnd = 58f;
        public const float VictoryHeightStart = 20f;
        public const float VictoryHeightEnd = 31f;
        /// <summary>Degrees of yaw swept over Main.</summary>
        public const float VictorySweep = 75f;
        public const float VictoryLookHeight = 2f;

        // ---- shake and clearance -------------------------------------------------------------------
        public const float IgnitionShake = 0.06f;
        public const float ClimbShake = 0.12f;
        /// <summary>Camera stays at least this far above the terrain.</summary>
        public const float GroundClearance = 1.4f;
        /// <summary>The intro's path arcs up by this fraction of its length, so it sweeps over buildings.</summary>
        public const float IntroArc = 0.12f;
        /// <summary>
        /// Without the sky panorama (its shader is not in every build), the beat clears to this point of
        /// the body's own sky gradient (sine of elevation) instead of showing the classic camera's
        /// skybox, which is tuned for edge fill and never meant to be looked at.
        /// </summary>
        public const float SkyFallbackElevation = 0.15f;
        /// <summary>Radius of the camera's collision probe against buildings.</summary>
        public const float ProbeRadius = 0.7f;

        public static CameraBeatProfile For(CameraBeatKind kind, bool reduceMotion)
        {
            if (kind == CameraBeatKind.Launch) return reduceMotion ? LaunchReduced : Launch;
            return reduceMotion ? VictoryReduced : Victory;
        }

        /// <summary>Smooth ease-in-out (cubic). The camera's curve between poses.</summary>
        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Gentle ease-out (quadratic) for the orbit's pull-back.</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>Climb altitude in metres at <paramref name="t"/> (0..1) of Main for a lifter this tall.</summary>
        public static float ClimbAltitude(float t, float lifterHeight) =>
            Mathf.Pow(Mathf.Clamp01(t), LaunchClimbExponent) * LaunchClimbHeights * Mathf.Max(0.5f, lifterHeight);
    }

    /// <summary>
    /// The beat's state machine. Pure: the director feeds it unscaled time and whether its
    /// subject still exists, and asks it for the phase. Completion is reported exactly once.
    /// </summary>
    public sealed class CameraBeatSequence
    {
        public CameraBeatKind Kind { get; private set; }
        public CameraBeatProfile Profile { get; private set; }
        public CameraBeatPhase Phase { get; private set; } = CameraBeatPhase.Idle;
        public CameraBeatEnd End { get; private set; } = CameraBeatEnd.None;
        public float Elapsed { get; private set; }
        public bool ReduceMotion { get; private set; }

        private bool _completionTaken;

        public bool Active => Phase != CameraBeatPhase.Idle && Phase != CameraBeatPhase.Done;
        public bool Finished => Phase == CameraBeatPhase.Done;

        public void Begin(CameraBeatKind kind, bool reduceMotion)
        {
            Kind = kind;
            ReduceMotion = reduceMotion;
            Profile = CameraBeatTuning.For(kind, reduceMotion);
            Elapsed = 0f;
            End = CameraBeatEnd.None;
            _completionTaken = false;
            Phase = CameraBeatPhase.Intro;
            Advance();
        }

        /// <summary>Advance by unscaled seconds. A lost subject ends the beat at once.</summary>
        public void Tick(float dt, bool targetAlive)
        {
            if (!Active) return;
            if (!targetAlive)
            {
                Finish(CameraBeatEnd.TargetLost);
                return;
            }
            Elapsed += Mathf.Max(0f, dt);
            Advance();
        }

        /// <summary>Player skip. True when it ended a running beat; ignored during the grace window.</summary>
        public bool Skip()
        {
            if (!Active || Elapsed < CameraBeatTuning.SkipGrace) return false;
            Finish(CameraBeatEnd.Skipped);
            return true;
        }

        /// <summary>The scene is going away: stop without a completion callback.</summary>
        public void Abort()
        {
            if (!Active) return;
            Finish(CameraBeatEnd.Aborted);
            _completionTaken = true;
        }

        /// <summary>True exactly once after the beat ends (not after Abort): run the handoff.</summary>
        public bool TakeCompletion()
        {
            if (!Finished || _completionTaken) return false;
            _completionTaken = true;
            return true;
        }

        /// <summary>0..1 through the current phase.</summary>
        public float PhaseT
        {
            get
            {
                if (Profile == null) return 0f;
                float start = PhaseStart(Phase);
                float len = PhaseLength(Phase);
                return len <= 0f ? 1f : Mathf.Clamp01((Elapsed - start) / len);
            }
        }

        /// <summary>0..1 through Main (0 before it, 1 after).</summary>
        public float MainT
        {
            get
            {
                if (Profile == null) return 0f;
                float start = Profile.Intro + Profile.Hold;
                return Mathf.Clamp01((Elapsed - start) / Profile.Main);
            }
        }

        /// <summary>0..1 black over the end of the beat (launch fade-out).</summary>
        public float Fade
        {
            get
            {
                if (Profile == null || Profile.FadeOut <= 0f) return 0f;
                float mainEnd = Profile.Intro + Profile.Hold + Profile.Main;
                return Mathf.Clamp01((Elapsed - (mainEnd - Profile.FadeOut)) / Profile.FadeOut);
            }
        }

        private float PhaseStart(CameraBeatPhase p)
        {
            switch (p)
            {
                case CameraBeatPhase.Intro: return 0f;
                case CameraBeatPhase.Hold: return Profile.Intro;
                case CameraBeatPhase.Main: return Profile.Intro + Profile.Hold;
                case CameraBeatPhase.Return: return Profile.Intro + Profile.Hold + Profile.Main;
                default: return 0f;
            }
        }

        private float PhaseLength(CameraBeatPhase p)
        {
            switch (p)
            {
                case CameraBeatPhase.Intro: return Profile.Intro;
                case CameraBeatPhase.Hold: return Profile.Hold;
                case CameraBeatPhase.Main: return Profile.Main;
                case CameraBeatPhase.Return: return Profile.Return;
                default: return 0f;
            }
        }

        private void Advance()
        {
            // Zero-length phases fall straight through, so a reduced profile skips Intro and Return.
            while (Active && Elapsed >= PhaseStart(Phase) + PhaseLength(Phase))
            {
                if (Phase == CameraBeatPhase.Return)
                {
                    Finish(CameraBeatEnd.Completed);
                    return;
                }
                Phase = Phase + 1;
            }
        }

        private void Finish(CameraBeatEnd end)
        {
            End = end;
            Phase = CameraBeatPhase.Done;
        }
    }

    /// <summary>
    /// Shot geometry. Pure functions of the subject and the player's starting view, so the
    /// director only has to read the scene and write the camera.
    /// </summary>
    public static class CameraBeatShots
    {
        /// <summary>
        /// The player's view as a perspective pose with the same framing at the ground focus, so the
        /// beat can ease off an orthographic camera without a jump in what is on screen.
        /// </summary>
        public static BeatPose FromCamera(Vector3 position, Quaternion rotation, bool orthographic,
            float orthoSize, float fov, float beatFov)
        {
            Vector3 fwd = rotation * Vector3.forward;
            var ground = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(position, fwd);
            bool hit = ground.Raycast(ray, out float dist) && dist > 0f;
            if (!hit) dist = 30f;
            Vector3 focus = ray.GetPoint(dist);
            if (!orthographic)
                return new BeatPose(position, focus, fov);

            float lens = Mathf.Clamp(beatFov, 10f, 90f);
            float d = Mathf.Max(4f, orthoSize) / Mathf.Tan(lens * 0.5f * Mathf.Deg2Rad);
            return new BeatPose(focus - fwd * d, focus, lens);
        }

        /// <summary>Flat heading of a view, for placing shots on the side the player was looking from.</summary>
        public static Vector3 FlatForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
        }

        /// <summary>The 3/4 low shot of the lifter on its pad, from <paramref name="yaw"/> degrees off the player's heading.</summary>
        public static BeatPose LaunchPad(Vector3 padBase, float lifterHeight, Vector3 viewFlat, bool still,
            float yaw = CameraBeatTuning.LaunchYawDefault)
        {
            float h = Mathf.Max(0.5f, lifterHeight);
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * FlatForward(viewFlat);
            float dist = (still ? CameraBeatTuning.LaunchStillDistance : CameraBeatTuning.LaunchShotDistance) * h;
            float up = (still ? CameraBeatTuning.LaunchStillHeight : CameraBeatTuning.LaunchShotHeight) * h;
            float look = (still ? CameraBeatTuning.LaunchStillLook : CameraBeatTuning.LaunchLookHeight) * h;
            return new BeatPose(padBase - dir * dist + Vector3.up * up, padBase + Vector3.up * look,
                CameraBeatTuning.BeatFov);
        }

        /// <summary>
        /// Following the climb: the camera cranes up and eases back, and tilts up to the lifter until
        /// <see cref="CameraBeatTuning.LaunchMaxTiltUp"/>; past that the lifter climbs out of the top of the frame.
        /// </summary>
        public static BeatPose LaunchClimb(BeatPose padShot, Vector3 lifterBase, float lifterHeight, float t)
        {
            float h = Mathf.Max(0.5f, lifterHeight);
            float e = CameraBeatTuning.EaseInOut(t);
            Vector3 away = FlatForward(padShot.Position - padShot.LookAt);
            Vector3 pos = padShot.Position
                          + Vector3.up * (CameraBeatTuning.LaunchCameraRise * h * e)
                          + away * (CameraBeatTuning.LaunchCameraPullback * h * e);
            Vector3 look = lifterBase + Vector3.up * (CameraBeatTuning.LaunchLookHeight * h);
            look = CapTilt(pos, look, CameraBeatTuning.LaunchMaxTiltUp);
            float fov = Mathf.Lerp(CameraBeatTuning.BeatFov, CameraBeatTuning.LaunchClimbFov, e);
            return new BeatPose(pos, look, fov);
        }

        /// <summary>A look point no more than <paramref name="maxUpDeg"/> above the horizontal from <paramref name="from"/>.</summary>
        public static Vector3 CapTilt(Vector3 from, Vector3 look, float maxUpDeg)
        {
            Vector3 d = look - from;
            float flat = new Vector2(d.x, d.z).magnitude;
            if (flat < 1e-4f) return from + new Vector3(1f, 0f, 0f); // straight overhead: look level, never up the exhaust
            float maxRise = flat * Mathf.Tan(Mathf.Clamp(maxUpDeg, 0f, 89f) * Mathf.Deg2Rad);
            if (d.y <= maxRise) return look;
            return new Vector3(look.x, from.y + maxRise, look.z);
        }

        /// <summary>The slow orbit and pull-back over the colony. <paramref name="t"/> is 0..1 of Main.</summary>
        public static BeatPose VictoryOrbit(Vector3 center, Vector3 viewFlat, float t)
        {
            float e = CameraBeatTuning.EaseOut(t);
            Vector3 back = -FlatForward(viewFlat);
            Vector3 dir = Quaternion.Euler(0f, CameraBeatTuning.VictorySweep * t, 0f) * back;
            float r = Mathf.Lerp(CameraBeatTuning.VictoryRadiusStart, CameraBeatTuning.VictoryRadiusEnd, e);
            float y = Mathf.Lerp(CameraBeatTuning.VictoryHeightStart, CameraBeatTuning.VictoryHeightEnd, e);
            return new BeatPose(center + dir * r + Vector3.up * y,
                center + Vector3.up * CameraBeatTuning.VictoryLookHeight, CameraBeatTuning.VictoryFov);
        }

        /// <summary>
        /// Where the camera should be at this point of the beat. <see cref="BeatContext.Start"/> is the
        /// player's view (see <see cref="FromCamera"/>); the lifter base moves during the climb.
        /// </summary>
        public static BeatPose Evaluate(CameraBeatSequence seq, BeatContext ctx)
        {
            if (seq == null || seq.Profile == null) return ctx.Start;
            bool moves = seq.Profile.CameraMoves;

            if (seq.Kind == CameraBeatKind.Launch)
            {
                BeatPose pad = LaunchPad(ctx.Subject, ctx.LifterHeight, ctx.ViewFlat, !moves, ctx.LaunchYaw);
                if (!moves) return pad; // reduce motion: a locked-off wide shot, the lifter climbs through it
                switch (seq.Phase)
                {
                    case CameraBeatPhase.Intro:
                        return Travel(ctx.Start, pad, seq.PhaseT);
                    case CameraBeatPhase.Hold:
                        return pad;
                    default:
                        return LaunchClimb(pad, ctx.LifterBase, ctx.LifterHeight, seq.MainT);
                }
            }

            BeatPose first = VictoryOrbit(ctx.Subject, ctx.ViewFlat, 0f);
            if (!moves) return first; // reduce motion: one still wide shot
            switch (seq.Phase)
            {
                case CameraBeatPhase.Intro:
                    return Travel(ctx.Start, first, seq.PhaseT);
                case CameraBeatPhase.Hold:
                    return first;
                case CameraBeatPhase.Main:
                    return VictoryOrbit(ctx.Subject, ctx.ViewFlat, seq.MainT);
                case CameraBeatPhase.Return:
                    return Travel(VictoryOrbit(ctx.Subject, ctx.ViewFlat, 1f), ctx.Start, seq.PhaseT);
                default:
                    return ctx.Start;
            }
        }

        /// <summary>
        /// Eased move between two poses whose path arcs up (by <see cref="CameraBeatTuning.IntroArc"/>
        /// of its length) so a camera dropping toward the pad sweeps over the buildings in between.
        /// Starts exactly on <paramref name="a"/> and ends exactly on <paramref name="b"/>.
        /// </summary>
        public static BeatPose Travel(BeatPose a, BeatPose b, float t)
        {
            float e = CameraBeatTuning.EaseInOut(t);
            BeatPose p = BeatPose.Lerp(a, b, e);
            float lift = CameraBeatTuning.IntroArc * Vector3.Distance(a.Position, b.Position) * Mathf.Sin(Mathf.PI * e);
            p.Position += Vector3.up * lift;
            return p;
        }

        /// <summary>
        /// True when the straight line between two points stays at least <paramref name="margin"/>
        /// above the terrain (no hill between the camera and its subject).
        /// </summary>
        public static bool TerrainClear(Vector3 from, Vector3 to, Func<float, float, float> groundHeight, float margin,
            int samples = 16)
        {
            if (groundHeight == null) return true;
            for (int i = 1; i < samples; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)samples);
                if (p.y < groundHeight(p.x, p.z) + margin) return false;
            }
            return true;
        }

        /// <summary>
        /// Keep the camera above the terrain: samples the height under it and a ring around it
        /// (the near plane has width), and lifts it to the clearance.
        /// </summary>
        public static Vector3 AboveGround(Vector3 pos, Func<float, float, float> groundHeight, float clearance,
            float ringRadius = 1.2f)
        {
            if (groundHeight == null) return pos;
            float top = groundHeight(pos.x, pos.z);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                top = Mathf.Max(top, groundHeight(pos.x + Mathf.Cos(a) * ringRadius, pos.z + Mathf.Sin(a) * ringRadius));
            }
            float min = top + clearance;
            if (pos.y < min) pos.y = min;
            return pos;
        }
    }

    /// <summary>
    /// Everything about a camera the beat changes, captured before and written back after, so the
    /// player's view returns exactly as it was.
    /// </summary>
    public struct CameraSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Orthographic;
        public float OrthographicSize;
        public float FieldOfView;
        public float NearClip;
        public float FarClip;
        public CameraClearFlags ClearFlags;
        public Color BackgroundColor;
        public bool Valid;

        public static CameraSnapshot Capture(Camera cam)
        {
            if (cam == null) return default;
            var t = cam.transform;
            return new CameraSnapshot
            {
                Position = t.position,
                Rotation = t.rotation,
                Orthographic = cam.orthographic,
                OrthographicSize = cam.orthographicSize,
                FieldOfView = cam.fieldOfView,
                NearClip = cam.nearClipPlane,
                FarClip = cam.farClipPlane,
                ClearFlags = cam.clearFlags,
                BackgroundColor = cam.backgroundColor,
                Valid = true
            };
        }

        public void Apply(Camera cam)
        {
            if (!Valid || cam == null) return;
            cam.orthographic = Orthographic;
            cam.orthographicSize = OrthographicSize;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = NearClip;
            cam.farClipPlane = FarClip;
            cam.clearFlags = ClearFlags;
            cam.backgroundColor = BackgroundColor;
            cam.transform.SetPositionAndRotation(Position, Rotation);
        }
    }
}
