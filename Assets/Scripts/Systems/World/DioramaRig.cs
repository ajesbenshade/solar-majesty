using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Pure math for the perspective "diorama" camera. The zoom value keeps its orthographic
    /// meaning (half the visible height at the focus, in metres), so every caller that reads or
    /// sets orthographicSize still works. Near zoom looks like the classic iso view; toward max
    /// zoom the pitch lifts until the horizon and a band of sky come into frame.
    /// </summary>
    public static class DioramaRig
    {
        /// <summary>Narrow lens keeps iso-like proportions up close.</summary>
        public const float FieldOfView = 32f;
        /// <summary>Pitch at full zoom-out. Below FieldOfView / 2, so sky shows above the horizon.</summary>
        public const float HorizonPitch = 6f;
        /// <summary>Zoom fraction (0 = closest) where the pitch starts lifting toward the horizon.</summary>
        public const float LiftStart = 0.55f;
        /// <summary>Close-up pitch is this much steeper than the player's orbit pitch.</summary>
        public const float CloseTilt = 4f;
        public const float MinClearance = 3f;

        /// <summary>Camera-to-focus distance that shows <paramref name="zoom"/> metres of half-height.</summary>
        public static float Distance(float zoom) =>
            Mathf.Max(0.5f, zoom) / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>Pitch for a zoom fraction (0 closest, 1 farthest), given the player's orbit pitch.</summary>
        public static float Pitch(float orbitPitch, float zoom01)
        {
            float k = Mathf.Clamp01((zoom01 - LiftStart) / (1f - LiftStart));
            float s = k * k * (3f - 2f * k);
            return Mathf.Lerp(orbitPitch + CloseTilt, HorizonPitch, s);
        }

        /// <summary>True when the frame's top edge is above the horizon (sky visible).</summary>
        public static bool ShowsSky(float pitch) => pitch < FieldOfView * 0.5f;

        /// <summary>
        /// Linear fog for a perspective view: clear through the focus, then haze out to the body's
        /// horizon so the map edge and the flat skirt melt into the sky's horizon colour.
        /// </summary>
        public static void Fog(float focusDistance, out float start, out float end)
        {
            float d = Mathf.Max(1f, focusDistance);
            start = d * 1.25f + 25f;
            end = start + Mathf.Max(220f, d * 2.2f);
        }
    }
}
