using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Sun light for one moment of the light cycle. Multipliers apply to the body's tuned values.</summary>
    public struct SunState
    {
        /// <summary>Light rotation (x = elevation, y = azimuth).</summary>
        public Vector3 Euler;
        public Color Color;
        /// <summary>Multiplier on <see cref="CelestialBodyProfile.SunIntensity"/>.</summary>
        public float Intensity;
        /// <summary>Multiplier on ambient / fill / fog brightness.</summary>
        public float Ambient;
        /// <summary>0 = day, 1 = deep night (blue hour / earthshine).</summary>
        public float Night;
        /// <summary>0..1 how low and warm the sun is (golden hour).</summary>
        public float Golden;
    }

    /// <summary>
    /// Visual light cycle: dawn → golden hour → high sun → golden hour → blue-hour night → dawn.
    /// Pure math. At elapsed 0 it returns each body's tuned look exactly (SunEuler, SunColor,
    /// full intensity), so stills and the opening frame are unchanged. The sun never drops below
    /// <see cref="MinElevation"/>: night dims and cools the light instead of removing it, so the
    /// colony stays readable.
    /// </summary>
    public static class SunPath
    {
        /// <summary>One full light cycle = 8 Sols of mission time (a Sol is 60 s).</summary>
        public const float CycleSeconds = 8f * MajestyEconomy.DaySeconds;
        /// <summary>Share of the cycle the sun is up.</summary>
        public const float DayShare = 0.72f;
        public const float MinElevation = 7f;
        /// <summary>Degrees of azimuth the sun sweeps across one day.</summary>
        public const float SweepDegrees = 140f;
        public const float NightIntensity = 0.30f;
        public const float NightAmbient = 0.50f;
        const float LowSunIntensity = 0.72f;
        const float LowSunAmbient = 0.85f;
        const float MaxPeak = 68f;

        public static SunState Evaluate(CelestialBodyProfile body, double elapsedSeconds, float cycleSeconds = CycleSeconds)
        {
            Vector3 baseEuler = body != null ? body.SunEuler : new Vector3(45f, -30f, 0f);
            Color baseColor = body != null ? body.SunColor : Color.white;
            float baseElev = Mathf.Max(MinElevation + 1f, baseEuler.x);
            float peak = Mathf.Clamp(baseElev + 14f, baseElev, Mathf.Max(baseElev, MaxPeak));
            float d0 = RisingDayFraction(baseElev, peak);

            double cycles = cycleSeconds > 0.01f ? elapsedSeconds / cycleSeconds : 0.0;
            float phase = Frac(d0 * DayShare + (float)(cycles - System.Math.Floor(cycles)));

            // Low-sun window ends at the tuned elevation, so the tuned look is never tinted.
            float lowTop = Mathf.Min(MinElevation + 14f, baseElev);
            Color golden = GoldenColor(body, baseColor);
            Color night = NightColor(body);

            var s = new SunState();
            if (phase < DayShare)
            {
                float d = phase / DayShare;
                float elev = MinElevation + (peak - MinElevation) * Mathf.Sin(Mathf.PI * d);
                float low = 1f - Smooth(MinElevation, lowTop, elev);
                s.Euler = new Vector3(elev, baseEuler.y + (d - d0) * SweepDegrees, baseEuler.z);
                s.Color = Color.Lerp(baseColor, golden, low);
                s.Intensity = Mathf.Lerp(1f, LowSunIntensity, low);
                s.Ambient = Mathf.Lerp(1f, LowSunAmbient, low);
                s.Golden = low;
                s.Night = 0f;
            }
            else
            {
                float n = (phase - DayShare) / (1f - DayShare);
                // Hump: 0 at dusk and dawn, 1 through the middle of the night.
                float nightF = Smooth(0f, 0.25f, n) * Smooth(0f, 0.25f, 1f - n);
                float endAz = baseEuler.y + (1f - d0) * SweepDegrees;
                float startAz = baseEuler.y - d0 * SweepDegrees;
                s.Euler = new Vector3(MinElevation, Mathf.Lerp(endAz, startAz, n), baseEuler.z);
                s.Color = Color.Lerp(golden, night, nightF);
                s.Intensity = Mathf.Lerp(LowSunIntensity, NightIntensity, nightF);
                s.Ambient = Mathf.Lerp(LowSunAmbient, NightAmbient, nightF);
                s.Golden = 1f - nightF;
                s.Night = nightF;
            }
            s.Color.a = 1f;
            return s;
        }

        /// <summary>Earth: amber. Mars: the famous blue sunset. Airless worlds: the sun stays white.</summary>
        public static Color GoldenColor(CelestialBodyProfile body, Color baseColor)
        {
            if (body == null) return baseColor;
            return body.Id switch
            {
                CelestialBodyId.Earth => new Color(1f, 0.64f, 0.36f),
                CelestialBodyId.Mars => new Color(0.80f, 0.86f, 1f),
                _ => baseColor
            };
        }

        /// <summary>Night light: moonlight on Earth, earthshine on Luna, dim dust-blue on Mars.</summary>
        public static Color NightColor(CelestialBodyProfile body)
        {
            if (body == null) return new Color(0.5f, 0.58f, 0.8f);
            return body.Id switch
            {
                CelestialBodyId.Earth => new Color(0.46f, 0.56f, 0.86f),
                CelestialBodyId.Luna => new Color(0.56f, 0.66f, 0.96f),
                CelestialBodyId.Mars => new Color(0.56f, 0.52f, 0.72f),
                CelestialBodyId.Europa => new Color(0.86f, 0.74f, 0.58f), // Jupiter-shine
                _ => new Color(0.5f, 0.58f, 0.8f)
            };
        }

        /// <summary>Day fraction (0..0.5) on the rising side where the sun sits at the tuned elevation.</summary>
        static float RisingDayFraction(float baseElev, float peak)
        {
            float span = peak - MinElevation;
            if (span <= 0.001f) return 0.5f;
            float k = Mathf.Clamp01((baseElev - MinElevation) / span);
            return Mathf.Asin(k) / Mathf.PI;
        }

        static float Smooth(float a, float b, float x)
        {
            if (b <= a) return x >= b ? 1f : 0f;
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
