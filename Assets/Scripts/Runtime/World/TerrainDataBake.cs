using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>A body of standing or running water carved into the height field (Earth).</summary>
    [Serializable]
    public struct TerrainWater
    {
        public Vector3 Center;      // y = water surface level
        public float Radius;        // lake radius, or river half-width
        public float Length;        // river segment length (0 for lakes)
        public float YawDeg;        // river segment heading
        public bool IsLake;
    }

    /// <summary>
    /// Runtime analogue of BOXOPHOBIC Terrain Data Baker outputs: height, world-space normals
    /// (TDB packing), splat weights, and a mask map. Campus pads stay flat.
    ///
    /// MaskMap channels: R ambient occlusion (horizon-based), G crater freshness / ejecta /
    /// ray brightness, B regional albedo feature (dark sand, mare, salt lineae, soil), A height 0–1.
    /// </summary>
    [Serializable]
    public sealed class TerrainBake
    {
        public int Resolution;
        public float WorldWidth;
        public float WorldHeight;
        public float Amplitude;
        public float MinHeight;
        public float MaxHeight;
        public CelestialBodyId BodyId;
        public float[] Heights;
        public float[] Rock;
        public float[] Fresh;
        public Texture2D HeightMap;
        public Texture2D NormalMap;
        public Texture2D SplatMap;
        public Texture2D MaskMap;
        public readonly List<TerrainWater> Water = new List<TerrainWater>(64);

        public float SampleHeight(float wx, float wz) => Sample(Heights, wx, wz);

        /// <summary>0 on dust / sand, 1 on bare rock (slopes, cliffs, fresh crater rims).</summary>
        public float SampleRockiness(float wx, float wz)
        {
            float r = Rock != null ? Sample(Rock, wx, wz) : 0f;
            float f = Fresh != null ? Sample(Fresh, wx, wz) : 0f;
            return Mathf.Clamp01(r + f * 0.6f);
        }

        public float Sample(float[] field, float wx, float wz)
        {
            if (field == null || Resolution < 2) return 0f;
            float u = Mathf.Clamp01(wx / Mathf.Max(0.01f, WorldWidth)) * (Resolution - 1);
            float v = Mathf.Clamp01(wz / Mathf.Max(0.01f, WorldHeight)) * (Resolution - 1);
            int x0 = Mathf.FloorToInt(u);
            int z0 = Mathf.FloorToInt(v);
            int x1 = Mathf.Min(Resolution - 1, x0 + 1);
            int z1 = Mathf.Min(Resolution - 1, z0 + 1);
            float tx = u - x0;
            float tz = v - z0;
            float a = field[z0 * Resolution + x0];
            float b = field[z0 * Resolution + x1];
            float c = field[z1 * Resolution + x0];
            float d = field[z1 * Resolution + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }
    }

    /// <summary>Lives on GroundPlane so dressing can bind baker maps after the mesh exists.</summary>
    public sealed class TerrainBakeHolder : MonoBehaviour
    {
        public TerrainBake Bake;
    }

    /// <summary>
    /// Seeded natural-planet terrain (v2, 2026-09-22). Each body gets its own geology instead
    /// of one Perlin field scaled per body:
    ///
    /// * Mars — eroded rolling plains, transverse aeolian ridges in the lows, layered buttes,
    ///   degraded power-law craters, a sinuous dry channel.
    /// * Luna — craters at every size with fresh bright ejecta and rays, darker smooth mare.
    /// * Earth — weathered hills with lakes in real basins and rivers in carved valleys.
    /// * Europa — bright ice cut by reddish double-ridge lineae, jumbled chaos terrain.
    /// * Belt — rough ridged rubble and dense cratering.
    ///
    /// Relief is gentler where the colony grows (<see cref="SettleEnvelope"/>) and exactly flat on
    /// the yard pads, so placement, docking and NavMesh keep y = 0. Mirrored by
    /// Blender/scripts/terrain_proto (numpy + Blender previews).
    /// </summary>
    public static class TerrainDataBake
    {
        public const int MapResolution = 512;

        /// <summary>Commons + HAB + airlock disk. Relief may begin just outside this.</summary>
        public const float ClusterFlatRadius = 12f;
        public const float PadFlatRadius = 8f;
        public const float CampusBFlatRadius = 10f;
        public const float YardBlendRadius = 5f;

        /// <summary>Legacy aliases — cluster pad, not the old 48 m still-covering disk.</summary>
        public const float FlatRadius = ClusterFlatRadius;
        public const float BlendRadius = YardBlendRadius;

        /// <summary>Mesa lip in the far third of the ortho-10 still (iso +x+z).</summary>
        public static readonly Vector3 MesaLipLocal = new Vector3(16f, 0f, 18f);

        /// <summary>Canyon trench center; misses yard pads.</summary>
        public static readonly Vector3 CanyonLocal = new Vector3(-8f, 0f, 18f);

        /// <summary>Concept left-rear bowl, ~18 m from Commons.</summary>
        public static readonly Vector3 SignatureCraterLocal = new Vector3(-14f, 0f, 12f);

        /// <summary>Earth vista pond basin (PlanetaryMapDressing.EnsureEarthVista).</summary>
        public static readonly Vector3 VistaPondLocal = new Vector3(14.5f, 0f, 6.2f);

        /// <summary>The bake of the current world (set by GameLoop). Null in isolated tests.</summary>
        public static TerrainBake Current { get; set; }

        /// <summary>Ground height of the live terrain at world XZ (0 when there is no bake).</summary>
        public static float GroundHeight(float x, float z) => Current != null ? Current.SampleHeight(x, z) : 0f;

        public static float GroundHeight(Vector3 world) => GroundHeight(world.x, world.z);

        // ------------------------------------------------------------------------------------

        public static TerrainBake Generate(float worldWidth, float worldHeight, int seed, CelestialBodyProfile body)
        {
            int n = MapResolution;
            CelestialBodyId id = body != null ? body.Id : CelestialBodyId.Earth;
            var g = new Grid(n, worldWidth, worldHeight);
            var bake = new TerrainBake
            {
                Resolution = n,
                WorldWidth = worldWidth,
                WorldHeight = worldHeight,
                BodyId = id,
                Heights = g.H,
                Fresh = g.Fresh
            };

            switch (id)
            {
                case CelestialBodyId.Mars: GenMars(g, seed, body); break;
                case CelestialBodyId.Luna: GenLuna(g, seed, body); break;
                case CelestialBodyId.Belt: GenBelt(g, seed); break;
                case CelestialBodyId.Europa: GenEuropa(g, seed); break;
                default: GenEarth(g, seed, body, bake.Water); break;
            }

            // Yard pads stay exactly level. Any non-finite sample (a float edge case in a generator)
            // is zeroed so one bad value can never poison the mesh bounds, normals, or NavMesh.
            int bad = 0;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                if (float.IsNaN(g.H[i]) || float.IsInfinity(g.H[i])) { g.H[i] = 0f; bad++; }
                if (float.IsNaN(g.Fresh[i]) || float.IsInfinity(g.Fresh[i])) g.Fresh[i] = 0f;
                g.H[i] *= CampusFlatten(g.X(x), g.Z(z));
            }
            if (bad > 0)
                Debug.LogWarning($"[TerrainDataBake] {id} seed={seed}: zeroed {bad} non-finite height samples.");

            BakeMaps(bake, g, id);
            return bake;
        }

        public static float AmplitudeFor(CelestialBodyProfile body)
        {
            if (body == null) return 4f;
            switch (body.Id)
            {
                case CelestialBodyId.Mars: return 6f;
                case CelestialBodyId.Luna: return 6f;
                case CelestialBodyId.Belt: return 8f;
                case CelestialBodyId.Europa: return 2.5f;
                default: return 5f;
            }
        }

        /// <summary>
        /// 0 under yard pads (docks / NavMesh / suit crossings), 1 in open land.
        /// Empty dirt between yards may roll; the ortho-10 far third is outside the disks.
        /// </summary>
        public static float CampusFlatten(float x, float z)
        {
            float keep = 1f;
            keep = Mathf.Min(keep, FalloffAt(x, z, ColonyLayout.CampusOrigin, ClusterFlatRadius, YardBlendRadius));
            keep = Mathf.Min(keep, FalloffAt(x, z, LaunchSite.PadWorld, PadFlatRadius, YardBlendRadius));
            keep = Mathf.Min(keep, FalloffAt(x, z, ColonyLayout.CampusBOrigin, CampusBFlatRadius, YardBlendRadius));
            return keep;
        }

        /// <summary>Relief envelope: gentle rolling ground where the colony grows, full relief beyond.</summary>
        public static float SettleEnvelope(float x, float z)
        {
            float d = FlatDist(x, z, ColonyLayout.CampusOrigin);
            return 0.42f + 0.58f * TerrainNoise.Smooth(22f, 70f, d);
        }

        /// <summary>
        /// Base splat by height/slope. R dust, G rock, B vegetation / dark sand / lineae, A wet /
        /// mare / crater floor. Weights sum to 1. Generate() refines this with feature masks.
        /// </summary>
        public static Color SplatFor(CelestialBodyId id, float height01, float slope)
        {
            float rock = SmoothRange(0.22f, 0.62f, slope);
            float low = 1f - SmoothRange(0.28f, 0.48f, height01);
            float high = SmoothRange(0.58f, 0.82f, height01);
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
                case CelestialBodyId.Mars:
                    rock = SmoothRange(0.16f, 0.46f, slope);
                    grass = 0f;
                    wet = low * (1f - rock) * 0.18f;
                    break;
                case CelestialBodyId.Luna:
                    rock = SmoothRange(0.14f, 0.42f, slope);
                    grass = high * (1f - rock) * 0.03f;
                    wet = low * (1f - rock) * 0.62f;
                    break;
                default:
                    grass = high * (1f - rock) * 0.06f;
                    wet = low * (1f - rock) * 0.42f;
                    break;
            }

            float dust = Mathf.Max(0f, 1f - rock - grass - wet);
            float sum = dust + rock + grass + wet;
            if (sum < 1e-4f) return new Color(1f, 0f, 0f, 0f);
            return new Color(dust / sum, rock / sum, grass / sum, wet / sum);
        }

        /// <summary>
        /// Procedural base relief at a point (noise + landmarks, no craters, no pad flattening).
        /// Kept for diagnostics; <see cref="Generate"/> is the source of truth.
        /// </summary>
        public static float Height(float x, float z, int seed, CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Mars:
                    return MarsBase(x, z, seed, out _, out _, out _) * SettleEnvelope(x, z)
                           + MarsLandmarks(x, z, seed);
                case CelestialBodyId.Luna:
                    return LunaBase(x, z, seed, out _) * SettleEnvelope(x, z) + LandmarkCrater(x, z, 8f, 3.4f, 0.75f);
                case CelestialBodyId.Belt:
                    return BeltBase(x, z, seed) * SettleEnvelope(x, z);
                case CelestialBodyId.Europa:
                    return 3.2f * TerrainNoise.Fbm(x / 140f, z / 140f, seed, 5);
                default:
                    return EarthBase(x, z, seed) * SettleEnvelope(x, z);
            }
        }

        // ---------------------------------------------------------------- grid ---------------

        private sealed class Grid
        {
            public readonly int N;
            public readonly float W, Hgt, StepX, StepZ;
            public readonly float[] H, Fresh, Feature, Wet, Rock;

            public Grid(int n, float w, float h)
            {
                N = n;
                W = w;
                Hgt = h;
                StepX = w / (n - 1);
                StepZ = h / (n - 1);
                H = new float[n * n];
                Fresh = new float[n * n];
                Feature = new float[n * n];
                Wet = new float[n * n];
                Rock = new float[n * n];
            }

            public float X(int i) => i * StepX;
            public float Z(int j) => j * StepZ;

            public float At(float wx, float wz)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(wx / StepX), 0, N - 1);
                int j = Mathf.Clamp(Mathf.RoundToInt(wz / StepZ), 0, N - 1);
                return H[j * N + i];
            }

            public void Bounds(float cx, float cz, float r, out int i0, out int i1, out int j0, out int j1)
            {
                i0 = Mathf.Max(0, Mathf.FloorToInt((cx - r) / StepX));
                i1 = Mathf.Min(N - 1, Mathf.CeilToInt((cx + r) / StepX));
                j0 = Mathf.Max(0, Mathf.FloorToInt((cz - r) / StepZ));
                j1 = Mathf.Min(N - 1, Mathf.CeilToInt((cz + r) / StepZ));
            }
        }

        // ---------------------------------------------------------------- Mars ---------------

        private static float MarsBase(float x, float z, int seed, out float dunes, out float field, out float mesa)
        {
            Warp(x, z, seed, 90f, 22f, out float xw, out float zw);
            float plains = 14f * TerrainNoise.ErodedFbm(xw / 150f, zw / 150f, seed, 6, 1.3f);
            float medium = 1.8f * TerrainNoise.Fbm(xw / 28f, zw / 28f, seed + 11, 4);
            float small = 0.28f * TerrainNoise.Fbm(x / 5.5f, z / 5.5f, seed + 13, 3);
            float h = plains + medium + small;

            // Transverse aeolian ridges collect in the lows: long gentle stoss, short steep lee.
            float low = TerrainNoise.Smooth(1.2f, -2.0f, plains);
            float fieldNoise = TerrainNoise.Smooth(-0.05f, 0.3f, TerrainNoise.Fbm(x / 120f, z / 120f, seed + 5, 3));
            field = Mathf.Clamp01(fieldNoise * (0.35f + 0.65f * low));
            float th = SeedAngle(seed, 3);
            float s = x * Mathf.Cos(th) + z * Mathf.Sin(th) + 10f * TerrainNoise.Fbm(x / 45f, z / 45f, seed + 7, 3);
            float t = s / 7.5f;
            t -= Mathf.Floor(t);
            float crest = 0.55f + 0.45f * TerrainNoise.Fbm(x / 18f, z / 18f, seed + 8, 2);
            dunes = AsymDune(t) * crest * field;
            h += 0.62f * dunes;

            // Far layered buttes.
            float dc = FlatDist(x, z, ColonyLayout.CampusOrigin);
            float blob = TerrainNoise.Fbm(x / 70f, z / 70f, seed + 9, 4);
            float far = TerrainNoise.Smooth(60f, 95f, dc);
            float m1 = TerrainNoise.Smooth(0.14f, 0.20f, blob);
            float m2 = TerrainNoise.Smooth(0.24f, 0.31f, blob);
            mesa = (0.6f * m1 + 0.4f * m2) * far;
            h += 6.5f * mesa;
            return h;
        }

        private static float MarsLandmarks(float x, float z, int seed)
        {
            return LandmarkMesa(x, z, seed) + LandmarkChannel(x, z) + LandmarkCrater(x, z, 7.4f, 3.6f, 0.8f);
        }

        private static void GenMars(Grid g, int seed, CelestialBodyProfile body)
        {
            int n = g.N;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int k = j * n + i;
                g.H[k] = MarsBase(x, z, seed, out float dunes, out float field, out float mesa);
                g.Feature[k] = Mathf.Clamp01(dunes * 1.2f + 0.35f * field);
                g.Rock[k] = mesa;
            }

            int count = Mathf.Max(20, (body != null ? body.CraterCount : 56) * 5);
            var craters = CraterPopulation(seed, g.W, count, 1.2f, 28f, 2f, 0.85f, 201, 22f);
            StampCraters(g, craters, 0.14f, 0.035f, false);

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int k = j * n + i;
                g.H[k] = g.H[k] * SettleEnvelope(x, z) + MarsLandmarks(x, z, seed);
                // Regional dark basaltic sand patches (albedo only).
                float regional = 0.45f * TerrainNoise.Smooth(0.02f, 0.28f,
                    TerrainNoise.Fbm(x / 150f, z / 150f, seed + 311, 3));
                g.Feature[k] = Mathf.Max(g.Feature[k], regional);
            }
        }

        private static float AsymDune(float t) =>
            t < 0.72f ? TerrainNoise.Smooth(0f, 0.72f, t) : 1f - TerrainNoise.Smooth(0.72f, 1f, t);

        private static float LandmarkMesa(float x, float z, int seed)
        {
            const float height = 5.8f;
            const float radius = 8.2f;
            Vector3 c = ColonyLayout.CampusOrigin + MesaLipLocal;
            float dx = x - c.x, dz = z - c.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d > radius * 2.2f) return 0f;
            float ang = Mathf.Atan2(dz, dx);
            float edge = radius * (1f + 0.16f * TerrainNoise.Fbm(Mathf.Cos(ang) * 2.2f + 7f,
                Mathf.Sin(ang) * 2.2f - 3f, seed + 41, 3));
            float t = d / edge;
            // Caprock plateau with layered steps on the cliff, plus a talus apron.
            float cap = TerrainNoise.Smooth(1.02f, 0.86f, t);
            float apron = TerrainNoise.Smooth(1.55f, 0.95f, t);
            float steps = Mathf.Floor(cap * 4f) / 4f;
            cap = cap * 0.75f + steps * 0.25f;
            return height * (0.78f * cap + 0.22f * apron);
        }

        private static float LandmarkChannel(float x, float z)
        {
            const float depth = 3.3f, width = 6f, length = 30f;
            Vector3 c = ColonyLayout.CampusOrigin + CanyonLocal;
            float u = x - c.x;
            if (Mathf.Abs(u) > length * 0.5f + 6f) return 0f;
            float wig = 3.2f * Mathf.Sin(u / 7f) + 1.4f * Mathf.Sin(u / 3.1f);
            float v = (z - c.z) - wig;
            float along = TerrainNoise.Smooth(length * 0.5f + 6f, length * 0.5f - 4f, Mathf.Abs(u));
            float q = 1f - (v / (width * 0.5f)) * (v / (width * 0.5f));
            float prof = q > 0f ? Mathf.Pow(q, 0.8f) : 0f;
            float bank = Mathf.Exp(-Sq((Mathf.Abs(v) - width * 0.62f) / 1.2f)) * 0.25f;
            return (-depth * prof + bank * depth * 0.2f) * along;
        }

        private static float LandmarkCrater(float x, float z, float radius, float depth, float rim)
        {
            Vector3 c = ColonyLayout.CampusOrigin + SignatureCraterLocal;
            float rn = FlatDist(x, z, c) / radius;
            if (rn < 1f) return -depth + (depth + rim) * rn * rn;
            return rim * Mathf.Pow(rn, -3f);
        }

        // ---------------------------------------------------------------- Luna ---------------

        private static float LunaBase(float x, float z, int seed, out float mare)
        {
            Warp(x, z, seed, 110f, 18f, out float xw, out float zw);
            float plains = 12f * TerrainNoise.ErodedFbm(xw / 170f, zw / 170f, seed, 6, 1f);
            float medium = 1.4f * TerrainNoise.Fbm(xw / 30f, zw / 30f, seed + 11, 4);
            float small = 0.22f * TerrainNoise.Fbm(x / 5f, z / 5f, seed + 13, 3);
            mare = TerrainNoise.Smooth(0.02f, 0.22f, TerrainNoise.Fbm(x / 230f, z / 230f, seed + 3, 3));
            return (plains + medium) * (1f - 0.7f * mare) - 1.5f * mare + small;
        }

        private static void GenLuna(Grid g, int seed, CelestialBodyProfile body)
        {
            int n = g.N;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = j * n + i;
                g.H[k] = LunaBase(g.X(i), g.Z(j), seed, out float mare);
                g.Feature[k] = mare;
            }
            int count = Mathf.Max(200, (body != null ? body.CraterCount : 96) * 14);
            var craters = CraterPopulation(seed, g.W, count, 0.9f, 42f, 2.05f, 0.5f, 202, 20f);
            StampCraters(g, craters, 0.2f, 0.045f, true);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int k = j * n + i;
                g.H[k] = g.H[k] * SettleEnvelope(x, z) + LandmarkCrater(x, z, 8f, 3.4f, 0.75f);
            }
        }

        // ---------------------------------------------------------------- Earth --------------

        private static float EarthBase(float x, float z, int seed)
        {
            Warp(x, z, seed, 120f, 26f, out float xw, out float zw);
            float hills = 20f * TerrainNoise.ErodedFbm(xw / 210f, zw / 210f, seed, 6, 0.9f);
            float medium = 2.2f * TerrainNoise.Fbm(xw / 40f, zw / 40f, seed + 11, 4);
            float small = 0.15f * TerrainNoise.Fbm(x / 6f, z / 6f, seed + 13, 3);
            float dc = FlatDist(x, z, ColonyLayout.CampusOrigin);
            float rocks = 7f * Mathf.Max(0f, TerrainNoise.Ridged(x / 55f, z / 55f, seed + 17, 4) - 0.62f)
                          * TerrainNoise.Smooth(70f, 110f, dc);
            return hills + medium + small + rocks;
        }

        private static void GenEarth(Grid g, int seed, CelestialBodyProfile body, List<TerrainWater> water)
        {
            int n = g.N;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int k = j * n + i;
                g.H[k] = EarthBase(x, z, seed) * SettleEnvelope(x, z);
                // Bare-soil patches in the meadow (albedo only).
                g.Feature[k] = TerrainNoise.Smooth(0.2f, 0.55f, TerrainNoise.Fbm(x / 26f, z / 26f, seed + 21, 4));
            }

            int lakes = body != null ? body.LakeCount : 8;
            int rivers = body != null ? body.RiverCount : 5;
            CarveLakes(g, seed, lakes, water);
            CarveRivers(g, seed, rivers, water);

            // Vista pond basin next to the drop (EnsureEarthVista seats the pond water in it).
            Vector3 pond = ColonyLayout.CampusOrigin + VistaPondLocal;
            g.Bounds(pond.x, pond.z, 6.5f, out int a0, out int a1, out int b0, out int b1);
            for (int j = b0; j <= b1; j++)
            for (int i = a0; i <= a1; i++)
            {
                int k = j * n + i;
                float rn = FlatDist(g.X(i), g.Z(j), pond) / 3.8f;
                if (rn < 1f) g.H[k] += -0.8f * (1f - rn * rn);
                g.Wet[k] = Mathf.Max(g.Wet[k], TerrainNoise.Smooth(1.5f, 1f, rn));
            }
        }

        private static void CarveLakes(Grid g, int seed, int lakes, List<TerrainWater> water)
        {
            if (lakes <= 0) return;
            int n = g.N;
            var cand = new List<Vector4>(160);
            for (int i = 0; i < 160; i++)
            {
                float x = TerrainNoise.Hash01(i, 1, seed) * (g.W - 40f) + 20f;
                float z = TerrainNoise.Hash01(i, 2, seed) * (g.Hgt - 40f) + 20f;
                if (FlatDist(x, z, ColonyLayout.CampusOrigin) < 32f) continue;
                cand.Add(new Vector4(g.At(x, z), x, z, i));
            }
            cand.Sort((a, b) => a.x.CompareTo(b.x));

            int placed = 0;
            foreach (var c in cand)
            {
                if (placed >= lakes) break;
                float x = c.y, z = c.z;
                int idx = (int)c.w;
                bool clash = false;
                foreach (var w in water)
                    if (FlatDist(x, z, w.Center) < 45f) { clash = true; break; }
                if (clash) continue;

                float hi = c.x;
                float r = 7f + TerrainNoise.Hash01(idx, 3, seed) * 9f;
                float level = hi - 0.3f;
                g.Bounds(x, z, r * 2.2f, out int i0, out int i1, out int j0, out int j1);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * n + i;
                    float dx = g.X(i) - x, dz = g.Z(j) - z;
                    float ang = Mathf.Atan2(dz, dx);
                    float lob = 1f + 0.25f * Mathf.Sin(ang * 2f + idx) + 0.15f * Mathf.Sin(ang * 3f + 2f * idx);
                    float rn = Mathf.Sqrt(dx * dx + dz * dz) / (r * lob);
                    float q = Mathf.Max(0f, 1f - rn * rn);
                    float basin = hi - 1.5f * Mathf.Pow(q, 0.7f);
                    float blend = TerrainNoise.Smooth(1.6f, 1f, rn);
                    float h = g.H[k];
                    h = Mathf.Min(h, h * (1f - blend) + basin * blend);
                    // A low natural lip so the flat water plane never spills past the shore.
                    if (rn > 1.0f && rn < 2.1f) h = Mathf.Max(h, level + 0.08f + 0.1f * (rn - 1f));
                    g.H[k] = h;
                    g.Wet[k] = Mathf.Max(g.Wet[k], TerrainNoise.Smooth(1.45f, 0.95f, rn));
                }
                water.Add(new TerrainWater
                {
                    Center = new Vector3(x, level, z),
                    Radius = r * 1.45f,
                    IsLake = true
                });
                placed++;
            }
        }

        private static void CarveRivers(Grid g, int seed, int rivers, List<TerrainWater> water)
        {
            int n = g.N;
            const int segs = 70;
            var pts = new Vector2[segs + 1];
            var bed = new float[segs + 1];
            Vector3 campus = ColonyLayout.CampusOrigin;
            float half = g.W * 0.5f;
            for (int k = 0; k < rivers; k++)
            {
                float a0 = TerrainNoise.Hash01(k, 7, seed) * Mathf.PI * 2f;
                float sx = Mathf.Clamp(half + Mathf.Cos(a0) * g.W * 0.75f, 2f, g.W - 2f);
                float sz = Mathf.Clamp(half + Mathf.Sin(a0) * g.Hgt * 0.75f, 2f, g.Hgt - 2f);
                float ex, ez;
                TerrainWater? target = null;
                if (k % 2 == 0)
                {
                    float best = float.MaxValue;
                    foreach (var w in water)
                    {
                        if (!w.IsLake) continue;
                        float d = FlatDist(sx, sz, w.Center);
                        if (d < best) { best = d; target = w; }
                    }
                }
                if (target.HasValue)
                {
                    ex = target.Value.Center.x;
                    ez = target.Value.Center.z;
                }
                else
                {
                    float a1 = a0 + Mathf.PI * 0.55f + TerrainNoise.Hash01(k, 8, seed) * 0.9f;
                    ex = Mathf.Clamp(half + Mathf.Cos(a1) * g.W * 0.75f, 2f, g.W - 2f);
                    ez = Mathf.Clamp(half + Mathf.Sin(a1) * g.Hgt * 0.75f, 2f, g.Hgt - 2f);
                }

                float len = Mathf.Max(1f, Mathf.Sqrt((ex - sx) * (ex - sx) + (ez - sz) * (ez - sz)));
                float nx = -(ez - sz) / len, nz = (ex - sx) / len;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs;
                    float px = sx + (ex - sx) * t;
                    float pz = sz + (ez - sz) * t;
                    float wander = (22f * TerrainNoise.Fbm(t * 2.6f + k * 1.7f, k * 7.1f, seed + 61, 3)
                                    + 6f * Mathf.Sin(t * 17f + k)) * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)));
                    px += nx * wander;
                    pz += nz * wander;
                    float dcx = px - campus.x, dcz = pz - campus.z;
                    float dd = Mathf.Sqrt(dcx * dcx + dcz * dcz);
                    if (dd < 38f)
                    {
                        px = campus.x + dcx / Mathf.Max(dd, 1e-3f) * 38f;
                        pz = campus.z + dcz / Mathf.Max(dd, 1e-3f) * 38f;
                    }
                    pts[s] = new Vector2(px, pz);
                }

                // Bed descends downstream (running minimum), so water never climbs a hill.
                float cur = float.MaxValue;
                for (int s = 0; s <= segs; s++)
                {
                    cur = Mathf.Min(cur, g.At(pts[s].x, pts[s].y));
                    bed[s] = cur;
                }

                for (int s = 0; s < segs; s++)
                {
                    Vector2 a = pts[s], b = pts[s + 1];
                    float width = 2.2f + 1.0f * Mathf.Sin(s * 0.37f + k) + 1.4f * s / segs;
                    float bedH = Mathf.Min(bed[s], bed[s + 1]) - 0.9f;
                    float level = bedH + 0.55f;
                    float reach = width * 3.5f;
                    Vector2 mid = (a + b) * 0.5f;
                    Vector2 v = b - a;
                    float ll = Mathf.Max(v.sqrMagnitude, 1e-6f);
                    g.Bounds(mid.x, mid.y, reach + v.magnitude * 0.5f, out int i0, out int i1, out int j0, out int j1);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int idx = j * n + i;
                        float x = g.X(i), z = g.Z(j);
                        float tt = Mathf.Clamp01(((x - a.x) * v.x + (z - a.y) * v.y) / ll);
                        float qx = a.x + v.x * tt - x, qz = a.y + v.y * tt - z;
                        float d = Mathf.Sqrt(qx * qx + qz * qz);
                        if (d > reach) continue;
                        float prof = bedH + (d / width) * (d / width) * 0.9f;
                        float blend = TerrainNoise.Smooth(width * 3.5f, width, d);
                        float h = g.H[idx];
                        h = Mathf.Min(h, h * (1f - blend) + Mathf.Min(h, prof) * blend);
                        if (d > width * 1.05f && d < width * 2.6f) h = Mathf.Max(h, level + 0.08f);
                        g.H[idx] = h;
                        g.Wet[idx] = Mathf.Max(g.Wet[idx], TerrainNoise.Smooth(width * 2f, width * 0.7f, d));
                    }
                    water.Add(new TerrainWater
                    {
                        Center = new Vector3(mid.x, level, mid.y),
                        Radius = width * 0.95f,
                        Length = v.magnitude * 1.15f,
                        YawDeg = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg,
                        IsLake = false
                    });
                }
            }
        }

        // ---------------------------------------------------------------- Europa -------------

        private static void GenEuropa(Grid g, int seed)
        {
            int n = g.N;
            const int lineae = 11;
            var ang = new float[lineae];
            var ox = new float[lineae];
            var oz = new float[lineae];
            var bend = new float[lineae];
            var w = new float[lineae];
            var hgt = new float[lineae];
            for (int k = 0; k < lineae; k++)
            {
                ang[k] = TerrainNoise.Hash01(k, 31, seed) * Mathf.PI;
                ox[k] = TerrainNoise.Hash01(k, 32, seed) * g.W;
                oz[k] = TerrainNoise.Hash01(k, 33, seed) * g.Hgt;
                bend[k] = (TerrainNoise.Hash01(k, 34, seed) - 0.5f) * 0.002f;
                w[k] = 2.2f + 3f * TerrainNoise.Hash01(k, 35, seed);
                hgt[k] = 0.6f + 0.9f * TerrainNoise.Hash01(k, 36, seed);
            }

            // Chaos regions (jumbled ice rafts), kept away from the drop.
            var chaosC = new Vector3[2];
            var chaosR = new float[2];
            for (int k = 0; k < 2; k++)
            {
                float cx = TerrainNoise.Hash01(k, 41, seed) * (g.W - 120f) + 60f;
                float cz = TerrainNoise.Hash01(k, 42, seed) * (g.Hgt - 120f) + 60f;
                if (FlatDist(cx, cz, ColonyLayout.CampusOrigin) < 90f) cx = g.W - cx;
                chaosC[k] = new Vector3(cx, 0f, cz);
                chaosR[k] = 38f + 20f * TerrainNoise.Hash01(k, 43, seed);
            }

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int idx = j * n + i;
                float h = 3.2f * TerrainNoise.Fbm(x / 140f, z / 140f, seed, 5)
                          + 0.12f * TerrainNoise.Fbm(x / 5f, z / 5f, seed + 13, 3);
                float lin = 0f;
                for (int k = 0; k < lineae; k++)
                {
                    float dx = Mathf.Cos(ang[k]), dz = Mathf.Sin(ang[k]);
                    float along = (x - ox[k]) * dx + (z - oz[k]) * dz;
                    float side = -(x - ox[k]) * dz + (z - oz[k]) * dx + along * along * bend[k];
                    float ad0 = Mathf.Abs(side);
                    if (ad0 > w[k] * 5f + 8f) continue;
                    side += 2.5f * TerrainNoise.Fbm(along / 40f, k * 3.7f, seed + 71, 3);
                    float ad = Mathf.Abs(side);
                    float ridge = Mathf.Exp(-Sq((ad - w[k]) / (0.42f * w[k])))
                                  - 0.45f * Mathf.Exp(-Sq(ad / (0.28f * w[k])));
                    h += hgt[k] * ridge;
                    lin = Mathf.Max(lin, TerrainNoise.Smooth(w[k] * 2.4f, w[k] * 0.6f, ad) * (0.5f + 0.5f * k / 10f));
                }
                float cracks = TerrainNoise.Ridged(x / 22f, z / 22f, seed + 17, 3);
                h -= 0.18f * TerrainNoise.Smooth(0.78f, 0.95f, cracks);
                lin = Mathf.Max(lin, 0.45f * TerrainNoise.Smooth(0.8f, 0.95f, cracks));

                float chaos = 0f;
                for (int k = 0; k < 2; k++)
                {
                    float d = FlatDist(x, z, chaosC[k]);
                    if (d > chaosR[k] * 1.4f) continue;
                    float m = TerrainNoise.Smooth(1f, 0.7f,
                        d / chaosR[k] * (1f + 0.2f * TerrainNoise.Fbm(x / 20f, z / 20f, seed + 44, 2)));
                    if (m <= 0f) continue;
                    ChaosBlock(x, z, seed, out float blocks, out float gap);
                    h = h * (1f - m) + (h * 0.3f + blocks * gap - 0.9f * (1f - gap)) * m;
                    chaos = Mathf.Max(chaos, m);
                }

                g.H[idx] = h;
                g.Feature[idx] = Mathf.Clamp01(lin * 0.85f + chaos * 0.55f);
            }

            var craters = CraterPopulation(seed, g.W, 30, 1.5f, 16f, 2.2f, 0.1f, 204, 24f);
            StampCraters(g, craters, 0.16f, 0.04f, false);
        }

        /// <summary>Jittered Voronoi ice raft: tilted block height and a crack mask (1 = solid).</summary>
        private static void ChaosBlock(float x, float z, int seed, out float blocks, out float gap)
        {
            const float cell = 10f;
            int gx = Mathf.FloorToInt(x / cell);
            int gz = Mathf.FloorToInt(z / cell);
            float best = 1e9f, second = 1e9f;
            int bx = gx, bz = gz;
            for (int oz = -1; oz <= 1; oz++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = gx + ox, cz = gz + oz;
                float px = (cx + TerrainNoise.Hash01(cx, cz, seed + 47)) * cell;
                float pz = (cz + TerrainNoise.Hash01(cx, cz, seed + 48)) * cell;
                float d = Mathf.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                if (d < best)
                {
                    second = best;
                    best = d;
                    bx = cx;
                    bz = cz;
                }
                else if (d < second)
                {
                    second = d;
                }
            }
            float h1 = TerrainNoise.Hash01(bx, bz, seed + 45);
            float tx = (TerrainNoise.Hash01(bx, bz, seed + 46) - 0.5f) * 0.25f;
            float tz = (TerrainNoise.Hash01(bx, bz, seed + 49) - 0.5f) * 0.25f;
            blocks = (h1 - 0.3f) * 2.2f + tx * (x - bx * cell) + tz * (z - bz * cell);
            gap = TerrainNoise.Smooth(0.6f, 1.8f, second - best);
        }

        // ---------------------------------------------------------------- Belt ---------------

        private static float BeltBase(float x, float z, int seed)
        {
            Warp(x, z, seed, 80f, 20f, out float xw, out float zw);
            return 11f * (TerrainNoise.Ridged(xw / 110f, zw / 110f, seed, 5) - 0.5f)
                   + 12f * TerrainNoise.ErodedFbm(xw / 60f, zw / 60f, seed + 3, 5)
                   + 1f * TerrainNoise.Fbm(x / 11f, z / 11f, seed + 11, 4);
        }

        private static void GenBelt(Grid g, int seed)
        {
            int n = g.N;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                g.H[j * n + i] = BeltBase(g.X(i), g.Z(j), seed);
            var craters = CraterPopulation(seed, g.W, 900, 1f, 30f, 2f, 0.55f, 205, 20f);
            StampCraters(g, craters, 0.2f, 0.05f, false);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                g.H[j * n + i] *= SettleEnvelope(g.X(i), g.Z(j));
        }

        // ---------------------------------------------------------------- craters ------------

        private struct Crater
        {
            public float X, Z, R, Age;
            public int Seed;
        }

        /// <summary>
        /// Power-law crater sizes, N(&gt;R) ∝ R^-slope — many small, few large, as on real surfaces.
        /// Age 0 = fresh (sharp rim, bright ejecta), 1 = degraded. Oldest are stamped first.
        /// </summary>
        private static List<Crater> CraterPopulation(int seed, float width, int count, float rmin, float rmax,
            float slope, float ageBias, int salt, float campusClear)
        {
            var list = new List<Crater>(count);
            int s = 0;
            float Next() => TerrainNoise.Hash01(s++, salt, seed);
            float tail = 1f - Mathf.Pow(rmin / rmax, slope);
            float agePow = 1f / Mathf.Max(0.05f, 1f - ageBias + 0.5f);
            Vector3 campus = ColonyLayout.CampusOrigin;
            int tries = 0;
            while (list.Count < count && tries < count * 6)
            {
                tries++;
                float u = Next();
                float r = rmin * Mathf.Pow(1f - u * tail, -1f / slope);
                float x = Next() * width;
                float z = Next() * width;
                float age = Mathf.Pow(Next(), agePow);
                int cs = (int)(Next() * 1e6f);
                if (FlatDist(x, z, campus) <= campusClear + r) continue;
                list.Add(new Crater { X = x, Z = z, R = r, Age = age, Seed = cs });
            }
            list.Sort((a, b) => b.Age.CompareTo(a.Age));
            return list;
        }

        private static void StampCraters(Grid g, List<Crater> craters, float depthRatio, float rimRatio, bool rays)
        {
            int n = g.N;
            foreach (var c in craters)
            {
                float fresh = 1f - c.Age;
                float ext = c.R * (rays && fresh > 0.8f && c.R > 3f ? 5.5f : fresh > 0.6f ? 2.6f : 1.8f);
                g.Bounds(c.X, c.Z, ext, out int i0, out int i1, out int j0, out int j1);
                if (i0 > i1 || j0 > j1) continue;
                float depth = depthRatio * 2f * c.R * (1f - 0.72f * c.Age);
                float rim = rimRatio * 2f * c.R * (1f - 0.8f * c.Age);
                float flat = c.R > 18f ? 0.35f : 0f;
                float hc = g.At(c.X, c.Z);
                float blendBase = 0.55f + 0.45f * fresh;
                int lobeA = c.Seed % 7, lobeB = c.Seed % 11;
                int nray = 7 + c.Seed % 6;
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * n + i;
                    float dx = g.X(i) - c.X, dz = g.Z(j) - c.Z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > ext) continue;
                    float a = Mathf.Atan2(dz, dx);
                    float wob = 1f + 0.06f * Mathf.Sin(a * 3f + lobeA) + 0.04f * Mathf.Sin(a * 5f + lobeB);
                    float rn = d / (c.R * wob);

                    float prof;
                    if (rn < 1f)
                    {
                        prof = -depth + (depth + rim) * rn * rn;
                        if (flat > 0f) prof = Mathf.Max(prof, -depth * (1f - flat));
                        if (c.R > 24f) prof += Mathf.Exp(-Sq(rn / 0.16f)) * depth * 0.45f * (1f - c.Age);
                    }
                    else
                    {
                        prof = Mathf.Max(0f, rim * Mathf.Pow(rn, -3f) - rim * 0.03f);
                    }
                    prof += Mathf.Exp(-Sq((rn - 1f) / 0.12f)) * rim * 0.15f;

                    // Younger craters overprint whatever floor they land on.
                    float kk = TerrainNoise.Smooth(1.15f, 0.85f, rn) * blendBase;
                    float h = g.H[k];
                    g.H[k] = h * (1f - kk) + (hc * 0.6f + h * 0.4f) * kk + prof;

                    if (fresh > 0.45f)
                    {
                        float f = (fresh - 0.45f) / 0.55f;
                        float ej = Mathf.Clamp01(Mathf.Pow(Mathf.Max(rn, 0.9f), -2.2f)) * TerrainNoise.Smooth(0.7f, 1f, rn);
                        float inner = TerrainNoise.Smooth(1f, 0.6f, rn) * 0.35f;
                        float m = (ej + inner) * f;
                        if (rays && fresh > 0.8f && c.R > 3f)
                        {
                            // Many thin, faint, broken rays rather than a starfish.
                            float ray = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * nray * 1.5f + c.Seed)), 40f)
                                        * (0.5f + 0.5f * TerrainNoise.Gradient(a * 6f, rn * 1.3f, c.Seed))
                                        * TerrainNoise.Smooth(5.5f, 1.2f, rn) * TerrainNoise.Smooth(0.9f, 1.3f, rn);
                            m += Mathf.Max(0f, ray) * 0.55f * f;
                        }
                        g.Fresh[k] = Mathf.Max(g.Fresh[k], Mathf.Clamp01(m));
                    }
                }
            }
        }

        // ---------------------------------------------------------------- maps ---------------

        private static void BakeMaps(TerrainBake bake, Grid g, CelestialBodyId id)
        {
            int n = g.N;
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < g.H.Length; i++)
            {
                if (g.H[i] < minH) minH = g.H[i];
                if (g.H[i] > maxH) maxH = g.H[i];
            }
            bake.MinHeight = minH;
            bake.MaxHeight = maxH;
            bake.Amplitude = Mathf.Max(0.5f, Mathf.Max(Mathf.Abs(minH), Mathf.Abs(maxH)));
            float span = Mathf.Max(0.001f, maxH - minH);

            float[] ao = HorizonAO(g);

            var heightPx = new Color[n * n];
            var normalPx = new Color[n * n];
            var splatPx = new Color[n * n];
            var maskPx = new Color[n * n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                float h = g.H[i];
                float hL = g.H[z * n + Mathf.Max(0, x - 1)];
                float hR = g.H[z * n + Mathf.Min(n - 1, x + 1)];
                float hD = g.H[Mathf.Max(0, z - 1) * n + x];
                float hU = g.H[Mathf.Min(n - 1, z + 1) * n + x];
                Vector3 nrm = new Vector3((hL - hR) / (2f * g.StepX), 1f, (hD - hU) / (2f * g.StepZ)).normalized;
                normalPx[i] = new Color(nrm.x * 0.5f + 0.5f, 1f, nrm.z * 0.5f + 0.5f, 1f);

                float h01 = (h - minH) / span;
                heightPx[i] = new Color(h01, h01, h01, 1f);

                float slope = 1f - Mathf.Clamp01(nrm.y);
                float lap = (hL + hR + hD + hU - 4f * h) / (g.StepX * g.StepZ);
                float cavity = Mathf.Clamp(lap * 0.6f, -1f, 1f);
                Color s = RefineSplat(id, SplatFor(id, h01, slope), slope, cavity, g.Feature[i], g.Wet[i], g.Rock[i]);
                splatPx[i] = s;
                g.Rock[i] = s.g;

                maskPx[i] = new Color(ao[i], Mathf.Clamp01(g.Fresh[i]), Mathf.Clamp01(g.Feature[i]), h01);
            }

            bake.HeightMap = MakeTex("SM_Bake_Height", n, heightPx, true);
            bake.NormalMap = MakeTex("SM_Bake_Normal", n, normalPx, true);
            bake.SplatMap = MakeTex("SM_Bake_Splat", n, splatPx, false);
            bake.MaskMap = MakeTex("SM_Bake_Mask", n, maskPx, true);
            bake.Rock = g.Rock;
        }

        /// <summary>Feature-aware splat. Keeps the channel meaning of <see cref="SplatFor"/>.</summary>
        private static Color RefineSplat(CelestialBodyId id, Color s, float slope, float cavity,
            float feature, float wet, float mesa)
        {
            float dust = s.r, rock = s.g, b = s.b, a = s.a;
            switch (id)
            {
                case CelestialBodyId.Mars:
                    rock = Mathf.Max(rock, TerrainNoise.Smooth(0.10f, 0.34f, slope));
                    b = Mathf.Clamp01(feature * 0.75f + 0.3f * TerrainNoise.Smooth(0.02f, 0.2f, cavity)) * (1f - rock);
                    a *= 0.6f;
                    break;
                case CelestialBodyId.Luna:
                    rock = TerrainNoise.Smooth(0.10f, 0.34f, slope) * 0.6f;
                    a = feature * 0.85f * (1f - rock);
                    b = 0f;
                    break;
                case CelestialBodyId.Earth:
                    rock = TerrainNoise.Smooth(0.2f, 0.45f, slope);
                    a = wet * 0.8f * (1f - rock);
                    float soil = Mathf.Clamp01(feature * 0.6f + rock * 0.5f) * 0.55f;
                    b = Mathf.Max(0f, 1f - rock - a - soil);
                    break;
                case CelestialBodyId.Europa:
                    rock = TerrainNoise.Smooth(0.10f, 0.34f, slope) * 0.4f;
                    b = feature * (1f - rock);
                    a = 0f;
                    break;
                default:
                    rock = TerrainNoise.Smooth(0.10f, 0.34f, slope) * 0.7f;
                    b = TerrainNoise.Smooth(0.02f, 0.25f, cavity) * 0.6f * (1f - rock);
                    a = 0f;
                    break;
            }
            dust = Mathf.Max(0f, 1f - rock - b - a);
            float sum = dust + rock + b + a;
            if (sum < 1e-4f) return new Color(1f, 0f, 0f, 0f);
            return new Color(dust / sum, rock / sum, b / sum, a / sum);
        }

        /// <summary>Horizon-based ambient occlusion over the height grid (8 directions).</summary>
        private static float[] HorizonAO(Grid g)
        {
            int n = g.N;
            var ao = new float[n * n];
            const int dirs = 8;
            const float maxDist = 24f;
            var cx = new float[dirs];
            var cz = new float[dirs];
            for (int k = 0; k < dirs; k++)
            {
                cx[k] = Mathf.Cos(k / (float)dirs * Mathf.PI * 2f);
                cz[k] = Mathf.Sin(k / (float)dirs * Mathf.PI * 2f);
            }
            float step = g.StepX;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                float h = g.H[i];
                float acc = 0f;
                for (int k = 0; k < dirs; k++)
                {
                    float best = 0f;
                    float dist = step;
                    while (dist <= maxDist)
                    {
                        int sx = Mathf.Clamp(x + Mathf.RoundToInt(cx[k] * dist / step), 0, n - 1);
                        int sz = Mathf.Clamp(z + Mathf.RoundToInt(cz[k] * dist / step), 0, n - 1);
                        float sl = (g.H[sz * n + sx] - h) / dist;
                        if (sl > best) best = sl;
                        dist *= 1.6f;
                    }
                    acc += 1f - best / Mathf.Sqrt(1f + best * best);
                }
                ao[i] = acc / dirs;
            }
            return ao;
        }

        private static Texture2D MakeTex(string name, int n, Color[] px, bool linear)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---------------------------------------------------------------- grading ------------

        /// <summary>
        /// Level the ground under a placed building (a graded construction pad with a soft apron),
        /// so structures outside the pre-flattened yards never float or sink. Updates the height
        /// field and the terrain textures; returns true if anything changed.
        /// </summary>
        public static bool LevelFootprint(TerrainBake bake, Vector3 center, Vector2 halfExtents, float apron,
            float targetY = 0f)
        {
            if (bake == null || bake.Heights == null) return false;
            int n = bake.Resolution;
            float stepX = bake.WorldWidth / (n - 1);
            float stepZ = bake.WorldHeight / (n - 1);
            float rx = halfExtents.x + apron, rz = halfExtents.y + apron;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((center.x - rx) / stepX));
            int i1 = Mathf.Min(n - 1, Mathf.CeilToInt((center.x + rx) / stepX));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((center.z - rz) / stepZ));
            int j1 = Mathf.Min(n - 1, Mathf.CeilToInt((center.z + rz) / stepZ));
            if (i0 > i1 || j0 > j1) return false;

            bool changed = false;
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x = i * stepX, z = j * stepZ;
                float ox = Mathf.Max(0f, Mathf.Abs(x - center.x) - halfExtents.x);
                float oz = Mathf.Max(0f, Mathf.Abs(z - center.z) - halfExtents.y);
                float d = Mathf.Sqrt(ox * ox + oz * oz);
                float w = 1f - TerrainNoise.Smooth(0f, Mathf.Max(0.01f, apron), d);
                if (w <= 0f) continue;
                int k = j * n + i;
                float h = Mathf.Lerp(bake.Heights[k], targetY, w);
                if (Mathf.Abs(h - bake.Heights[k]) > 1e-4f)
                {
                    bake.Heights[k] = h;
                    changed = true;
                }
            }
            if (!changed) return false;

            // Refresh the baked textures in the touched block (+1 texel for normals).
            int a0 = Mathf.Max(0, i0 - 1), a1 = Mathf.Min(n - 1, i1 + 1);
            int b0 = Mathf.Max(0, j0 - 1), b1 = Mathf.Min(n - 1, j1 + 1);
            float span = Mathf.Max(0.001f, bake.MaxHeight - bake.MinHeight);
            for (int j = b0; j <= b1; j++)
            for (int i = a0; i <= a1; i++)
            {
                int k = j * n + i;
                float h = bake.Heights[k];
                float hL = bake.Heights[j * n + Mathf.Max(0, i - 1)];
                float hR = bake.Heights[j * n + Mathf.Min(n - 1, i + 1)];
                float hD = bake.Heights[Mathf.Max(0, j - 1) * n + i];
                float hU = bake.Heights[Mathf.Min(n - 1, j + 1) * n + i];
                Vector3 nrm = new Vector3((hL - hR) / (2f * stepX), 1f, (hD - hU) / (2f * stepZ)).normalized;
                float h01 = Mathf.Clamp01((h - bake.MinHeight) / span);
                bake.NormalMap?.SetPixel(i, j, new Color(nrm.x * 0.5f + 0.5f, 1f, nrm.z * 0.5f + 0.5f, 1f));
                bake.HeightMap?.SetPixel(i, j, new Color(h01, h01, h01, 1f));
                if (bake.MaskMap != null)
                {
                    Color m = bake.MaskMap.GetPixel(i, j);
                    m.a = h01;
                    bake.MaskMap.SetPixel(i, j, m);
                }
                if (bake.Rock != null) bake.Rock[k] = Mathf.Min(bake.Rock[k], 1f - Mathf.Clamp01(nrm.y) * 2f);
            }
            bake.NormalMap?.Apply(false, false);
            bake.HeightMap?.Apply(false, false);
            bake.MaskMap?.Apply(false, false);
            return true;
        }

        // ---------------------------------------------------------------- helpers ------------

        private static void Warp(float x, float z, int seed, float scale, float amount, out float xw, out float zw)
        {
            xw = x + TerrainNoise.Fbm(x / scale, z / scale, seed + 501, 3) * amount;
            zw = z + TerrainNoise.Fbm(x / scale + 31.7f, z / scale - 12.3f, seed + 502, 3) * amount;
        }

        private static float SeedAngle(int seed, int salt) => TerrainNoise.Hash01(seed, salt, 991) * Mathf.PI * 2f;

        private static float Sq(float v) => v * v;

        private static float FlatDist(float x, float z, Vector3 o)
        {
            float dx = x - o.x, dz = z - o.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// HLSL-style smoothstep: 0 below <paramref name="edge0"/>, 1 above <paramref name="edge1"/>.
        /// Unity's Mathf.SmoothStep interpolates from→to with a 0–1 t instead.
        /// </summary>
        private static float SmoothRange(float edge0, float edge1, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));
        }

        private static float FalloffAt(float x, float z, Vector3 origin, float radius, float blend)
        {
            float d = FlatDist(x, z, origin);
            if (d <= radius) return 0f;
            if (d >= radius + blend) return 1f;
            return Mathf.SmoothStep(0f, 1f, (d - radius) / blend);
        }
    }
}
