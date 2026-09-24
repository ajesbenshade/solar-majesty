using UnityEngine;

namespace SolarMajesty
{
    /// <summary>What hangs in a world's sky.</summary>
    public enum SkyPlanetKind
    {
        None,
        /// <summary>Earth seen from Luna — blue oceans, cloud swirls, a blue limb.</summary>
        Earth,
        /// <summary>Jupiter seen from Europa — banded, with the Great Red Spot.</summary>
        Jupiter,
        /// <summary>Phobos seen from Mars — a small cratered potato.</summary>
        Phobos
    }

    /// <summary>A body in the sky, in degrees. Azimuth follows light/camera yaw (0 = +Z, 90 = +X).</summary>
    public struct SkyPlanet
    {
        public SkyPlanetKind Kind;
        public float AzimuthDeg;
        public float ElevationDeg;
        public float RadiusDeg;
    }

    /// <summary>
    /// Pure colour functions for a world's sky panorama (latitude/longitude, the layout Unity's
    /// Skybox/Panoramic expects). No textures here; <c>SkyPanorama</c> rasterises it. The horizon
    /// colour is the body's fog colour so the fogged ground melts into the sky with no seam.
    /// Planet sizes are exaggerated on purpose: the diorama camera shows only a narrow band of sky
    /// above the horizon, and an Earthrise should read.
    /// </summary>
    public static class SkyPainter
    {
        /// <summary>Default camera yaw — skies put their showpiece where the player looks.</summary>
        public const float ViewAzimuth = 45f;

        public static bool IsAirless(CelestialBodyProfile body) =>
            body == null || (body.Id != CelestialBodyId.Earth && body.Id != CelestialBodyId.Mars);

        /// <summary>Number of stars to scatter (0 on Earth by day-sky, a few on dusty Mars).</summary>
        public static int StarCount(CelestialBodyProfile body)
        {
            if (body == null) return 0;
            return body.Id switch
            {
                CelestialBodyId.Earth => 0,
                CelestialBodyId.Mars => 0, // bright dusty day sky: stars would read as noise
                _ => 5200
            };
        }

        public static SkyPlanet PlanetFor(CelestialBodyProfile body)
        {
            if (body == null) return default;
            return body.Id switch
            {
                CelestialBodyId.Luna => new SkyPlanet { Kind = SkyPlanetKind.Earth, AzimuthDeg = ViewAzimuth + 14f, ElevationDeg = 4.5f, RadiusDeg = 5.2f },
                CelestialBodyId.Europa => new SkyPlanet { Kind = SkyPlanetKind.Jupiter, AzimuthDeg = ViewAzimuth - 10f, ElevationDeg = 7f, RadiusDeg = 11f },
                CelestialBodyId.Mars => new SkyPlanet { Kind = SkyPlanetKind.Phobos, AzimuthDeg = ViewAzimuth + 22f, ElevationDeg = 7.5f, RadiusDeg = 1.1f },
                _ => default
            };
        }

        /// <summary>Unit direction for panorama UV (matches Skybox/Panoramic's ToRadialCoords).</summary>
        public static Vector3 DirFromUV(float u, float v)
        {
            float lon = (0.5f - u) * 2f * Mathf.PI;
            float lat = (1f - v) * Mathf.PI;
            float s = Mathf.Sin(lat);
            return new Vector3(s * Mathf.Cos(lon), Mathf.Cos(lat), s * Mathf.Sin(lon));
        }

        public static Vector2 UVFromDir(Vector3 d)
        {
            d = Normalize(d);
            float lon = Mathf.Atan2(d.z, d.x);
            float lat = Mathf.Acos(Mathf.Clamp(d.y, -1f, 1f));
            return new Vector2(0.5f - lon / (2f * Mathf.PI), 1f - lat / Mathf.PI);
        }

        /// <summary>Direction for an azimuth/elevation in degrees (azimuth 0 = +Z, 90 = +X).</summary>
        public static Vector3 DirFromAzEl(float azDeg, float elDeg)
        {
            float az = azDeg * Mathf.Deg2Rad, el = elDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        }

        /// <summary>Unit vector from the ground toward the sun for a light rotation.</summary>
        public static Vector3 ToSun(Vector3 sunEuler) => -(Quaternion.Euler(sunEuler) * Vector3.forward);

        /// <summary>Sky gradient by height (y = sin elevation). Below the horizon: fog colour.</summary>
        public static Color Gradient(CelestialBodyProfile body, float y)
        {
            Color horizon = body != null ? body.FogColor : Color.grey;
            Color zenith = body != null ? body.SkyTop : Color.black;
            if (y <= 0f) return Opaque(horizon);
            // Air scatters a wide band; an airless sky goes black a few degrees up.
            float t = IsAirless(body) ? Mathf.Clamp01(y / 0.06f) : Mathf.Pow(Mathf.Clamp01(y), 0.45f);
            if (IsAirless(body)) t = t * t * (3f - 2f * t);
            return Opaque(Color.Lerp(horizon, zenith, t));
        }

