using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Text size for the IMGUI HUD, separate from the HUD scale. HUD scale zooms the whole console
    /// (icons, minimap, chrome); text size only makes the words bigger, and the panels that hold
    /// words grow with them instead of clipping.
    ///
    /// At 100 % every helper returns its input unchanged, so layouts and tests written for the
    /// default size stay exact.
    /// </summary>
    public static class HudTextScale
    {
        public const float Min = 0.9f;
        public const float Max = 1.5f;
        public const float Step = 0.1f;
        public const float Default = 1f;

        /// <summary>
        /// Big display type (titles, the treasury number) grows by at most this many pixels: the
        /// setting exists for the small text, and a 44 px wordmark at 150 % would only push things off screen.
        /// </summary>
        public const float MaxFontGrowthPx = 6f;

        /// <summary>Share of the text growth that panel widths take (words get wider; panels widen by half as much and wrap or fit the rest).</summary>
        public const float WidthShare = 0.5f;

        private static float _factor = Default;

        /// <summary>The applied text size (1 = 100 %).</summary>
        public static float Factor => _factor;

        /// <summary>Bumps whenever <see cref="Factor"/> changes, so cached GUIStyles know to rebuild.</summary>
        public static int Version { get; private set; }

        public static float RoundToStep(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Default;
            float snapped = Mathf.Round(value / Step) * Step;
            snapped = Mathf.Round(snapped * 100f) / 100f;
            return Mathf.Clamp(snapped, Min, Max);
        }

        /// <summary>Applies a text size (snapped to 10 % steps between 90 % and 150 %).</summary>
        public static void Set(float value)
        {
            float next = RoundToStep(value);
            if (Mathf.Approximately(next, _factor)) return;
            _factor = next;
            Version++;
        }

        /// <summary>One step up or down; returns the new size.</summary>
        public static float Stepped(float current, int direction) =>
            RoundToStep(RoundToStep(current) + Step * Mathf.Sign(direction));

        /// <summary>Font size for a style designed at <paramref name="basePx"/> at 100 %.</summary>
        public static int Font(int basePx) => Font(basePx, _factor);

        public static int Font(int basePx, float factor)
        {
            if (basePx <= 0) return basePx;
            float grow = basePx * (factor - 1f);
            grow = Mathf.Clamp(grow, -MaxFontGrowthPx, MaxFontGrowthPx);
            return Mathf.Max(1, Mathf.RoundToInt(basePx + grow));
        }

        /// <summary>Height of a text line or row designed at <paramref name="px"/>: grows with the text.</summary>
        public static float Line(float px) => Line(px, _factor);

        public static float Line(float px, float factor) => factor == 1f ? px : px * factor;

        /// <summary>Extra pixels a block of text rows designed at <paramref name="px"/> needs (0 at 100 %).</summary>
        public static float Grow(float px) => Line(px) - px;

        /// <summary>Width of a panel designed at <paramref name="px"/>: grows by <see cref="WidthShare"/> of the text growth.</summary>
        public static float Width(float px) => Width(px, _factor);

        public static float Width(float px, float factor) =>
            factor == 1f ? px : px * (1f + (factor - 1f) * WidthShare);

        /// <summary>"120 %" style label for the settings row.</summary>
        public static string Percent(float factor) => $"{Mathf.RoundToInt(RoundToStep(factor) * 100f)}%";
    }
}
