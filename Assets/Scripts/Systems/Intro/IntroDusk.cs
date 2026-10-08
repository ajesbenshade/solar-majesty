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
    /// Dusk treatment for the launch intro. The sky already reads orange; this lowers and warms
    /// the key light and the ambient fill so the ground does too. Callers snapshot first and restore
    /// those snapshots — these methods never touch a live light.
    /// </summary>
    public static class IntroDusk
    {
        public const float MaxElevation = 12f;

        static readonly Color WarmSun = new Color(1f, 0.46f, 0.16f, 1f);
        static readonly Color WarmFill = new Color(1f, 0.58f, 0.32f, 1f);
        static readonly Color WarmSky = new Color(0.62f, 0.28f, 0.12f, 1f);
        static readonly Color WarmEquator = new Color(0.40f, 0.22f, 0.10f, 1f);
        static readonly Color WarmGround = new Color(0.10f, 0.05f, 0.03f, 1f);

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

        /// <summary>Earth's fog is a pale blue. Pull that haze into the dusk band under the sun.</summary>
        public static Color TintFog(Color current) =>
            Tint(current, new Color(0.50f, 0.22f, 0.08f, 1f), 0.9f, 0.65f);

        /// <summary>Procedural sky tint. A blue multiplier is the pale band under an orange sun disk.</summary>
        public static Color TintSkyTint(Color current)
        {
            var color = Color.Lerp(current, new Color(1f, 0.40f, 0.14f, 1f), 0.8f);
            color.a = 1f;
            return color;
        }

        /// <summary>Skybox ground color, the hemisphere under the horizon.</summary>
        public static Color TintSkyGround(Color current) =>
            Tint(current, new Color(0.16f, 0.07f, 0.03f, 1f), 0.8f, 0.6f);

        /// <summary>Thick Rayleigh air reads as a blue stripe. Ease it toward a thin dusk haze.</summary>
        public static float TintAtmosphere(float current) => Mathf.Lerp(current, 0.28f, 0.82f);

        static Color Tint(Color current, Color warm, float toward, float dim)
        {
            var color = Color.Lerp(current, warm, toward) * dim;
            color.a = 1f;
            return color;
        }
    }
}
