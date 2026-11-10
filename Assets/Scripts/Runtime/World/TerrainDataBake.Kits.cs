using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The terrain kits of generated worlds. A kit reshapes the ground of the Sol world it
    /// borrows and adds one thing the colony has to live with:
    ///
    /// * Volcanic — Mars relief cut by lava channels running out from vents. Lava burns a robot
    ///   that stands in it and nothing can be built on it.
    /// * Archipelago — a sea with one connected landmass of islands and sandbars. Nothing is
    ///   built on the sea and nobody walks through it.
    /// * DuneSea — long dune ridges. The loose sand high on a dune slows travel; the firm
    ///   ground between dunes does not.
    ///
    /// All three keep the colony's own ground clear (<see cref="HomeRadius"/>).
    /// </summary>
    public static partial class TerrainDataBake
    {
        /// <summary>No lava, sea or loose sand closer to the Commons than this: it covers the village rings, the pad and the outpost.</summary>
        public const float HomeRadius = 62f;

        /// <summary>Sea level of an archipelago. The yard pads sit at 0, so the sea is below them.</summary>
        public const float SeaLevel = -0.3f;

        /// <summary>The sea is carved out of the walkable ground in cells this many to a side.</summary>
        public const int SeaCellsPerSide = 48;

        /// <summary>Lava strength above this counts as standing in lava.</summary>
        public const float LavaEdge = 0.35f;

        private static bool NearHome(float x, float z, float radius) =>
            FlatDist(x, z, ColonyLayout.CampusOrigin) < radius ||
            FlatDist(x, z, ColonyLayout.CampusBOrigin) < radius * 0.8f ||
            FlatDist(x, z, LaunchSite.PadWorld) < radius * 0.6f;

        // ---------------------------------------------------------------- Volcanic -----------

        /// <summary>Cut lava channels into finished relief and record where the lava is.</summary>
        private static void CarveLava(Grid g, int seed, TerrainBake bake)
        {
            int n = g.N;
            bake.Lava = new float[n * n];
            int flows = 5 + Mathf.FloorToInt(TerrainNoise.Hash01(seed, 71, 5) * 4f);
            for (int f = 0; f < flows; f++)
            {
                // A vent well away from the colony; a few tries, then this flow is skipped.
                float vx = 0f, vz = 0f;
                bool found = false;
                for (int attempt = 0; attempt < 12 && !found; attempt++)
                {
                    vx = Mathf.Lerp(24f, g.W - 24f, TerrainNoise.Hash01(seed + f * 17, attempt, 811));
                    vz = Mathf.Lerp(24f, g.Hgt - 24f, TerrainNoise.Hash01(seed + f * 17, attempt, 812));
                    found = !NearHome(vx, vz, HomeRadius + 22f);
                }
                if (!found) continue;

                var flow = new TerrainRiver();
                float heading = TerrainNoise.Hash01(seed + f, 3, 813) * Mathf.PI * 2f;
                float halfWidth = 2.2f + 1.4f * TerrainNoise.Hash01(seed + f, 4, 814);
                int steps = 26 + Mathf.FloorToInt(TerrainNoise.Hash01(seed + f, 5, 815) * 30f);
                float x = vx, z = vz;
                for (int s = 0; s <= steps; s++)
                {
                    if (x < 8f || z < 8f || x > g.W - 8f || z > g.Hgt - 8f) break;
                    if (NearHome(x, z, HomeRadius + 6f)) break;
                    // Wide at the vent, narrowing to nothing where the flow has cooled.
                    float taper = 1f - TerrainNoise.Smooth(0.7f, 1f, s / (float)steps);
                    flow.Points.Add(new Vector3(x, 0f, z));
                    flow.HalfWidths.Add(halfWidth * (0.45f + 0.55f * taper));
                    heading += 0.42f * TerrainNoise.Fbm(x / 34f, z / 34f, seed + 821 + f, 2);
                    x += Mathf.Cos(heading) * 3f;
                    z += Mathf.Sin(heading) * 3f;
                }
                if (flow.Points.Count < 4) continue;

                for (int p = 0; p < flow.Points.Count - 1; p++)
                    StampLava(g, bake.Lava, flow.Points[p], flow.Points[p + 1], flow.HalfWidths[p], flow.HalfWidths[p + 1]);
                // The vent itself: a pool a little wider than the channel.
                StampLava(g, bake.Lava, flow.Points[0], flow.Points[0], halfWidth * 1.9f, halfWidth * 1.9f);

                // The surface sits a little above the carved bed, below the banks.
                for (int p = 0; p < flow.Points.Count; p++)
                {
                    Vector3 q = flow.Points[p];
                    q.y = g.At(q.x, q.z) + 0.3f;
                    flow.Points[p] = q;
                }
                bake.LavaFlows.Add(flow);
            }
        }

        private static void StampLava(Grid g, float[] lava, Vector3 a, Vector3 b, float halfA, float halfB)
        {
            float reach = Mathf.Max(halfA, halfB) + 2.4f;
            g.Bounds(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), reach, out int i0, out _, out int j0, out _);
            g.Bounds(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z), reach, out _, out int i1, out _, out int j1);
            float abx = b.x - a.x, abz = b.z - a.z;
            float len2 = abx * abx + abz * abz;
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x = g.X(i), z = g.Z(j);
                float t = len2 > 1e-4f ? Mathf.Clamp01(((x - a.x) * abx + (z - a.z) * abz) / len2) : 0f;
                float dx = x - (a.x + abx * t), dz = z - (a.z + abz * t);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float half = Mathf.Lerp(halfA, halfB, t);
                if (d > half + 2.4f) continue;
                int k = j * g.N + i;
                float inside = 1f - TerrainNoise.Smooth(half * 0.65f, half, d);
                if (inside > lava[k])
                {
                    // Each texel is carved once, to the deepest profile that reaches it.
                    g.H[k] -= 0.9f * (inside - lava[k]);
                    lava[k] = inside;
                }
                // A low levee of cooled crust along each bank.
                float levee = Mathf.Exp(-Sq((d - half - 0.9f) / 0.8f));
                g.Rock[k] = Mathf.Max(g.Rock[k], 0.7f * levee);
                g.Feature[k] = Mathf.Max(g.Feature[k], Mathf.Max(inside, 0.6f * levee));
            }
        }

        // ---------------------------------------------------------------- DuneSea ------------

        /// <summary>Long transverse dunes over gentle ground, with the loose sand marked.</summary>
        private static void GenDuneSea(Grid g, int seed, TerrainBake bake)
        {
            int n = g.N;
            bake.LooseSand = new float[n * n];
            float th = SeedAngle(seed, 9);
            float cos = Mathf.Cos(th), sin = Mathf.Sin(th);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int k = j * n + i;
                Warp(x, z, seed, 120f, 18f, out float xw, out float zw);
                float swell = 3.2f * TerrainNoise.Fbm(xw / 170f, zw / 170f, seed + 501, 4);

                // Ridges march across the wind; their crests wander and break into barchans.
                float s = xw * cos + zw * sin + 16f * TerrainNoise.Fbm(x / 70f, z / 70f, seed + 503, 3);
                float t = s / 34f;
                t -= Mathf.Floor(t);
                float crest = 0.5f + 0.5f * Mathf.Clamp01(0.5f + 1.4f * TerrainNoise.Fbm(x / 55f, z / 55f, seed + 505, 3));
                float dune = AsymDune(t) * crest;
                // Small ripples ride the big dunes.
                float r = (xw * cos + zw * sin) / 5.5f;
                r -= Mathf.Floor(r);
                float ripple = 0.16f * AsymDune(r) * dune;

                // The colony stands on firm ground: dunes fade out toward it.
                float away = TerrainNoise.Smooth(HomeRadius * 0.55f, HomeRadius + 30f, FlatDist(x, z, ColonyLayout.CampusOrigin));
                g.H[k] = (swell + (5.2f * dune + ripple) * away) * SettleEnvelope(x, z);
                // Loose sand is the upper part of a dune; the troughs between are firm.
                bake.LooseSand[k] = TerrainNoise.Smooth(0.38f, 0.7f, dune) * away;
                g.Feature[k] = Mathf.Clamp01(0.25f + 0.6f * dune);
            }
        }

        // ---------------------------------------------------------------- Archipelago --------

        /// <summary>Islands in a shallow sea. <see cref="FinishSea"/> then joins them into one landmass.</summary>
        private static void GenArchipelago(Grid g, int seed)
        {
            int n = g.N;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = g.X(i), z = g.Z(j);
                int k = j * n + i;
                Warp(x, z, seed, 110f, 26f, out float xw, out float zw);
                float m = TerrainNoise.Fbm(xw / 92f, zw / 92f, seed + 401, 4) - 0.03f;
                // The home island: always land around the colony.
                float home = 1f - TerrainNoise.Smooth(HomeRadius, HomeRadius + 45f, FlatDist(x, z, ColonyLayout.CampusOrigin));
                m = Mathf.Lerp(m, Mathf.Max(m, 0.2f), home);

                float h;
                if (m >= 0f)
                {
                    float inland = TerrainNoise.Smooth(0f, 0.16f, m);
                    float hills = Mathf.Max(0f, EarthBase(x, z, seed)) * SettleEnvelope(x, z);
                    h = inland * (0.5f + 0.42f * hills) + 0.12f * TerrainNoise.Fbm(x / 9f, z / 9f, seed + 403, 3) * inland;
                }
                else
                {
                    h = -3.2f * TerrainNoise.Smooth(0f, 0.22f, -m);
                }
                g.H[k] = h;
                // Beach and sea bed read as wet sand.
                g.Wet[k] = TerrainNoise.Smooth(0.07f, -0.03f, m);
                g.Feature[k] = Mathf.Clamp01(1f - TerrainNoise.Smooth(0f, 0.1f, m));
            }
        }

        /// <summary>
        /// Make every piece of land reachable on foot from the Commons. Islands of any size get a
        /// sandbar to the nearest connected land; specks too small to matter are sunk. Then the
        /// sea cells (what the walkable ground has cut out of it) are recorded on the bake.
        /// </summary>
        private static void FinishSea(Grid g, TerrainBake bake)
        {
            int c = SeaCellsPerSide;
            float cw = g.W / c, ch = g.Hgt / c;
            var sea = new bool[c * c];
            var main = new bool[c * c];
            var queue = new Queue<int>(c * c);
            var from = new int[c * c];
            int homeCell = CellOf(ColonyLayout.CampusOrigin.x, ColonyLayout.CampusOrigin.z, cw, ch, c);

            for (int pass = 0; pass < 80; pass++)
            {
                for (int k = 0; k < sea.Length; k++) sea[k] = CellIsSea(g, k % c, k / c, cw, ch);
                Flood(sea, main, queue, homeCell, c);

                // The nearest stranded cell to the mainland, by a walk through the sea.
                for (int k = 0; k < from.Length; k++) from[k] = main[k] ? k : -1;
                queue.Clear();
                for (int k = 0; k < main.Length; k++) if (main[k]) queue.Enqueue(k);
                int stranded = -1;
                while (queue.Count > 0 && stranded < 0)
                {
                    int k = queue.Dequeue();
                    int cx = k % c, cz = k / c;
                    for (int d = 0; d < 4 && stranded < 0; d++)
                    {
                        int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= c || nz >= c) continue;
                        int nk = nz * c + nx;
                        if (from[nk] >= 0) continue;
                        from[nk] = k;
                        if (!sea[nk]) stranded = nk;
                        else queue.Enqueue(nk);
                    }
                }
                if (stranded < 0) break; // all land is one piece

                if (IslandSize(sea, main, stranded, c) < 6)
                {
                    SinkCell(g, stranded % c, stranded / c, cw, ch);
                    continue;
                }
                // Raise a sandbar back along the walk.
                for (int k = from[stranded]; !main[k]; k = from[k])
                    RaiseBar(g, (k % c + 0.5f) * cw, (k / c + 0.5f) * ch, cw);
            }

            // Whatever is still cut off after the passes goes under.
            for (int k = 0; k < sea.Length; k++) sea[k] = CellIsSea(g, k % c, k / c, cw, ch);
            Flood(sea, main, queue, homeCell, c);
            for (int k = 0; k < sea.Length; k++)
            {
                if (sea[k] || main[k]) continue;
                SinkCell(g, k % c, k / c, cw, ch);
                sea[k] = true;
            }

            bake.HasSea = true;
            bake.SeaCells = sea;
        }

        private static int CellOf(float x, float z, float cw, float ch, int c) =>
            Mathf.Clamp(Mathf.FloorToInt(z / ch), 0, c - 1) * c + Mathf.Clamp(Mathf.FloorToInt(x / cw), 0, c - 1);

        // A cell is sea only when its centre and four inner points are all under water, so a
        // cell the shore runs through stays walkable.
        private static bool CellIsSea(Grid g, int cx, int cz, float cw, float ch)
        {
            float x = (cx + 0.5f) * cw, z = (cz + 0.5f) * ch;
            float ox = cw * 0.35f, oz = ch * 0.35f;
            return g.At(x, z) < SeaLevel && g.At(x - ox, z - oz) < SeaLevel && g.At(x + ox, z - oz) < SeaLevel &&
                   g.At(x - ox, z + oz) < SeaLevel && g.At(x + ox, z + oz) < SeaLevel;
        }

        private static void Flood(bool[] sea, bool[] main, Queue<int> queue, int start, int c)
        {
            System.Array.Clear(main, 0, main.Length);
            queue.Clear();
            if (sea[start]) return;
            main[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                int cx = k % c, cz = k / c;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= c || nz >= c) continue;
                    int nk = nz * c + nx;
                    if (sea[nk] || main[nk]) continue;
                    main[nk] = true;
                    queue.Enqueue(nk);
                }
            }
        }

        private static int IslandSize(bool[] sea, bool[] main, int start, int c)
        {
            var seen = new HashSet<int> { start };
            var open = new Queue<int>();
            open.Enqueue(start);
            while (open.Count > 0 && seen.Count < 6)
            {
                int k = open.Dequeue();
                int cx = k % c, cz = k / c;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= c || nz >= c) continue;
                    int nk = nz * c + nx;
                    if (sea[nk] || main[nk] || !seen.Add(nk)) continue;
                    open.Enqueue(nk);
                }
            }
            return seen.Count;
        }

        private static void SinkCell(Grid g, int cx, int cz, float cw, float ch)
        {
            int i0 = Mathf.Max(0, Mathf.CeilToInt(cx * cw / g.StepX)), i1 = Mathf.Min(g.N - 1, Mathf.FloorToInt((cx + 1) * cw / g.StepX));
            int j0 = Mathf.Max(0, Mathf.CeilToInt(cz * ch / g.StepZ)), j1 = Mathf.Min(g.N - 1, Mathf.FloorToInt((cz + 1) * ch / g.StepZ));
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                int k = j * g.N + i;
                g.H[k] = Mathf.Min(g.H[k], SeaLevel - 0.5f);
                g.Wet[k] = 1f;
            }
        }

        private static void RaiseBar(Grid g, float x, float z, float cell)
        {
            float full = cell * 0.62f, reach = cell * 1.05f;
            g.Bounds(x, z, reach, out int i0, out int i1, out int j0, out int j1);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float d = FlatDist(g.X(i), g.Z(j), new Vector3(x, 0f, z));
                if (d > reach) continue;
                int k = j * g.N + i;
                float top = SeaLevel + 0.38f * (1f - TerrainNoise.Smooth(full, reach, d));
                if (top <= g.H[k]) continue;
                g.H[k] = top;
                g.Wet[k] = 1f; // a sandbar is wet sand
            }
        }
    }
}
