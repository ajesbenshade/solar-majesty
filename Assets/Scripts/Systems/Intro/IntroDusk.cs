using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>Directional light the intro may tint, then put back exactly.</summary>
    public struct IntroLightSample
    {
        public Quaternion Rotation;
        public Color Color;
        public float Intensity;
    }

    /// <summary>Ambient values the intro may tint, then put back exactly.</summary>
    public struct IntroAmbientSample
    {
        public AmbientMode Mode;
        public Color Sky;
        public Color Equator;
        public Color Ground;
        public Color Flat;
        public float Intensity;
    }

    /// <summary>
    /// Earth-at-dusk treatment for the launch intro: a low gold sun, a blue-hour sky overhead and
    /// green meadow bounce from below, so the campus reads as Earth rather than a red Luna/Mars plain.
    /// No cyan here; cyan is reserved for the Core. Callers snapshot first and restore
    /// those snapshots — these methods never touch a live light.
    /// </summary>
    public static class IntroDusk
    {
        public const float MaxElevation = 8f;

        // Golden-hour key, not red: keeps grass green under it.
        static readonly Color WarmSun = new Color(1f, 0.62f, 0.30f, 1f);
        // Fill is skylight: soft blue-violet (blue-hour), never cyan.
        static readonly Color WarmFill = new Color(0.52f, 0.54f, 0.82f, 1f);
        static readonly Color WarmSky = new Color(0.34f, 0.36f, 0.58f, 1f);
        // Warm peach at the horizon band where the sun sits.
        static readonly Color WarmEquator = new Color(0.58f, 0.40f, 0.26f, 1f);
        // Meadow bounce.
        static readonly Color WarmGround = new Color(0.12f, 0.20f, 0.08f, 1f);
        static readonly Color DuskFog = new Color(0.40f, 0.44f, 0.30f, 1f);
        static readonly Color DuskSkyTint = new Color(0.82f, 0.62f, 0.58f, 1f);
        static readonly Color DuskSkyGround = new Color(0.16f, 0.24f, 0.10f, 1f);

        public static float Elevation(Quaternion rotation)
        {
            float x = rotation.eulerAngles.x;
            if (x > 180f) x -= 360f;
            return x;
        }

        public static IntroLightSample TintSun(IntroLightSample current, CelestialBodyProfile body)
        {
            var state = SunPath.Evaluate(body, SunPath.DuskElapsed(body));
            float pitch = state.Euler.x;
            if (pitch > MaxElevation) pitch = MaxElevation;
            var color = Color.Lerp(state.Color, WarmSun, 0.75f);
            color.a = 1f;
            return new IntroLightSample
            {
                Rotation = Quaternion.Euler(pitch, state.Euler.y, state.Euler.z),
                Color = color,
                Intensity = current.Intensity * 0.4f
            };
        }

        public static IntroLightSample TintFill(IntroLightSample current)
        {
            var color = Color.Lerp(current.Color, WarmFill, 0.55f);
            color.a = 1f;
            return new IntroLightSample
            {
                Rotation = current.Rotation,
                Color = color,
                Intensity = current.Intensity * 0.4f
            };
        }

        public static IntroAmbientSample TintAmbient(IntroAmbientSample current)
        {
            return new IntroAmbientSample
            {
                Mode = current.Mode,
                Sky = Tint(current.Sky, WarmSky, 0.82f, 0.62f),
                Equator = Tint(current.Equator, WarmEquator, 0.7f, 0.55f),
                Ground = Tint(current.Ground, WarmGround, 0.55f, 0.5f),
                Flat = Tint(current.Flat, WarmSky, 0.75f, 0.5f),
                Intensity = current.Intensity * 0.62f
            };
        }

        /// <summary>Earth's fog is a pale blue. Pull it to a dim green-gold meadow haze (green dominant, no cyan).</summary>
        public static Color TintFog(Color current) =>
            Tint(current, DuskFog, 0.85f, 0.75f);

        /// <summary>Procedural sky tint. Only partly warmed, so the zenith keeps Earth's blue-hour blue
        /// over a rose horizon instead of going all orange.</summary>
        public static Color TintSkyTint(Color current)
        {
            var color = Color.Lerp(current, DuskSkyTint, 0.55f);
            color.a = 1f;
            return color;
        }

        /// <summary>Skybox ground color, the hemisphere under the horizon: dark meadow green.</summary>
        public static Color TintSkyGround(Color current) =>
            Tint(current, DuskSkyGround, 0.8f, 0.7f);

        /// <summary>Thin the Rayleigh band a little so it does not read as a cyan stripe, but keep enough
        /// air for an Earth sky (Luna/Mars read comes from stripping it).</summary>
        public static float TintAtmosphere(float current) => Mathf.Lerp(current, 0.6f, 0.6f);

        static Color Tint(Color current, Color warm, float toward, float dim)
        {
            var color = Color.Lerp(current, warm, toward) * dim;
            color.a = 1f;
            return color;
        }
    }
}
