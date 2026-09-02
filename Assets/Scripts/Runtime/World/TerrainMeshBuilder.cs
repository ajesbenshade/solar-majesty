using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Replaces the flat Unity plane with a displaced surface: broad dunes, ridge lines, and a
    /// grain pass. The visual target has rock relief and crater rims; a flat plane with half-buried
    /// spheres for boulders is the single largest reason the in-engine stills read as greybox.
    ///
    /// Campus footprints stay perfectly level. Building placement, docking, and NavMesh all assume
    /// y = 0 under the colony, so relief is faded out around each campus origin.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        /// <summary>Vertices per side. 160 gives ~2.4 m spacing on a 384 m map for ~25k tris.</summary>
        private const int Resolution = 160;

        /// <summary>Radius around a campus origin held dead flat, before the blend ring.</summary>
        private const float FlatRadius = 26f;

        /// <summary>Distance over which relief fades back in beyond the flat radius.</summary>
        private const float BlendRadius = 22f;

        public static Mesh Build(float worldWidth, float worldHeight, int seed, CelestialBodyProfile body)
        {
            int n = Resolution;
            float amp = AmplitudeFor(body);

            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n];

            // Offsetting by seed keeps each conquest of a body visually distinct.
            float ox = (seed % 977) * 0.37f;
            float oz = (seed % 691) * 0.53f;

            for (int z = 0; z < n; z++)
            {
                float tz = z / (float)(n - 1);
                float wz = tz * worldHeight;

                for (int x = 0; x < n; x++)
                {
                    float tx = x / (float)(n - 1);
                    float wx = tx * worldWidth;
                    int i = z * n + x;

                    float h = Height(wx + ox, wz + oz) * amp;
                    h *= CampusFlatten(wx, wz);

                    verts[i] = new Vector3(wx, h, wz);
                    uvs[i] = new Vector2(tx, tz);

                    // Red channel carries height for the ground shader's rock/dust blend.
                    float exposure = Mathf.InverseLerp(-amp * 0.6f, amp * 0.9f, h);
                    colors[i] = new Color(exposure, 0f, 0f, 1f);
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
                name = $"SM_Terrain_{(body != null ? body.ShortCode : "GEN")}",
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

        private static float AmplitudeFor(CelestialBodyProfile body)
        {
            if (body == null) return 1.6f;
            switch (body.Id)
            {
                case CelestialBodyId.Mars: return 2.4f;
                case CelestialBodyId.Luna: return 2.0f;
                case CelestialBodyId.Belt: return 2.8f;
                case CelestialBodyId.Europa: return 1.2f;
                default: return 1.5f;   // Earth: gentle meadow relief
            }
        }

        /// <summary>Layered noise: broad dunes, a ridge band, and fine grain.</summary>
        private static float Height(float x, float z)
        {
            float dunes = Mathf.PerlinNoise(x * 0.010f, z * 0.010f) - 0.5f;

            // Absolute-valued noise makes creases that read as ridge and crater-rim lines.
            float ridged = 0.5f - Mathf.Abs(Mathf.PerlinNoise(x * 0.028f, z * 0.028f) - 0.5f) * 2f;
            ridged *= ridged;

            float grain = (Mathf.PerlinNoise(x * 0.11f, z * 0.11f) - 0.5f) * 0.25f;

            return dunes * 2.2f + ridged * 0.9f + grain;
        }

        /// <summary>0 inside a campus pad, 1 well outside it, smooth in between.</summary>
        private static float CampusFlatten(float x, float z)
        {
            float keep = 1f;
            keep = Mathf.Min(keep, FalloffAt(x, z, ColonyLayout.CampusOrigin));
            keep = Mathf.Min(keep, FalloffAt(x, z, ColonyLayout.CampusBOrigin));
            return keep;
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
