using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Runtime analogue of BOXOPHOBIC Terrain Data Baker outputs: height, world-space
    /// normals (TDB packing), splat weights, and a cavity mask. Campus pads stay flat.
    /// </summary>
    public sealed class TerrainBake
    {
        public int Resolution;
        public float WorldWidth;
        public float WorldHeight;
        public float Amplitude;
        public CelestialBodyId BodyId;
        public float[] Heights;
        public Texture2D HeightMap;
        public Texture2D NormalMap;
        public Texture2D SplatMap;
        public Texture2D MaskMap;

        public float SampleHeight(float wx, float wz)
        {
            if (Heights == null || Resolution < 2) return 0f;
            float u = Mathf.Clamp01(wx / Mathf.Max(0.01f, WorldWidth)) * (Resolution - 1);
            float v = Mathf.Clamp01(wz / Mathf.Max(0.01f, WorldHeight)) * (Resolution - 1);
            int x0 = Mathf.FloorToInt(u);
            int z0 = Mathf.FloorToInt(v);
            int x1 = Mathf.Min(Resolution - 1, x0 + 1);
            int z1 = Mathf.Min(Resolution - 1, z0 + 1);
            float tx = u - x0;
            float tz = v - z0;
            float a = Heights[z0 * Resolution + x0];
            float b = Heights[z0 * Resolution + x1];
            float c = Heights[z1 * Resolution + x0];
            float d = Heights[z1 * Resolution + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }
    }

    /// <summary>Lives on GroundPlane so dressing can bind baker maps after the mesh exists.</summary>
    public sealed class TerrainBakeHolder : MonoBehaviour
    {
        public TerrainBake Bake;
    }

    /// <summary>
    /// Bakes Terrain Data Baker-style maps from a seeded height field. Does not use Unity
    /// Terrain (no runtime TerrainData); the sampling matches TDB: interpolated height,
    /// world-space XZ normals packed as (nx*0.5+0.5, 1, nz*0.5+0.5), RGBA splat, cavity mask.
    /// </summary>
    public static class TerrainDataBake
    {
        public const int MapResolution = 256;
        public const float FlatRadius = 26f;
        public const float BlendRadius = 22f;

        public static TerrainBake Generate(float worldWidth, float worldHeight, int seed, CelestialBodyProfile body)
        {
            int n = MapResolution;
            var bake = new TerrainBake
            {
                Resolution = n,
                WorldWidth = worldWidth,
                WorldHeight = worldHeight,
                Amplitude = AmplitudeFor(body),
                BodyId = body != null ? body.Id : CelestialBodyId.Earth,
                Heights = new float[n * n]
            };

            CelestialBodyId id = body != null ? body.Id : CelestialBodyId.Earth;
            float ox = (seed % 977) * 0.37f;
            float oz = (seed % 691) * 0.53f;
            float amp = bake.Amplitude;

            float minH = float.MaxValue;
            float maxH = float.MinValue;
            for (int z = 0; z < n; z++)
            {
                float wz = (z / (float)(n - 1)) * worldHeight;
                for (int x = 0; x < n; x++)
                {
                    float wx = (x / (float)(n - 1)) * worldWidth;
                    float h = Height(wx + ox, wz + oz, seed, id) * amp;
                    h *= CampusFlatten(wx, wz);
                    bake.Heights[z * n + x] = h;
                    if (h < minH) minH = h;
                    if (h > maxH) maxH = h;
                }
            }

            float span = Mathf.Max(0.001f, maxH - minH);
            float stepX = worldWidth / (n - 1);
            float stepZ = worldHeight / (n - 1);

            var heightTex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
            {
                name = "SM_Bake_Height",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var normalTex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
            {
                name = "SM_Bake_Normal",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var splatTex = new Texture2D(n, n, TextureFormat.RGBA32, false, false)
            {
                name = "SM_Bake_Splat",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var maskTex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
            {
                name = "SM_Bake_Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var heightPx = new Color[n * n];
            var normalPx = new Color[n * n];
            var splatPx = new Color[n * n];
            var maskPx = new Color[n * n];

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float h = bake.Heights[i];
                    float hL = bake.Heights[z * n + Mathf.Max(0, x - 1)];
                    float hR = bake.Heights[z * n + Mathf.Min(n - 1, x + 1)];
                    float hD = bake.Heights[Mathf.Max(0, z - 1) * n + x];
                    float hU = bake.Heights[Mathf.Min(n - 1, z + 1) * n + x];

                    // World-space normal from central differences — same idea as TerrainData.GetInterpolatedNormal.
                    Vector3 nrm = new Vector3(hL - hR, stepX + stepZ, hD - hU).normalized;
                    // TDB world-space packing: R = nx*0.5+0.5, G = 1, B = nz*0.5+0.5.
                    normalPx[i] = new Color(nrm.x * 0.5f + 0.5f, 1f, nrm.z * 0.5f + 0.5f, 1f);

                    float h01 = (h - minH) / span;
                    heightPx[i] = new Color(h01, h01, h01, 1f);

                    float slope = 1f - Mathf.Clamp01(nrm.y);
                    Color splat = SplatFor(id, h01, slope);
                    splatPx[i] = splat;

                    float cavity = Mathf.Clamp01(((hL + hR + hD + hU) * 0.25f - h) / Mathf.Max(0.08f, amp * 0.35f));
                    float ao = 1f - cavity * 0.72f;
                    maskPx[i] = new Color(ao, 0.22f + splat.a * 0.35f, 0f, h01);
                }
            }

            heightTex.SetPixels(heightPx);
            normalTex.SetPixels(normalPx);
            splatTex.SetPixels(splatPx);
            maskTex.SetPixels(maskPx);
            heightTex.Apply(false, false);
            normalTex.Apply(false, false);
            splatTex.Apply(false, false);
            maskTex.Apply(false, false);

            bake.HeightMap = heightTex;
            bake.NormalMap = normalTex;
            bake.SplatMap = splatTex;
            bake.MaskMap = maskTex;
            return bake;
        }

        public static float AmplitudeFor(CelestialBodyProfile body)
        {
            if (body == null) return 1.6f;
            switch (body.Id)
            {
                case CelestialBodyId.Mars: return 3.2f;
                case CelestialBodyId.Luna: return 2.6f;
                case CelestialBodyId.Belt: return 3.4f;
                case CelestialBodyId.Europa: return 1.15f;
                default: return 1.85f;
            }
        }

        public static float CampusFlatten(float x, float z)
        {
            float keep = 1f;
            keep = Mathf.Min(keep, FalloffAt(x, z, ColonyLayout.CampusOrigin));
            keep = Mathf.Min(keep, FalloffAt(x, z, ColonyLayout.CampusBOrigin));
            return keep;
        }

        /// <summary>
        /// R dust, G rock, B vegetation / dark dune, A wet / crater floor. Weights sum to 1.
        /// </summary>
        public static Color SplatFor(CelestialBodyId id, float height01, float slope)
        {
            float rock = Mathf.SmoothStep(0.22f, 0.62f, slope);
            float low = 1f - Mathf.SmoothStep(0.28f, 0.48f, height01);
            float high = Mathf.SmoothStep(0.58f, 0.82f, height01);
            float grass;
            float wet;
            switch (id)
            {
                case CelestialBodyId.Earth:
                    grass = (1f - rock) * (1f - low) * 0.78f;
                    wet = low * (1f - rock) * 0.85f;
                    break;
                case CelestialBodyId.Europa:
                    grass = 0f;
                    wet = (1f - rock) * 0.65f;
                    break;
                default:
                    // B is TDB Snow on Mars — keep it a sparse pale-ridge hint, never a cap.
                    grass = high * (1f - rock) * 0.06f;
                    wet = low * (1f - rock) * 0.42f;
                    break;
            }

            float dust = Mathf.Max(0f, 1f - rock - grass - wet);
            float sum = dust + rock + grass + wet;
            if (sum < 1e-4f) return new Color(1f, 0f, 0f, 0f);
            return new Color(dust / sum, rock / sum, grass / sum, wet / sum);
        }

        public static float Height(float x, float z, int seed, CelestialBodyId id)
        {
            float warp = 14f;
            float xw = x + (Mathf.PerlinNoise(x * 0.0065f, z * 0.0065f) - 0.5f) * warp;
            float zw = z + (Mathf.PerlinNoise(x * 0.0065f + 19.1f, z * 0.0065f) - 0.5f) * warp;

            float dunes = Fbm(xw * 0.0085f, zw * 0.0085f, 5) - 0.5f;
            float ridged = 0.5f - Mathf.Abs(Mathf.PerlinNoise(xw * 0.026f, zw * 0.026f) - 0.5f) * 2f;
            ridged *= ridged;
            float grain = (Mathf.PerlinNoise(xw * 0.10f, zw * 0.10f) - 0.5f) * 0.22f;

            switch (id)
            {
                case CelestialBodyId.Earth:
                    return dunes * 1.55f + ridged * 0.28f + grain * 0.7f;
                case CelestialBodyId.Mars:
                    return dunes * 2.15f + ridged * 1.05f + grain + Craters(x, z, seed, 0.58f) * 1.55f;
                case CelestialBodyId.Luna:
                    return dunes * 1.2f + ridged * 0.7f + grain + Craters(x, z, seed, 0.84f) * 2.05f;
                case CelestialBodyId.Belt:
                    return dunes * 1.8f + ridged * 1.45f + grain * 1.4f;
                case CelestialBodyId.Europa:
                    return dunes * 0.55f + ridged * 0.18f + grain * 0.35f + IceCracks(xw, zw) * 0.42f;
                default:
                    return dunes * 2.0f + ridged * 0.9f + grain;
            }
        }

        private static float Craters(float x, float z, int seed, float density)
        {
            const float cell = 38f;
            int ix = Mathf.FloorToInt(x / cell);
            int iz = Mathf.FloorToInt(z / cell);
            float acc = 0f;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = ix + dx;
                int cz = iz + dz;
                float hx = Hash(cx, cz, seed);
                if (hx > density) continue;
                float px = (cx + 0.18f + Hash(cx, cz, seed + 11) * 0.64f) * cell;
                float pz = (cz + 0.18f + Hash(cx, cz, seed + 29) * 0.64f) * cell;
                float radius = Mathf.Lerp(4.5f, 13.5f, Hash(cx, cz, seed + 47));
                float dxw = x - px;
                float dzw = z - pz;
                float d = Mathf.Sqrt(dxw * dxw + dzw * dzw);
                if (d >= radius) continue;
                float t = d / radius;
                // Bowl + a raised rim so craters read at isometric distance.
                float bowl = (1f - t * t) * -1f;
                float rim = Mathf.Exp(-Mathf.Pow((t - 0.78f) * 7f, 2f)) * 0.28f;
                acc += (bowl + rim) * Mathf.Lerp(0.55f, 1f, Hash(cx, cz, seed + 71));
            }
            return acc;
        }

        private static float IceCracks(float x, float z)
        {
            float a = Mathf.Abs(Mathf.PerlinNoise(x * 0.04f, z * 0.011f) - 0.5f);
            float b = Mathf.Abs(Mathf.PerlinNoise(x * 0.012f, z * 0.038f) - 0.5f);
            float crack = 1f - Mathf.SmoothStep(0.0f, 0.08f, Mathf.Min(a, b));
            return -crack * 0.55f;
        }

        private static float Fbm(float x, float z, int octaves)
        {
            float v = 0f;
            float a = 0.5f;
            float f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                v += a * Mathf.PerlinNoise(x * f, z * f);
                f *= 2.03f;
                a *= 0.5f;
            }
            return v;
        }

        private static float Hash(int x, int z, int seed)
        {
            int n = x * 374761393 + z * 668265263 + seed * 1274126177;
            n = (n ^ (n >> 13)) * 1274126177;
            return (n & 0x7fffffff) / (float)int.MaxValue;
        }

        private static float FalloffAt(float x, float z, Vector3 origin)
        {
            float dx = x - origin.x;
            float dz = z - origin.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d <= FlatRadius) return 0f;
            if (d >= FlatRadius + BlendRadius) return 1f;
            return Mathf.SmoothStep(0f, 1f, (d - FlatRadius) / BlendRadius);
        }
    }
}
