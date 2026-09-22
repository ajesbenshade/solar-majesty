using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Displaces GroundPlane from a <see cref="TerrainBake"/> (Terrain Data Baker-style height
    /// field). Campus footprints stay level so placement, docking, and NavMesh stay at y = 0.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        /// <summary>Vertices per side. 385 on a 384 m map is 1 m — crater rims and ridges stay crisp.</summary>
        public const int Resolution = 385;

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

            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n];
            for (int z = 0; z < n; z++)
            {
                float tz = z / (float)(n - 1);
                for (int x = 0; x < n; x++)
                {
                    float tx = x / (float)(n - 1);
                    int i = z * n + x;
                    float wx = tx * worldWidth, wz = tz * worldHeight;
                    verts[i] = new Vector3(wx, bake.SampleHeight(wx, wz), wz);
                    uvs[i] = new Vector2(tx, tz);
                    colors[i] = VertexColor(bake, wx, wz, verts[i].y);
                }
            }

            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    // Alternate the diagonal so long ridges do not pick up a sawtooth along one axis.
                    if (((x + z) & 1) == 0)
                    {
                        tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                        tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
                    }
                    else
                    {
                        tris[t++] = i; tris[t++] = i + n; tris[t++] = i + n + 1;
                        tris[t++] = i; tris[t++] = i + n + 1; tris[t++] = i + 1;
                    }
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
            mesh.MarkDynamic();
            return mesh;
        }

        /// <summary>
        /// Re-read heights inside a world-space rectangle after <see cref="TerrainDataBake.LevelFootprint"/>
        /// graded a pad. Only the touched vertices move; normals are recomputed for the mesh.
        /// </summary>
        public static void RefreshRegion(Mesh mesh, TerrainBake bake, Vector3 center, Vector2 halfExtents)
        {
            if (mesh == null || bake == null) return;
            int n = Resolution;
            if (mesh.vertexCount != n * n) return;
            var verts = mesh.vertices;
            var colors = mesh.colors;
            float sx = bake.WorldWidth / (n - 1), sz = bake.WorldHeight / (n - 1);
            int i0 = Mathf.Max(0, Mathf.FloorToInt((center.x - halfExtents.x) / sx) - 1);
            int i1 = Mathf.Min(n - 1, Mathf.CeilToInt((center.x + halfExtents.x) / sx) + 1);
            int j0 = Mathf.Max(0, Mathf.FloorToInt((center.z - halfExtents.y) / sz) - 1);
            int j1 = Mathf.Min(n - 1, Mathf.CeilToInt((center.z + halfExtents.y) / sz) + 1);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                int k = j * n + i;
                Vector3 v = verts[k];
                v.y = bake.SampleHeight(v.x, v.z);
                verts[k] = v;
                if (colors != null && colors.Length == verts.Length)
                    colors[k] = VertexColor(bake, v.x, v.z, v.y);
            }
            mesh.vertices = verts;
            if (colors != null && colors.Length == verts.Length) mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>R exposure (height), GBA base splat for shaders that read vertex colour.</summary>
        private static Color VertexColor(TerrainBake bake, float wx, float wz, float h)
        {
            float amp = Mathf.Max(0.01f, bake.Amplitude);
            float exposure = Mathf.InverseLerp(-amp * 0.6f, amp * 0.9f, h);
            float slope = EstimateSlope(bake, wx, wz);
            Color splat = TerrainDataBake.SplatFor(bake.BodyId, Mathf.InverseLerp(-amp, amp, h), slope);
            return new Color(exposure, splat.g, splat.b, splat.a);
        }

        private static float EstimateSlope(TerrainBake bake, float wx, float wz)
        {
            const float e = 1f;
            float hL = bake.SampleHeight(Mathf.Max(0f, wx - e), wz);
            float hR = bake.SampleHeight(Mathf.Min(bake.WorldWidth, wx + e), wz);
            float hD = bake.SampleHeight(wx, Mathf.Max(0f, wz - e));
            float hU = bake.SampleHeight(wx, Mathf.Min(bake.WorldHeight, wz + e));
            Vector3 n = new Vector3(hL - hR, e * 2f, hD - hU).normalized;
            return 1f - Mathf.Clamp01(n.y);
        }
    }
}
