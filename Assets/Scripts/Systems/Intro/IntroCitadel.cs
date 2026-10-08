using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Dims the citadel's warm window glow while the title holds, so the gold letters
    /// read against the dark ground and sky. Callers snapshot emission and light intensity
    /// and put those snapshots back. These methods never write a material.
    /// </summary>
    public static class IntroCitadel
    {
        /// <summary>Authored camera lock. The dim eases in here and holds through the fade.</summary>
        public const float HoldStart = 3.25f;

        public const float RampSeconds = 0.45f;

        /// <summary>Fraction of the authored emission or intensity left at full dim.</summary>
        public const float Floor = 0.15f;

        public static float Weight(float time)
        {
            if (time <= HoldStart || RampSeconds <= 0.0001f) return 0f;
            float u = Mathf.Clamp01((time - HoldStart) / RampSeconds);
            return u * u * (3f - 2f * u);
        }

        public static float Keep(float time) => Mathf.Lerp(1f, Floor, Weight(time));

        /// <summary>Warm citadel glass. Cyan bars and red beacons stay at their authored glow.</summary>
        public static bool IsWindowEmission(Color emission)
        {
            if (emission.maxColorComponent < 0.35f) return false;
            return emission.r > emission.g
                   && emission.g > emission.b
                   && emission.r > 0.8f
                   && emission.g > 0.4f;
        }

        public static Color ScaleEmission(Color authored, float keep) =>
            new Color(authored.r * keep, authored.g * keep, authored.b * keep, authored.a);

        public static float ScaleIntensity(float authored, float keep) => authored * keep;
    }
}
