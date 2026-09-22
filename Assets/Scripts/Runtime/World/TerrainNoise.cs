using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Seeded, allocation-free noise for the planet generators.
    ///
    /// Replaces Mathf.PerlinNoise, which has no seed (every map drew the same field, offset),
    /// repeats every 256 units, and gives no derivatives. Gradient noise with analytic derivatives
    /// drives the "eroded" fBm below: slopes suppress finer octaves the way real weathering
    /// smooths valleys and keeps crests sharp.
    ///
    /// Mirrored by Blender/scripts/terrain_proto/tproto.py (keep the constants in sync).
    /// </summary>
    public static class TerrainNoise
    {
        private const float Rc = 0.8f;   // octave rotation (~36.87°) breaks grid alignment
        private const float Rs = 0.6f;

        private static readonly float[] Gx = BuildDirs(true);
        private static readonly float[] Gz = BuildDirs(false);

        private static float[] BuildDirs(bool x)
        {
            var d = new float[16];
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f + 0.19634954f;
                d[i] = x ? Mathf.Cos(a) : Mathf.Sin(a);
            }
            return d;
        }

        public static uint Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = ((uint)x * 0x8DA6B343u) ^ ((uint)z * 0xD8163841u) ^ ((uint)seed * 0xCB1AB31Fu);
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Uniform [0, 1).</summary>
        public static float Hash01(int x, int z, int seed) => (Hash(x, z, seed) & 0xFFFFFFu) / 16777216f;

        /// <summary>2D gradient noise, value in ~[-1, 1], with d/dx and d/dz.</summary>
        public static float Gradient(float x, float z, int seed, out float dx, out float dz)
        {
            float fxF = Mathf.Floor(x);
            float fzF = Mathf.Floor(z);
            int ix = (int)fxF;
            int iz = (int)fzF;
            float fx = x - fxF;
            float fz = z - fzF;
            float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float v = fz * fz * fz * (fz * (fz * 6f - 15f) + 10f);
            float du = 30f * fx * fx * (fx - 1f) * (fx - 1f);
            float dv = 30f * fz * fz * (fz - 1f) * (fz - 1f);

            uint ha = Hash(ix, iz, seed) & 15u;
            uint hb = Hash(ix + 1, iz, seed) & 15u;
            uint hc = Hash(ix, iz + 1, seed) & 15u;
            uint hd = Hash(ix + 1, iz + 1, seed) & 15u;
            float gax = Gx[ha], gaz = Gz[ha];
            float gbx = Gx[hb], gbz = Gz[hb];
            float gcx = Gx[hc], gcz = Gz[hc];
            float gdx = Gx[hd], gdz = Gz[hd];

            float va = gax * fx + gaz * fz;
            float vb = gbx * (fx - 1f) + gbz * fz;
            float vc = gcx * fx + gcz * (fz - 1f);
            float vd = gdx * (fx - 1f) + gdz * (fz - 1f);
            float k = va - vb - vc + vd;
            float val = va + u * (vb - va) + v * (vc - va) + u * v * k;
            dx = gax + u * (gbx - gax) + v * (gcx - gax) + u * v * (gax - gbx - gcx + gdx) + du * (vb - va + v * k);
            dz = gaz + u * (gbz - gaz) + v * (gcz - gaz) + u * v * (gaz - gbz - gcz + gdz) + dv * (vc - va + u * k);
            const float s = 1.4142f;
            dx *= s;
            dz *= s;
            return val * s;
        }

        public static float Gradient(float x, float z, int seed) => Gradient(x, z, seed, out _, out _);

        /// <summary>Plain fBm, ~[-0.65, 0.65] (std ≈ 0.18 at 4 octaves).</summary>
        public static float Fbm(float x, float z, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float f = 0f;
            float a = 0.5f;
            for (int i = 0; i < octaves; i++)
            {
                f += a * Gradient(x, z, seed + i * 131);
                float nx = (Rc * x - Rs * z) * lacunarity;
                z = (Rs * x + Rc * z) * lacunarity;
                x = nx;
                a *= gain;
            }
            return f;
        }

        /// <summary>
        /// Derivative-damped fBm (std ≈ 0.135 at 6 octaves). Octaves on steep ground are attenuated,
        /// so valleys and plains smooth out while crests keep detail — reads as weathered terrain.
        /// </summary>
        public static float ErodedFbm(float x, float z, int seed, int octaves, float erosion = 1f,
            float lacunarity = 2f, float gain = 0.5f)
        {
            float f = 0f, ddx = 0f, ddz = 0f;
            float a = 0.5f;
            float c = 1f, s = 0f, scale = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Gradient(x, z, seed + i * 131, out float nx, out float nz);
                float bx = (c * nx + s * nz) * scale;
                float bz = (-s * nx + c * nz) * scale;
                ddx += bx * a;
                ddz += bz * a;
                f += a * n / (1f + erosion * (ddx * ddx + ddz * ddz));
                float px = (Rc * x - Rs * z) * lacunarity;
                z = (Rs * x + Rc * z) * lacunarity;
                x = px;
                float c2 = c * Rc - s * Rs;
                s = s * Rc + c * Rs;
                c = c2;
                scale *= lacunarity;
                a *= gain;
            }
            return f;
        }

        /// <summary>Ridged multifractal, [0, 1] (mean ≈ 0.52).</summary>
        public static float Ridged(float x, float z, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float f = 0f;
            float w = 1f;
            float a = 0.5f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Gradient(x, z, seed + i * 131);
                float r = 1f - Mathf.Abs(n);
                r = r * r * w;
                w = Mathf.Clamp01(r * 2f);
                f += a * r;
                float nx = (Rc * x - Rs * z) * lacunarity;
                z = (Rs * x + Rc * z) * lacunarity;
                x = nx;
                a *= gain;
            }
            return f;
        }

        /// <summary>HLSL smoothstep (edges may be reversed).</summary>
        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
