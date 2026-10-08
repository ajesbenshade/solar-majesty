using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Staggered title reveal and the one-shot gold glint. Shared by the Timeline clip baker
    /// and the code fallback. Missing children are simply skipped.
    /// </summary>
    public static class IntroTitleMotion
    {
        public const float RevealStep = 0.15f;
        public const float RevealDuration = 0.22f;
        /// <summary>Seconds after the sting / title-on before the glint starts. The three words have begun by then.</summary>
        public const float GlintLead = 0.45f;
        public const float GlintDuration = 0.6f;
        public const float GlintFrom = 0.85f;
        public const float GlintTo = -0.85f;

        public const int StepSolar = 0;
        public const int StepEmblem = 1;
        public const int StepMajesty = 2;

        public static float RevealWeight(float time, int step)
        {
            float start = IntroShot.TitleOn + step * RevealStep;
            if (time <= start || RevealDuration <= 0.0001f) return 0f;
            float u = Mathf.Clamp01((time - start) / RevealDuration);
            return u * u * (3f - 2f * u);
        }

        public static float GlintOffset(float time)
        {
            float start = IntroShot.TitleOn + GlintLead;
            if (time <= start) return GlintFrom;
            float u = Mathf.Clamp01((time - start) / GlintDuration);
            u = u * u * (3f - 2f * u);
            return Mathf.Lerp(GlintFrom, GlintTo, u);
        }

        /// <summary>
        /// <c>_BaseMap_ST</c> is (scale.x, scale.y, offset.x, offset.y). The glint sweeps offset.x (z).
        /// </summary>
        public static Vector4 WithGlint(Vector4 baseMapSt, float time)
        {
            baseMapSt.z = GlintOffset(time);
            return baseMapSt;
        }
    }
}
