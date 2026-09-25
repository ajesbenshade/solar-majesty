using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum IconId
    {
        Regolith,
        Ice,
        Metals,
        Power,
        Beds,
        Flag,
        Build,
        Research,
        Threat,
        Robot,
        Alert,
        Speed,
        Commons,
        Pad,
        // HUD glyphs: drawn white, tinted where they are used.
        GlyphBuild,
        GlyphFlag,
        GlyphTech,
        Camp,
        Party,
        Menu,
        Check,
        Pause,
        Play,
        Sun,
        // HUD colour icons.
        Coin,
        House,
        Bag,
        Hammer
    }

    /// <summary>
    /// Runtime-drawn icon set.
    ///
    /// The project has no texture assets and no artist, and a HUD of bare text is the single most
    /// obvious "unfinished" tell. These are drawn from primitives (discs, bars, chevrons) into small
    /// textures with a soft edge, which reads as a deliberate flat icon language at HUD size and
    /// costs nothing to author. Signed-distance style anti-aliasing keeps them crisp at any HUD scale.
    /// </summary>
    public static class UiIcons
    {
        private const int Size = 64;

        private static readonly Dictionary<IconId, Texture2D> Cache = new Dictionary<IconId, Texture2D>(16);

        public static Texture2D Get(IconId id)
        {
            if (Cache.TryGetValue(id, out Texture2D cached) && cached != null)
                return cached;

            Texture2D tex = Build(id);
            Cache[id] = tex;
            return tex;
        }

        public static void Clear()
        {
            foreach (var kv in Cache)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value);
            }
            Cache.Clear();
        }

        private static Texture2D Build(IconId id)
        {
            var px = new Color[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            switch (id)
            {
                case IconId.Regolith: DrawRegolith(px); break;
                case IconId.Ice: DrawIce(px); break;
                case IconId.Metals: DrawMetals(px); break;
                case IconId.Power: DrawPower(px); break;
                case IconId.Beds: DrawBeds(px); break;
                case IconId.Flag: DrawFlag(px); break;
                case IconId.Build: DrawBuild(px); break;
                case IconId.Research: DrawResearch(px); break;
                case IconId.Threat: DrawThreat(px); break;
                case IconId.Robot: DrawRobot(px); break;
                case IconId.Alert: DrawAlert(px); break;
                case IconId.Speed: DrawSpeed(px); break;
                case IconId.Commons: DrawCommons(px); break;
                case IconId.Pad: DrawPad(px); break;
                case IconId.GlyphBuild: DrawGlyphBuild(px); break;
                case IconId.GlyphFlag: DrawGlyphFlag(px); break;
                case IconId.GlyphTech: DrawGlyphTech(px); break;
                case IconId.Camp: DrawCamp(px); break;
                case IconId.Party: DrawParty(px); break;
                case IconId.Menu: DrawMenu(px); break;
                case IconId.Check: DrawCheck(px); break;
                case IconId.Pause: DrawPause(px); break;
                case IconId.Play: DrawPlay(px); break;
                case IconId.Sun: DrawSun(px); break;
                case IconId.Coin: DrawCoin(px); break;
                case IconId.House: DrawHouse(px); break;
                case IconId.Bag: DrawBag(px); break;
                case IconId.Hammer: DrawHammer(px); break;
            }

            // Mips keep the glyphs smooth at the 16-24 px the HUD draws them.
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "SM_Icon_" + id,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ---- primitives -----------------------------------------------------
        // Every shape is a signed distance blended over ~1.5 px, which is what keeps the icons from
        // looking like jagged pixel art when the HUD is scaled.

        private static void Blend(Color[] px, int x, int y, Color c, float coverage)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            coverage = Mathf.Clamp01(coverage);
            if (coverage <= 0f) return;

            int i = y * Size + x;
            Color dst = px[i];
            float a = c.a * coverage;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0.0001f)
            {
                px[i] = Color.clear;
                return;
            }

            Color rgb = (c * a + dst * dst.a * (1f - a)) / outA;
            px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
        }

        private static void Disc(Color[] px, float cx, float cy, float radius, Color c)
        {
            int min = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(cx, cy) - radius - 2f));
            int max = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(cx, cy) + radius + 2f));
            for (int y = min; y <= max; y++)
            for (int x = min; x <= max; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                Blend(px, x, y, c, Mathf.Clamp01(radius - d + 0.5f));
            }
        }

        private static void Ring(Color[] px, float cx, float cy, float radius, float thickness, Color c)
        {
            int min = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(cx, cy) - radius - 2f));
            int max = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(cx, cy) + radius + 2f));
            for (int y = min; y <= max; y++)
            for (int x = min; x <= max; x++)
            {
                float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius);
                Blend(px, x, y, c, Mathf.Clamp01(thickness * 0.5f - d + 0.5f));
            }
        }

        private static void Box(Color[] px, float x0, float y0, float x1, float y1, Color c)
        {
            for (int y = Mathf.Max(0, (int)y0 - 1); y <= Mathf.Min(Size - 1, (int)y1 + 1); y++)
            for (int x = Mathf.Max(0, (int)x0 - 1); x <= Mathf.Min(Size - 1, (int)x1 + 1); x++)
            {
                float cov = Mathf.Clamp01(Mathf.Min(x - x0 + 1f, x1 - x + 1f)) *
                            Mathf.Clamp01(Mathf.Min(y - y0 + 1f, y1 - y + 1f));
                Blend(px, x, y, c, cov);
            }
        }

        private static void Tri(Color[] px, Vector2 a, Vector2 b, Vector2 cc, Color col)
        {
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, cc.x)) - 1f;
            float maxX = Mathf.Max(a.x, Mathf.Max(b.x, cc.x)) + 1f;
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, cc.y)) - 1f;
            float maxY = Mathf.Max(a.y, Mathf.Max(b.y, cc.y)) + 1f;

            for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(Size - 1, (int)maxY); y++)
            for (int x = Mathf.Max(0, (int)minX); x <= Mathf.Min(Size - 1, (int)maxX); x++)
            {
                // 2x2 supersample rather than an exact SDF; plenty at this size.
                float hits = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    var p = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);
                    if (InTriangle(p, a, b, cc)) hits += 0.25f;
                }
                Blend(px, x, y, col, hits);
            }
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        // ---- glyphs ---------------------------------------------------------

        private static readonly Color Dust = new Color(0.80f, 0.58f, 0.36f);
        private static readonly Color IceBlue = new Color(0.55f, 0.86f, 0.98f);
        private static readonly Color Steel = new Color(0.78f, 0.80f, 0.84f);
        private static readonly Color Amber = new Color(1f, 0.74f, 0.22f);
        private static readonly Color Cyan = new Color(0.35f, 0.88f, 0.98f);
        private static readonly Color Red = new Color(0.95f, 0.34f, 0.28f);
        private static readonly Color Bone = new Color(0.94f, 0.94f, 0.92f);

        /// <summary>Three stacked aggregate lumps.</summary>
        private static void DrawRegolith(Color[] px)
        {
            Disc(px, 22f, 22f, 11f, Dust);
            Disc(px, 42f, 20f, 9f, Dust * 0.82f);
            Disc(px, 32f, 38f, 13f, Dust * 0.92f);
        }

        /// <summary>Hexagonal ice crystal.</summary>
        private static void DrawIce(Color[] px)
        {
            var c = new Vector2(32f, 32f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI / 3f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 22f;
                DrawLine(px, c - d, c + d, 5f, IceBlue);
            }
            Disc(px, 32f, 32f, 6f, Color.white);
        }

        /// <summary>Stacked ingots.</summary>
        private static void DrawMetals(Color[] px)
        {
            Box(px, 14f, 34f, 50f, 46f, Steel);
            Box(px, 20f, 20f, 44f, 32f, Steel * 0.85f);
            Box(px, 14f, 34f, 50f, 36f, Color.white * 0.6f);
        }

        /// <summary>Lightning bolt.</summary>
        private static void DrawPower(Color[] px)
        {
            Tri(px, new Vector2(36f, 56f), new Vector2(20f, 32f), new Vector2(34f, 32f), Amber);
            Tri(px, new Vector2(28f, 8f), new Vector2(44f, 32f), new Vector2(30f, 32f), Amber);
        }

        /// <summary>Bunk with a pillow.</summary>
        private static void DrawBeds(Color[] px)
        {
            Box(px, 12f, 24f, 52f, 30f, Bone);
            Box(px, 12f, 30f, 18f, 44f, Bone * 0.8f);
            Box(px, 46f, 30f, 52f, 44f, Bone * 0.8f);
            Disc(px, 22f, 20f, 6f, Cyan);
        }

        /// <summary>Pennant on a pole.</summary>
        private static void DrawFlag(Color[] px)
        {
            Box(px, 18f, 10f, 22f, 54f, Steel);
            Tri(px, new Vector2(22f, 46f), new Vector2(50f, 38f), new Vector2(22f, 28f), Amber);
        }

        /// <summary>Crossed spanner bars.</summary>
        private static void DrawBuild(Color[] px)
        {
            DrawLine(px, new Vector2(14f, 14f), new Vector2(50f, 50f), 7f, Steel);
            DrawLine(px, new Vector2(50f, 14f), new Vector2(14f, 50f), 7f, Steel * 0.8f);
            Disc(px, 32f, 32f, 6f, Amber);
        }

        /// <summary>Atom: nucleus plus an orbit.</summary>
        private static void DrawResearch(Color[] px)
        {
            Ring(px, 32f, 32f, 20f, 4f, Cyan);
            Ring(px, 32f, 32f, 11f, 3f, Cyan * 0.75f);
            Disc(px, 32f, 32f, 6f, Color.white);
        }

        /// <summary>Fanged mandible arc.</summary>
        private static void DrawThreat(Color[] px)
        {
            Ring(px, 32f, 30f, 18f, 6f, Red);
            Tri(px, new Vector2(22f, 30f), new Vector2(28f, 12f), new Vector2(34f, 30f), Red);
            Tri(px, new Vector2(34f, 30f), new Vector2(40f, 12f), new Vector2(46f, 30f), Red);
        }

        /// <summary>Blocky head with a sensor visor.</summary>
        private static void DrawRobot(Color[] px)
        {
            Box(px, 16f, 16f, 48f, 46f, Bone);
            Box(px, 22f, 30f, 42f, 38f, Cyan);
            Box(px, 26f, 46f, 38f, 54f, Steel);
        }

        /// <summary>Warning triangle.</summary>
        private static void DrawAlert(Color[] px)
        {
            Tri(px, new Vector2(32f, 56f), new Vector2(8f, 10f), new Vector2(56f, 10f), Amber);
            Box(px, 30f, 22f, 34f, 42f, new Color(0.1f, 0.09f, 0.08f));
            Disc(px, 32f, 16f, 3f, new Color(0.1f, 0.09f, 0.08f));
        }

        /// <summary>Double chevron.</summary>
        private static void DrawSpeed(Color[] px)
        {
            Tri(px, new Vector2(14f, 50f), new Vector2(34f, 32f), new Vector2(14f, 14f), Bone);
            Tri(px, new Vector2(32f, 50f), new Vector2(52f, 32f), new Vector2(32f, 14f), Bone * 0.75f);
        }

        /// <summary>Geodesic dome.</summary>
        private static void DrawCommons(Color[] px)
        {
            Disc(px, 32f, 26f, 22f, Bone);
            Box(px, 8f, 8f, 56f, 26f, Bone);
            Box(px, 8f, 8f, 56f, 13f, Amber);
            DrawLine(px, new Vector2(32f, 13f), new Vector2(32f, 48f), 2.5f, Steel * 0.6f);
            Ring(px, 32f, 26f, 12f, 2f, Steel * 0.6f);
        }

        /// <summary>Landing pad rings with corner marks.</summary>
        private static void DrawPad(Color[] px)
        {
            Ring(px, 32f, 32f, 24f, 4f, Amber);
            Ring(px, 32f, 32f, 12f, 3f, Bone);
            Box(px, 30f, 8f, 34f, 18f, Bone);
            Box(px, 30f, 46f, 34f, 56f, Bone);
        }

        // ---- HUD glyphs (monochrome white: tinted at draw time) ------------

        /// <summary>Claw hammer, for the Build verb.</summary>
        private static void DrawGlyphBuild(Color[] px)
        {
            DrawLine(px, new Vector2(16f, 10f), new Vector2(40f, 40f), 6f, Color.white);
            DrawLine(px, new Vector2(31f, 51f), new Vector2(53f, 33f), 11f, Color.white);
            Disc(px, 31f, 51f, 5.5f, Color.white);
        }

        /// <summary>Pennant on a pole, for the Flag verb.</summary>
        private static void DrawGlyphFlag(Color[] px)
        {
            Box(px, 15f, 8f, 20f, 56f, Color.white);
            Disc(px, 17.5f, 56f, 3.5f, Color.white);
            Tri(px, new Vector2(20f, 54f), new Vector2(54f, 44f), new Vector2(20f, 30f), Color.white);
        }

        /// <summary>Atom: three orbits round a nucleus, for Research.</summary>
        private static void DrawGlyphTech(Color[] px)
        {
            var c = new Vector2(32f, 32f);
            Ellipse(px, c, 25f, 9f, 0f, 3.6f, Color.white);
            Ellipse(px, c, 25f, 9f, 60f, 3.6f, Color.white);
            Ellipse(px, c, 25f, 9f, 120f, 3.6f, Color.white);
            Disc(px, 32f, 32f, 5.5f, Color.white);
        }

        /// <summary>Two opposed arrows: hop between campuses.</summary>
        private static void DrawCamp(Color[] px)
        {
            DrawLine(px, new Vector2(10f, 42f), new Vector2(42f, 42f), 5f, Color.white);
            Tri(px, new Vector2(40f, 52f), new Vector2(55f, 42f), new Vector2(40f, 32f), Color.white);
            DrawLine(px, new Vector2(22f, 20f), new Vector2(54f, 20f), 5f, Color.white);
            Tri(px, new Vector2(24f, 30f), new Vector2(9f, 20f), new Vector2(24f, 10f), Color.white);
        }

        /// <summary>Three figures: form a party.</summary>
        private static void DrawParty(Color[] px)
        {
            var side = new Color(1f, 1f, 1f, 0.6f);
            Disc(px, 15f, 38f, 6f, side);
            Disc(px, 15f, 21f, 9f, side);
            Box(px, 6f, 8f, 24f, 21f, side);
            Disc(px, 49f, 38f, 6f, side);
            Disc(px, 49f, 21f, 9f, side);
            Box(px, 40f, 8f, 58f, 21f, side);
            Disc(px, 32f, 43f, 8.5f, Color.white);
            Disc(px, 32f, 22f, 12.5f, Color.white);
            Box(px, 19.5f, 8f, 44.5f, 22f, Color.white);
        }

        /// <summary>Three bars: the pause menu.</summary>
        private static void DrawMenu(Color[] px)
        {
            DrawLine(px, new Vector2(16f, 20f), new Vector2(48f, 20f), 5.5f, Color.white);
            DrawLine(px, new Vector2(16f, 32f), new Vector2(48f, 32f), 5.5f, Color.white);
            DrawLine(px, new Vector2(16f, 44f), new Vector2(48f, 44f), 5.5f, Color.white);
        }

        private static void DrawCheck(Color[] px)
        {
            DrawLine(px, new Vector2(13f, 33f), new Vector2(26f, 19f), 8f, Color.white);
            DrawLine(px, new Vector2(26f, 19f), new Vector2(52f, 46f), 8f, Color.white);
        }

        private static void DrawPause(Color[] px)
        {
            Box(px, 17f, 13f, 27f, 51f, Color.white);
            Box(px, 37f, 13f, 47f, 51f, Color.white);
        }

        private static void DrawPlay(Color[] px)
        {
            Tri(px, new Vector2(19f, 11f), new Vector2(19f, 53f), new Vector2(53f, 32f), Color.white);
        }

        /// <summary>Sun: the sol clock.</summary>
        private static void DrawSun(Color[] px)
        {
            Disc(px, 32f, 32f, 10.5f, Color.white);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                DrawLine(px, new Vector2(32f, 32f) + d * 16f, new Vector2(32f, 32f) + d * 26f, 4.5f, Color.white);
            }
        }

        // ---- HUD colour icons -----------------------------------------------

        private static readonly Color CoinGold = new Color(1f, 0.81f, 0.38f);
        private static readonly Color CoinDeep = new Color(0.70f, 0.47f, 0.15f);

        /// <summary>Gold coin struck with the Solar Majesty sun — the treasury.</summary>
        private static void DrawCoin(Color[] px)
        {
            Disc(px, 32f, 31f, 27f, CoinDeep);
            Disc(px, 32f, 33f, 26f, CoinGold);
            Ring(px, 32f, 33f, 21f, 2.5f, CoinDeep);
            Disc(px, 32f, 33f, 6.5f, CoinDeep);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                DrawLine(px, new Vector2(32f, 33f) + d * 10f, new Vector2(32f, 33f) + d * 16f, 3f, CoinDeep);
            }
            Disc(px, 22f, 46f, 5f, new Color(1f, 1f, 1f, 0.45f));
        }

        /// <summary>Pitched-roof house: tax-paying homes.</summary>
        private static void DrawHouse(Color[] px)
        {
            Box(px, 14f, 8f, 50f, 34f, Bone);
            Tri(px, new Vector2(6f, 32f), new Vector2(32f, 56f), new Vector2(58f, 32f), new Color(0.96f, 0.52f, 0.20f));
            Box(px, 28f, 8f, 36f, 22f, new Color(0.16f, 0.13f, 0.11f));
            Box(px, 18f, 22f, 24f, 28f, Cyan);
            Box(px, 40f, 22f, 46f, 28f, Cyan);
        }

        /// <summary>Tied coin sack: tax collectors on their rounds.</summary>
        private static void DrawBag(Color[] px)
        {
            var sack = new Color(0.84f, 0.64f, 0.36f);
            Disc(px, 32f, 24f, 18f, sack);
            Tri(px, new Vector2(20f, 54f), new Vector2(32f, 40f), new Vector2(44f, 54f), sack * 0.92f);
            Box(px, 26f, 36f, 38f, 44f, sack * 0.9f);
            DrawLine(px, new Vector2(23f, 41f), new Vector2(41f, 41f), 3.5f, new Color(0.40f, 0.24f, 0.10f));
            Ring(px, 32f, 23f, 7f, 3f, CoinGold);
        }

        /// <summary>Wood-and-steel hammer: the village raising buildings.</summary>
        private static void DrawHammer(Color[] px)
        {
            DrawLine(px, new Vector2(16f, 10f), new Vector2(40f, 40f), 6f, new Color(0.70f, 0.48f, 0.28f));
            DrawLine(px, new Vector2(31f, 51f), new Vector2(53f, 33f), 11f, Steel);
            Disc(px, 31f, 51f, 5.5f, Steel);
        }

        private static void Ellipse(Color[] px, Vector2 c, float rx, float ry, float angleDeg, float thickness, Color col)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);
            float reach = Mathf.Max(rx, ry) + thickness + 1f;
            int minX = Mathf.Max(0, (int)(c.x - reach));
            int maxX = Mathf.Min(Size - 1, (int)(c.x + reach));
            int minY = Mathf.Max(0, (int)(c.y - reach));
            int maxY = Mathf.Min(Size - 1, (int)(c.y + reach));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - c.x;
                float dy = y + 0.5f - c.y;
                float u = dx * cos + dy * sin;
                float v = -dx * sin + dy * cos;
                float k = Mathf.Sqrt(u * u / (rx * rx) + v * v / (ry * ry));
                if (k < 0.0001f) continue;
                float gx = u / (rx * rx * k);
                float gy = v / (ry * ry * k);
                float dist = Mathf.Abs(k - 1f) / Mathf.Max(0.0001f, Mathf.Sqrt(gx * gx + gy * gy));
                Blend(px, x, y, col, Mathf.Clamp01(thickness * 0.5f - dist + 0.5f));
            }
        }

        private static void DrawLine(Color[] px, Vector2 a, Vector2 b, float thickness, Color c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;
            Vector2 dir = d / len;

            int minX = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - thickness - 1f));
            int maxX = Mathf.Min(Size - 1, (int)(Mathf.Max(a.x, b.x) + thickness + 1f));
            int minY = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - thickness - 1f));
            int maxY = Mathf.Min(Size - 1, (int)(Mathf.Max(a.y, b.y) + thickness + 1f));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp(Vector2.Dot(p - a, dir), 0f, len);
                float dist = Vector2.Distance(p, a + dir * t);
                Blend(px, x, y, c, Mathf.Clamp01(thickness * 0.5f - dist + 0.5f));
            }
        }
    }
}
