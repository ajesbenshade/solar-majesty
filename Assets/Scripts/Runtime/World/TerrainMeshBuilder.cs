using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Displaces GroundPlane from a <see cref="TerrainBake"/> (Terrain Data Baker-style height
    /// field). Campus footprints stay level so placement, docking, and NavMesh stay at y = 0.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        /// <summary>Vertices per side. 192 on a 384 m map is ~2 m spacing.</summary>
        public const int Resolution = 192;

        public static Mesh Build(float worldWidth, float worldHeight, int seed, CelestialBodyProfile body)
        {
            return Build(TerrainDataBake.Generate(worldWidth, worldHeight, seed, body));
        }

        public static Mesh Build(TerrainBake bake)
        {
            if (bake == null || bake.Heights == null)
                return Build(384f, 384f, 1, CelestialBodyCatalog.Mars());

            int n = Resolution;
            float worldWidth = bake.WorldWidth;
            float worldHeight = bake.WorldHeight;
            float amp = Mathf.Max(0.01f, bake.Amplitude);

            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n];

            for (int z = 0; z < n; z++)
            {
                float tz = z / (float)(n - 1);
                float wz = tz * worldHeight;
                for (int x = 0; x < n; x++)
                {
                    float tx = x / (float)(n - 1);
                    float wx = tx * worldWidth;
                    int i = z * n + x;
                    float h = bake.SampleHeight(wx, wz);
                    verts[i] = new Vector3(wx, h, wz);
                    uvs[i] = new Vector2(tx, tz);

                    float exposure = Mathf.InverseLerp(-amp * 0.6f, amp * 0.9f, h);
                    float slope = EstimateSlope(bake, wx, wz, worldWidth, worldHeight);
                    Color splat = TerrainDataBake.SplatFor(
                        bake.BodyId,
                        Mathf.InverseLerp(-amp, amp, h),
                        slope);
                    colors[i] = new Color(exposure, splat.g, splat.b, splat.a);
                }
            }

            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tris[t++] = i;
                    tris[t++] = i + n;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + n;
                    tris[t++] = i + n + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "SM_Terrain_Bake",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float EstimateSlope(TerrainBake bake, float wx, float wz, float worldW, float worldH)
        {
            const float e = 1.6f;
            float hL = bake.SampleHeight(Mathf.Max(0f, wx - e), wz);
            float hR = bake.SampleHeight(Mathf.Min(worldW, wx + e), wz);
            float hD = bake.SampleHeight(wx, Mathf.Max(0f, wz - e));
            float hU = bake.SampleHeight(wx, Mathf.Min(worldH, wz + e));
            Vector3 n = new Vector3(hL - hR, e * 2f, hD - hU).normalized;
            return 1f - Mathf.Clamp01(n.y);
        }
    }
}
