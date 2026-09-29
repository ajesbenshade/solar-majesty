using UnityEngine;

namespace SolarMajesty
{
    // Mars — thin CO₂ air, dust storms and −60 °C. The tuned reference look.
    public static partial class PlanetArchitecture
    {
        static ArchStyle StyleMars() =>
            new ArchStyle
            {
                Name = "Printed regolith colony",
                Rationale = "Thin CO₂ air, dust storms and −60 °C: print thick walls from local regolith, pressurise under domes, keep the heat in.",
                Hull = new Color(0.92f, 0.86f, 0.78f), Trim = new Color(0.96f, 0.42f, 0.08f),
                Dark = new Color(0.24f, 0.20f, 0.18f), Glass = new Color(0.86f, 0.78f, 0.66f, 0.40f),
                Glow = new Color(1f, 0.62f, 0.26f), GlowEmission = new Color(1.8f, 0.85f, 0.25f),
                Shell = new Color(0.66f, 0.40f, 0.28f), Plant = new Color(0.30f, 0.52f, 0.28f),
                Foil = new Color(0.86f, 0.66f, 0.24f), Ice = new Color(0.8f, 0.9f, 1f, 0.35f),
                Steam = new Color(1f, 0.95f, 0.9f, 0.25f),
                DustColor = new Color(0.62f, 0.36f, 0.22f), DustAmount = 0.24f, WearAmount = 0.16f
            };


        static void Mars(Builder p, ArchArchetype a)
        {
            float w = p.W, d = p.D, h = p.H;
            if (a != ArchArchetype.Pad)
            {
                // Printed regolith windbreak on the storm side, a dark dust skirt and heater glow.
                p.PrintedWall(new Vector3(0, 0, -d * 0.62f), w * 0.9f, Mathf.Min(h * 0.8f, 2.4f), 0.45f, gap: 2f);
                p.Box("DustSkirt", ArchRole.Dark, new Vector3(0, 0.12f, 0), new Vector3(w * 0.98f, 0.24f, d * 0.98f));
                p.WindowBands(0.55f, ArchRole.Glow, slit: true);
            }

            switch (a)
            {
                case ArchArchetype.Dwelling:
                    // Printed tapering habitat tower (layered strata), after the printed-regolith Mars habitat designs.
                    p.PrintedTower(new Vector3(-w * 0.36f, 0, d * 0.34f), 1.8f, h + 1.8f, 9);
                    break;
                case ArchArchetype.Hub:
                    p.Sphere("PressureDome", ArchRole.Glass, new Vector3(0, h, 0), new Vector3(w * 0.8f, w * 0.62f, d * 0.8f));
                    // Meridian ribs: thin shells hugging the dome.
                    for (int i = 0; i < 4; i++)
                        p.Sphere("DomeRib" + i, ArchRole.Trim, new Vector3(0, h, 0), new Vector3(0.09f, w * 0.63f, d * 0.81f), new Vector3(0, i * 45f, 0));
                    p.PrintedRing(new Vector3(0, h - 0.05f, 0), w * 0.82f, 0.5f, 3);
                    break;
                case ArchArchetype.Power:
                    // Compact fission reactor (Kilopower-class): the sun is weak and storms last weeks.
                    Vector3 at = new Vector3(w * 0.32f, 0, -d * 0.1f);
                    p.Cyl("ReactorCore", ArchRole.Dark, at + new Vector3(0, 1.0f, 0), new Vector3(0.8f, 2f, 0.8f));
                    for (int i = 0; i < 6; i++)
                        p.Box("ReactorFin" + i, ArchRole.Hull, at + new Vector3(0, 2.1f, 0), new Vector3(0.05f, 1.6f, 1.6f), new Vector3(0, i * 30f, 0));
                    p.Sphere("ReactorGlow", ArchRole.Glow, at + new Vector3(0, 0.6f, 0), Vector3.one * 0.5f);
                    break;
                case ArchArchetype.Extractor:
                    // ISRU propellant plant: tanks and pipework.
                    for (int i = 0; i < 2; i++)
                        p.Cyl("IsruTank" + i, ArchRole.Hull, new Vector3(-w * 0.22f - i * 1.1f, 0.6f, d * 0.36f), new Vector3(1f, 2.2f, 1f), new Vector3(90, 0, 0));
                    p.Box("IsruPipe", ArchRole.Trim, new Vector3(-w * 0.1f, 1.2f, d * 0.2f), new Vector3(w * 0.5f, 0.12f, 0.12f));
                    break;
                case ArchArchetype.Workshop:
                    // Printed barrel vault: regolith over a pressure shell, laid in visible courses.
                    float len = w * 0.62f, dia = d * 0.56f;
                    p.Cyl("PrintedVault", ArchRole.Shell, new Vector3(0, h - 0.15f, 0), new Vector3(dia, len, dia), new Vector3(0, 0, 90));
                    for (int i = 0; i < 5; i++)
                        p.Cyl("VaultCourse" + i, ArchRole.Shell, new Vector3(-len * 0.4f + i * len * 0.2f, h - 0.15f, 0), new Vector3(dia + 0.12f, 0.1f, dia + 0.12f), new Vector3(0, 0, 90));
                    p.Box("VaultDoor", ArchRole.Glow, new Vector3(len * 0.5f + 0.01f, h + dia * 0.12f, 0), new Vector3(0.04f, dia * 0.3f, dia * 0.35f));
                    break;
                case ArchArchetype.Defense:
                    p.PrintedWall(new Vector3(0, 0, d * 0.62f), w * 0.9f, 1.6f, 0.4f, gap: 2f);
                    p.Mast(new Vector3(w * 0.4f, 0, d * 0.4f), h + 2f, ArchRole.Trim, ArchRole.Glow);
                    break;
                case ArchArchetype.Pad:
                    for (int s = 0; s < 4; s++)
                    {
                        float ang = 45f + s * 90f;
                        Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                        p.Box("BlastWall" + s, ArchRole.Shell, dir * (p.R + 0.9f) + new Vector3(0, 0.7f, 0),
                            new Vector3(p.R * 0.9f, 1.4f, 0.5f), new Vector3(0, ang, 0));
                    }
                    p.EdgeLights(ArchRole.Glow, 12);
                    break;
                case ArchArchetype.Wonder:
                    p.PrintedTower(Vector3.zero + new Vector3(0, h - 0.2f, 0), w * 0.5f, 6f, 14);
                    break;
            }
        }
    }
}
