using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Dense detail pass for the core buildings: panel lines, vents, pipes, antennas, running
    /// lights, lit windows and trim on the white/black/orange industrial look. The locked
    /// silhouettes (Commons dome citadel, boxy tan HAB) keep every original part; the detail is
    /// layered on top through <see cref="DetailBatch"/>, so it costs a few merged meshes.
    /// Workshop, Defense Battery, Watchtower and Market are full procedural builds.
    ///
    /// Every new part carries a <c>_S{n}_</c> stage token for <see cref="ConstructionStages"/>.
    /// Cameras sit at yaw 45°, so the -Z and -X faces are the ones the player sees first.
    /// </summary>
    public static partial class HeroBuildingKits
    {
        private const int SFound = ConstructionStages.Foundation;
        private const int SFrame = ConstructionStages.Frame;
        private const int SShell = ConstructionStages.Shell;
        private const int SFit = ConstructionStages.FitOut;

        private static readonly Color Cream = new Color(0.88f, 0.82f, 0.74f);
        private static readonly Color Cream2 = new Color(0.80f, 0.74f, 0.66f);
        private static readonly Color WarmGlass = new Color(0.95f, 0.78f, 0.52f);
        private static readonly Color WarmEmit = new Color(1.55f, 1.02f, 0.50f);
        private static readonly Color RedLamp = new Color(0.90f, 0.16f, 0.08f);
        private static readonly Color RedEmit = new Color(1.9f, 0.22f, 0.06f);
        private static readonly Color GreenEmit = new Color(0.25f, 1.4f, 0.45f);
        private static readonly Color AmberEmit = new Color(1.6f, 0.75f, 0.12f);
        private static readonly Color PanelTan = new Color(0.82f, 0.66f, 0.49f);

        // ------------------------------------------------------------------ face helpers

        /// <summary>A wall face: origin at ground level on the face, outward normal, viewer's right.</summary>
        private readonly struct Face
        {
            public readonly Vector3 O, R, N;
            public readonly Quaternion Rot;

            public Face(Vector3 origin, Vector3 normal)
            {
                O = new Vector3(origin.x, 0f, origin.z);
                N = normal.normalized;
                R = Vector3.Cross(N, Vector3.up).normalized;
                Rot = Quaternion.LookRotation(N, Vector3.up);
            }

            public Vector3 P(float x, float y, float o) => O + R * x + Vector3.up * y + N * o;
        }

        private static void FBox(DetailBatch k, in Face f, float x, float y, float w, float h, float depth, Color c, Color emit = default) =>
            k.Box(f.P(x, y, depth * 0.5f), new Vector3(w, h, depth), f.Rot, c, emit);

        private static void Window(DetailBatch k, in Face f, float x, float y, float w, float h, Color glass, Color glow)
        {
            FBox(k, f, x, y, w, h, 0.03f, glass, glow);
            FBox(k, f, x, y + h * 0.5f + 0.025f, w + 0.1f, 0.05f, 0.06f, Carbon);
            FBox(k, f, x, y - h * 0.5f - 0.03f, w + 0.14f, 0.06f, 0.08f, Carbon);
            FBox(k, f, x - w * 0.5f - 0.025f, y, 0.05f, h, 0.06f, Carbon);
            FBox(k, f, x + w * 0.5f + 0.025f, y, 0.05f, h, 0.06f, Carbon);
        }

        private static void Grille(DetailBatch k, in Face f, float x, float y, float w, float h)
        {
            FBox(k, f, x, y, w + 0.06f, h + 0.06f, 0.03f, Carbon);
            int n = Mathf.Max(2, Mathf.RoundToInt(h / 0.07f));
            for (int i = 0; i < n; i++)
                FBox(k, f, x, y - h * 0.5f + (i + 0.5f) * h / n, w, 0.026f, 0.05f, Steel);
        }

        private static void LightBar(DetailBatch k, in Face f, float x, float y, float w, Color emit)
        {
            FBox(k, f, x, y, w + 0.05f, 0.07f, 0.04f, Carbon);
            FBox(k, f, x, y, w, 0.034f, 0.055f, Cyan, emit);
        }

        private static void WallLamp(DetailBatch k, in Face f, float x, float y, Color emit)
        {
            FBox(k, f, x, y, 0.06f, 0.12f, 0.06f, Carbon);
            FBox(k, f, x, y + 0.06f, 0.16f, 0.05f, 0.15f, Carbon);
            k.Box(f.P(x, y + 0.03f, 0.1f), new Vector3(0.12f, 0.02f, 0.09f), f.Rot, Cream, emit);
        }

        private static void Door(DetailBatch k, in Face f, float x, float w, float h, Color leaf, float y0)
        {
            FBox(k, f, x, y0 + h * 0.5f + 0.02f, w + 0.18f, h + 0.1f, 0.05f, Carbon);
            FBox(k, f, x, y0 + h * 0.5f, w, h, 0.07f, leaf);
            FBox(k, f, x, y0 + h * 0.72f, w * 0.55f, 0.1f, 0.08f, WarmGlass, WarmEmit * 0.7f);
            FBox(k, f, x, y0 + h * 0.5f, 0.02f, h, 0.075f, Carbon);
            FBox(k, f, x + w * 0.5f + 0.14f, y0 + h * 0.45f, 0.08f, 0.14f, 0.06f, Graphite);
            FBox(k, f, x + w * 0.5f + 0.14f, y0 + h * 0.45f + 0.03f, 0.04f, 0.03f, 0.07f, Cyan, CyanEmit);
            Hazard(k, f.P(x - w * 0.5f, y0 + 0.012f, 0.18f), f.P(x + w * 0.5f, y0 + 0.012f, 0.18f), 0.14f, 0.02f);
        }

        private static void Hazard(DetailBatch k, Vector3 a, Vector3 b, float width, float height)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.2f));
            Quaternion rot = Quaternion.LookRotation(d / len, Vector3.up);
            for (int i = 0; i < n; i++)
                k.Box(Vector3.Lerp(a, b, (i + 0.5f) / n), new Vector3(width, height, len / n * 0.98f), rot,
                    i % 2 == 0 ? Yellow : Carbon);
        }

        private static void Railing(DetailBatch k, Vector3 a, Vector3 b, float h, Color c)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / n);
                k.Box(p + Vector3.up * h * 0.5f, new Vector3(0.03f, h, 0.03f), c);
            }
            k.Beam(a + Vector3.up * h, b + Vector3.up * h, 0.035f, c);
            k.Beam(a + Vector3.up * h * 0.5f, b + Vector3.up * h * 0.5f, 0.022f, c);
        }

        private static void Ladder(DetailBatch k, in Face f, float x, float y0, float y1)
        {
            FBox(k, f, x - 0.13f, (y0 + y1) * 0.5f, 0.03f, y1 - y0, 0.14f, Steel);
            FBox(k, f, x + 0.13f, (y0 + y1) * 0.5f, 0.03f, y1 - y0, 0.14f, Steel);
            for (float y = y0 + 0.15f; y < y1; y += 0.22f)
                k.Box(f.P(x, y, 0.1f), new Vector3(0.26f, 0.022f, 0.022f), f.Rot, Steel);
        }

        private static void Antenna(DetailBatch k, Vector3 p, float h)
        {
            k.Box(p + Vector3.up * 0.05f, new Vector3(0.16f, 0.1f, 0.16f), Graphite);
            k.Rod(p, p + Vector3.up * h, 0.035f, Steel);
            k.Box(p + Vector3.up * h * 0.62f, new Vector3(0.34f, 0.025f, 0.025f), Steel);
            k.Box(p + Vector3.up * h * 0.82f, new Vector3(0.22f, 0.025f, 0.025f), Steel);
            k.Box(p + Vector3.up * h * 0.72f, new Vector3(0.025f, 0.025f, 0.26f), Steel);
            k.Ball(p + Vector3.up * (h + 0.04f), 0.08f, RedLamp, RedEmit);
        }

        private static void Dish(DetailBatch k, Vector3 p, float dia, float yaw, float tilt)
        {
            k.Cyl(p + Vector3.up * 0.12f, 0.1f, 0.24f, Steel);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            Vector3 c = p + Vector3.up * (0.24f + dia * 0.3f);
            k.Box(p + Vector3.up * 0.26f, new Vector3(0.18f, 0.06f, 0.1f), Quaternion.Euler(0f, yaw, 0f), Graphite);
            k.Add(PrimitiveType.Sphere, c, new Vector3(dia, dia * 0.18f, dia), rot, White);
            k.Rod(c, c + rot * Vector3.up * dia * 0.45f, 0.02f, Steel);
            k.Ball(c + rot * Vector3.up * dia * 0.45f, 0.05f, Carbon);
        }

        private static void RoofUnit(DetailBatch k, Vector3 p, float sx, float sz, float h)
        {
            k.Box(p + Vector3.up * h * 0.5f, new Vector3(sx, h, sz), Cream2);
            k.Box(p + Vector3.up * (h + 0.01f), new Vector3(sx + 0.02f, 0.02f, sz + 0.02f), Carbon);
            float fan = Mathf.Min(sx, sz) * 0.7f;
            k.Cyl(p + Vector3.up * (h + 0.025f), fan, 0.02f, Graphite);
            k.Cyl(p + Vector3.up * (h + 0.035f), fan * 0.25f, 0.02f, Steel);
            k.Box(p + Vector3.up * (h + 0.03f), new Vector3(fan * 0.95f, 0.012f, 0.02f), Steel);
            k.Box(p + Vector3.up * (h + 0.03f), new Vector3(0.02f, 0.012f, fan * 0.95f), Steel);
            for (int i = 0; i < 3; i++)
                k.Box(p + new Vector3(-sx * 0.5f - 0.01f, h * (0.25f + i * 0.22f), 0f), new Vector3(0.02f, 0.03f, sz * 0.8f), Carbon);
        }

        private static void Tank(DetailBatch k, Vector3 p, float dia, float h, Color c)
        {
            k.Cyl(p + Vector3.up * (h * 0.5f), dia, h, c);
            k.Ball(p + Vector3.up * h, new Vector3(dia, dia * 0.4f, dia), c);
            k.Cyl(p + Vector3.up * (h * 0.25f), dia + 0.03f, 0.05f, Carbon);
            k.Cyl(p + Vector3.up * (h * 0.75f), dia + 0.03f, 0.05f, Carbon);
            k.Cyl(p + Vector3.up * 0.04f, dia + 0.08f, 0.08f, Graphite);
        }

        private static void NavLight(DetailBatch k, Vector3 p, Color emit)
        {
            k.Cyl(p + Vector3.up * 0.06f, 0.05f, 0.12f, Carbon);
            k.Ball(p + Vector3.up * 0.14f, 0.08f, emit == RedEmit ? RedLamp : Cyan, emit);
        }

        private static void PipeRun(DetailBatch k, Vector3[] pts, float dia, Color c)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                k.Rod(pts[i], pts[i + 1], dia, c);
                if (i > 0) k.Ball(pts[i], dia * 1.25f, c);
                float len = Vector3.Distance(pts[i], pts[i + 1]);
                int clamps = Mathf.FloorToInt(len / 0.6f);
                for (int j = 1; j <= clamps; j++)
                {
                    Vector3 at = Vector3.Lerp(pts[i], pts[i + 1], j / (clamps + 1f));
                    k.Rod(at - (pts[i + 1] - pts[i]).normalized * 0.02f, at + (pts[i + 1] - pts[i]).normalized * 0.02f, dia * 1.5f, Carbon);
                }
            }
        }

        private static void Greebles(DetailBatch k, Vector3 c, float hx, float hz, int n, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * hx;
                float z = (float)(rng.NextDouble() * 2 - 1) * hz;
                float sx = 0.12f + (float)rng.NextDouble() * 0.3f;
                float sz = 0.12f + (float)rng.NextDouble() * 0.3f;
                float sy = 0.05f + (float)rng.NextDouble() * 0.16f;
                Color col = (i % 3) switch { 0 => Steel, 1 => Graphite, _ => Cream2 };
                k.Box(c + new Vector3(x, sy * 0.5f, z), new Vector3(sx, sy, sz), col);
                if (i % 4 == 0)
                    k.Box(c + new Vector3(x, sy + 0.012f, z), new Vector3(sx * 0.6f, 0.024f, sz * 0.6f), Carbon);
            }
        }

        /// <summary>Thin cell-grid lines across a tilted PV panel.</summary>
        private static void CellLines(DetailBatch k, Vector3 center, float cw, float cd, Quaternion tilt, float lift)
        {
            Color line = new Color(0.36f, 0.44f, 0.58f);
            for (int i = 1; i < 4; i++)
            {
                float x = -cw * 0.5f + cw * i / 4f;
                k.Box(center + tilt * new Vector3(x, lift, 0f), new Vector3(0.014f, 0.008f, cd * 0.98f), tilt, line);
            }
            for (int i = 1; i < 3; i++)
            {
                float z = -cd * 0.5f + cd * i / 3f;
                k.Box(center + tilt * new Vector3(0f, lift, z), new Vector3(cw * 0.98f, 0.008f, 0.014f), tilt, line);
            }
        }

        private static void Arc(DetailBatch k, Vector3 center, Vector3 across, float rx, float ry, float thick, Color c, int seg = 10)
        {
            Vector3 prev = center + across * rx;
            for (int s = 1; s <= seg; s++)
            {
                float a = Mathf.PI * s / seg;
                Vector3 pt = center + across * (rx * Mathf.Cos(a)) + Vector3.up * (ry * Mathf.Sin(a));
                k.Beam(prev, pt, thick, c);
                prev = pt;
            }
        }

        private static string Key(string kit, float w, float d) => $"{kit}_{w:0.00}x{d:0.00}";

        // ------------------------------------------------------------------ HAB

        private static void DetailHabitat(Transform root, float w, float d)
        {
            float bx = w * HabBoxFill, bz = d * HabBoxFill;
            float hx = bx * 0.5f, hz = bz * 0.5f;
            const float y0 = 0.18f, top = 0.18f + 2.28f;
            var k = new DetailBatch(root, Key("Hab", w, d));

            // Foundation: plinth hazard edge, service pads.
            k.Stage = SFound;
            Hazard(k, new Vector3(-hx * 1.05f, 0.165f, -hz * 1.05f), new Vector3(hx * 1.05f, 0.165f, -hz * 1.05f), 0.07f, 0.012f);
            Hazard(k, new Vector3(-hx * 1.05f, 0.165f, -hz * 1.05f), new Vector3(-hx * 1.05f, 0.165f, hz * 1.05f), 0.07f, 0.012f);
            k.Box(new Vector3(-hx - 0.55f, 0.1f, 0f), new Vector3(0.9f, 0.2f, 1.3f), Concrete);
            k.Box(new Vector3(-hx - 0.55f, 0.21f, 0f), new Vector3(0.84f, 0.02f, 1.24f), Graphite);

            // Frame: exoskeleton ribs and corner columns.
            k.Stage = SFrame;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.Box(new Vector3(sx * hx, (0.16f + top) * 0.5f, sz * hz), new Vector3(0.22f, top - 0.16f + 0.06f, 0.22f), Carbon);
            foreach (float rx in new[] { -0.74f, 0.74f })
            {
                k.Box(new Vector3(rx, (0.16f + top) * 0.5f, -hz - 0.04f), new Vector3(0.13f, top - 0.16f, 0.08f), Carbon);
                k.Box(new Vector3(rx, (0.16f + top) * 0.5f, hz + 0.04f), new Vector3(0.13f, top - 0.16f, 0.08f), Carbon);
            }
            foreach (float rz in new[] { -0.9f, 0.9f })
            {
                k.Box(new Vector3(-hx - 0.04f, (0.16f + top) * 0.5f, rz), new Vector3(0.08f, top - 0.16f, 0.13f), Carbon);
                k.Box(new Vector3(hx + 0.04f, (0.16f + top) * 0.5f, rz), new Vector3(0.08f, top - 0.16f, 0.13f), Carbon);
            }

            // Shell: raised cladding panels between ribs, roof parapet.
            k.Stage = SShell;
            var front = new Face(new Vector3(0f, 0f, -hz), Vector3.back);
            var back = new Face(new Vector3(0f, 0f, hz), Vector3.forward);
            var left = new Face(new Vector3(-hx, 0f, 0f), Vector3.left);
            var right = new Face(new Vector3(hx, 0f, 0f), Vector3.right);
            float[] zBays = { -(hx + 0.74f) * 0.5f, 0f, (hx + 0.74f) * 0.5f };
            float bayW = hx - 0.74f - 0.22f;
            foreach (var f in new[] { front, back })
                for (int i = 0; i < 3; i++)
                {
                    float wBay = i == 1 ? 1.26f : bayW;
                    Panel(k, f, zBays[i], 0.72f, wBay, 0.82f);
                    Panel(k, f, zBays[i], 2.14f, wBay, 0.38f);
                }
            float sideBay = hz - 0.9f - 0.22f;
            foreach (var f in new[] { left, right })
            {
                Panel(k, f, -(hz + 0.9f) * 0.5f, 1.25f, sideBay, 1.8f);
                Panel(k, f, (hz + 0.9f) * 0.5f, 1.25f, sideBay, 1.8f);
                Panel(k, f, 0f, 2.2f, 1.5f, 0.3f);
            }
            k.Box(new Vector3(0f, top + 0.07f, -hz + 0.05f), new Vector3(bx, 0.14f, 0.1f), Carbon);
            k.Box(new Vector3(0f, top + 0.07f, hz - 0.05f), new Vector3(bx, 0.14f, 0.1f), Carbon);
            k.Box(new Vector3(-hx + 0.05f, top + 0.07f, 0f), new Vector3(0.1f, 0.14f, bz), Carbon);
            k.Box(new Vector3(hx - 0.05f, top + 0.07f, 0f), new Vector3(0.1f, 0.14f, bz), Carbon);

            // Fit-out.
            k.Stage = SFit;
            float py = y0 + 2.28f * 0.5f + 0.22f;
            for (int i = 0; i < 3; i++)
            {
                float px = (i - 1) * bx * 0.28f;
                foreach (float s in new[] { -1f, 1f })
                {
                    k.Cyl(new Vector3(px, py, s * (hz + 0.105f)), 0.25f, 0.02f, Quaternion.Euler(90f, 0f, 0f), WarmGlass, WarmEmit);
                    k.Box(new Vector3(px, py + 0.26f, s * (hz + 0.07f)), new Vector3(0.5f, 0.04f, 0.14f), Carbon);
                }
            }
            // Street-side door on -X with canopy, sign, lamps and steps.
            Door(k, left, 0f, 0.72f, 1.2f, Orange, 0.22f);
            FBox(k, left, 0f, 1.62f, 1.2f, 0.05f, 0.5f, Carbon);
            LightBar(k, left, 0f, 1.72f, 0.8f, CyanEmit);
            WallLamp(k, left, -0.62f, 1.3f, WarmEmit);
            WallLamp(k, left, 0.62f, 1.3f, WarmEmit);
            k.Box(new Vector3(-hx - 0.3f, 0.26f, 0f), new Vector3(0.36f, 0.08f, 0.9f), Concrete);
            // Vents, conduits, service cabinet.
            Grille(k, front, 0f, 0.55f, 0.8f, 0.28f);
            Grille(k, back, 0f, 0.55f, 0.8f, 0.28f);
            Grille(k, right, -(hz + 0.9f) * 0.5f, 0.5f, 0.7f, 0.24f);
            FBox(k, left, (hz + 0.9f) * 0.5f, 0.75f, 0.5f, 0.66f, 0.16f, Graphite);
            FBox(k, left, (hz + 0.9f) * 0.5f, 1.0f, 0.36f, 0.05f, 0.17f, Orange);
            FBox(k, left, (hz + 0.9f) * 0.5f - 0.14f, 0.62f, 0.05f, 0.05f, 0.17f, Cyan, GreenEmit);
            FBox(k, left, (hz + 0.9f) * 0.5f - 0.04f, 0.62f, 0.05f, 0.05f, 0.17f, Cyan, CyanEmit);
            PipeRun(k, new[]
            {
                new Vector3(-hx - 0.12f, 0.2f, -hz + 0.36f), new Vector3(-hx - 0.12f, top - 0.2f, -hz + 0.36f),
                new Vector3(-hx - 0.12f, top - 0.2f, -hz + 1.2f), new Vector3(-hx + 0.2f, top + 0.12f, -hz + 1.2f)
            }, 0.07f, Steel);
            PipeRun(k, new[]
            {
                new Vector3(hx + 0.1f, 0.35f, -hz + 0.4f), new Vector3(hx + 0.1f, 0.35f, hz - 0.4f)
            }, 0.09f, Orange);
            PipeRun(k, new[]
            {
                new Vector3(hx + 0.1f, 0.5f, -hz + 0.4f), new Vector3(hx + 0.1f, 0.5f, hz - 0.4f)
            }, 0.06f, Steel);
            LightBar(k, front, 0f, 0.98f, bx * 0.8f, CyanEmit * 0.8f);
            // Roof: HVAC strip on the front, tank + mast + hatch on the back strip.
            float roofZf = -hz + 0.36f;
            RoofUnit(k, new Vector3(-hx * 0.55f, top, roofZf), 0.55f, 0.42f, 0.26f);
            RoofUnit(k, new Vector3(0f, top, roofZf), 0.55f, 0.42f, 0.26f);
            RoofUnit(k, new Vector3(hx * 0.55f, top, roofZf), 0.55f, 0.42f, 0.26f);
            float roofZb = hz - 0.58f;
            k.Cyl(new Vector3(-hx * 0.45f, top + 0.3f, roofZb), 0.5f, 1.2f, Quaternion.Euler(0f, 0f, 90f), White);
            k.Cyl(new Vector3(-hx * 0.45f, top + 0.3f, roofZb), 0.53f, 0.05f, Quaternion.Euler(0f, 0f, 90f), Carbon);
            k.Box(new Vector3(-hx * 0.45f - 0.4f, top + 0.08f, roofZb), new Vector3(0.1f, 0.16f, 0.44f), Graphite);
            k.Box(new Vector3(-hx * 0.45f + 0.4f, top + 0.08f, roofZb), new Vector3(0.1f, 0.16f, 0.44f), Graphite);
            k.Box(new Vector3(hx * 0.2f, top + 0.05f, roofZb), new Vector3(0.6f, 0.1f, 0.6f), Graphite);
            k.Box(new Vector3(hx * 0.2f, top + 0.11f, roofZb), new Vector3(0.5f, 0.03f, 0.5f), Orange);
            Antenna(k, new Vector3(hx - 0.35f, top, hz - 0.35f), 1.5f);
            Dish(k, new Vector3(hx * 0.62f, top, roofZb + 0.1f), 0.5f, 225f, -30f);
            Railing(k, new Vector3(-hx + 0.15f, top + 0.14f, hz - 0.15f), new Vector3(hx - 0.15f, top + 0.14f, hz - 0.15f), 0.3f, Yellow);
            NavLight(k, new Vector3(-hx + 0.1f, top + 0.14f, -hz + 0.1f), RedEmit);
            NavLight(k, new Vector3(hx - 0.1f, top + 0.14f, -hz + 0.1f), CyanEmit);
            NavLight(k, new Vector3(-hx + 0.1f, top + 0.14f, hz - 0.1f), CyanEmit);
            // PV cell grid lines on the roof array (same layout as BuildHabitat).
            float roofY = y0 + 2.28f;
            Quaternion tilt = Quaternion.Euler(-28f, 0f, 0f);
            float cellW = bx * 0.18f, cellD = bz * 0.22f, pitchX = bx * 0.21f, pitchZ = bz * 0.24f;
            float ox = -pitchX * 1.5f, oz = -bz * 0.08f - pitchZ * 0.5f;
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 4; c++)
                {
                    Vector3 cell = new Vector3(ox + c * pitchX, roofY + 0.18f, oz + r * pitchZ) + new Vector3(0f, 0.03f, 0f);
                    CellLines(k, cell, cellW, cellD, tilt, 0.02f);
                    k.Box(cell + new Vector3(0f, -0.14f, cellD * 0.3f), new Vector3(0.05f, 0.2f, 0.05f), Steel);
                }
            k.Build();
        }

        private static void Panel(DetailBatch k, in Face f, float x, float y, float w, float h)
        {
            if (w <= 0.05f || h <= 0.05f) return;
            FBox(k, f, x, y, w, h, 0.03f, PanelTan);
            FBox(k, f, x, y + h * 0.5f - 0.03f, w * 0.96f, 0.02f, 0.04f, new Color(0.60f, 0.46f, 0.33f));
        }

        // ------------------------------------------------------------------ COMMONS

        private static void DetailCommons(Transform root, float w, float d, Color hull)
        {
            float r = Mathf.Min(w, d) * 0.38f;
            float R = r * 1.22f;
            float domeTop = HeroBuildingKits.CommonsCrownY - 0.36f;
            var k = new DetailBatch(root, Key("Commons", w, d));
            float[] portals = { 180f, 270f };

            // Foundation: paved apron and entry stairs.
            k.Stage = SFound;
            k.Cyl(new Vector3(0f, 0.015f, 0f), R * 2f + 0.8f, 0.03f, PadDeck);
            foreach (float a in portals)
            {
                Vector3 dir = Dir(a);
                Quaternion yaw = Quaternion.Euler(0f, a, 0f);
                for (int s = 0; s < 3; s++)
                {
                    float y = 0.56f - 0.18f * (s + 1);
                    Vector3 at = dir * (R + 0.18f + s * 0.34f);
                    k.Box(at + Vector3.up * (y * 0.5f + 0.045f), new Vector3(1.6f, y + 0.09f, 0.34f), yaw, Concrete);
                    k.Box(at + Vector3.up * (y + 0.1f) - dir * 0.14f, new Vector3(1.6f, 0.012f, 0.05f), yaw, Yellow);
                }
            }

            // Frame: eight flying buttresses from the plinth to the drum crown.
            k.Stage = SFrame;
            for (int i = 0; i < 8; i++)
            {
                float a = 22.5f + i * 45f;
                Vector3 dir = Dir(a);
                Quaternion yaw = Quaternion.Euler(0f, a, 0f);
                Vector3 foot = dir * (R * 0.9f) + Vector3.up * 0.56f;
                Vector3 head = dir * (r * 1.02f) + Vector3.up * 1.66f;
                k.Box(foot + Vector3.up * 0.06f, new Vector3(0.34f, 0.12f, 0.4f), yaw, Graphite);
                k.Beam(foot, head, 0.17f, Carbon);
                k.Beam(foot + dir * -0.05f + Vector3.up * 0.3f, dir * (r * 1.01f) + Vector3.up * 0.9f, 0.08f, Graphite);
                k.Box(head, new Vector3(0.26f, 0.12f, 0.26f), yaw, Graphite);
            }

            // Shell: drum cladding between the meridians, window band backing, entry vestibules.
            k.Stage = SShell;
            for (int i = 0; i < 12; i++)
                foreach (float off in new[] { 9f, 21f })
                {
                    float a = i * 30f + off;
                    if (NearPortal(a, portals, 14f)) continue;
                    Quaternion yaw = Quaternion.Euler(0f, a, 0f);
                    k.Box(Dir(a) * (r + 0.012f) + Vector3.up * 0.98f, new Vector3(0.58f, 0.52f, 0.03f), yaw, Cream2);
                    k.Box(Dir(a) * (r + 0.02f) + Vector3.up * 1.2f, new Vector3(0.5f, 0.03f, 0.03f), yaw, Carbon);
                }
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f + 7.5f;
                k.Box(Dir(a) * (r + 0.01f) + Vector3.up * 1.54f, new Vector3(0.84f, 0.2f, 0.03f), Quaternion.Euler(0f, a, 0f), Carbon);
            }
            foreach (float a in portals)
            {
                Vector3 dir = Dir(a);
                Quaternion yaw = Quaternion.Euler(0f, a, 0f);
                k.Plate(dir * (r + 0.12f) + Vector3.up * 1.1f, new Vector3(1.5f, 1.08f, 0.9f), yaw, hull);
                k.Box(dir * (r + 0.16f) + Vector3.up * 1.68f, new Vector3(1.66f, 0.09f, 1.02f), yaw, Carbon);
                k.Box(dir * (r + 0.16f) + Vector3.up * 1.74f, new Vector3(1.4f, 0.03f, 0.9f), yaw, Orange);
                foreach (float s in new[] { -1f, 1f })
                    k.Box(dir * (r + 0.36f) + Vector3.Cross(dir, Vector3.up) * (s * 0.72f) + Vector3.up * 1.1f,
                        new Vector3(0.12f, 1.1f, 0.5f), yaw, Carbon);
            }

            // Fit-out.
            k.Stage = SFit;
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f + 7.5f;
                k.Box(Dir(a) * (r + 0.03f) + Vector3.up * 1.54f, new Vector3(0.62f, 0.11f, 0.03f), Quaternion.Euler(0f, a, 0f), WarmGlass, WarmEmit);
            }
            for (int i = 0; i < 40; i++)
            {
                float a = i * 9f;
                k.Box(Dir(a) * (r * 1.12f + 0.005f) + Vector3.up * 0.6f, new Vector3(0.2f, 0.035f, 0.02f), Quaternion.Euler(0f, a, 0f), Cyan, CyanEmit);
            }
            foreach (float a in portals)
            {
                Vector3 dir = Dir(a);
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                var f = new Face(dir * (r + 0.57f), dir);
                FBox(k, f, 0f, 1.0f, 1.0f, 0.88f, 0.05f, Carbon);
                FBox(k, f, -0.24f, 0.98f, 0.44f, 0.8f, 0.07f, Glass, GlassEmit * 3f);
                FBox(k, f, 0.24f, 0.98f, 0.44f, 0.8f, 0.07f, Glass, GlassEmit * 3f);
                FBox(k, f, 0f, 0.98f, 0.04f, 0.84f, 0.08f, Orange);
                LightBar(k, f, 0f, 1.5f, 0.9f, CyanEmit);
                WallLamp(k, f, -0.66f, 1.3f, WarmEmit);
                WallLamp(k, f, 0.66f, 1.3f, WarmEmit);
                Hazard(k, dir * (r + 0.62f) + side * -0.55f + Vector3.up * 0.57f, dir * (r + 0.62f) + side * 0.55f + Vector3.up * 0.57f, 0.12f, 0.015f);
            }
            for (int i = 0; i < 16; i++)
            {
                float a = 11.25f + i * 22.5f;
                if (NearPortal(a, portals, 20f)) continue;
                Vector3 p = Dir(a) * (R - 0.14f) + Vector3.up * 0.56f;
                k.Cyl(p + Vector3.up * 0.1f, 0.09f, 0.2f, Graphite);
                k.Cyl(p + Vector3.up * 0.22f, 0.11f, 0.04f, WarmGlass, WarmEmit);
            }
            for (int i = 0; i < 8; i++)
                NavLight(k, Dir(22.5f + i * 45f) * (r * 1.02f) + Vector3.up * 1.72f, i % 2 == 0 ? RedEmit : CyanEmit);
            // Back-of-house: tanks, HVAC and pipes on the plinth ring behind the drum.
            Tank(k, Dir(28f) * (r + 0.38f) + Vector3.up * 0.56f, 0.48f, 0.9f, White);
            Tank(k, Dir(44f) * (r + 0.38f) + Vector3.up * 0.56f, 0.48f, 0.9f, White);
            RoofUnit(k, Dir(64f) * (r + 0.36f) + Vector3.up * 0.56f, 0.5f, 0.4f, 0.4f);
            RoofUnit(k, Dir(118f) * (r + 0.36f) + Vector3.up * 0.56f, 0.5f, 0.4f, 0.4f);
            PipeRun(k, new[]
            {
                Dir(28f) * (r + 0.38f) + Vector3.up * 1.3f, Dir(28f) * (r + 0.04f) + Vector3.up * 1.3f
            }, 0.07f, Steel);
            PipeRun(k, new[]
            {
                Dir(44f) * (r + 0.38f) + Vector3.up * 1.2f, Dir(44f) * (r + 0.04f) + Vector3.up * 1.2f
            }, 0.07f, Orange);
            // Crown comms on the cupola.
            float cap = domeTop + 0.36f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = Dir(i * 120f + 30f) * (r * 0.09f) + Vector3.up * cap;
                k.Rod(p, p + Vector3.up * (0.55f + i * 0.12f), 0.025f, Steel);
                k.Ball(p + Vector3.up * (0.58f + i * 0.12f), 0.06f, RedLamp, RedEmit);
            }
            Dish(k, Dir(210f) * (r * 0.06f) + Vector3.up * cap, 0.34f, 210f, -35f);
            k.Build();
        }

        private static Vector3 Dir(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        private static bool NearPortal(float a, float[] portals, float within)
        {
            for (int i = 0; i < portals.Length; i++)
                if (Mathf.Abs(Mathf.DeltaAngle(a, portals[i])) < within) return true;
            return false;
        }

        // ------------------------------------------------------------------ LANDING PAD

        private static void DetailLandingPad(Transform root, float w, float d)
        {
            float span = Mathf.Min(w, d);
            float R = span * 0.46f;
            float deck = 0.225f;
            float half = span * 0.5f;
            var k = new DetailBatch(root, Key("Pad", w, d));

            // Foundation: blast grates in the deck, corner service pads.
            k.Stage = SFound;
            for (int i = 0; i < 8; i++)
            {
                float a = 22.5f + i * 45f;
                Quaternion yaw = Quaternion.Euler(0f, a, 0f);
                Vector3 at = Dir(a) * (R * 0.7f) + Vector3.up * deck;
                k.Box(at, new Vector3(0.62f, 0.012f, 1.0f), yaw, Carbon);
                for (int s = 0; s < 6; s++)
                    k.Box(at + yaw * new Vector3(0f, 0.008f, -0.4f + s * 0.16f), new Vector3(0.56f, 0.012f, 0.05f), yaw, Steel);
            }
            Vector3 cTower = new Vector3(half - 0.9f, 0f, half - 0.9f);
            Vector3 cTank = new Vector3(-half + 0.9f, 0f, half - 0.9f);
            Vector3 cBunker = new Vector3(half - 0.9f, 0f, -half + 0.9f);
            foreach (var c in new[] { cTower, cTank, cBunker })
                k.Box(c + Vector3.up * 0.06f, new Vector3(1.7f, 0.12f, 1.7f), Concrete);

            // Frame: service tower lattice and two light masts.
            k.Stage = SFrame;
            float towerH = 5.2f;
            Lattice(k, cTower + Vector3.up * 0.12f, 0.62f, towerH, Steel, Graphite);
            foreach (var c in new[] { cTank, cBunker })
            {
                Vector3 m = c + new Vector3(Mathf.Sign(c.x) * 0.55f, 0.12f, Mathf.Sign(c.z) * 0.55f);
                k.Box(m + Vector3.up * 1.6f, new Vector3(0.1f, 3.2f, 0.1f), Steel);
                k.Box(m + Vector3.up * 0.06f, new Vector3(0.3f, 0.12f, 0.3f), Graphite);
            }

            // Shell: service arm, tank farm, control bunker.
            k.Stage = SShell;
            Vector3 armBase = cTower + Vector3.up * (towerH - 0.5f);
            Vector3 toShip = (-new Vector3(cTower.x, 0f, cTower.z)).normalized;
            Vector3 armTip = new Vector3(0f, armBase.y, 0f) - toShip * 0.85f;
            Vector3 side = Vector3.Cross(toShip, Vector3.up) * 0.14f;
            k.Beam(armBase + side, armTip + side, 0.06f, White);
            k.Beam(armBase - side, armTip - side, 0.06f, White);
            k.Beam(armBase + Vector3.up * 0.28f, armTip + Vector3.up * 0.28f, 0.06f, White);
            int bays = 8;
            for (int i = 0; i < bays; i++)
            {
                Vector3 a = Vector3.Lerp(armBase, armTip, (float)i / bays), b = Vector3.Lerp(armBase, armTip, (i + 1f) / bays);
                k.Beam(a + side, b + Vector3.up * 0.28f, 0.025f, Steel);
                k.Beam(a - side, b + Vector3.up * 0.28f, 0.025f, Steel);
            }
            k.Box(cTower + Vector3.up * (towerH + 0.12f), new Vector3(0.9f, 0.1f, 0.9f), Carbon);
            k.Box(cTower + Vector3.up * (towerH - 1.6f), new Vector3(0.8f, 0.5f, 0.8f), White);
            k.Box(cTower + Vector3.up * (towerH - 1.35f), new Vector3(0.84f, 0.05f, 0.84f), Orange);
            for (int i = 0; i < 2; i++)
            {
                Vector3 t = cTank + new Vector3(-0.1f, 0.5f, -0.35f + i * 0.7f);
                k.Cyl(t, 0.56f, 1.3f, Quaternion.Euler(0f, 0f, 90f), White);
                k.Ball(t + new Vector3(0.65f, 0f, 0f), new Vector3(0.3f, 0.56f, 0.56f), White);
                k.Ball(t + new Vector3(-0.65f, 0f, 0f), new Vector3(0.3f, 0.56f, 0.56f), White);
                k.Cyl(t + new Vector3(0.35f, 0f, 0f), 0.59f, 0.05f, Quaternion.Euler(0f, 0f, 90f), Carbon);
                k.Cyl(t + new Vector3(-0.35f, 0f, 0f), 0.59f, 0.05f, Quaternion.Euler(0f, 0f, 90f), Carbon);
                k.Box(t + new Vector3(0.4f, -0.3f, 0f), new Vector3(0.1f, 0.2f, 0.4f), Graphite);
                k.Box(t + new Vector3(-0.4f, -0.3f, 0f), new Vector3(0.1f, 0.2f, 0.4f), Graphite);
            }
            k.Plate(cBunker + new Vector3(0.1f, 0.5f, -0.05f), new Vector3(1.1f, 0.76f, 0.9f), Quaternion.identity, White);
            k.Box(cBunker + new Vector3(0.1f, 0.92f, -0.05f), new Vector3(1.2f, 0.08f, 1.0f), Carbon);

            // Fit-out.
            k.Stage = SFit;
            for (int i = 0; i < 32; i++)
            {
                float a = i * 360f / 32f;
                k.Box(Dir(a) * (R - 0.08f) + Vector3.up * (deck + 0.02f), new Vector3(0.14f, 0.04f, 0.1f), Quaternion.Euler(0f, a, 0f),
                    Cyan, i % 4 == 0 ? AmberEmit : CyanEmit * 0.8f);
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f + 11.25f;
                k.Box(Dir(a) * (R * 0.58f) + Vector3.up * (deck + 0.005f), new Vector3(0.08f, 0.012f, 0.42f), Quaternion.Euler(0f, a + 90f, 0f), Orange);
            }
            // Fuel lines along the back edge, tank farm to tower.
            float zEdge = half - 0.1f;
            PipeRun(k, new[]
            {
                cTank + new Vector3(0.7f, 0.3f, 0.2f), new Vector3(-1.6f, 0.3f, zEdge), new Vector3(1.6f, 0.3f, zEdge),
                cTower + new Vector3(-0.4f, 0.3f, 0.2f), cTower + new Vector3(-0.4f, towerH - 0.5f, 0.2f)
            }, 0.1f, White);
            PipeRun(k, new[]
            {
                cTank + new Vector3(0.7f, 0.2f, 0.36f), new Vector3(-1.6f, 0.2f, zEdge + 0.16f), new Vector3(1.6f, 0.2f, zEdge + 0.16f),
                cTower + new Vector3(-0.2f, 0.2f, 0.4f), cTower + new Vector3(-0.2f, towerH - 0.8f, 0.4f)
            }, 0.08f, Orange);
            k.Rod(armTip, armTip + Vector3.down * 1.4f + toShip * 0.2f, 0.07f, Carbon);
            k.Box(armTip + Vector3.up * 0.14f, new Vector3(0.24f, 0.24f, 0.24f), Carbon);
            k.Ball(cTower + Vector3.up * (towerH + 0.4f), 0.14f, RedLamp, RedEmit);
            Antenna(k, cTower + new Vector3(0.25f, towerH + 0.17f, 0.25f), 0.9f);
            for (int i = 0; i < 4; i++)
                k.Box(cTower + new Vector3(-0.33f, 1.0f + i * 1.1f, 0f), new Vector3(0.02f, 0.06f, 0.4f), Cyan, CyanEmit);
            foreach (var c in new[] { cTank, cBunker })
            {
                Vector3 m = c + new Vector3(Mathf.Sign(c.x) * 0.55f, 0.12f, Mathf.Sign(c.z) * 0.55f);
                Quaternion face = Quaternion.LookRotation(-new Vector3(m.x, 0f, m.z).normalized);
                k.Box(m + Vector3.up * 3.22f, new Vector3(0.7f, 0.05f, 0.1f), face, Steel);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 lamp = m + Vector3.up * 3.32f + face * new Vector3(-0.22f + i * 0.22f, 0f, 0.04f);
                    k.Box(lamp, new Vector3(0.18f, 0.14f, 0.08f), face, Carbon);
                    k.Box(lamp + face * new Vector3(0f, 0f, 0.045f), new Vector3(0.14f, 0.1f, 0.01f), face, Cream, WarmEmit);
                }
            }
            var bunkerFace = new Face(cBunker + new Vector3(0.1f, 0f, -0.5f), Vector3.back);
            Window(k, bunkerFace, 0f, 0.62f, 0.8f, 0.18f, Cyan, CyanEmit * 0.6f);
            var bunkerSide = new Face(cBunker + new Vector3(-0.45f, 0f, -0.05f), Vector3.left);
            Door(k, bunkerSide, 0f, 0.36f, 0.62f, Orange, 0.12f);
            Dish(k, cBunker + new Vector3(0.3f, 0.96f, 0.1f), 0.42f, 225f, -40f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + 15f;
                Vector3 p = Dir(a) * (R + 0.2f);
                if (Mathf.Abs(p.x) > half - 0.1f || Mathf.Abs(p.z) > half - 0.1f) continue;
                k.Cyl(p + Vector3.up * 0.2f, 0.08f, 0.4f, Graphite);
                k.Cyl(p + Vector3.up * 0.42f, 0.1f, 0.05f, WarmGlass, AmberEmit);
            }
            k.Build();
        }

        private static void Lattice(DetailBatch k, Vector3 foot, float side, float h, Color leg, Color brace)
        {
            float s = side * 0.5f;
            Vector3[] c = { new Vector3(-s, 0f, -s), new Vector3(s, 0f, -s), new Vector3(s, 0f, s), new Vector3(-s, 0f, s) };
            for (int i = 0; i < 4; i++)
                k.Box(foot + c[i] + Vector3.up * h * 0.5f, new Vector3(0.07f, h, 0.07f), leg);
            int n = Mathf.CeilToInt(h / 0.55f);
            for (int j = 0; j < n; j++)
            {
                float y0 = j * h / n, y1 = (j + 1) * h / n;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = foot + c[i], b = foot + c[(i + 1) % 4];
                    k.Beam(a + Vector3.up * y1, b + Vector3.up * y1, 0.035f, brace);
                    k.Beam((j % 2 == 0 ? a : b) + Vector3.up * y0, (j % 2 == 0 ? b : a) + Vector3.up * y1, 0.025f, brace);
                }
            }
        }

        // ------------------------------------------------------------------ SOLAR FIELD

        private static void DetailSolarField(Transform root, float w, float d, Color hull)
        {
            var k = new DetailBatch(root, Key("Solar", w, d));
            float ny = d * 0.22f;
            float hullHx = w * 0.21f, hullHz = d * 0.17f;
            float cellW = w * 0.18f, cellD = d * 0.16f, pitchX = w * 0.20f, pitchZ = d * 0.17f;
            float originX = -pitchX * 1.5f, originZ = -d * 0.18f - pitchZ;
            Quaternion tilt = Quaternion.Euler(-38f, 0f, 0f);

            k.Stage = SFound;
            k.Box(new Vector3(0f, 0.19f, ny), new Vector3(hullHx * 2f + 0.3f, 0.08f, hullHz * 2f + 0.3f), Concrete);
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                    k.Box(new Vector3(originX + c * pitchX, 0.18f, originZ + r * pitchZ), new Vector3(0.2f, 0.07f, 0.2f), Concrete);
            float cabX = -w * 0.36f;
            k.Box(new Vector3(cabX, 0.18f, ny + 0.2f), new Vector3(0.8f, 0.07f, 2.4f), Concrete);

            k.Stage = SFrame;
            for (int r = 0; r < 3; r++)
                k.Rod(new Vector3(originX - cellW * 0.5f, 0.68f, originZ + r * pitchZ), new Vector3(-originX + cellW * 0.5f, 0.68f, originZ + r * pitchZ), 0.06f, Steel);

            k.Stage = SShell;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(cabX, 0.22f, ny - 0.75f + i * 0.6f);
                k.Plate(p + Vector3.up * 0.5f, new Vector3(0.6f, 1.0f, 0.5f), Quaternion.identity, White);
                k.Box(p + Vector3.up * 1.03f, new Vector3(0.66f, 0.06f, 0.56f), Carbon);
            }
            Vector3 xf = new Vector3(w * 0.35f, 0.22f, ny + 0.4f);
            k.Box(xf + Vector3.up * 0.4f, new Vector3(0.6f, 0.8f, 0.9f), Graphite);
            for (int i = 0; i < 7; i++)
                k.Box(xf + new Vector3(0.34f, 0.4f, -0.36f + i * 0.12f), new Vector3(0.12f, 0.66f, 0.03f), Steel);
            k.Cyl(xf + new Vector3(-0.1f, 0.9f, 0.2f), 0.14f, 0.2f, White);
            k.Cyl(xf + new Vector3(-0.1f, 0.9f, -0.2f), 0.14f, 0.2f, White);
            var hullFront = new Face(new Vector3(0f, 0f, ny - hullHz), Vector3.back);
            Panel(k, hullFront, -hullHx * 0.5f, 0.55f, hullHx * 0.8f, 0.6f);
            Panel(k, hullFront, hullHx * 0.5f, 0.55f, hullHx * 0.8f, 0.6f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                k.Box(new Vector3(0f, 2.35f, ny) + Dir(a) * 0.36f, new Vector3(0.03f, 0.62f, 0.14f), Quaternion.Euler(0f, a, 0f), Graphite);
            }

            k.Stage = SFit;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                    CellLines(k, new Vector3(originX + c * pitchX, 0.76f, originZ + r * pitchZ), cellW, cellD, tilt, 0.02f);
            float invX = -originX + cellW * 0.5f + 0.25f;
            for (int r = 0; r < 3; r++)
            {
                float z = originZ + r * pitchZ;
                k.Box(new Vector3(invX, 0.38f, z), new Vector3(0.24f, 0.34f, 0.3f), Graphite);
                k.Box(new Vector3(invX - 0.125f, 0.46f, z - 0.06f), new Vector3(0.01f, 0.05f, 0.05f), Cyan, GreenEmit);
                k.Box(new Vector3(invX - 0.125f, 0.46f, z + 0.04f), new Vector3(0.01f, 0.05f, 0.05f), Cyan, CyanEmit);
                k.Rod(new Vector3(invX, 0.22f, z), new Vector3(invX, 0.22f, ny - hullHz), 0.05f, Carbon);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(cabX - 0.31f, 0.22f, ny - 0.75f + i * 0.6f);
                var f = new Face(p, Vector3.left);
                Grille(k, f, 0f, 0.4f, 0.34f, 0.3f);
                FBox(k, f, -0.1f, 0.85f, 0.06f, 0.06f, 0.02f, Cyan, i == 2 ? AmberEmit : GreenEmit);
                FBox(k, f, 0.05f, 0.85f, 0.14f, 0.04f, 0.02f, Cyan, CyanEmit);
                FBox(k, f, 0f, 1.0f, 0.4f, 0.04f, 0.02f, Orange);
            }
            Window(k, hullFront, 0f, 1.25f, hullHx * 1.2f, 0.2f, Cyan, CyanEmit * 0.55f);
            LightBar(k, hullFront, 0f, 1.55f, hullHx * 1.5f, CyanEmit);
            var hullLeft = new Face(new Vector3(-hullHx, 0f, ny), Vector3.left);
            Ladder(k, hullLeft, 0.3f, 0.2f, 1.95f);
            Grille(k, hullLeft, -0.35f, 0.7f, 0.4f, 0.5f);
            float roofY = 1.89f;
            Railing(k, new Vector3(-hullHx, roofY, ny - hullHz), new Vector3(hullHx, roofY, ny - hullHz), 0.28f, Yellow);
            Railing(k, new Vector3(-hullHx, roofY, ny - hullHz), new Vector3(-hullHx, roofY, ny + hullHz), 0.28f, Yellow);
            Antenna(k, new Vector3(hullHx - 0.2f, roofY, ny + hullHz - 0.2f), 1.1f);
            NavLight(k, new Vector3(-w * 0.46f, 0.15f, -d * 0.46f), AmberEmit);
            NavLight(k, new Vector3(w * 0.46f, 0.15f, -d * 0.46f), AmberEmit);
            NavLight(k, new Vector3(-w * 0.46f, 0.15f, d * 0.46f), AmberEmit);
            k.Build();
        }

        // ------------------------------------------------------------------ WORKSHOP (full build)

        public static void BuildWorkshop(Transform root, float w, float d, Color accent, bool tall)
        {
            // Robot workshop: arched hangar with accent doors on the camera-facing -Z front.
            float hw = w * 0.36f;
            float zf = -d * 0.18f, zb = d * 0.40f;
            float depth = zb - zf, zm = (zf + zb) * 0.5f;
            const float y0 = 0.18f;
            float hh = tall ? 2.0f : 1.6f;
            float roofH = tall ? 1.1f : 0.95f;
            float crest = y0 + hh + roofH;
            float roofLen = depth + 0.3f;
            float roofZ = zm - 0.05f;

            Prim(root, "Dress_S1_ShopPlinth", PrimitiveType.Cube, new Vector3(0f, 0.09f, 0f), new Vector3(w * 0.94f, 0.18f, d * 0.94f), Carbon);
            Prim(root, "Dress_S1_ShopApron", PrimitiveType.Cube, new Vector3(0f, 0.2f, (zf - d * 0.47f) * 0.5f), new Vector3(hw * 2f, 0.04f, zf + d * 0.47f), Concrete);
            Prim(root, "Dress_S3_ShopHall", PrimitiveType.Cube, new Vector3(0f, y0 + hh * 0.5f, zm), new Vector3(hw * 2f, hh, depth), White);
            Prim(root, "Dress_S3_ShopRoof", PrimitiveType.Cylinder, new Vector3(0f, y0 + hh, roofZ),
                new Vector3(hw * 2f + 0.12f, roofLen * 0.5f, roofH * 2f), White, Quaternion.Euler(90f, 0f, 0f));
            Prim(root, "Dress_S3_ShopEave_L", PrimitiveType.Cube, new Vector3(-hw - 0.02f, y0 + hh, roofZ), new Vector3(0.14f, 0.12f, roofLen), Carbon);
            Prim(root, "Dress_S3_ShopEave_R", PrimitiveType.Cube, new Vector3(hw + 0.02f, y0 + hh, roofZ), new Vector3(0.14f, 0.12f, roofLen), Carbon);
            Prim(root, "Dress_S3_ShopStripe", PrimitiveType.Cube, new Vector3(0f, y0 + 0.28f, zm), new Vector3(hw * 2f + 0.04f, 0.08f, depth + 0.04f), Orange);
            Prim(root, "Dress_S3_ShopAnnex", PrimitiveType.Cube, new Vector3(hw * 0.35f, y0 + 0.55f, zb + 0.25f), new Vector3(hw * 1.1f, 1.1f, 0.5f), Cream2);

            float dw = hw * 1.3f, dh = hh * 0.92f;
            Prim(root, "Dress_S4_ShopDoor_L", PrimitiveType.Cube, new Vector3(-dw * 0.25f, y0 + dh * 0.5f, zf - 0.04f), new Vector3(dw * 0.5f - 0.02f, dh, 0.08f), accent);
            Prim(root, "Dress_S4_ShopDoor_R", PrimitiveType.Cube, new Vector3(dw * 0.25f, y0 + dh * 0.5f, zf - 0.04f), new Vector3(dw * 0.5f - 0.02f, dh, 0.08f), accent);
            Prim(root, "Dress_S4_ShopBeacon", PrimitiveType.Sphere, new Vector3(0f, crest + 0.12f, zf + 0.25f), new Vector3(0.2f, 0.2f, 0.2f), Cyan, CyanEmit);

            var k = new DetailBatch(root, Key($"Shop{(tall ? "T" : "")}{(Color32)accent}", w, d));
            var front = new Face(new Vector3(0f, 0f, zf), Vector3.back);
            var left = new Face(new Vector3(-hw, 0f, zm), Vector3.left);
            var right = new Face(new Vector3(hw, 0f, zm), Vector3.right);
            var back = new Face(new Vector3(0f, 0f, zb), Vector3.forward);

            k.Stage = SFound;
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(0f, 0.225f, zf - 0.55f - i * 0.28f), new Vector3(0.7f - i * 0.12f, 0.012f, 0.1f), Yellow);
            k.Box(new Vector3(-dw * 0.5f - 0.05f, 0.225f, zf - 1.0f), new Vector3(0.05f, 0.012f, 1.7f), Cream);
            k.Box(new Vector3(dw * 0.5f + 0.05f, 0.225f, zf - 1.0f), new Vector3(0.05f, 0.012f, 1.7f), Cream);
            Hazard(k, new Vector3(-hw, 0.23f, zf - 0.12f), new Vector3(hw, 0.23f, zf - 0.12f), 0.16f, 0.02f);

            // Frame: arched ribs down to wall pilasters, every ~0.6 m.
            k.Stage = SFrame;
            int ribs = Mathf.Max(4, Mathf.RoundToInt(roofLen / 0.62f));
            for (int i = 0; i <= ribs; i++)
            {
                float z = roofZ - roofLen * 0.5f + 0.06f + (roofLen - 0.12f) * i / ribs;
                Arc(k, new Vector3(0f, y0 + hh, z), Vector3.right, hw + 0.07f, roofH + 0.05f, 0.08f, Carbon, 12);
                if (z > zf + 0.02f && z < zb - 0.02f)
                {
                    k.Box(new Vector3(-hw - 0.04f, y0 + hh * 0.5f, z), new Vector3(0.08f, hh, 0.1f), Carbon);
                    k.Box(new Vector3(hw + 0.04f, y0 + hh * 0.5f, z), new Vector3(0.08f, hh, 0.1f), Carbon);
                }
            }
            k.Box(new Vector3(-dw * 0.5f - 0.12f, y0 + (dh + 0.2f) * 0.5f, zf - 0.08f), new Vector3(0.2f, dh + 0.2f, 0.18f), Carbon);
            k.Box(new Vector3(dw * 0.5f + 0.12f, y0 + (dh + 0.2f) * 0.5f, zf - 0.08f), new Vector3(0.2f, dh + 0.2f, 0.18f), Carbon);
            k.Box(new Vector3(0f, y0 + dh + 0.12f, zf - 0.09f), new Vector3(dw + 0.44f, 0.22f, 0.2f), Carbon);

            // Shell: gable glazing frame, side cladding.
            k.Stage = SShell;
            for (int i = 1; i < ribs; i++)
            {
                float z = roofZ - roofLen * 0.5f + 0.06f + (roofLen - 0.12f) * (i - 0.5f) / ribs;
                if (z < zf + 0.3f || z > zb - 0.3f) continue;
                foreach (var f in new[] { left, right })
                {
                    float x = f.N.x < 0 ? (zm - z) : (z - zm);
                    FBox(k, f, x, y0 + 0.75f, 0.42f, 0.7f, 0.025f, Cream2);
                }
            }
            FBox(k, back, 0f, y0 + hh * 0.5f, hw * 1.6f, hh * 0.7f, 0.025f, Cream2);

            // Fit-out.
            k.Stage = SFit;
            for (int i = 1; i < 5; i++)
                FBox(k, front, 0f, y0 + dh * i / 5f, dw - 0.06f, 0.025f, 0.09f, Carbon);
            FBox(k, front, 0f, y0 + dh * 0.5f, 0.03f, dh, 0.095f, Carbon);
            foreach (float s in new[] { -1f, 1f })
            {
                FBox(k, front, s * dw * 0.25f, y0 + dh * 0.72f, dw * 0.3f, 0.1f, 0.1f, Cyan, CyanEmit * 0.7f);
                WallLamp(k, front, s * (dw * 0.5f + 0.34f), y0 + hh - 0.2f, WarmEmit);
            }
            Hazard(k, front.P(-dw * 0.5f - 0.2f, y0 + dh + 0.12f, 0.2f), front.P(dw * 0.5f + 0.2f, y0 + dh + 0.12f, 0.2f), 0.14f, 0.1f);
            FBox(k, front, 0f, y0 + dh + 0.08f + 0.0f, dw + 0.9f, 0.05f, 0.22f, Steel);
            // Gable: glazed slits under the arch and a lit bay sign.
            for (int i = -2; i <= 2; i++)
            {
                float x = i * hw * 0.3f;
                float hgt = roofH * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / hw) * (x / hw))) * 0.62f;
                if (hgt < 0.15f) continue;
                FBox(k, front, x, y0 + hh + 0.08f + hgt * 0.5f, 0.14f, hgt, 0.04f, Glass, WarmEmit * 0.55f);
            }
            LightBar(k, front, 0f, y0 + hh + 0.02f, dw * 0.6f, CyanEmit);
            FBox(k, front, hw * 0.72f, y0 + hh * 0.72f, 0.34f, 0.34f, 0.04f, accent);
            FBox(k, front, hw * 0.72f, y0 + hh * 0.72f, 0.2f, 0.2f, 0.05f, Carbon);
            // Side (-X): clerestory windows, personnel door, vent, stack.
            int nWin = 4;
            for (int i = 0; i < nWin; i++)
            {
                float x = -depth * 0.5f + depth * (i + 0.5f) / nWin;
                Window(k, left, x, y0 + hh - 0.32f, 0.36f, 0.22f, WarmGlass, WarmEmit);
                Window(k, right, x, y0 + hh - 0.32f, 0.36f, 0.22f, WarmGlass, WarmEmit);
            }
            Door(k, left, depth * 0.5f - 0.55f, 0.46f, 0.95f, Graphite, y0);
            WallLamp(k, left, depth * 0.5f - 0.55f, y0 + 1.15f, WarmEmit);
            Grille(k, left, -depth * 0.2f, y0 + 0.62f, 0.6f, 0.32f);
            FBox(k, left, 0.1f, y0 + 0.62f, 0.4f, 0.5f, 0.14f, Graphite);
            FBox(k, left, 0.1f, y0 + 0.8f, 0.3f, 0.04f, 0.15f, Orange);
            FBox(k, left, 0.02f, y0 + 0.5f, 0.05f, 0.05f, 0.15f, Cyan, GreenEmit);
            PipeRun(k, new[]
            {
                new Vector3(-hw - 0.14f, y0 + 0.15f, zb - 0.45f), new Vector3(-hw - 0.14f, y0 + hh + 0.2f, zb - 0.45f),
                new Vector3(-hw * 0.7f, crest + 0.1f, zb - 0.45f), new Vector3(-hw * 0.7f, crest + 0.55f, zb - 0.45f)
            }, 0.1f, Steel);
            k.Cyl(new Vector3(-hw * 0.7f, crest + 0.6f, zb - 0.45f), 0.16f, 0.1f, Carbon);
            Grille(k, right, 0f, y0 + 0.5f, 1.2f, 0.26f);
            PipeRun(k, new[]
            {
                new Vector3(hw + 0.1f, y0 + 0.35f, zf + 0.3f), new Vector3(hw + 0.1f, y0 + 0.35f, zb - 0.3f)
            }, 0.08f, Orange);
            // Roof: crest skylight, stacks, HVAC, antenna.
            k.Box(new Vector3(0f, crest + 0.01f, roofZ), new Vector3(0.5f, 0.05f, roofLen * 0.78f), Glass, GlassEmit * 2f);
            for (int i = 0; i <= 6; i++)
                k.Box(new Vector3(0f, crest + 0.035f, roofZ - roofLen * 0.39f + roofLen * 0.78f * i / 6f), new Vector3(0.56f, 0.03f, 0.04f), Carbon);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * hw * 0.5f;
                float roofAt = y0 + hh + roofH * Mathf.Sqrt(1f - 0.25f) - 0.05f;
                Vector3 p = new Vector3(x, roofAt, zb - 0.5f);
                k.Cyl(p + Vector3.up * 0.45f, 0.26f, 0.9f, Graphite);
                k.Cyl(p + Vector3.up * 0.7f, 0.29f, 0.06f, Orange);
                k.Cyl(p + Vector3.up * 0.92f, 0.3f, 0.05f, Carbon);
                k.Cyl(p + Vector3.up * 0.9f, 0.18f, 0.03f, new Color(0.3f, 0.12f, 0.05f), new Color(1.2f, 0.4f, 0.08f));
                RoofUnit(k, new Vector3(x, roofAt - 0.02f, zf + 0.9f), 0.42f, 0.36f, 0.22f);
            }
            Antenna(k, new Vector3(hw * 0.2f, crest, zb - 0.15f), 1.1f);
            // Annex tanks and a charging pylon on the apron.
            Tank(k, new Vector3(-hw * 0.65f, y0, zb + 0.28f), 0.44f, 1.1f, White);
            Tank(k, new Vector3(-hw * 0.2f, y0, zb + 0.28f), 0.44f, 1.1f, White);
            var annexBack = new Face(new Vector3(hw * 0.35f, 0f, zb + 0.5f), Vector3.forward);
            Grille(k, annexBack, 0f, y0 + 0.6f, hw * 0.8f, 0.4f);
            Vector3 pylon = new Vector3(-hw + 0.25f, 0.22f, zf - 1.3f);
            k.Box(pylon + Vector3.up * 0.4f, new Vector3(0.24f, 0.8f, 0.2f), White);
            k.Box(pylon + new Vector3(0f, 0.58f, -0.105f), new Vector3(0.14f, 0.2f, 0.01f), Cyan, CyanEmit);
            k.Box(pylon + Vector3.up * 0.82f, new Vector3(0.28f, 0.04f, 0.24f), accent);
            k.Rod(pylon + new Vector3(0.12f, 0.3f, 0f), pylon + new Vector3(0.35f, 0.02f, -0.1f), 0.03f, Carbon);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 b = new Vector3(s * (dw * 0.5f + 0.3f), 0.22f, zf - 0.28f);
                k.Cyl(b + Vector3.up * 0.2f, 0.12f, 0.4f, Yellow);
                k.Cyl(b + Vector3.up * 0.3f, 0.125f, 0.06f, Carbon);
                k.Cyl(b + Vector3.up * 0.42f, 0.1f, 0.04f, Cyan, AmberEmit);
            }
            k.Build();
        }

        // ------------------------------------------------------------------ DEFENSE BATTERY (full build)

        public static void BuildDefenseBattery(Transform root, float w, float d, Color hull)
        {
            // Armoured bunker with sloped glacis and a twin-rail turret; the shield bubble is dressing.
            float core = Mathf.Min(w, d) * 0.28f;       // core half-size
            float glacisOut = Mathf.Min(w, d) * 0.39f;  // glacis foot half-size
            const float deckY = 1.42f;
            Prim(root, "Dress_S1_DefPlinth", PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(w * 0.93f, 0.22f, d * 0.93f), Carbon);
            Prim(root, "Dress_S1_DefTier", PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(w * 0.86f, 0.1f, d * 0.86f), Concrete);
            Prim(root, "Dress_S3_DefCore", PrimitiveType.Cube, new Vector3(0f, (0.32f + deckY) * 0.5f, 0f), new Vector3(core * 2f, deckY - 0.32f, core * 2f), hull);
            Prim(root, "Dress_S3_DefDeck", PrimitiveType.Cube, new Vector3(0f, deckY + 0.07f, 0f), new Vector3(core * 2f + 0.3f, 0.14f, core * 2f + 0.3f), Carbon);

            var k = new DetailBatch(root, Key("Defense", w, d));
            k.Stage = SFound;
            float ph = w * 0.43f;
            Hazard(k, new Vector3(-ph, 0.33f, -ph), new Vector3(ph, 0.33f, -ph), 0.1f, 0.02f);
            Hazard(k, new Vector3(-ph, 0.33f, -ph), new Vector3(-ph, 0.33f, ph), 0.1f, 0.02f);

            // Frame: corner buttresses carrying the glacis.
            k.Stage = SFrame;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.Beam(new Vector3(sx * glacisOut, 0.32f, sz * glacisOut), new Vector3(sx * core, deckY, sz * core), 0.42f, Carbon);

            // Shell: four sloped glacis plates, parapet, turret ring.
            k.Stage = SShell;
            for (int side = 0; side < 4; side++)
            {
                Quaternion yaw = Quaternion.Euler(0f, side * 90f, 0f);
                Vector3 outN = yaw * Vector3.back;
                Vector3 foot = outN * glacisOut + Vector3.up * 0.32f;
                Vector3 head = outN * core + Vector3.up * (deckY - 0.02f);
                Vector3 up = (head - foot).normalized;
                Quaternion rot = Quaternion.LookRotation(Vector3.Cross(yaw * Vector3.right, up), up);
                float len = Vector3.Distance(foot, head);
                k.Add(PrimitiveType.Cube, (foot + head) * 0.5f, new Vector3(core * 2f + 0.02f, len, 0.2f), rot, Graphite, default, plated: true);
                // Raised armour tiles.
                for (int t = 0; t < 3; t++)
                    for (int j = 0; j < 2; j++)
                    {
                        Vector3 at = Vector3.Lerp(foot, head, 0.28f + j * 0.42f) + (yaw * Vector3.right) * ((t - 1) * core * 0.62f)
                                     + rot * Vector3.forward * 0.11f;
                        k.Box(at, new Vector3(core * 0.56f, len * 0.36f, 0.04f), rot, Carbon);
                    }
            }
            for (int side = 0; side < 4; side++)
            {
                Quaternion yaw = Quaternion.Euler(0f, side * 90f, 0f);
                k.Box(yaw * new Vector3(0f, deckY + 0.24f, -core - 0.1f), new Vector3(core * 2f + 0.3f, 0.2f, 0.1f), yaw, hull);
            }
            k.Cyl(new Vector3(0f, deckY + 0.26f, 0f), core * 1.25f, 0.24f, Graphite);
            k.Cyl(new Vector3(0f, deckY + 0.39f, 0f), core * 1.32f, 0.04f, Orange);
            // Door vestibule through the -Z glacis.
            k.Plate(new Vector3(0f, 0.8f, -glacisOut + 0.3f), new Vector3(1.0f, 0.96f, 0.9f), Quaternion.identity, hull);
            k.Box(new Vector3(0f, 1.32f, -glacisOut + 0.3f), new Vector3(1.12f, 0.08f, 0.98f), Quaternion.identity, Carbon);

            // Fit-out: turret, capacitor banks, sensors, lights.
            k.Stage = SFit;
            Vector3 aim = new Vector3(-1f, 0f, 1f).normalized;
            Quaternion aimRot = Quaternion.LookRotation(aim);
            Quaternion elev = aimRot * Quaternion.Euler(-8f, 0f, 0f);
            Vector3 tBase = new Vector3(0f, deckY + 0.42f, 0f);
            k.Box(tBase + Vector3.up * 0.34f, new Vector3(1.3f, 0.62f, 1.7f), aimRot, White);
            k.Box(tBase + Vector3.up * 0.34f + aim * 0.95f, new Vector3(1.18f, 0.5f, 0.5f), aimRot * Quaternion.Euler(28f, 0f, 0f), Carbon);
            k.Box(tBase + Vector3.up * 0.68f, new Vector3(1.2f, 0.06f, 1.6f), aimRot, Carbon);
            k.Box(tBase + Vector3.up * 0.3f - aim * 0.9f, new Vector3(1.0f, 0.4f, 0.3f), aimRot, Graphite);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 sideV = aimRot * Vector3.right * (s * 0.67f);
                k.Box(tBase + Vector3.up * 0.34f + sideV, new Vector3(0.06f, 0.46f, 1.4f), aimRot, Carbon);
                k.Box(tBase + Vector3.up * 0.46f + sideV * 1.06f, new Vector3(0.02f, 0.06f, 0.9f), aimRot, Cyan, CyanEmit);
                Vector3 muzzleBase = tBase + Vector3.up * 0.38f + aimRot * new Vector3(s * 0.26f, 0f, 1.0f);
                Vector3 dirB = elev * Vector3.forward;
                k.Rod(muzzleBase, muzzleBase + dirB * 2.3f, 0.14f, Steel);
                k.Rod(muzzleBase, muzzleBase + dirB * 0.7f, 0.22f, Graphite);
                for (int c = 0; c < 4; c++)
                {
                    Vector3 at = muzzleBase + dirB * (0.85f + c * 0.3f);
                    k.Rod(at - dirB * 0.03f, at + dirB * 0.03f, 0.2f, Cyan, CyanEmit);
                }
                k.Box(muzzleBase + dirB * 2.35f, new Vector3(0.22f, 0.18f, 0.26f), elev, Carbon);
            }
            Vector3 mastP = tBase + Vector3.up * 0.7f - aim * 0.55f;
            k.Rod(mastP, mastP + Vector3.up * 0.8f, 0.06f, Steel);
            k.Box(mastP + Vector3.up * 0.82f, new Vector3(0.9f, 0.08f, 0.14f), Quaternion.Euler(0f, 20f, 0f), Graphite);
            k.Ball(mastP + Vector3.up * 0.95f, 0.1f, RedLamp, RedEmit);
            Dish(k, tBase + Vector3.up * 0.7f + aimRot * new Vector3(0.4f, 0f, -0.3f), 0.36f, 45f, -20f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * (core - 0.1f), deckY + 0.14f, sz * (core - 0.1f));
                    if (sx < 0 && sz > 0) continue; // turret swings over this corner
                    k.Cyl(p + Vector3.up * 0.35f, 0.36f, 0.7f, White);
                    for (int b = 0; b < 3; b++)
                        k.Cyl(p + Vector3.up * (0.15f + b * 0.2f), 0.38f, 0.04f, Cyan, CyanEmit);
                    k.Cyl(p + Vector3.up * 0.72f, 0.3f, 0.06f, Carbon);
                }
            var doorF = new Face(new Vector3(0f, 0f, -glacisOut - 0.15f), Vector3.back);
            Door(k, doorF, 0f, 0.62f, 0.86f, Orange, 0.32f);
            LightBar(k, doorF, 0f, 1.22f, 0.7f, CyanEmit);
            // Vision slit along the top of the two camera-facing glacis plates.
            k.Box(new Vector3(0f, deckY - 0.12f, -core - 0.12f), new Vector3(core * 1.6f, 0.06f, 0.06f), Cyan, CyanEmit);
            k.Box(new Vector3(-core - 0.12f, deckY - 0.12f, 0f), new Vector3(0.06f, 0.06f, core * 1.6f), Cyan, CyanEmit);
            float bp = w * 0.42f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * bp, 0.32f, sz * bp);
                    k.Cyl(p + Vector3.up * 0.3f, 0.16f, 0.6f, Carbon);
                    k.Cyl(p + Vector3.up * 0.45f, 0.17f, 0.06f, Yellow);
                    k.Ball(p + Vector3.up * 0.66f, 0.14f, Cyan, CyanEmit);
                }
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(w * 0.36f, 0.45f + i * 0.22f, -d * 0.2f + (i % 2) * 0.1f), new Vector3(0.4f, 0.2f, 0.3f), i % 2 == 0 ? Graphite : new Color(0.36f, 0.4f, 0.3f));
            k.Build();
        }

        // ------------------------------------------------------------------ WATCHTOWER (full build)

        private const float TowerPodY = 4.6f;
        private static readonly Vector3 TowerLaserPos = new Vector3(0f, TowerPodY + 0.55f, -1.12f);

        public static void BuildWatchtower(Transform root, float w, float d)
        {
            float ps = Mathf.Min(w, d) * 0.3f;
            const float shaftTop = TowerPodY;
            Prim(root, "Dress_S1_TowPlinth", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(ps * 2f + 0.4f, 0.2f, ps * 2f + 0.4f), Carbon);
            Prim(root, "Dress_S1_TowTier", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(ps * 2f, 0.1f, ps * 2f), Concrete);
            Prim(root, "Dress_S3_TowShaft", PrimitiveType.Cylinder, new Vector3(0f, (0.3f + shaftTop) * 0.5f, 0f), new Vector3(0.72f, (shaftTop - 0.3f) * 0.5f, 0.72f), White);
            Prim(root, "Dress_S3_TowPodBase", PrimitiveType.Cylinder, new Vector3(0f, shaftTop + 0.12f, 0f), new Vector3(2.2f, 0.12f, 2.2f), Carbon);
            Prim(root, "Dress_S3_TowPod", PrimitiveType.Cylinder, new Vector3(0f, shaftTop + 0.64f, 0f), new Vector3(1.95f, 0.4f, 1.95f), White);
            Prim(root, "Dress_S3_TowRoof", PrimitiveType.Cylinder, new Vector3(0f, shaftTop + 1.08f, 0f), new Vector3(2.3f, 0.05f, 2.3f), Carbon);
            Prim(root, "Dress_S3_TowDome", PrimitiveType.Sphere, new Vector3(0f, shaftTop + 1.13f, 0f), new Vector3(1.6f, 0.55f, 1.6f), White);
            Prim(root, "Dress_S4_TowBeacon", PrimitiveType.Sphere, new Vector3(0f, shaftTop + 2.2f, 0f), new Vector3(0.18f, 0.18f, 0.18f), Cyan, CyanEmit);
            Prim(root, "Dress_TowerLaser", PrimitiveType.Cube, TowerLaserPos, new Vector3(0.2f, 0.14f, 0.6f), Orange);

            var k = new DetailBatch(root, Key("Tower", w, d));
            k.Stage = SFound;
            Hazard(k, new Vector3(-ps, 0.31f, -ps), new Vector3(ps, 0.31f, -ps), 0.08f, 0.012f);
            Hazard(k, new Vector3(-ps, 0.31f, -ps), new Vector3(-ps, 0.31f, ps), 0.08f, 0.012f);

            // Frame: four raked legs to the shaft, with ring and cross bracing.
            k.Stage = SFrame;
            float legTop = 2.7f;
            Vector3[] feet = new Vector3[4];
            Vector3[] heads = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = i == 0 || i == 3 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                feet[i] = new Vector3(sx * (ps - 0.15f), 0.3f, sz * (ps - 0.15f));
                heads[i] = new Vector3(sx * 0.3f, legTop, sz * 0.3f);
                k.Beam(feet[i], heads[i], 0.17f, Graphite);
                k.Box(feet[i] + Vector3.up * 0.06f, new Vector3(0.34f, 0.12f, 0.34f), Carbon);
            }
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 a = Vector3.Lerp(feet[i], heads[i], 0.4f), b = Vector3.Lerp(feet[j], heads[j], 0.4f);
                k.Beam(a, b, 0.07f, Orange);
                k.Beam(feet[i] + Vector3.up * 0.1f, b, 0.045f, Steel);
                k.Beam(Vector3.Lerp(feet[i], heads[i], 0.4f), Vector3.Lerp(feet[j], heads[j], 0.8f), 0.045f, Steel);
            }

            // Shell: shaft rings, mid platform.
            k.Stage = SShell;
            for (float y = 0.9f; y < shaftTop - 0.2f; y += 0.8f)
                k.Cyl(new Vector3(0f, y, 0f), 0.76f, 0.05f, Carbon);
            k.Cyl(new Vector3(0f, legTop, 0f), 0.8f, 0.14f, Orange);
            k.Cyl(new Vector3(0f, legTop + 0.12f, 0f), 1.9f, 0.08f, Graphite);

            // Fit-out.
            k.Stage = SFit;
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f;
                k.Box(Dir(a) * 0.975f + Vector3.up * (shaftTop + 0.7f), new Vector3(0.3f, 0.3f, 0.03f), Quaternion.Euler(0f, a, 0f), Cyan, CyanEmit * 0.9f);
                k.Box(Dir(a + 11.25f) * 0.98f + Vector3.up * (shaftTop + 0.7f), new Vector3(0.06f, 0.36f, 0.04f), Quaternion.Euler(0f, a + 11.25f, 0f), Carbon);
            }
            for (int i = 0; i < 4; i++)
                NavLight(k, Dir(45f + i * 90f) * 1.08f + Vector3.up * (shaftTop + 1.11f), i % 2 == 0 ? RedEmit : CyanEmit);
            // Searchlights on the pod rim, facing the approaches.
            foreach (float a in new[] { 200f, 250f })
            {
                Quaternion yaw = Quaternion.Euler(12f, a, 0f);
                Vector3 p = Dir(a) * 1.18f + Vector3.up * (shaftTop + 0.2f);
                k.Box(p, new Vector3(0.24f, 0.2f, 0.3f), yaw, Carbon);
                k.Box(p + yaw * new Vector3(0f, 0f, 0.155f), new Vector3(0.18f, 0.15f, 0.01f), yaw, Cream, WarmEmit * 1.2f);
            }
            // Mast, radar bar, antennas.
            float mastBase = shaftTop + 1.35f;
            k.Rod(new Vector3(0f, mastBase, 0f), new Vector3(0f, shaftTop + 2.1f, 0f), 0.07f, Steel);
            k.Box(new Vector3(0f, shaftTop + 1.75f, 0f), new Vector3(1.1f, 0.07f, 0.16f), Quaternion.Euler(0f, 30f, 0f), Graphite);
            k.Box(new Vector3(0f, shaftTop + 1.75f, 0f), new Vector3(0.16f, 0.14f, 0.16f), Carbon);
            Antenna(k, new Vector3(0.5f, shaftTop + 1.1f, 0.35f), 0.9f);
            Dish(k, new Vector3(-0.45f, shaftTop + 1.1f, 0.4f), 0.34f, 300f, -30f);
            // Platform railing, ladder, lamps, laser mount.
            for (int i = 0; i < 16; i++)
            {
                float a0 = i * 22.5f, a1 = (i + 1) * 22.5f;
                Vector3 p0 = Dir(a0) * 0.9f + Vector3.up * (legTop + 0.16f), p1 = Dir(a1) * 0.9f + Vector3.up * (legTop + 0.16f);
                k.Box(p0 + Vector3.up * 0.16f, new Vector3(0.025f, 0.32f, 0.025f), Yellow);
                k.Beam(p0 + Vector3.up * 0.32f, p1 + Vector3.up * 0.32f, 0.03f, Yellow);
            }
            var ladderF = new Face(new Vector3(-0.36f, 0f, 0f), Vector3.left);
            Ladder(k, ladderF, 0f, 0.3f, shaftTop);
            var baseF = new Face(new Vector3(0f, 0f, -0.36f), Vector3.back);
            Door(k, baseF, 0f, 0.36f, 0.8f, Orange, 0.3f);
            WallLamp(k, baseF, 0f, 1.35f, WarmEmit);
            k.Box(TowerLaserPos + new Vector3(0f, -0.14f, 0.18f), new Vector3(0.3f, 0.1f, 0.3f), Carbon);
            k.Build();
            ShowWatchtowerLasers(root, false);
        }

        // ------------------------------------------------------------------ MARKET (full build)

        public static void BuildMarket(Transform root, float w, float d)
        {
            // Trade post: capsule kiosk at the back, louvred canopy on tree struts, stalls up front.
            float hx = w * 0.45f, hz = d * 0.42f;
            const float y0 = 0.2f;
            float kioskZ = hz - 0.75f;
            float kioskHalf = w * 0.26f;
            const float canopyY = 2.35f;
            Prim(root, "Dress_S1_MktPlinth", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(hx * 2f, 0.2f, hz * 2f), Carbon);
            Prim(root, "Dress_S1_MktDeck", PrimitiveType.Cube, new Vector3(0f, 0.21f, -0.1f), new Vector3(hx * 2f - 0.3f, 0.02f, hz * 2f - 0.5f), Concrete);
            Prim(root, "Dress_S3_MktKiosk", PrimitiveType.Cube, new Vector3(0f, y0 + 0.75f, kioskZ), new Vector3(kioskHalf * 2f, 1.5f, 1.2f), White);
            Prim(root, "Dress_S3_MktKioskEnd_L", PrimitiveType.Cylinder, new Vector3(-kioskHalf, y0 + 0.75f, kioskZ), new Vector3(1.2f, 0.75f, 1.2f), White);
            Prim(root, "Dress_S3_MktKioskEnd_R", PrimitiveType.Cylinder, new Vector3(kioskHalf, y0 + 0.75f, kioskZ), new Vector3(1.2f, 0.75f, 1.2f), White);
            Prim(root, "Dress_S3_MktKioskCap", PrimitiveType.Cube, new Vector3(0f, y0 + 1.54f, kioskZ), new Vector3(kioskHalf * 2f, 0.08f, 1.26f), Carbon);

            var k = new DetailBatch(root, Key("Market", w, d));
            k.Stage = SFound;
            for (int i = -4; i <= 4; i++)
                k.Box(new Vector3(i * 0.6f, 0.224f, -0.1f), new Vector3(0.02f, 0.008f, hz * 2f - 0.55f), Graphite);
            for (int i = -3; i <= 3; i++)
                k.Box(new Vector3(0f, 0.224f, -0.1f + i * 0.6f), new Vector3(hx * 2f - 0.35f, 0.008f, 0.02f), Graphite);
            Hazard(k, new Vector3(-hx + 0.05f, 0.21f, -hz + 0.05f), new Vector3(hx - 0.05f, 0.21f, -hz + 0.05f), 0.08f, 0.012f);

            // Frame: four tree columns branching up to the canopy.
            k.Stage = SFrame;
            float cx = hx - 0.45f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float cz in new[] { -hz + 0.9f, kioskZ - 0.85f })
                {
                    Vector3 foot = new Vector3(sx * cx, y0, cz);
                    Vector3 fork = foot + Vector3.up * 1.5f;
                    k.Box(foot + Vector3.up * 0.06f, new Vector3(0.26f, 0.12f, 0.26f), Carbon);
                    k.Beam(foot, fork, 0.14f, Graphite);
                    k.Beam(fork, new Vector3(sx * (cx + 0.3f), canopyY - 0.06f, cz - 0.5f), 0.07f, Graphite);
                    k.Beam(fork, new Vector3(sx * (cx + 0.3f), canopyY - 0.06f, cz + 0.5f), 0.07f, Graphite);
                    k.Beam(fork, new Vector3(sx * (cx - 0.5f), canopyY - 0.06f, cz), 0.07f, Graphite);
                }

            // Shell: canopy louvres and edge beams, kiosk cladding.
            k.Stage = SShell;
            float canHx = hx + 0.1f, canZ0 = -hz - 0.05f, canZ1 = kioskZ + 0.2f;
            k.Box(new Vector3(-canHx, canopyY, (canZ0 + canZ1) * 0.5f), new Vector3(0.1f, 0.12f, canZ1 - canZ0), Carbon);
            k.Box(new Vector3(canHx, canopyY, (canZ0 + canZ1) * 0.5f), new Vector3(0.1f, 0.12f, canZ1 - canZ0), Carbon);
            k.Box(new Vector3(0f, canopyY, canZ0), new Vector3(canHx * 2f, 0.14f, 0.1f), Orange);
            int slats = 11;
            for (int i = 0; i < slats; i++)
            {
                float z = canZ0 + 0.15f + (canZ1 - canZ0 - 0.3f) * i / (slats - 1);
                k.Box(new Vector3(0f, canopyY + 0.02f, z), new Vector3(canHx * 2f - 0.1f, 0.04f, 0.2f), Quaternion.Euler(-22f, 0f, 0f), i % 3 == 1 ? Orange : White);
            }
            var kf = new Face(new Vector3(0f, 0f, kioskZ - 0.6f), Vector3.back);
            FBox(k, kf, 0f, y0 + 1.3f, kioskHalf * 2f, 0.14f, 0.03f, Carbon);

            // Fit-out.
            k.Stage = SFit;
            Window(k, kf, -kioskHalf * 0.45f, y0 + 0.95f, kioskHalf * 0.8f, 0.42f, WarmGlass, WarmEmit);
            FBox(k, kf, kioskHalf * 0.45f, y0 + 0.62f, kioskHalf * 0.7f, 0.08f, 0.3f, Carbon);
            FBox(k, kf, kioskHalf * 0.45f, y0 + 0.95f, kioskHalf * 0.7f, 0.5f, 0.03f, WarmGlass, WarmEmit * 0.8f);
            FBox(k, kf, kioskHalf * 0.45f, y0 + 1.25f, kioskHalf * 0.72f, 0.05f, 0.25f, Orange);
            LightBar(k, kf, 0f, y0 + 1.42f, kioskHalf * 1.6f, CyanEmit);
            foreach (float s in new[] { -1f, 1f })
            {
                var vf = new Face(new Vector3(s * (kioskHalf + 0.62f), 0f, kioskZ - 0.1f), (Vector3.back + Vector3.right * s * 0.8f).normalized);
                FBox(k, vf, 0f, y0 + 0.55f, 0.34f, 1.1f, 0.24f, Graphite);
                FBox(k, vf, 0f, y0 + 0.8f, 0.24f, 0.3f, 0.25f, Cyan, CyanEmit);
                FBox(k, vf, 0f, y0 + 0.42f, 0.2f, 0.06f, 0.25f, Orange);
            }
            // Stalls with goods and price screens.
            Color[] goods = { Orange, Cyan, new Color(0.35f, 0.7f, 0.35f), White, Yellow, new Color(0.6f, 0.3f, 0.7f) };
            for (int s = 0; s < 3; s++)
            {
                float x = (s - 1) * hx * 0.62f;
                Vector3 c = new Vector3(x, y0, -hz + 0.9f);
                k.Box(c + Vector3.up * 0.23f, new Vector3(1.0f, 0.46f, 0.44f), White);
                k.Box(c + new Vector3(0f, 0.3f, -0.225f), new Vector3(1.0f, 0.08f, 0.01f), Orange);
                k.Box(c + Vector3.up * 0.48f, new Vector3(1.06f, 0.04f, 0.5f), Carbon);
                for (int g = 0; g < 4; g++)
                {
                    float gx = -0.36f + g * 0.24f;
                    float gh = 0.1f + (g + s) % 3 * 0.05f;
                    k.Box(c + new Vector3(gx, 0.5f + gh * 0.5f, 0.05f), new Vector3(0.18f, gh, 0.2f), goods[(g + s * 2) % goods.Length]);
                }
                k.Box(c + new Vector3(0.42f, 0.72f, 0.12f), new Vector3(0.03f, 0.44f, 0.03f), Steel);
                k.Box(c + new Vector3(0.42f, 0.98f, 0.12f), new Vector3(0.3f, 0.18f, 0.03f), Cyan, s == 1 ? AmberEmit : CyanEmit);
            }
            // Holo sign pylon on the front-left corner.
            Vector3 pyl = new Vector3(-hx + 0.3f, y0, -hz + 0.3f);
            k.Box(pyl + Vector3.up * 1.4f, new Vector3(0.24f, 2.8f, 0.24f), Carbon);
            k.Box(pyl + new Vector3(0f, 1.3f, -0.125f), new Vector3(0.08f, 2.2f, 0.01f), Cyan, CyanEmit);
            k.Box(pyl + new Vector3(-0.125f, 1.3f, 0f), new Vector3(0.01f, 2.2f, 0.08f), Cyan, CyanEmit);
            k.Box(pyl + new Vector3(0.35f, 2.55f, 0f), new Vector3(0.8f, 0.46f, 0.06f), Orange, new Color(1.2f, 0.45f, 0.08f));
            k.Box(pyl + new Vector3(0.35f, 2.55f, -0.035f), new Vector3(0.6f, 0.1f, 0.01f), Cream, WarmEmit);
            k.Ball(pyl + Vector3.up * 2.9f, 0.1f, RedLamp, RedEmit);
            // Cargo containers stacked on the right, string lights along the canopy front.
            Vector3 box = new Vector3(hx - 0.42f, y0, -0.1f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 c = box + Vector3.up * (0.33f + i * 0.66f);
                k.Box(c, new Vector3(0.64f, 0.64f, 1.3f), i == 0 ? White : Orange);
                for (int r = 0; r < 6; r++)
                    k.Box(c + new Vector3(-0.325f, 0f, -0.55f + r * 0.22f), new Vector3(0.02f, 0.58f, 0.04f), i == 0 ? Cream2 : new Color(0.8f, 0.35f, 0.08f));
            }
            for (int i = 0; i <= 12; i++)
            {
                float x = -canHx + canHx * 2f * i / 12f;
                float sag = Mathf.Sin(Mathf.PI * (i % 4) / 4f) * 0.12f;
                k.Ball(new Vector3(x, canopyY - 0.14f - sag, canZ0 - 0.02f), 0.07f, WarmGlass, WarmEmit);
            }
            k.Beam(new Vector3(-canHx, canopyY - 0.12f, canZ0 - 0.02f), new Vector3(canHx, canopyY - 0.12f, canZ0 - 0.02f), 0.012f, Carbon);
            // Benches, planters, cargo drone on the kiosk roof.
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 b = new Vector3(sx * hx * 0.5f, y0, -0.3f);
                k.Box(b + Vector3.up * 0.2f, new Vector3(0.7f, 0.06f, 0.24f), Graphite);
                k.Box(b + new Vector3(-0.3f, 0.1f, 0f), new Vector3(0.05f, 0.2f, 0.2f), Carbon);
                k.Box(b + new Vector3(0.3f, 0.1f, 0f), new Vector3(0.05f, 0.2f, 0.2f), Carbon);
                Vector3 pl = new Vector3(sx * (hx - 0.3f), y0, kioskZ - 0.95f);
                k.Box(pl + Vector3.up * 0.18f, new Vector3(0.36f, 0.36f, 0.36f), Cream2);
                k.Ball(pl + Vector3.up * 0.44f, new Vector3(0.34f, 0.26f, 0.34f), Plant);
            }
            Vector3 drone = new Vector3(0.4f, y0 + 1.62f, kioskZ);
            k.Box(drone + Vector3.up * 0.08f, new Vector3(0.4f, 0.12f, 0.3f), White);
            k.Box(drone + Vector3.up * 0.15f, new Vector3(0.2f, 0.03f, 0.14f), Orange);
            for (int i = 0; i < 4; i++)
            {
                Vector3 arm = new Vector3(i < 2 ? -0.3f : 0.3f, 0.1f, i % 2 == 0 ? -0.24f : 0.24f);
                k.Beam(drone + Vector3.up * 0.1f, drone + arm, 0.03f, Carbon);
                k.Cyl(drone + arm + Vector3.up * 0.03f, 0.22f, 0.01f, Graphite);
            }
            k.Box(new Vector3(-kioskHalf * 0.5f, y0 + 1.62f, kioskZ), new Vector3(0.5f, 0.02f, 0.5f), Carbon);
            k.Box(new Vector3(-kioskHalf * 0.5f, y0 + 1.635f, kioskZ), new Vector3(0.3f, 0.01f, 0.06f), Yellow);
            RoofUnit(k, new Vector3(-kioskHalf * 0.5f - 0.6f, y0 + 1.58f, kioskZ + 0.2f), 0.4f, 0.36f, 0.2f);
            k.Build();
        }
    }
}
