using UnityEngine;

namespace SolarMajesty
{
    public enum ColorBlindMode
    {
        Off = 0,
        Deuteranopia = 1,
        Protanopia = 2,
        Tritanopia = 3
    }

    /// <summary>
    /// Colour remapping for colourblind players.
    ///
    /// The HUD leans on red-for-danger and green-for-good, which is the exact pairing the most
    /// common form of colour blindness cannot separate. Rather than recolour every call site, UI
    /// colours are passed through here, which shifts the confusable hues apart while keeping the
    /// carbon-and-gold identity intact.
    ///
    /// Colour is never the only signal: severity also carries an icon and a text prefix.
    /// </summary>
    public static class Accessibility
    {
        public static ColorBlindMode Mode
        {
            get => (ColorBlindMode)Mathf.Clamp(DemoSettings.ColorBlindMode, 0, 3);
            set
            {
                DemoSettings.ColorBlindMode = (int)value;
                DemoSettings.SaveSettings();
            }
        }

        public static bool ReduceMotion => DemoSettings.ReduceMotion;

        public static string ModeLabel(ColorBlindMode mode)
        {
            switch (mode)
            {
                case ColorBlindMode.Deuteranopia: return "Deuteranopia";
                case ColorBlindMode.Protanopia: return "Protanopia";
                case ColorBlindMode.Tritanopia: return "Tritanopia";
                default: return "Off";
            }
        }

        /// <summary>
        /// Remap a UI colour for the active mode. Reds move toward orange and greens toward cyan
        /// or blue, which keeps them distinguishable under red-green deficiency.
        /// </summary>
        public static Color Adapt(Color c)
        {
            switch (Mode)
            {
                case ColorBlindMode.Deuteranopia:
                case ColorBlindMode.Protanopia:
                    return ShiftRedGreen(c);
                case ColorBlindMode.Tritanopia:
                    return ShiftBlueYellow(c);
                default:
                    return c;
            }
        }

        private static Color ShiftRedGreen(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < 0.12f) return c;   // Neutrals are already unambiguous.

            // Green (~0.33) toward cyan (~0.50); red (~0.0) toward amber (~0.08).
            if (h > 0.20f && h < 0.45f)
                h = Mathf.Lerp(h, 0.50f, 0.75f);
            else if (h < 0.06f || h > 0.94f)
                h = 0.075f;

            // Push saturation and value apart too, so the pair still separates in greyscale.
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.08f), Mathf.Clamp01(v)) is var rgb
                ? new Color(rgb.r, rgb.g, rgb.b, c.a)
                : c;
        }

        private static Color ShiftBlueYellow(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < 0.12f) return c;

            // Blue (~0.62) toward teal; yellow (~0.15) toward warm red.
            if (h > 0.52f && h < 0.75f)
                h = Mathf.Lerp(h, 0.47f, 0.7f);
            else if (h > 0.10f && h < 0.20f)
                h = 0.03f;

            Color rgb = Color.HSVToRGB(h, s, v);
            return new Color(rgb.r, rgb.g, rgb.b, c.a);
        }

        /// <summary>
        /// Text prefix carrying the same information as the colour, so severity survives a
        /// greyscale screenshot, a colourblind player, and a screen reader alike.
        /// </summary>
        public static string SeverityPrefix(AlertSeverity severity)
        {
            switch (severity)
            {
                case AlertSeverity.Critical: return "[!!] ";
                case AlertSeverity.Warning: return "[!] ";
                case AlertSeverity.Good: return "[+] ";
                default: return "";
            }
        }
    }
}
