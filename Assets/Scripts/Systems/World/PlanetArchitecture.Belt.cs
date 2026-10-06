using UnityEngine;

namespace SolarMajesty
{
    // Belt — microgravity rock, no air and a weak, distant sun.
    public static partial class PlanetArchitecture
    {
        static ArchStyle StyleBelt() =>
            new ArchStyle
            {
                Name = "Anchored microgravity station",
                Rationale = "Barely any gravity, no air and a weak distant sun: bolt down to the rock, spin for gravity, spread huge solar wings.",
                Hull = new Color(0.50f, 0.51f, 0.54f), Trim = new Color(0.95f, 0.76f, 0.12f),
                Dark = new Color(0.13f, 0.13f, 0.14f), Glass = new Color(0.25f, 0.35f, 0.45f, 0.55f),
                Glow = new Color(1f, 0.70f, 0.30f), GlowEmission = new Color(2.0f, 1.1f, 0.35f),
                Shell = new Color(0.30f, 0.28f, 0.26f), Plant = new Color(0.3f, 0.5f, 0.3f),
                Foil = new Color(0.88f, 0.68f, 0.24f), Ice = new Color(0.8f, 0.9f, 1f, 0.35f),
                Steam = new Color(1f, 1f, 1f, 0.25f),
                DustColor = new Color(0.18f, 0.17f, 0.16f), DustAmount = 0.20f, WearAmount = 0.22f
            };


        static void Belt(Builder p, ArchArchetype a)
        {
            float w = p.W, d = p.D, h = p.H;
            // Everything is bolted to the rock: anchor stilts, tethers, truss spars, floodlights.
            float[] sx = { -0.46f, 0.46f, -0.46f, 0.46f }, sz = { -0.46f, -0.46f, 0.46f, 0.46f };
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = new Vector3(w * sx[i], 0, d * sz[i]);
                p.Cyl("Anchor" + i, ArchRole.Dark, c + new Vector3(0, 0.35f, 0), new Vector3(0.34f, 0.7f, 0.34f));
                p.Cyl("AnchorPad" + i, ArchRole.Trim, c + new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.08f, 0.7f));
                Vector3 outward = new Vector3(sx[i], 0, sz[i]).normalized;
                p.Box("Tether" + i, ArchRole.Dark, c + outward * 0.9f + new Vector3(0, h * 0.45f, 0),
                    new Vector3(0.05f, h * 1.1f, 0.05f), new Vector3(0, 0, 0) + Tilt(outward, 32f));
            }
            if (a != ArchArchetype.Pad)
            {
                p.TrussFrame(h + 0.3f);
                p.Box("HazardBand", ArchRole.Trim, new Vector3(0, 0.3f, 0), new Vector3(w * 0.99f, 0.12f, d * 0.99f));
                for (int i = 0; i < 2; i++)
                    p.Sphere("Floodlight" + i, ArchRole.Glow, new Vector3(w * (i == 0 ? -0.5f : 0.5f), h + 0.45f, d * 0.5f), Vector3.one * 0.3f);
            }

            switch (a)
            {
                case ArchArchetype.Dwelling:
                    p.SpinRing(new Vector3(0, h + 1.9f, 0), w * 0.46f, 16, 45f);
                    break;
                case ArchArchetype.Hub:
                    p.SpinRing(new Vector3(0, h + 3.1f, 0), w * 0.36f, 20, 45f);
                    p.Box("DockSpine", ArchRole.Dark, new Vector3(0, h + 3.1f, 0), new Vector3(0.3f, 0.3f, d * 1.3f), new Vector3(0, 45f, 0));
                    break;
                case ArchArchetype.Power:
                    // The Belt gets a tenth of Earth's sunlight: huge wings.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        p.Box("WingSpar" + s, ArchRole.Dark, new Vector3(s * (w * 0.5f + 2.2f), h + 1.2f, 0), new Vector3(4.4f, 0.08f, 0.1f));
                        p.Box("Wing" + s, ArchRole.Glass, new Vector3(s * (w * 0.5f + 2.2f), h + 1.2f, 0), new Vector3(4.2f, 0.04f, d * 0.9f), new Vector3(15f * s, 0, 0));
                    }
                    break;
                case ArchArchetype.Extractor:
                    // Mass driver: throws ore off the rock toward the refinery.
                    // Beside the +Z doorway lane, not across it.
                    float mx = -w * 0.32f;
                    p.Box("DriverRailA", ArchRole.Dark, new Vector3(mx - 0.25f, h * 0.5f + 1.2f, d * 0.5f + 1.2f), new Vector3(0.12f, 0.12f, 5f), new Vector3(-24, 0, 0));
                    p.Box("DriverRailB", ArchRole.Dark, new Vector3(mx + 0.25f, h * 0.5f + 1.2f, d * 0.5f + 1.2f), new Vector3(0.12f, 0.12f, 5f), new Vector3(-24, 0, 0));
                    p.Box("DriverCoils", ArchRole.Glow, new Vector3(mx, h * 0.5f + 1.2f, d * 0.5f + 1.2f), new Vector3(0.4f, 0.04f, 4.6f), new Vector3(-24, 0, 0));
                    break;
                case ArchArchetype.Workshop:
                    p.Box("GantryBeam", ArchRole.Trim, new Vector3(0, h + 1.6f, 0), new Vector3(w * 1.1f, 0.25f, 0.3f));
                    // A-frame legs either side of the ±X doorway lanes.
                    for (int s = 0; s < 4; s++)
                    {
                        float lx = (s < 2 ? -1f : 1f) * w * 0.55f, lz = (s % 2 == 0 ? -1f : 1f) * 1.35f;
                        p.Box("GantryLeg" + s, ArchRole.Dark, new Vector3(lx, (h + 1.6f) * 0.5f, lz), new Vector3(0.18f, h + 1.7f, 0.18f), new Vector3(lz > 0 ? 9f : -9f, 0, 0));
                    }
                    break;
                case ArchArchetype.Defense:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 at = new Vector3(w * (i == 0 ? -0.3f : 0.3f), h + 0.6f, 0);
                        p.Sphere("PdTurret" + i, ArchRole.Hull, at, new Vector3(0.9f, 0.6f, 0.9f));
                        p.Box("PdBarrel" + i, ArchRole.Dark, at + new Vector3(0, 0.1f, 0.5f), new Vector3(0.1f, 0.1f, 0.9f), new Vector3(-20, 0, 0));
                    }
                    break;
                case ArchArchetype.Pad:
                    for (int s = 0; s < 4; s++)
                    {
                        float ang = s * 90f + 45f;
                        Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                        p.Box("CradleArm" + s, ArchRole.Trim, dir * (p.R * 0.9f) + new Vector3(0, 1.2f, 0), new Vector3(0.25f, 2.4f, 0.25f), Tilt(dir, -18f));
                    }
                    p.EdgeLights(ArchRole.Glow, 10);
                    break;
                case ArchArchetype.Wonder:
                    for (int i = 0; i < 6; i++)
                        p.Box("SpineSeg" + i, i % 2 == 0 ? ArchRole.Dark : ArchRole.Trim, new Vector3(0, h + 0.6f + i * 1.1f, 0), new Vector3(0.9f - i * 0.08f, 1f, 0.9f - i * 0.08f));
                    p.SpinRing(new Vector3(0, h + 5.4f, 0), w * 0.4f, 20, 45f);
                    break;
            }
        }
    }
}
