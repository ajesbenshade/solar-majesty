using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Timing and easing for the scripted camera beats (<see cref="CameraDirector"/>). Pure: no
    /// scene access, so the curves are unit tested. Every curve starts and ends at the player's
    /// own pose (blend 0), which is what lets the director hand control back without a jump.
    /// </summary>
    public static class CameraBeatMath
    {
        /// <summary>Input inside this window does not cancel (the click that started the beat).</summary>
        public const float InputGrace = 0.3f;

        // Launch: frame the pad, follow the craft up, come home.
        public const float LaunchFrameSeconds = 0.9f;
        public const float LaunchAscentSeconds = 3.0f;
        public const float LaunchReturnSeconds = 0.7f;
        /// <summary>Height the craft reaches at the end of the ascent (metres).</summary>
        public const float LaunchApex = 42f;
        /// <summary>Orthographic size framing the pad on the ground.</summary>
        public const float LaunchFrameZoom = 13f;
        /// <summary>Added to the framing size by the apex, so the plume stays in shot.</summary>
        public const float LaunchWidenZoom = 9f;

        // Victory: slow pull-back and part orbit over the Commons.
        public const float VictoryInSeconds = 1.8f;
        public const float VictoryHoldSeconds = 4.2f;
        public const float VictoryOutSeconds = 1.5f;
        public const float VictoryOrbitDegrees = 40f;
        /// <summary>Zoom multiplier at the widest point of the pull-back.</summary>
        public const float VictoryPullBack = 1.55f;
        /// <summary>The pull-back is never tighter than this, even from a close-up.</summary>
        public const float VictoryMinZoom = 16f;

        public static float LaunchDuration => LaunchFrameSeconds + LaunchAscentSeconds + LaunchReturnSeconds;

        public static float VictoryDuration => VictoryInSeconds + VictoryHoldSeconds + VictoryOutSeconds;

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        /// <summary>
        /// 0 → 1 over <paramref name="inSeconds"/>, holds, 1 → 0 over <paramref name="outSeconds"/>,
        /// eased at both ends. 0 before the start and after the end.
        /// </summary>
        public static float Envelope(float elapsed, float inSeconds, float holdSeconds, float outSeconds)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed < inSeconds)
                return EaseInOutCubic(elapsed / Mathf.Max(0.0001f, inSeconds));
            float outStart = inSeconds + holdSeconds;
            if (elapsed <= outStart) return 1f;
            if (elapsed >= outStart + outSeconds) return 0f;
            return 1f - EaseInOutCubic((elapsed - outStart) / Mathf.Max(0.0001f, outSeconds));
        }

        /// <summary>How far the camera has left the player's pose for the launch beat (0..1).</summary>
        public static float LaunchBlend(float elapsed) =>
            Envelope(elapsed, LaunchFrameSeconds, LaunchAscentSeconds, LaunchReturnSeconds);

        /// <summary>Craft height above the pad. Holds on the pad while the camera frames it, then accelerates.</summary>
        public static float CraftHeight(float elapsed) =>
            LaunchApex * EaseInQuad((elapsed - LaunchFrameSeconds) / LaunchAscentSeconds);

        /// <summary>Orthographic size during the launch beat, starting and ending at <paramref name="startZoom"/>.</summary>
        public static float LaunchZoom(float elapsed, float startZoom)
        {
            float rise = CraftHeight(elapsed) / LaunchApex;
            float framed = LaunchFrameZoom + LaunchWidenZoom * rise;
            return Mathf.Lerp(startZoom, framed, LaunchBlend(elapsed));
        }

        /// <summary>Point the launch camera looks at: the player's focus blending to the rising craft.</summary>
        public static Vector3 LaunchFocus(float elapsed, Vector3 startFocus, Vector3 pad)
        {
            Vector3 craft = pad + Vector3.up * CraftHeight(elapsed);
            return LerpPoint(startFocus, craft, LaunchBlend(elapsed));
        }

        public static float VictoryBlend(float elapsed) =>
            Envelope(elapsed, VictoryInSeconds, VictoryHoldSeconds, VictoryOutSeconds);

        /// <summary>Yaw offset (degrees) of the victory orbit. Unwinds to 0 as the beat hands back.</summary>
        public static float VictoryYaw(float elapsed)
        {
            float travel = EaseInOutCubic(elapsed / (VictoryInSeconds + VictoryHoldSeconds));
            return VictoryOrbitDegrees * travel * VictoryBlend(elapsed);
        }

        /// <summary>Orthographic size of the pull-back, never past <paramref name="maxZoom"/>.</summary>
        public static float VictoryZoom(float elapsed, float startZoom, float maxZoom)
        {
            float wide = Mathf.Max(startZoom * VictoryPullBack, VictoryMinZoom);
            wide = Mathf.Min(wide, Mathf.Max(startZoom, maxZoom));
            return Mathf.Lerp(startZoom, wide, VictoryBlend(elapsed));
        }

        public static Vector3 VictoryFocus(float elapsed, Vector3 startFocus, Vector3 commons) =>
            LerpPoint(startFocus, commons, VictoryBlend(elapsed));

        /// <summary>Any player input after the grace window hands control straight back.</summary>
        public static bool ShouldCancel(float elapsed, bool playerInput) =>
            playerInput && elapsed >= InputGrace;

        public static Vector3 LerpPoint(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }
    }
}