        /// <summary>
        /// Colour of the sky planet at disc coordinates (px, py in -1..1, x right / y up as seen), or
        /// alpha 0 outside the disc. Shaded by the sun, so its phase agrees with the ground's light.
        /// </summary>
        public static Color ShadePlanet(SkyPlanet p, Vector3 toSun, float px, float py)
        {
            float r2 = px * px + py * py;
            float limb = 1.08f; // atmosphere glow ring just outside the disc
            if (p.Kind == SkyPlanetKind.None || r2 > limb * limb) return new Color(0, 0, 0, 0);

            PlanetBasis(p, out Vector3 center, out Vector3 right, out Vector3 up);
            Vector3 f = -center;                                           // planet → viewer

            if (r2 > 1f)
            {
                if (p.Kind != SkyPlanetKind.Earth && p.Kind != SkyPlanetKind.Jupiter) return new Color(0, 0, 0, 0);
                float rim = 1f - (Mathf.Sqrt(r2) - 1f) / (limb - 1f);
                Vector3 edgeN = Normalize(right * px + up * py);
                float lit = Mathf.Clamp01(Vector3.Dot(edgeN, toSun) + 0.3f);
                Color glow = p.Kind == SkyPlanetKind.Earth ? new Color(0.45f, 0.70f, 1f) : new Color(0.95f, 0.85f, 0.70f);
                return new Color(glow.r, glow.g, glow.b, rim * rim * lit * 0.8f);
            }

            float pz = Mathf.Sqrt(1f - r2);
            Vector3 n = Normalize(right * px + up * py + f * pz);
            float diffuse = Mathf.Max(0f, Vector3.Dot(n, toSun));
            float terminator = Mathf.Clamp01(diffuse * 1.4f);
            Color albedo = Surface(p.Kind, px, py, pz);
            float ambient = p.Kind == SkyPlanetKind.Phobos ? 0.02f : 0.035f;
            Color c = albedo * (ambient + terminator);
            if (p.Kind == SkyPlanetKind.Earth)
            {
                // Blue limb haze on the lit side.
                float fres = Mathf.Pow(1f - pz, 3f);
                c += new Color(0.30f, 0.52f, 0.95f) * fres * terminator * 0.8f;
            }
            c.a = 1f;
            return c;
        }

        static Color Surface(SkyPlanetKind kind, float px, float py, float pz)
        {
            switch (kind)
            {
                case SkyPlanetKind.Earth:
                {
                    float land = Fbm(px * 2.6f + 3.1f, py * 2.6f - 1.7f, 11);
                    float cloud = Fbm(px * 4.2f - 7.3f, py * 5.0f + 2.2f, 29);
                    Color ocean = new Color(0.06f, 0.20f, 0.48f);
                    Color ground = Color.Lerp(new Color(0.22f, 0.36f, 0.16f), new Color(0.62f, 0.52f, 0.34f), Fbm(px * 6f, py * 6f, 5));
                    // Soft coastlines and ragged ice caps: hard thresholds alias into blocks.
                    float coast = Mathf.Clamp01((land - 0.53f) / 0.06f);
                    Color baseC = Color.Lerp(ocean, ground, coast * coast * (3f - 2f * coast));
                    float capEdge = 0.80f + (Fbm(px * 5f + 9f, py * 3f, 23) - 0.5f) * 0.16f;
                    float cap = Mathf.Clamp01((Mathf.Abs(py) - capEdge) / 0.05f);
                    baseC = Color.Lerp(baseC, new Color(0.92f, 0.95f, 1f), cap);
                    float c = Mathf.Clamp01((cloud - 0.50f) / 0.22f);
                    return Color.Lerp(baseC, new Color(0.96f, 0.97f, 1f), c * 0.9f);
                }
                case SkyPlanetKind.Jupiter:
                {
                    float lat = py + (Fbm(px * 3f, py * 9f, 17) - 0.5f) * 0.06f;
                    float band = Mathf.Sin(lat * 19f) * 0.5f + 0.5f;
                    Color cream = new Color(0.93f, 0.86f, 0.72f);
                    Color rust = new Color(0.70f, 0.46f, 0.30f);
                    Color c = Color.Lerp(cream, rust, band * (0.55f + 0.45f * Fbm(px * 8f, lat * 30f, 3)));
                    float sx = (px - 0.28f) / 0.16f, sy = (py + 0.36f) / 0.08f;
                    if (sx * sx + sy * sy < 1f) c = Color.Lerp(c, new Color(0.78f, 0.34f, 0.22f), 0.85f); // Great Red Spot
                    return c;
                }
                case SkyPlanetKind.Phobos:
                {
                    float crater = Fbm(px * 7f + 1f, py * 7f + 4f, 41);
                    return Color.Lerp(new Color(0.46f, 0.41f, 0.37f), new Color(0.70f, 0.64f, 0.58f), crater);
                }
                default:
                    return Color.black;
            }
        }

        /// <summary>
        /// Direction to the planet's centre and the viewer's screen right/up at it
        /// (Unity: right = up × forward). Disc coords: px = dot(dir, right) / sin(radius).
        /// </summary>
        public static void PlanetBasis(SkyPlanet p, out Vector3 center, out Vector3 right, out Vector3 up)
        {
            center = DirFromAzEl(p.AzimuthDeg, p.ElevationDeg);
            right = Normalize(Vector3.Cross(Vector3.up, center));
            up = Vector3.Cross(center, right);
        }

        /// <summary>Faint Milky Way glow along a tilted great circle (airless skies).</summary>
        public static float MilkyWay(Vector3 dir)
        {
            Vector3 pole = Normalize(new Vector3(0.35f, 0.55f, -0.76f));
            float d = Vector3.Dot(Normalize(dir), pole);
            float band = Mathf.Exp(-d * d / 0.018f);
            return band * (0.55f + 0.45f * Fbm(dir.x * 6f + dir.y * 2f, dir.z * 6f - dir.y * 3f, 57));
        }

        /// <summary>Deterministic value-noise fBm in 0..1.</summary>
        public static float Fbm(float x, float y, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * ValueNoise(x, y, seed + o * 101);
                norm += amp;
                x *= 2.03f;
                y *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static Vector3 Normalize(Vector3 v)
        {
            float m = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            return m > 1e-6f ? new Vector3(v.x / m, v.y / m, v.z / m) : Vector3.zero;
        }

        static Color Opaque(Color c)
        {
            c.a = 1f;
            return c;
        }
    }
}
