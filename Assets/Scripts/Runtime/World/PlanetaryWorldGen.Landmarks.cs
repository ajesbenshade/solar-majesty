using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Themed landmarks for the airless and icy worlds, plus the extra Earth set: an Apollo-style
    /// descent stage, rover wrecks, a rolled boulder and its track, Martian hoodoos and a dusted-over
    /// hab, a Belt mining rig, geode and hull wreck, Europan geysers, penitente fields and a cryobot
    /// drill, an Earth homestead and a fallen giant tree. All built with <see cref="DetailBatch"/>
    /// in the landmark's local space; <c>g</c> gives the ground offset under a local point so long
    /// pieces (tracks, fences, trunks) follow the terrain.
    /// </summary>
    public partial class PlanetaryWorldGen
    {
        private static readonly Color LmGold = new Color(0.86f, 0.66f, 0.24f);
        private static readonly Color LmGoldDark = new Color(0.62f, 0.44f, 0.16f);
        private static readonly Color LmWhite = new Color(0.88f, 0.88f, 0.86f);
        private static readonly Color LmDarkMetal = new Color(0.26f, 0.27f, 0.29f);
        private static readonly Color LmRed = new Color(0.78f, 0.18f, 0.12f);
        private static readonly Color LmRedEmit = new Color(2.2f, 0.25f, 0.08f);
        private static readonly Color LmAmber = new Color(0.95f, 0.62f, 0.12f);
        private static readonly Color LmAmberEmit = new Color(2.4f, 1.3f, 0.2f);
        private static readonly Color LmWood = new Color(0.46f, 0.31f, 0.18f);
        private static readonly Color LmWoodDark = new Color(0.32f, 0.21f, 0.12f);
        private static readonly Color LmHay = new Color(0.86f, 0.72f, 0.38f);
        private static readonly Color LmLeaf = new Color(0.30f, 0.50f, 0.20f);
        private static readonly Color LmIce = new Color(0.82f, 0.92f, 0.98f);
        private static readonly Color LmIceDeep = new Color(0.55f, 0.75f, 0.90f);
        private static readonly Color LmIceEmit = new Color(0.5f, 1.4f, 2.4f);
        private static readonly Color LmCrystal = new Color(0.62f, 0.42f, 1.0f);
        private static readonly Color LmCrystalEmit = new Color(1.3f, 0.6f, 2.6f);
        private static readonly Color LmTeal = new Color(0.30f, 0.85f, 0.85f);
        private static readonly Color LmTealEmit = new Color(0.3f, 2.0f, 1.9f);
        private static readonly Color LmSolar = new Color(0.10f, 0.16f, 0.32f);
        private static readonly Color LmSolarEmit = new Color(0.04f, 0.16f, 0.5f);

        // ------------------------------------------------------------------ Earth extras

        private static void Homestead(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // Stone cottage shell with a fallen roof.
            k.Box(new Vector3(-1.5f, 1.1f, 2.2f), new Vector3(4.6f, 2.2f, 0.45f), PoiStone);
            k.Box(new Vector3(-3.6f, 0.9f, 0.6f), new Vector3(0.45f, 1.8f, 3.4f), PoiStoneDark);
            k.Box(new Vector3(0.6f, 0.6f, 0.9f), new Vector3(0.45f, 1.2f, 2.2f), PoiStone);
            k.Box(new Vector3(-2.6f, 0.35f, -1.0f), new Vector3(2.2f, 0.7f, 0.45f), PoiStone);
            k.Box(new Vector3(-1.5f, 2.55f, 2.2f), new Vector3(1.2f, 0.7f, 0.45f), Quaternion.Euler(0f, 0f, 45f), PoiStone);
            k.Box(new Vector3(-2.9f, 2.3f, 2.25f), new Vector3(0.6f, 1.6f, 0.6f), PoiStoneDark); // chimney
            for (int i = 0; i < 5; i++)
                k.Beam(new Vector3(-3.4f + i * 0.9f, 2.1f, 2.0f), new Vector3(-3.1f + i * 0.85f, 0.2f, -0.6f), 0.14f, LmWoodDark);
            k.Box(new Vector3(-1.6f, 0.9f, 0.7f), new Vector3(3.6f, 0.08f, 2.6f), Quaternion.Euler(28f, 0f, 6f), LmWood);
            // Fenced field with crop rows.
            var fieldC = new Vector3(3.6f, 0f, -1.2f);
            for (int row = 0; row < 7; row++)
            {
                var p = fieldC + new Vector3(-2.2f + row * 0.72f, 0f, 0f);
                k.Box(p + Vector3.up * (0.12f + g(p)), new Vector3(0.26f, 0.24f, 4.6f), row % 2 == 0 ? LmLeaf : LmLeaf * 0.85f);
                k.Box(p + new Vector3(0.36f, 0.03f + g(p), 0f), new Vector3(0.3f, 0.06f, 4.8f), LmWoodDark * 0.9f);
            }
            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    Vector3 a = side switch
                    {
                        0 => fieldC + new Vector3(-3f + t * 6f, 0f, -2.9f),
                        1 => fieldC + new Vector3(-3f + t * 6f, 0f, 2.9f),
                        2 => fieldC + new Vector3(-3f, 0f, -2.9f + t * 5.8f),
                        _ => fieldC + new Vector3(3f, 0f, -2.9f + t * 5.8f)
                    };
                    if (side == 1 && i == 3) continue; // gate gap
                    float y = g(a);
                    k.Box(a + Vector3.up * (0.55f + y), new Vector3(0.14f, 1.1f, 0.14f), LmWoodDark);
                    if (i < 6)
                    {
                        Vector3 b = side switch
                        {
                            0 => a + new Vector3(1f, 0f, 0f),
                            1 => a + new Vector3(1f, 0f, 0f),
                            _ => a + new Vector3(0f, 0f, 5.8f / 6f)
                        };
                        float yb = g(b);
                        k.Beam(a + Vector3.up * (0.8f + y), b + Vector3.up * (0.8f + yb), 0.07f, LmWood);
                        k.Beam(a + Vector3.up * (0.45f + y), b + Vector3.up * (0.45f + yb), 0.07f, LmWood);
                    }
                }
            }
            // Hay bales, a cart and a stone well.
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(-1.8f + i * 1.1f + Rf(r, -0.2f, 0.2f), 0.45f, -3.2f + Rf(r, -0.3f, 0.3f));
                k.Cyl(p + Vector3.up * g(p), 0.9f, 1.1f, Quaternion.Euler(0f, Rf(r, 0f, 40f), 90f), LmHay);
            }
            var cart = new Vector3(-4.6f, 0f, -2.6f);
            float cy = g(cart);
            k.Box(cart + new Vector3(0f, 0.75f + cy, 0f), new Vector3(1.4f, 0.5f, 2.2f), Quaternion.Euler(0f, 20f, 4f), LmWood);
            k.Cyl(cart + new Vector3(-0.8f, 0.5f + cy, -0.4f), 1.0f, 0.12f, Quaternion.Euler(0f, 20f, 90f), LmWoodDark);
            k.Cyl(cart + new Vector3(0.75f, 0.5f + cy, 0.15f), 1.0f, 0.12f, Quaternion.Euler(0f, 20f, 90f), LmWoodDark);
            k.Beam(cart + new Vector3(0.2f, 0.6f + cy, -1.2f), cart + new Vector3(0.6f, 0.15f + cy, -2.6f), 0.1f, LmWood);
            var well = new Vector3(1.8f, 0f, 3.4f);
            float wy = g(well);
            k.Cyl(well + Vector3.up * (0.45f + wy), 1.5f, 0.9f, PoiStone);
            k.Cyl(well + Vector3.up * (0.88f + wy), 1.0f, 0.05f, new Color(0.10f, 0.18f, 0.22f));
            k.Beam(well + new Vector3(-0.7f, 0.9f + wy, 0f), well + new Vector3(-0.7f, 2.2f + wy, 0f), 0.12f, LmWoodDark);
            k.Beam(well + new Vector3(0.7f, 0.9f + wy, 0f), well + new Vector3(0.7f, 2.2f + wy, 0f), 0.12f, LmWoodDark);
            k.Box(well + new Vector3(0f, 2.35f + wy, 0f), new Vector3(1.9f, 0.08f, 1.2f), Quaternion.Euler(0f, 0f, 0f), LmWood);
            k.Rod(well + new Vector3(-0.7f, 1.8f + wy, 0f), well + new Vector3(0.7f, 1.8f + wy, 0f), 0.1f, LmWood);
        }

        private static void FallenGiant(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // A huge trunk lying along +X, its root plate standing up at the -X end.
            var a = new Vector3(-5.5f, 0f, 0f);
            var b = new Vector3(6.5f, 0f, 0.6f);
            float ya = g(a) + 0.75f, yb = g(b) + 0.55f;
            k.Rod(a + Vector3.up * ya, b + Vector3.up * yb, 1.5f, LmWoodDark);
            k.Rod(a + Vector3.up * (ya + 0.05f), Vector3.Lerp(a, b, 0.5f) + Vector3.up * ((ya + yb) * 0.5f + 0.05f), 1.42f, LmWood * 0.8f);
            k.Cyl(new Vector3(-6.0f, 1.6f + g(a), 0f), 4.2f, 0.7f, Quaternion.Euler(0f, 0f, 90f), new Color(0.36f, 0.27f, 0.18f));
            for (int i = 0; i < 9; i++)
            {
                float ang = i * 40f * Mathf.Deg2Rad;
                var tip = new Vector3(-6.2f, 1.6f + Mathf.Sin(ang) * 2.4f + g(a), Mathf.Cos(ang) * 2.4f);
                k.Rod(new Vector3(-6.0f, 1.6f + g(a), 0f), tip, 0.18f, LmWoodDark);
            }
            // Broken branches, a split end, moss, mushrooms and ferns along the log.
            for (int i = 0; i < 5; i++)
            {
                float t = 0.2f + i * 0.15f;
                var p = Vector3.Lerp(a, b, t);
                float y = Mathf.Lerp(ya, yb, t);
                var dir = new Vector3(Rf(r, -0.4f, 0.4f), 1f, i % 2 == 0 ? 0.9f : -0.9f).normalized;
                k.Rod(p + Vector3.up * y, p + Vector3.up * y + dir * Rf(r, 1.2f, 2.2f), 0.28f, LmWoodDark);
            }
            for (int i = 0; i < 6; i++)
            {
                float t = Rf(r, 0.05f, 0.95f);
                var p = Vector3.Lerp(a, b, t);
                k.Ball(p + Vector3.up * (Mathf.Lerp(ya, yb, t) + 0.6f), new Vector3(Rf(r, 0.8f, 1.6f), 0.25f, 0.9f), PoiMoss);
            }
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(Rf(r, -4f, 5f), 0f, (i % 2 == 0 ? 1.1f : -1.0f) + Rf(r, -0.3f, 0.3f));
                float y = g(p);
                float h = Rf(r, 0.2f, 0.45f);
                k.Cyl(p + Vector3.up * (h * 0.5f + y), 0.09f, h, new Color(0.92f, 0.88f, 0.8f));
                k.Ball(p + Vector3.up * (h + y), new Vector3(0.36f, 0.16f, 0.36f), i % 3 == 0 ? LmRed : new Color(0.62f, 0.42f, 0.26f));
            }
            k.Box(b + new Vector3(0.35f, yb, 0f), new Vector3(0.3f, 1.3f, 1.3f), Quaternion.Euler(0f, 0f, 15f), new Color(0.72f, 0.58f, 0.4f));
        }

        // ------------------------------------------------------------------ Luna

        private static void Lander(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // Descent stage: an octagon of gold foil on four splayed legs.
            k.Box(new Vector3(0f, 1.5f, 0f), new Vector3(2.6f, 1.2f, 2.6f), LmGold);
            k.Box(new Vector3(0f, 1.5f, 0f), new Vector3(2.6f, 1.2f, 2.6f), Quaternion.Euler(0f, 45f, 0f), LmGoldDark);
            k.Box(new Vector3(0f, 2.15f, 0f), new Vector3(2.9f, 0.1f, 2.9f), Quaternion.Euler(0f, 22.5f, 0f), LmDarkMetal);
            k.Cyl(new Vector3(0f, 0.75f, 0f), 1.0f, 0.5f, LmDarkMetal); // engine bell
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + i * 90f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var foot = dir * 2.7f;
                k.Rod(dir * 1.2f + Vector3.up * 1.9f, foot + Vector3.up * 0.25f, 0.14f, LmGold);
                k.Rod(dir * 1.3f + Vector3.up * 1.0f, foot + Vector3.up * 0.3f, 0.08f, LmWhite);
                k.Cyl(foot + Vector3.up * 0.06f, 0.75f, 0.12f, LmWhite);
                if (i == 0)
                    for (int s = 0; s < 6; s++)
                        k.Box(Vector3.Lerp(dir * 1.25f + Vector3.up * 1.6f, foot + Vector3.up * 0.35f, s / 5f) + new Vector3(dir.z, 0f, -dir.x) * 0.22f,
                            new Vector3(0.5f, 0.04f, 0.12f), Quaternion.LookRotation(dir), LmWhite);
            }
            // Flag, plaque, experiment packages, a dish, a dropped tool rack.
            k.Rod(new Vector3(-3.8f, 0f, 1.2f), new Vector3(-3.8f, 2.6f, 1.2f), 0.05f, LmWhite);
            // Colony banner: orange field, white chevron, gold sun disc.
            k.Box(new Vector3(-3.3f, 2.25f, 1.2f), new Vector3(1.0f, 0.62f, 0.02f), new Color(0.95f, 0.45f, 0.12f));
            k.Box(new Vector3(-3.3f, 2.25f, 1.19f), new Vector3(0.5f, 0.08f, 0.02f), Quaternion.Euler(0f, 0f, 35f), LmWhite);
            k.Box(new Vector3(-3.3f, 2.25f, 1.19f), new Vector3(0.5f, 0.08f, 0.02f), Quaternion.Euler(0f, 0f, -35f), LmWhite);
            k.Cyl(new Vector3(-3.3f, 2.4f, 1.185f), 0.18f, 0.02f, Quaternion.Euler(90f, 0f, 0f), LmGold);
            k.Box(new Vector3(3.8f, 0.3f, -2.2f), new Vector3(0.9f, 0.6f, 0.7f), LmGold);
            k.Box(new Vector3(3.8f, 0.66f, -2.2f), new Vector3(1.0f, 0.06f, 0.8f), LmWhite);
            k.Box(new Vector3(4.6f, 0.2f, -0.8f), new Vector3(0.6f, 0.4f, 0.6f), LmWhite);
            k.Rod(new Vector3(4.6f, 0.4f, -0.8f), new Vector3(4.6f, 1.3f, -0.8f), 0.05f, LmDarkMetal);
            k.Add(PrimitiveType.Sphere, new Vector3(4.6f, 1.35f, -0.8f), new Vector3(0.8f, 0.12f, 0.8f), Quaternion.Euler(-30f, 0f, 0f), LmWhite);
            k.Box(new Vector3(2.8f, 0.04f, 2.6f), new Vector3(1.1f, 0.08f, 0.8f), Quaternion.Euler(0f, 30f, 0f), LmDarkMetal);
            // Boot prints and rover tracks heading off across the regolith.
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3(-1.2f - i * 0.45f, 0f, -1.8f - Mathf.Sin(i * 0.4f) * 0.6f + (i % 2) * 0.22f);
                k.Box(p + Vector3.up * (0.01f + g(p)), new Vector3(0.2f, 0.02f, 0.09f), new Color(0.40f, 0.39f, 0.38f));
            }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 16; i++)
                {
                    var p = new Vector3(2.2f + i * 0.8f, 0f, 3.2f + side * 0.9f + i * 0.08f);
                    k.Box(p + Vector3.up * (0.01f + g(p)), new Vector3(0.82f, 0.02f, 0.2f), new Color(0.40f, 0.39f, 0.38f));
                }
        }

        private static void RoverWreck(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            var tilt = Quaternion.Euler(4f, 18f, -7f);
            Vector3 T(Vector3 v) => tilt * v;
            // Chassis, deck, mast with camera head, high-gain dish, RTG at the back.
            k.Box(T(new Vector3(0f, 0.85f, 0f)), new Vector3(1.6f, 0.45f, 2.6f), tilt, LmWhite);
            k.Box(T(new Vector3(0f, 1.12f, 0f)), new Vector3(1.75f, 0.08f, 2.8f), tilt, LmGoldDark);
            k.Rod(T(new Vector3(0.5f, 1.15f, 1.0f)), T(new Vector3(0.5f, 2.4f, 1.0f)), 0.1f, LmWhite);
            k.Box(T(new Vector3(0.5f, 2.5f, 1.05f)), new Vector3(0.55f, 0.28f, 0.3f), tilt, LmWhite);
            k.Box(T(new Vector3(0.5f, 2.5f, 1.22f)), new Vector3(0.36f, 0.12f, 0.05f), tilt, new Color(0.08f, 0.09f, 0.12f));
            k.Rod(T(new Vector3(-0.5f, 1.15f, 0.3f)), T(new Vector3(-0.5f, 1.6f, 0.3f)), 0.06f, LmDarkMetal);
            k.Add(PrimitiveType.Sphere, T(new Vector3(-0.5f, 1.65f, 0.3f)), new Vector3(0.9f, 0.12f, 0.9f), tilt * Quaternion.Euler(-25f, 0f, 0f), LmWhite);
            k.Cyl(T(new Vector3(0f, 1.05f, -1.55f)), 0.5f, 0.8f, tilt * Quaternion.Euler(60f, 0f, 0f), LmDarkMetal);
            for (int i = 0; i < 4; i++)
                k.Box(T(new Vector3(0f, 1.05f, -1.55f)), new Vector3(0.05f, 0.7f, 0.9f), tilt * Quaternion.Euler(60f, i * 45f, 0f), LmDarkMetal);
            // Six wheels on rocker arms; one has come off.
            for (int i = 0; i < 6; i++)
            {
                int side = i < 3 ? -1 : 1;
                float z = (i % 3 - 1) * 1.05f;
                var hub = new Vector3(side * 1.05f, 0.42f, z);
                if (i == 5)
                {
                    var loose = new Vector3(2.6f, 0f, -1.6f);
                    k.Cyl(loose + Vector3.up * (0.12f + g(loose)), 0.85f, 0.4f, Quaternion.Euler(0f, 30f, 0f), LmDarkMetal);
                    continue;
                }
                k.Cyl(T(hub), 0.85f, 0.4f, tilt * Quaternion.Euler(0f, 0f, 90f), LmDarkMetal);
                k.Cyl(T(hub + new Vector3(side * 0.21f, 0f, 0f)), 0.4f, 0.04f, tilt * Quaternion.Euler(0f, 0f, 90f), LmWhite);
                k.Beam(T(hub + new Vector3(-side * 0.15f, 0.1f, 0f)), T(new Vector3(side * 0.75f, 0.85f, z * 0.6f)), 0.08f, LmWhite);
            }
            // Drifted regolith against the low side, scattered panels, tracks behind.
            k.Ball(T(new Vector3(-1.2f, 0.15f, 0.2f)), new Vector3(1.2f, 0.5f, 3.2f), new Color(0.55f, 0.5f, 0.45f));
            for (int i = 0; i < 5; i++)
                k.Box(new Vector3(Rf(r, -3f, 3f), 0.04f, Rf(r, -3f, 3f)), new Vector3(Rf(r, 0.3f, 0.7f), 0.04f, Rf(r, 0.3f, 0.6f)),
                    Quaternion.Euler(0f, Rf(r, 0f, 180f), 0f), i % 2 == 0 ? LmWhite : LmGoldDark);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 14; i++)
                {
                    var p = tilt * new Vector3(side * 1.05f, 0f, -2.2f - i * 0.75f);
                    k.Box(new Vector3(p.x, 0.012f + g(p), p.z), new Vector3(0.38f, 0.024f, 0.78f), tilt, new Color(0.34f, 0.3f, 0.27f) * 0.8f);
                }
        }

        private static void BoulderTrack(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // A house-sized boulder that rolled down a slope, leaving a dotted trail of pits.
            k.Ball(new Vector3(0f, 1.6f + g(Vector3.zero), 0f), new Vector3(3.8f, 3.3f, 3.5f), new Color(0.48f, 0.47f, 0.45f));
            k.Ball(new Vector3(0.9f, 2.4f + g(Vector3.zero), -0.5f), new Vector3(2.0f, 1.6f, 1.9f), new Color(0.56f, 0.55f, 0.53f));
            k.Ball(new Vector3(-1.4f, 0.6f, 1.4f), new Vector3(1.4f, 0.9f, 1.2f), new Color(0.44f, 0.43f, 0.41f));
            for (int i = 0; i < 26; i++)
            {
                float t = i * 0.75f;
                var p = new Vector3(Mathf.Sin(t * 0.13f) * 0.9f, 0f, -2.6f - t);
                float y = g(p);
                k.Ball(p + Vector3.up * (y - 0.02f), new Vector3(1.1f, 0.08f, 0.6f), new Color(0.30f, 0.29f, 0.28f));
                if (i % 3 == 0)
                    k.Ball(p + new Vector3(0.75f, y + 0.04f, 0f), new Vector3(0.3f, 0.12f, 0.24f), new Color(0.6f, 0.59f, 0.57f));
            }
        }

        // ------------------------------------------------------------------ Mars

        private static void Hoodoos(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            var bandA = new Color(0.62f, 0.38f, 0.24f);
            var bandB = new Color(0.48f, 0.27f, 0.17f);
            var cap = new Color(0.36f, 0.22f, 0.15f);
            for (int h = 0; h < 7; h++)
            {
                float ang = h * 0.9f + Rf(r, -0.2f, 0.2f);
                float dist = h == 0 ? 0f : Rf(r, 2.0f, 4.6f);
                var p = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float y = g(p);
                float tall = h == 0 ? 6.5f : Rf(r, 2.4f, 5.2f);
                int layers = Mathf.Max(3, Mathf.RoundToInt(tall / 0.8f));
                float baseW = Rf(r, 1.2f, 1.9f);
                for (int L = 0; L < layers; L++)
                {
                    float t = L / (float)layers;
                    // Pinched waist, wider bands where harder rock resists the wind.
                    float w = baseW * (1f - 0.45f * Mathf.Sin(t * Mathf.PI * 0.9f)) * (L % 2 == 0 ? 1f : 0.88f);
                    k.Cyl(p + Vector3.up * (y + (L + 0.5f) * tall / layers), w, tall / layers, Quaternion.Euler(0f, L * 23f, 0f),
                        L % 2 == 0 ? bandA : bandB);
                }
                k.Box(p + Vector3.up * (y + tall + 0.25f), new Vector3(baseW * 1.3f, 0.5f, baseW * 1.1f),
                    Quaternion.Euler(Rf(r, -6f, 6f), Rf(r, 0f, 90f), Rf(r, -6f, 6f)), cap);
            }
            for (int i = 0; i < 16; i++)
            {
                var p = new Vector3(Rf(r, -5.5f, 5.5f), 0f, Rf(r, -5.5f, 5.5f));
                float s = Rf(r, 0.25f, 0.8f);
                k.Box(p + Vector3.up * (s * 0.3f + g(p)), new Vector3(s, s * 0.6f, s * 0.8f),
                    Quaternion.Euler(Rf(r, 0f, 30f), Rf(r, 0f, 90f), Rf(r, 0f, 30f)), i % 2 == 0 ? bandB : cap);
            }
        }

        private static void DustHab(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            var dust = new Color(0.66f, 0.44f, 0.30f);
            // Inflatable dome half-buried, an airlock tube, a toppled mast, dusty solar rows.
            k.Ball(new Vector3(0f, 0.6f, 0f), new Vector3(6.2f, 3.6f, 6.2f), new Color(0.84f, 0.80f, 0.74f));
            for (int i = 0; i < 6; i++)
                k.Box(new Vector3(0f, 1.6f, 0f), new Vector3(0.08f, 3.2f, 6.25f), Quaternion.Euler(0f, i * 30f, 0f), new Color(0.6f, 0.58f, 0.55f));
            k.Ball(new Vector3(-1.8f, 0.5f, 1.6f), new Vector3(4.4f, 1.8f, 3.0f), dust);
            k.Ball(new Vector3(2.2f, 0.2f, -1.2f), new Vector3(3.2f, 1.1f, 2.4f), dust);
            k.Cyl(new Vector3(0f, 0.9f, -3.6f), 1.5f, 2.2f, Quaternion.Euler(90f, 0f, 0f), LmWhite);
            k.Box(new Vector3(0f, 0.9f, -4.75f), new Vector3(1.2f, 1.4f, 0.12f), LmDarkMetal);
            k.Box(new Vector3(0f, 1.8f, -4.7f), new Vector3(0.25f, 0.12f, 0.1f), LmRed, LmRedEmit);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector3(4.2f + i * 1.5f, 0f, -2.5f + row * 1.6f);
                    float y = g(p);
                    k.Rod(p + Vector3.up * y, p + Vector3.up * (0.6f + y), 0.07f, LmDarkMetal);
                    k.Box(p + Vector3.up * (0.68f + y), new Vector3(1.3f, 0.05f, 0.9f), Quaternion.Euler(0f, 0f, 25f), LmSolar * 1.4f, LmSolarEmit * 0.5f);
                    k.Box(p + Vector3.up * (0.73f + y), new Vector3(0.6f, 0.03f, 0.5f), Quaternion.Euler(0f, 0f, 25f), dust);
                }
            var mast = new Vector3(-4.2f, 0f, -1.2f);
            float my = g(mast);
            k.Rod(mast + Vector3.up * (0.2f + my), mast + new Vector3(3.2f, 0.25f + my, 1.4f), 0.12f, LmWhite);
            k.Add(PrimitiveType.Sphere, mast + new Vector3(3.3f, 0.5f + my, 1.45f), new Vector3(1.0f, 0.16f, 1.0f), Quaternion.Euler(70f, 30f, 0f), LmWhite);
            for (int i = 0; i < 6; i++)
                k.Box(new Vector3(Rf(r, -5f, 5f), 0.15f, Rf(r, -5f, 5f)), Vector3.one * Rf(r, 0.3f, 0.6f),
                    Quaternion.Euler(Rf(r, 0f, 20f), Rf(r, 0f, 90f), 0f), i % 2 == 0 ? LmWhite : new Color(0.55f, 0.48f, 0.38f));
        }

        // ------------------------------------------------------------------ Belt

        private static void MiningRig(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // Tripod derrick over a drill hole, a deck with tanks and a hopper feeding an ore pile.
            var top = new Vector3(0f, 7.2f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float ang = (90f + i * 120f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(ang) * 3.0f, 0f, Mathf.Sin(ang) * 3.0f);
                k.Rod(foot + Vector3.up * g(foot), top, 0.28f, LmAmber);
                for (int b = 1; b < 4; b++)
                {
                    float t = b / 4f;
                    var p = Vector3.Lerp(foot, top, t);
                    float ang2 = (90f + (i + 1) * 120f) * Mathf.Deg2Rad;
                    var p2 = Vector3.Lerp(new Vector3(Mathf.Cos(ang2) * 3f, 0f, Mathf.Sin(ang2) * 3f), top, t);
                    k.Beam(p, p2, 0.1f, LmDarkMetal);
                }
                k.Cyl(foot + Vector3.up * (0.15f + g(foot)), 0.9f, 0.3f, LmDarkMetal);
            }
            k.Box(top + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 1.2f), LmDarkMetal);
            k.Ball(top + Vector3.up * 0.95f, 0.3f, LmAmber, LmAmberEmit);
            k.Rod(top, new Vector3(0f, 0.2f, 0f), 0.22f, LmWhite);
            k.Cyl(new Vector3(0f, 0.05f, 0f), 1.6f, 0.1f, new Color(0.06f, 0.05f, 0.05f));
            k.Box(new Vector3(-3.6f, 0.6f, -2.2f), new Vector3(3.2f, 1.2f, 2.4f), LmDarkMetal);
            k.Box(new Vector3(-3.6f, 1.25f, -2.2f), new Vector3(3.3f, 0.1f, 2.5f), LmAmber);
            for (int i = 0; i < 2; i++)
                k.Cyl(new Vector3(-4.4f + i * 1.5f, 2.0f, -2.2f), 1.1f, 1.4f, LmWhite);
            k.Box(new Vector3(-2.4f, 1.6f, -1.0f), new Vector3(0.3f, 0.3f, 0.3f), LmRed, LmRedEmit);
            // Hopper and conveyor to the ore pile.
            k.Box(new Vector3(3.2f, 1.6f, -2.0f), new Vector3(1.6f, 1.2f, 1.6f), Quaternion.Euler(0f, 0f, 0f), LmAmber);
            k.Rod(new Vector3(3.2f, 0f, -2.0f), new Vector3(3.2f, 1.0f, -2.0f), 0.2f, LmDarkMetal);
            k.Beam(new Vector3(3.2f, 1.2f, -1.2f), new Vector3(5.2f, 0.5f, 1.6f), 0.5f, LmDarkMetal);
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(5.6f + Rf(r, -1f, 1f), 0f, 2.2f + Rf(r, -1f, 1f));
                k.Ball(p + Vector3.up * (0.2f + g(p)), Vector3.one * Rf(r, 0.4f, 0.9f), i % 3 == 0 ? LmTeal * 0.7f : new Color(0.45f, 0.4f, 0.36f),
                    i % 3 == 0 ? LmTealEmit * 0.4f : default);
            }
            for (int i = 0; i < 3; i++)
            {
                float ang = (30f + i * 120f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(ang) * 4.6f, 0f, Mathf.Sin(ang) * 4.6f);
                k.Rod(p + Vector3.up * g(p), p + Vector3.up * (1.6f + g(p)), 0.08f, LmDarkMetal);
                k.Ball(p + Vector3.up * (1.7f + g(p)), 0.22f, LmAmber, LmAmberEmit);
            }
        }

        private static void Geode(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            var shell = new Color(0.36f, 0.33f, 0.31f);
            // Cracked-open shell: a ring of heavy rock halves around a glowing crystal heart.
            for (int i = 0; i < 7; i++)
            {
                float ang = i * Mathf.PI * 2f / 7f + Rf(r, -0.15f, 0.15f);
                var p = new Vector3(Mathf.Cos(ang) * 2.4f, 0.9f, Mathf.Sin(ang) * 2.4f);
                k.Ball(p, new Vector3(Rf(r, 1.8f, 2.6f), Rf(r, 1.6f, 2.4f), Rf(r, 1.4f, 2.0f)), i % 2 == 0 ? shell : shell * 1.2f);
            }
            k.Ball(new Vector3(0f, 0.1f, 0f), new Vector3(3.6f, 0.6f, 3.6f), shell * 0.7f);
            for (int i = 0; i < 22; i++)
            {
                float ang = Rf(r, 0f, Mathf.PI * 2f);
                float d = Rf(r, 0f, 1.6f);
                var p = new Vector3(Mathf.Cos(ang) * d, 0.3f, Mathf.Sin(ang) * d);
                float len = Rf(r, 0.6f, 2.0f) * (1.2f - d / 2f);
                var dir = (p.normalized * 0.6f + Vector3.up).normalized;
                bool teal = i % 3 == 0;
                k.Add(PrimitiveType.Cube, p + dir * len * 0.5f, new Vector3(0.22f, len, 0.22f), Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 45f, 0f),
                    teal ? LmTeal : LmCrystal, teal ? LmTealEmit : LmCrystalEmit);
            }
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(Rf(r, -5f, 5f), 0f, Rf(r, -5f, 5f));
                if (p.magnitude < 3.4f) continue;
                k.Add(PrimitiveType.Cube, p + Vector3.up * (0.2f + g(p)), new Vector3(0.15f, 0.5f, 0.15f), Quaternion.Euler(Rf(r, 20f, 70f), Rf(r, 0f, 360f), 0f),
                    LmCrystal, LmCrystalEmit * 0.6f);
            }
        }

        private static void HullWreck(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // A broken hull section on its side: skin, exposed ribs, a dead engine bell, debris.
            k.Cyl(new Vector3(0f, 1.6f, 0f), 3.6f, 7.0f, Quaternion.Euler(0f, 0f, 96f), LmWhite * 0.85f);
            k.Cyl(new Vector3(3.6f, 1.6f, 0f), 3.3f, 0.5f, Quaternion.Euler(0f, 0f, 90f), LmDarkMetal);
            for (int i = 0; i < 6; i++)
            {
                float x = 4.0f + i * 0.55f;
                for (int s = 0; s < 6; s++)
                {
                    float a0 = (-30f + s * 40f) * Mathf.Deg2Rad, a1 = (-30f + (s + 1) * 40f) * Mathf.Deg2Rad;
                    var p0 = new Vector3(x, 1.6f + Mathf.Sin(a0) * 1.7f, Mathf.Cos(a0) * 1.7f);
                    var p1 = new Vector3(x, 1.6f + Mathf.Sin(a1) * 1.7f, Mathf.Cos(a1) * 1.7f);
                    if (p0.y < 0.1f || p1.y < 0.1f) continue;
                    if (s > 3 - i % 3) break;
                    k.Beam(p0, p1, 0.16f, LmDarkMetal);
                }
            }
            k.Cyl(new Vector3(-4.3f, 1.6f, 0f), 2.4f, 1.2f, Quaternion.Euler(0f, 0f, 90f), LmDarkMetal);
            k.Cyl(new Vector3(-5.2f, 1.6f, 0f), 1.6f, 0.8f, Quaternion.Euler(0f, 0f, 90f), new Color(0.15f, 0.14f, 0.13f));
            k.Box(new Vector3(0.6f, 3.35f, 0.4f), new Vector3(3.6f, 0.1f, 1.6f), Quaternion.Euler(0f, 0f, 6f), LmRed);
            k.Box(new Vector3(-1.2f, 2.6f, 1.75f), new Vector3(0.9f, 0.5f, 0.06f), new Color(0.12f, 0.18f, 0.25f), new Color(0.1f, 0.4f, 0.8f));
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(Rf(r, -6f, 7f), 0f, Rf(r, -4f, 4f));
                if (Mathf.Abs(p.z) < 1.9f && Mathf.Abs(p.x) < 4f) continue;
                float y = g(p);
                k.Box(p + Vector3.up * (0.06f + y), new Vector3(Rf(r, 0.4f, 1.4f), 0.07f, Rf(r, 0.3f, 1.0f)),
                    Quaternion.Euler(Rf(r, 0f, 25f), Rf(r, 0f, 180f), Rf(r, 0f, 25f)), i % 3 == 0 ? LmRed : LmWhite * 0.8f);
            }
            k.Ball(new Vector3(4.2f, 0.4f, 1.2f), 0.3f, LmAmber, LmAmberEmit);
        }

        // ------------------------------------------------------------------ Europa

        private static void Geyser(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // Vent crater of heaved ice blocks, a frozen plume column and glowing blue fractures.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * Mathf.PI * 2f / 12f + Rf(r, -0.1f, 0.1f);
                var p = new Vector3(Mathf.Cos(ang) * 2.6f, 0f, Mathf.Sin(ang) * 2.6f);
                float s = Rf(r, 0.8f, 1.5f);
                k.Box(p + Vector3.up * (s * 0.35f + g(p)), new Vector3(s * 1.2f, s, s * 0.8f),
                    Quaternion.Euler(Rf(r, -25f, 25f), -ang * Mathf.Rad2Deg, Rf(r, 10f, 35f)), i % 2 == 0 ? LmIce : LmIceDeep);
            }
            k.Cyl(new Vector3(0f, 0.04f, 0f), 3.2f, 0.08f, new Color(0.05f, 0.12f, 0.18f), LmIceEmit * 0.35f);
            float y = 0.4f;
            for (int i = 0; i < 9; i++)
            {
                float w = Mathf.Lerp(1.6f, 3.4f, i / 8f) * (1f + Mathf.Sin(i * 1.7f) * 0.12f);
                float h = Mathf.Lerp(1.1f, 1.6f, i / 8f);
                k.Ball(new Vector3(Mathf.Sin(i * 0.6f) * 0.25f, y + h * 0.5f, Mathf.Cos(i * 0.5f) * 0.2f), new Vector3(w, h, w), i % 2 == 0 ? LmIce : Color.white * 0.95f);
                y += h * 0.72f;
            }
            k.Cyl(new Vector3(0f, y * 0.5f, 0f), 0.7f, y, LmIceDeep, LmIceEmit * 0.25f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f + Rf(r, -15f, 15f);
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                float len = Rf(r, 3f, 5f);
                for (int s = 0; s < 4; s++)
                {
                    var m = dir * (3.3f + len * (s + 0.5f) / 4f) + new Vector3(dir.z, 0f, -dir.x) * Rf(r, -0.15f, 0.15f);
                    k.Box(m + Vector3.up * (0.02f + g(m)), new Vector3(0.1f, 0.04f, len / 4f + 0.1f),
                        Quaternion.LookRotation(dir) * Quaternion.Euler(0f, Rf(r, -12f, 12f), 0f), new Color(0.3f, 0.6f, 0.9f), LmIceEmit * 0.7f);
                }
            }
            k.Cyl(new Vector3(0f, 0.02f, 0f), 9f, 0.03f, new Color(0.92f, 0.96f, 1f));
        }

        private static void IceSpires(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // Penitentes: rows of blade-like spires all leaning the same way, sculpted by the sun.
            var lean = Quaternion.Euler(-8f, 35f, 0f);
            for (int row = 0; row < 5; row++)
                for (int i = 0; i < 6; i++)
                {
                    var p = new Vector3(-4.5f + i * 1.7f + (row % 2) * 0.8f + Rf(r, -0.3f, 0.3f), 0f, -3.6f + row * 1.8f + Rf(r, -0.3f, 0.3f));
                    float tall = Rf(r, 1.6f, 4.4f) * (1f - Mathf.Abs(row - 2) * 0.12f);
                    float y = g(p);
                    k.Box(p + Vector3.up * (y + tall * 0.45f), new Vector3(Rf(r, 0.35f, 0.6f), tall, Rf(r, 0.25f, 0.45f)),
                        lean * Quaternion.Euler(0f, Rf(r, -10f, 10f), Rf(r, -5f, 5f)), (row + i) % 3 == 0 ? LmIceDeep : LmIce);
                    k.Add(PrimitiveType.Cube, p + Vector3.up * (y + tall * 0.95f), new Vector3(0.18f, 0.6f, 0.18f),
                        lean * Quaternion.Euler(0f, 45f, 0f), Color.white * 0.97f);
                }
        }

        private static void Cryobot(DetailBatch k, System.Random r, Func<Vector3, float> g)
        {
            // Drill tower over a melt hole glowing from the ocean below, a heated hab and cable reels.
            k.Cyl(new Vector3(0f, 0.03f, 0f), 2.2f, 0.06f, new Color(0.04f, 0.10f, 0.16f), LmIceEmit * 0.6f);
            k.Cyl(new Vector3(0f, 0.08f, 0f), 2.8f, 0.1f, LmIceDeep);
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + i * 90f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(ang) * 1.8f, 0f, Mathf.Sin(ang) * 1.8f);
                k.Rod(foot, new Vector3(0f, 5.5f, 0f), 0.16f, LmWhite);
            }
            for (int i = 1; i < 4; i++)
                k.Box(new Vector3(0f, i * 1.3f, 0f), new Vector3(2.6f - i * 0.55f, 0.08f, 2.6f - i * 0.55f), Quaternion.Euler(0f, 45f, 0f), LmAmber);
            k.Box(new Vector3(0f, 5.7f, 0f), new Vector3(0.9f, 0.5f, 0.9f), LmWhite);
            k.Rod(new Vector3(0f, 5.5f, 0f), new Vector3(0f, -0.2f, 0f), 0.06f, LmDarkMetal);
            k.Ball(new Vector3(0f, 6.1f, 0f), 0.25f, LmRed, LmRedEmit);
            var hab = new Vector3(-4.4f, 0f, 1.6f);
            float hy = g(hab);
            k.Cyl(hab + Vector3.up * (1.1f + hy), 3.0f, 2.2f, LmWhite);
            k.Ball(hab + Vector3.up * (2.2f + hy), new Vector3(3.0f, 1.2f, 3.0f), LmWhite);
            k.Box(hab + new Vector3(0f, 1.2f + hy, -1.55f), new Vector3(1.0f, 1.4f, 0.1f), LmDarkMetal);
            for (int i = 0; i < 5; i++)
                k.Box(hab + new Vector3(Mathf.Cos(i * 1.25f) * 1.52f, 1.5f + hy, Mathf.Sin(i * 1.25f) * 1.52f), new Vector3(0.4f, 0.25f, 0.05f),
                    Quaternion.Euler(0f, -i * 1.25f * Mathf.Rad2Deg + 90f, 0f), LmAmber, LmAmberEmit * 0.6f);
            for (int i = 0; i < 2; i++)
            {
                var reel = new Vector3(2.8f, 0f, -1.8f + i * 1.6f);
                float ry = g(reel);
                k.Cyl(reel + Vector3.up * (0.7f + ry), 1.3f, 0.9f, Quaternion.Euler(90f, 0f, 0f), LmAmber);
                k.Cyl(reel + Vector3.up * (0.7f + ry), 0.9f, 0.95f, Quaternion.Euler(90f, 0f, 0f), LmDarkMetal);
            }
            k.Beam(new Vector3(2.2f, 0.7f, -1.0f), new Vector3(0.4f, 1.2f, 0f), 0.06f, LmDarkMetal);
            k.Beam(hab + new Vector3(1.5f, 0.4f, 0f), new Vector3(-1.0f, 0.2f, 0.6f), 0.08f, LmDarkMetal);
        }
    }
}
