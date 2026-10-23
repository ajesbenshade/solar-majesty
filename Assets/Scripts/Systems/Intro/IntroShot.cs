using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Default 5 second intro: fly past a stand-in Earth, settle on the colony, hold for the title.
    /// The generated Timeline camera is baked from <see cref="Sample"/>. The hand-authored camera
    /// is not. The title slot stays on the old 30° settle; the fallback camera ends on the
    /// authored pose so the lifted title stays in frame.
    /// </summary>
    public static class IntroShot
    {
        public const float Duration = 5f;
        public const float CameraSettle = 3.35f;
        public const float TitleOn = 2.85f;
        public const float TitleOff = Duration;
        /// <summary>Seconds of fade on the shot, then the same again over the title screen. One curve, no hold at black.</summary>
        public const float CrossfadeSeconds = 0.6f;
        public const float FadeStart = 4.4f;
        public const float FadeOutSeconds = CrossfadeSeconds;

        /// <summary>Standing water and the colony ground sit near y = 0. The sweep stays above both.</summary>
        public const float MinCameraHeight = 4f;

        /// <summary>Ignore input for a breath so the click that focused the window does not skip the shot.</summary>
        public const float SkipArmSeconds = 0.12f;

        public const float EarthScale = 14f;

        /// <summary>Metres in front of the settled camera. The slot scale stays 1 so a swapped model is not resized.</summary>
        public const float TitleDistance = 11f;

        /// <summary>
        /// World-up lift so the gold letters sit against the sky instead of the citadel's lit windows.
        /// The title slot stays on the old settle. The fallback camera pitches up to see it.
        /// </summary>
        public const float TitleLift = 5f;

        /// <summary>A hitch after boot cannot jump the shot. One intro frame is at most this long.</summary>
        public const float MaxFrameDelta = 1f / 20f;

        public struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
        }

        public static Vector3 EarthPosition(Vector3 colonyFocus) =>
            colonyFocus + new Vector3(-62f, 28f, 18f);

        /// <summary>Old colony settle, <c>GameLoop.ConfigureCamera</c>: focus + (-18, 22, -18), pitch 30, yaw 45.
        /// The title slot is built from this. The fallback camera does not end here.</summary>
        public static Vector3 ColonySettlePosition(Vector3 colonyFocus) =>
            colonyFocus + new Vector3(-18f, 22f, -18f);

        /// <summary>
        /// Fallback end, the same pose as the authored intro camera.
        /// At <c>ColonyLayout.CameraFocus</c> (192, 0, 190) that is (173.05, 19.78, 171.05), pitched 9° up, FOV 35.
        /// </summary>
        public static readonly Vector3 FallbackEndAtFocus = new Vector3(173.05f, 19.78f, 171.05f);
        static readonly Vector3 FallbackFocus = new Vector3(192f, 0f, 190f);
        public const float FallbackEndPitch = -9f;
        public const float FallbackEndFov = 35f;

        public static Vector3 FallbackEndPosition(Vector3 colonyFocus) =>
            colonyFocus + (FallbackEndAtFocus - FallbackFocus);

        public static Pose Sample(float time, Vector3 colonyFocus, bool reduceMotion)
        {
            float clock = reduceMotion ? CameraSettle : time;
            float u = Mathf.Clamp01(clock / CameraSettle);
            float s = u * u * (3f - 2f * u);

            Vector3 earth = EarthPosition(colonyFocus);
            Vector3 start = earth + new Vector3(-22f, 4f, -26f);
            Vector3 lookStart = earth + new Vector3(4f, -2f, 2f);
            Quaternion startRot = Quaternion.LookRotation(lookStart - start, Vector3.up);

            Vector3 end = FallbackEndPosition(colonyFocus);
            Quaternion endRot = Quaternion.Euler(FallbackEndPitch, 45f, 0f);

            return new Pose
            {
                Position = Vector3.Lerp(start, end, s),
                Rotation = Quaternion.Slerp(startRot, endRot, s),
                FieldOfView = Mathf.Lerp(48f, FallbackEndFov, s)
            };
        }

        public static float ClampDelta(float unscaledDelta)
        {
            if (unscaledDelta <= 0f) return 0f;
            return unscaledDelta > MaxFrameDelta ? MaxFrameDelta : unscaledDelta;
        }

        /// <summary>
        /// World pose of the title slot once the camera has settled.
        /// Local +Z matches the camera forward, so the readable face (local -Z) points back at the lens
        /// and the pivot sits at the centre. Lifted on world Y so the letters clear the lit windows.
        /// The placeholder and <c>SM_Title_SolarMajesty</c> share this pose.
        /// </summary>
        public static Pose TitlePose(Vector3 colonyFocus)
        {
            // Stays on the old 30° settle. The authored camera was keyed to this slot.
            Vector3 anchor = ColonySettlePosition(colonyFocus);
            Quaternion anchorRot = Quaternion.Euler(30f, 45f, 0f);
            return new Pose
            {
                Position = anchor + anchorRot * Vector3.forward * TitleDistance + Vector3.up * TitleLift,
                Rotation = anchorRot,
                FieldOfView = FallbackEndFov
            };
        }

        public static bool TitleVisible(float time) => time >= TitleOn && time < TitleOff;

        public static float FadeAlpha(float time) => CrossfadeAlpha(time - FadeStart);

        /// <summary>
        /// One tent from the intro into the title screen. Peaks at <see cref="CrossfadeSeconds"/>
        /// and is back to clear at twice that. It does not sit at full black.
        /// </summary>
        public static float CrossfadeAlpha(float secondsSinceFadeStart)
        {
            if (secondsSinceFadeStart <= 0f || CrossfadeSeconds <= 0.0001f) return 0f;
            if (secondsSinceFadeStart < CrossfadeSeconds)
                return secondsSinceFadeStart / CrossfadeSeconds;
            float down = (secondsSinceFadeStart - CrossfadeSeconds) / CrossfadeSeconds;
            if (down >= 1f) return 0f;
            return 1f - down;
        }
    }

    public enum IntroPlayback
    {
        Fallback = 0,
        Timeline = 1
    }
}
