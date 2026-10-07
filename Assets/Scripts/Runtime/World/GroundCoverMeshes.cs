using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Low-poly ground-cover meshes built in code, one per <see cref="GroundCoverKind"/>, sized
    /// about 1 m so the instance scale is the real size. Vertex data follows SM_GroundCover:
    /// colour (alpha = glow), UV0.x wind weight, UV0.y tint mask. Built once and shared.
    /// </summary>
    public static class GroundCoverMeshes
    {
        private static readonly Dictionary<GroundCoverKind, Mesh> Cache = new Dictionary<GroundCoverKind, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();

        public static Mesh Get(GroundCoverKind kind)
        {
            if (Cache.TryGetValue(kind, out var mesh) && mesh != null) return mesh;
            var b = new Builder();
            var rng = new System.Random(7919 + (int)kind * 104729);
            switch (kind)
            {
                case GroundCoverKind.GrassTuft: GrassTuft(b, rng, 10, 0.5f, 1.0f, 0.22f); break;
                case GroundCoverKind.TallGrass: TallGrass(b, rng); break;
                case GroundCoverKind.Flowers: Flowers(b, rng); break;
                case GroundCoverKind.Pebbles: Stones(b, rng, 6, 0.42f, 0.10f, 0.22f, 0.55f, 0.18f); break;
                case GroundCoverKind.Bush: Bush(b, rng); break;
                case GroundCoverKind.Fern: Fern(b, rng); break;
                case GroundCoverKind.Mushrooms: Mushrooms(b, rng); break;
                case GroundCoverKind.Rubble: Stones(b, rng, 9, 0.55f, 0.08f, 0.26f, 0.45f, 0.42f); break;
                case GroundCoverKind.Boulder: Boulder(b, rng); break;
                case GroundCoverKind.Crystals: Crystals(b, rng); break;
                case GroundCoverKind.IceShards: IceShards(b, rng); break;
                case GroundCoverKind.Reeds: Reeds(b, rng); break;
                default: Twigs(b, rng); break;
            }
            mesh = b.Build("SM_Cover_" + kind);
            Cache[kind] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ plants

        private static void GrassTuft(Builder b, System.Random r, int blades, float hMin, float hMax, float spread)
        {
            for (int i = 0; i < blades; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = spread * Mathf.Sqrt((float)r.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Mathf.Lerp(hMin, hMax, (float)r.NextDouble());
                float lean = Mathf.Lerp(0.15f, 0.45f, (float)r.NextDouble()) * h;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float shade = Mathf.Lerp(0.8f, 1.05f, (float)r.NextDouble());
                b.Blade(root, outward, h, Mathf.Lerp(0.05f, 0.085f, (float)r.NextDouble()), lean,
                    Grey(0.42f * shade), Grey(1.0f * shade), 1f);
            }
        }

        private static void TallGrass(Builder b, System.Random r)
        {
            GrassTuft(b, r, 9, 0.75f, 1.25f, 0.16f);
            var seed = new Color(0.86f, 0.78f, 0.52f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                var root = new Vector3(Mathf.Cos(a) * 0.06f, 0f, Mathf.Sin(a) * 0.06f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = Mathf.Lerp(1.15f, 1.45f, (float)r.NextDouble());
                b.Blade(root, dir, h, 0.025f, 0.25f, Grey(0.5f), Grey(0.85f), 1f);
                Vector3 top = root + dir * 0.25f + Vector3.up * h;
                b.Spindle(top, dir * 0.06f + Vector3.up * 0.2f, 0.04f, seed, 1f, 0f);
            }
        }

        private static void Reeds(Builder b, System.Random r)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = 0.2f * Mathf.Sqrt((float)r.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = Mathf.Lerp(1.1f, 1.8f, (float)r.NextDouble());
                b.Blade(root, dir, h, 0.045f, 0.12f, Grey(0.45f), Grey(0.95f), 1f);
            }
            var head = new Color(0.36f, 0.24f, 0.14f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.3f;
                var root = new Vector3(Mathf.Cos(a) * 0.08f, 0f, Mathf.Sin(a) * 0.08f);
                float h = 1.4f + i * 0.15f;
                b.Blade(root, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), h, 0.02f, 0.04f, Grey(0.5f), Grey(0.8f), 1f);
                b.Spindle(root + Vector3.up * (h - 0.05f), Vector3.up * 0.28f, 0.05f, head, 1f, 0f);
            }
        }

        private static void Flowers(Builder b, System.Random r)
        {
            var stem = new Color(0.24f, 0.44f, 0.17f, 0f);
            var centre = new Color(0.98f, 0.82f, 0.22f, 0f);
            // Leafy base so the patch does not float on bare ground.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.047f + (float)r.NextDouble() * 0.5f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Blade(dir * 0.03f, dir, 0.32f, 0.07f, 0.22f, stem * 0.7f, stem * 1.1f, 0.6f, 0f);
            }
            int heads = 5;
            for (int i = 0; i < heads; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = 0.22f * Mathf.Sqrt((float)r.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Mathf.Lerp(0.45f, 0.85f, (float)r.NextDouble());
                var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f;
                Vector3 top = root + lean + Vector3.up * h;
                b.Blade(root, lean.normalized, h, 0.022f, 0.08f, stem * 0.8f, stem, 1f, 0f);
                float pr = Mathf.Lerp(0.09f, 0.14f, (float)r.NextDouble());
                int petals = 5;
                float spin = (float)r.NextDouble() * 6.28f;
                for (int p = 0; p < petals; p++)
                {
                    float pa = spin + p * Mathf.PI * 2f / petals;
                    float pb = pa + Mathf.PI * 2f / petals * 0.5f;
                    Vector3 tip = top + new Vector3(Mathf.Cos(pa) * pr, 0.03f, Mathf.Sin(pa) * pr);
                    Vector3 side = top + new Vector3(Mathf.Cos(pb) * pr * 0.55f, 0.01f, Mathf.Sin(pb) * pr * 0.55f);
                    b.Tri(top, tip, side, Grey(1f), Grey(0.92f), Grey(0.96f),
                        new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector3.up);
                    float pc = pa - Mathf.PI * 2f / petals * 0.5f;
                    Vector3 side2 = top + new Vector3(Mathf.Cos(pc) * pr * 0.55f, 0.01f, Mathf.Sin(pc) * pr * 0.55f);
                    b.Tri(top, side2, tip, Grey(1f), Grey(0.96f), Grey(0.92f),
                        new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector3.up);
                }
                b.Solid(Ico(0), top + Vector3.up * 0.035f, new Vector3(0.035f, 0.025f, 0.035f), Quaternion.identity,
                    centre, 1f, 0f, null, 0f);
            }
        }

        private static void Fern(Builder b, System.Random r)
        {
            int fronds = 9;
            for (int i = 0; i < fronds; i++)
            {
                float a = i * Mathf.PI * 2f / fronds + (float)r.NextDouble() * 0.4f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x);
                float len = Mathf.Lerp(0.75f, 1.05f, (float)r.NextDouble());
                // Arching frond: 4 segments rising then drooping, leaflets as a zig-zag strip.
                Vector3 prev = Vector3.zero;
                float prevW = 0.02f;
                for (int s = 1; s <= 4; s++)
                {
                    float t = s / 4f;
                    Vector3 p = dir * (len * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI * 0.85f) * 0.55f * len);
                    float w = Mathf.Lerp(0.16f, 0.03f, t) * len;
                    float shade0 = Mathf.Lerp(0.5f, 1f, (s - 1) / 4f);
                    float shade1 = Mathf.Lerp(0.5f, 1f, t);
                    var n = Vector3.Cross(p - prev, side).normalized;
                    if (n.y < 0f) n = -n;
                    n = Vector3.Lerp(n, Vector3.up, 0.5f).normalized;
                    var wa = new Vector2((s - 1) / 4f, 1f);
                    var wb = new Vector2(t, 1f);
                    b.Quad(prev - side * prevW, prev + side * prevW, p + side * w, p - side * w,
                        Grey(shade0), Grey(shade0), Grey(shade1), Grey(shade1), wa, wa, wb, wb, n);
                    prev = p;
                    prevW = w;
                }
            }
        }

        private static void Bush(Builder b, System.Random r)
        {
            int lobes = 6;
            for (int i = 0; i < lobes; i++)
            {
                float a = i * 1.1f + (float)r.NextDouble();
                float d = i == 0 ? 0f : Mathf.Lerp(0.18f, 0.36f, (float)r.NextDouble());
                float rad = i == 0 ? 0.42f : Mathf.Lerp(0.24f, 0.34f, (float)r.NextDouble());
                var c = new Vector3(Mathf.Cos(a) * d, rad * 0.85f + (i == 0 ? 0.12f : 0f), Mathf.Sin(a) * d);
                b.Solid(Ico(1), c, new Vector3(rad, rad * 0.85f, rad), Quaternion.Euler(0f, a * 57f, 0f),
                    Grey(1f), 0f, 1f, r, 0.18f, shadeByHeight: true, windTop: 0.25f);
            }
            // A few darker twigs poking out at the base.
            var twig = new Color(0.32f, 0.22f, 0.14f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.2f;
                var dir = new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)).normalized;
                b.Spindle(dir * 0.3f, dir * 0.25f, 0.02f, twig, 0f, 0f);
            }
        }

        private static void Mushrooms(Builder b, System.Random r)
        {
            var stem = new Color(0.92f, 0.88f, 0.80f, 0f);
            var spot = new Color(0.97f, 0.95f, 0.9f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = 0.28f * Mathf.Sqrt((float)r.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Mathf.Lerp(0.18f, 0.42f, (float)r.NextDouble());
                float cap = Mathf.Lerp(0.09f, 0.18f, (float)r.NextDouble());
                b.Cylinder(root, root + Vector3.up * h, cap * 0.3f, cap * 0.25f, 6, stem, 0f, 0f);
                // Cap: a squat cone with a rim.
                Vector3 top = root + Vector3.up * h;
                b.Cone(top - Vector3.up * 0.02f, top + Vector3.up * cap * 0.75f, cap, 7, Grey(1f), 0f, 1f);
                if (i % 2 == 0)
                    b.Solid(Ico(0), top + new Vector3(cap * 0.3f, cap * 0.45f, 0f), Vector3.one * cap * 0.14f,
                        Quaternion.identity, spot, 0f, 0f, null, 0f);
            }
        }

        private static void Twigs(Builder b, System.Random r)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI);
                var c = new Vector3((float)(r.NextDouble() - 0.5) * 0.6f, 0.025f, (float)(r.NextDouble() - 0.5) * 0.6f);
                var half = new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * Mathf.Lerp(0.2f, 0.45f, (float)r.NextDouble());
                b.Cylinder(c - half, c + half, 0.025f, 0.018f, 5, Grey(Mathf.Lerp(0.75f, 1f, (float)r.NextDouble())), 0f, 1f);
            }
            // Leaf litter: flat scraps.
            for (int i = 0; i < 10; i++)
            {
                var c = new Vector3((float)(r.NextDouble() - 0.5) * 0.9f, 0.01f, (float)(r.NextDouble() - 0.5) * 0.9f);
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                var u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.07f;
                var v = new Vector3(-u.z, 0f, u.x) * 0.6f;
                b.Tri(c - u, c + v, c + u, Grey(0.9f), Grey(0.8f), Grey(0.95f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector3.up);
            }
        }

        // ------------------------------------------------------------------ minerals

        private static void Stones(Builder b, System.Random r, int count, float spread, float sMin, float sMax,
            float flatten, float jitter)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = spread * Mathf.Sqrt((float)r.NextDouble());
                float s = Mathf.Lerp(sMin, sMax, (float)(r.NextDouble() * r.NextDouble()));
                var c = new Vector3(Mathf.Cos(a) * d, s * flatten * 0.45f, Mathf.Sin(a) * d);
                var size = new Vector3(s * Mathf.Lerp(0.8f, 1.3f, (float)r.NextDouble()), s * flatten,
                    s * Mathf.Lerp(0.7f, 1.1f, (float)r.NextDouble()));
                var rot = Quaternion.Euler((float)r.NextDouble() * 25f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 25f);
                b.Solid(Ico(0), c, size, rot, Grey(Mathf.Lerp(0.72f, 1.05f, (float)r.NextDouble())), 0f, 1f, r, jitter);
            }
        }

        private static void Boulder(Builder b, System.Random r)
        {
            b.Solid(Ico(1), new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.52f, 0.5f),
                Quaternion.Euler(8f, 20f, 4f), Grey(1f), 0f, 1f, r, 0.22f, shadeByHeight: true);
            b.Solid(Ico(0), new Vector3(0.55f, 0.12f, 0.25f), new Vector3(0.22f, 0.16f, 0.2f),
                Quaternion.Euler(0f, 40f, 12f), Grey(0.9f), 0f, 1f, r, 0.2f);
            b.Solid(Ico(0), new Vector3(-0.35f, 0.08f, -0.45f), new Vector3(0.15f, 0.1f, 0.14f),
                Quaternion.Euler(10f, 70f, 0f), Grey(0.85f), 0f, 1f, r, 0.2f);
        }

        private static void Crystals(Builder b, System.Random r)
        {
            b.Solid(Ico(0), new Vector3(0f, 0.08f, 0f), new Vector3(0.32f, 0.16f, 0.3f), Quaternion.identity,
                new Color(0.30f, 0.29f, 0.31f, 0f), 0f, 0f, r, 0.25f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f + (float)r.NextDouble() * 0.5f;
                float tilt = i == 0 ? 4f : Mathf.Lerp(18f, 42f, (float)r.NextDouble());
                float len = i == 0 ? 1.0f : Mathf.Lerp(0.4f, 0.8f, (float)r.NextDouble());
                float rad = i == 0 ? 0.12f : Mathf.Lerp(0.06f, 0.1f, (float)r.NextDouble());
                var rot = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                var root = new Vector3(Mathf.Cos(a) * 0.08f, 0.02f, Mathf.Sin(a) * 0.08f);
                b.Prism(root, rot, rad, len, len * 0.3f, 6, Grey(Mathf.Lerp(0.85f, 1f, (float)r.NextDouble())), 0.35f);
            }
        }

        private static void IceShards(Builder b, System.Random r)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = 0.35f * Mathf.Sqrt((float)r.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float len = Mathf.Lerp(0.45f, 1.1f, (float)r.NextDouble());
                // Penitentes lean together toward the (fixed) sun.
                var rot = Quaternion.Euler(Mathf.Lerp(-8f, 14f, (float)r.NextDouble()), 35f, Mathf.Lerp(-6f, 6f, (float)r.NextDouble()));
                b.Prism(root, rot, Mathf.Lerp(0.07f, 0.13f, (float)r.NextDouble()), len * 0.35f, len * 0.65f, 4,
                    Grey(Mathf.Lerp(0.85f, 1.05f, (float)r.NextDouble())), 0.04f);
            }
            b.Solid(Ico(0), new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.08f, 0.4f), Quaternion.identity,
                Grey(0.95f), 0f, 1f, r, 0.2f);
        }

        // ------------------------------------------------------------------ helpers

        private static Color Grey(float v) => new Color(v, v, v, 0f);

        private static (Vector3[] v, int[] t)[] _ico = new (Vector3[] v, int[] t)[2];

        /// <summary>Unit icosphere (0 = icosahedron, 1 = one subdivision).</summary>
        private static (Vector3[] v, int[] t) Ico(int subdiv)
        {
            subdiv = Mathf.Clamp(subdiv, 0, 1);
            if (_ico[subdiv].v != null) return _ico[subdiv];
            float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var t = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            if (subdiv == 1)
            {
                var mid = new Dictionary<long, int>();
                int Mid(int a, int c)
                {
                    long key = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[c]) * 0.5f).normalized);
                    mid[key] = v.Count - 1;
                    return v.Count - 1;
                }
                var t2 = new List<int>(t.Count * 4);
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], bb = t[i + 1], c = t[i + 2];
                    int ab = Mid(a, bb), bc = Mid(bb, c), ca = Mid(c, a);
                    t2.AddRange(new[] { a, ab, ca, bb, bc, ab, c, ca, bc, ab, bc, ca });
                }
                t = t2;
            }
            _ico[subdiv] = (v.ToArray(), t.ToArray());
            return _ico[subdiv];
        }

        private sealed class Builder
        {
            private readonly List<Vector3> _v = new List<Vector3>(512);
            private readonly List<Vector3> _n = new List<Vector3>(512);
            private readonly List<Color> _c = new List<Color>(512);
            private readonly List<Vector2> _u = new List<Vector2>(512);
            private readonly List<int> _t = new List<int>(1024);

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc,
                Vector2 ua, Vector2 ub, Vector2 uc, Vector3 normal)
            {
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _n.Add(normal); _n.Add(normal); _n.Add(normal);
                _c.Add(ca); _c.Add(cb); _c.Add(cc);
                _u.Add(ua); _u.Add(ub); _u.Add(uc);
                _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd,
                Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
            {
                Tri(a, b, c, ca, cb, cc, ua, ub, uc, normal);
                Tri(a, c, d, ca, cc, cd, ua, uc, ud, normal);
            }

            /// <summary>A tapered, bent blade (5 vertices). Normals lean up so it shades like the ground.</summary>
            public void Blade(Vector3 root, Vector3 outward, float height, float width, float lean,
                Color baseCol, Color tipCol, float wind, float tintMask = 1f)
            {
                outward.y = 0f;
                if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
                outward.Normalize();
                var side = new Vector3(-outward.z, 0f, outward.x);
                Vector3 mid = root + outward * (lean * 0.35f) + Vector3.up * (height * 0.55f);
                Vector3 tip = root + outward * lean + Vector3.up * height;
                Color midCol = Color.Lerp(baseCol, tipCol, 0.55f);
                var n = Vector3.Lerp(Vector3.Cross(side, tip - root).normalized, Vector3.up, 0.65f).normalized;
                if (n.y < 0f) n = -n;
                var u0 = new Vector2(0f, tintMask);
                var u1 = new Vector2(0.35f * wind, tintMask);
                var u2 = new Vector2(wind, tintMask);
                Tri(root - side * width, mid - side * width * 0.6f, root + side * width, baseCol, midCol, baseCol, u0, u1, u0, n);
                Tri(root + side * width, mid - side * width * 0.6f, mid + side * width * 0.6f, baseCol, midCol, midCol, u0, u1, u1, n);
                Tri(mid - side * width * 0.6f, tip, mid + side * width * 0.6f, midCol, tipCol, midCol, u1, u2, u1, n);
            }

            /// <summary>Thin 3-sided spindle from <paramref name="from"/> along <paramref name="dir"/>.</summary>
            public void Spindle(Vector3 from, Vector3 dir, float radius, Color col, float wind, float tintMask)
            {
                Vector3 axis = dir.normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(axis, side);
                Vector3 mid = from + dir * 0.5f;
                Vector3 tip = from + dir;
                var u = new Vector2(wind, tintMask);
                for (int k = 0; k < 3; k++)
                {
                    float a0 = k * Mathf.PI * 2f / 3f, a1 = (k + 1) * Mathf.PI * 2f / 3f;
                    Vector3 o0 = (side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0)) * radius;
                    Vector3 o1 = (side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1)) * radius;
                    Vector3 n = ((o0 + o1) * 0.5f).normalized;
                    Tri(from, mid + o1, mid + o0, col, col, col, u, u, u, n);
                    Tri(mid + o0, mid + o1, tip, col, col, col, u, u, u, n);
                }
            }

            public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, int sides, Color col, float wind, float tintMask)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(axis, side);
                var u = new Vector2(wind, tintMask);
                for (int k = 0; k < sides; k++)
                {
                    float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0);
                    Vector3 d1 = side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1);
                    Vector3 n = ((d0 + d1) * 0.5f).normalized;
                    Quad(a + d0 * ra, a + d1 * ra, b + d1 * rb, b + d0 * rb, col, col, col, col, u, u, u, u, n);
                }
            }

            public void Cone(Vector3 baseCentre, Vector3 apex, float radius, int sides, Color col, float wind, float tintMask)
            {
                var u = new Vector2(wind, tintMask);
                Color under = col * 0.7f;
                under.a = col.a;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                    Vector3 p0 = baseCentre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                    Vector3 p1 = baseCentre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                    Vector3 n = Vector3.Cross(p1 - apex, p0 - apex).normalized;
                    if (n.y < 0f) n = -n;
                    Tri(apex, p1, p0, col, col, col, u, u, u, n);
                    Tri(baseCentre, p0, p1, under, under, under, u, u, u, Vector3.down);
                }
            }

            /// <summary>Hexagonal (or n-sided) prism with a pointed tip, rotated about its root.</summary>
            public void Prism(Vector3 root, Quaternion rot, float radius, float length, float tipLength, int sides,
                Color col, float glow)
            {
                col.a = glow;
                var u = new Vector2(0f, 1f);
                Vector3 tip = root + rot * (Vector3.up * (length + tipLength));
                for (int k = 0; k < sides; k++)
                {
                    float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                    Vector3 o0 = rot * new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                    Vector3 o1 = rot * new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                    Vector3 top = rot * (Vector3.up * length);
                    Vector3 n = ((o0 + o1) * 0.5f).normalized;
                    Color dark = col * 0.78f;
                    dark.a = glow;
                    Quad(root + o0, root + o1, root + top + o1, root + top + o0, dark, dark, col, col, u, u, u, u, n);
                    Vector3 tn = Vector3.Cross(root + top + o1 - tip, root + top + o0 - tip).normalized;
                    if (Vector3.Dot(tn, n) < 0f) tn = -tn;
                    Color bright = col * 1.12f;
                    bright.a = glow;
                    Tri(root + top + o0, root + top + o1, tip, col, col, bright, u, u, u, tn);
                }
            }

            /// <summary>Faceted convex solid from a unit icosphere: scaled, rotated, optionally jittered.</summary>
            public void Solid((Vector3[] v, int[] t) shape, Vector3 centre, Vector3 size, Quaternion rot, Color col,
                float wind, float tintMask, System.Random jitterRng, float jitter, bool shadeByHeight = false,
                float windTop = 0f)
            {
                var pts = new Vector3[shape.v.Length];
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector3 p = shape.v[i];
                    if (jitterRng != null && jitter > 0f)
                        p *= 1f + ((float)jitterRng.NextDouble() - 0.5f) * 2f * jitter;
                    pts[i] = centre + rot * Vector3.Scale(p, size);
                }
                float minY = centre.y - size.y, maxY = centre.y + size.y;
                for (int i = 0; i < shape.t.Length; i += 3)
                {
                    Vector3 a = pts[shape.t[i]], b = pts[shape.t[i + 1]], c = pts[shape.t[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                    Vector3 fc = (a + b + c) / 3f;
                    if (Vector3.Dot(n, fc - centre) < 0f) { n = -n; (b, c) = (c, b); }
                    Color ca = col, cb = col, cc = col;
                    if (shadeByHeight)
                    {
                        ca = Shade(col, a.y, minY, maxY);
                        cb = Shade(col, b.y, minY, maxY);
                        cc = Shade(col, c.y, minY, maxY);
                    }
                    float wa = wind + windTop * Mathf.InverseLerp(minY, maxY, a.y);
                    float wb = wind + windTop * Mathf.InverseLerp(minY, maxY, b.y);
                    float wc = wind + windTop * Mathf.InverseLerp(minY, maxY, c.y);
                    Tri(a, b, c, ca, cb, cc, new Vector2(wa, tintMask), new Vector2(wb, tintMask), new Vector2(wc, tintMask), n);
                }
            }

            private static Color Shade(Color c, float y, float minY, float maxY)
            {
                float t = Mathf.InverseLerp(minY, maxY, y);
                float k = Mathf.Lerp(0.55f, 1.08f, t);
                return new Color(c.r * k, c.g * k, c.b * k, c.a);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (_v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetColors(_c);
                mesh.SetUVs(0, _u);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
