using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// UI scale for the IMGUI overseer chrome. 5% steps from 75% to 200%.
    /// Auto follows screen height against a 1080p reference. The applied scale
    /// steps down when the virtual layout would drop under 1024×640.
    /// </summary>
    public static class HudScaleMath
    {
        public const float Min = 0.75f;
        public const float Max = 2f;
        public const float Step = 0.05f;
        public const float ReferenceHeight = 1080f;
        public const float MinViewWidth = 1024f;
        public const float MinViewHeight = 640f;

        public static float RoundToStep(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            float snapped = Mathf.Round(value / Step) * Step;
            snapped = Mathf.Round(snapped * 100f) / 100f;
            return Mathf.Clamp(snapped, Min, Max);
        }

        /// <summary>
        /// A small screen held close (Steam Deck, other PC handhelds). Auto scale then never goes
        /// below 100 %: at 1280x800 the plain height rule gave 75 %, which shrank the 9 px HUD text
        /// to about 7 px on a 7" screen. Set by the runtime on the Deck or with <c>-handheld</c>.
        /// </summary>
        public static bool Handheld;

        /// <summary>1080p → 100%, 1440p → 135%, 2160p → 200%, 720p → 75% (100% on a handheld).</summary>
        public static float AutoForHeight(int screenHeight) => AutoForHeight(screenHeight, Handheld);

        public static float AutoForHeight(int screenHeight, bool handheld)
        {
            if (screenHeight <= 0) return 1f;
            float raw = Mathf.Clamp(screenHeight / ReferenceHeight, Min, Max);
            if (handheld) raw = Mathf.Max(raw, 1f);
            return RoundToStep(raw);
        }

        /// <summary>
        /// Caps scale so Screen/scale stays at least <see cref="MinViewWidth"/> by
        /// <see cref="MinViewHeight"/>. A window smaller than that floor may go under 75%, down to 50%.
        /// </summary>
        public static float ClampToFit(float scale, int screenWidth, int screenHeight)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
            scale = Mathf.Clamp(scale, Min, Max);
            if (screenWidth <= 0 || screenHeight <= 0) return scale;
            float cap = Mathf.Min(screenWidth / MinViewWidth, screenHeight / MinViewHeight);
            if (cap >= scale) return scale;
            if (cap < 0.5f) cap = 0.5f;
            return cap < Min ? cap : Mathf.Min(scale, cap);
        }

        public static float Effective(bool auto, float explicitScale, int screenWidth, int screenHeight)
        {
            float chosen = auto ? AutoForHeight(screenHeight) : RoundToStep(explicitScale);
            return ClampToFit(chosen, screenWidth, screenHeight);
        }
    }
}
