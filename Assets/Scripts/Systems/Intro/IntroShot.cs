using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Default 5 second intro: fly past a stand-in Earth, settle on the colony, hold for the title.
    /// The Timeline builder bakes these poses into the camera track. The runtime uses the same
    /// math when that asset is missing, so both paths frame the same shot.
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

        public struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
        }

        public static Vector3 EarthPosition(Vector3 colonyFocus) =>
            colonyFocus + new Vector3(-62f, 28f, 18f);

        /// <summary>Colony settle matches <c>GameLoop.ConfigureCamera</c>: focus + (-18, 22, -18), pitch 30, yaw 45.</summary>
        public static Vector3 ColonySettlePosition(Vector3 colonyFocus) =>
            colonyFocus + new Vector3(-18f, 22f, -18f);

        public static Pose Sample(float time, Vector3 colonyFocus, bool reduceMotion)
        {
            float clock = reduceMotion ? CameraSettle : time;
            float u = Mathf.Clamp01(clock / CameraSettle);
            float s = u * u * (3f - 2f * u);

            Vector3 earth = EarthPosition(colonyFocus);
            Vector3 start = earth + new Vector3(-22f, 4f, -26f);
            Vector3 lookStart = earth + new Vector3(4f, -2f, 2f);
            Quaternion startRot = Quaternion.LookRotation(lookStart - start, Vector3.up);

            Vector3 end = ColonySettlePosition(colonyFocus);
            Quaternion endRot = Quaternion.Euler(30f, 45f, 0f);

            return new Pose
            {
                Position = Vector3.Lerp(start, end, s),
                Rotation = Quaternion.Slerp(startRot, endRot, s),
                FieldOfView = Mathf.Lerp(48f, 35f, s)
            };
        }

        /// <summary>
        /// World pose of the title slot once the camera has settled.
        /// Local +Z matches the camera forward, so the readable face (local -Z) points back at the lens
        /// and the pivot sits at the centre. The placeholder and <c>SM_Title_SolarMajesty</c> share this pose.
        /// </summary>
        public static Pose TitlePose(Vector3 colonyFocus)
        {
            Pose cam = Sample(CameraSettle, colonyFocus, false);
            return new Pose
            {
                Position = cam.Position + cam.Rotation * Vector3.forward * TitleDistance,
                Rotation = cam.Rotation,
                FieldOfView = cam.FieldOfView
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
