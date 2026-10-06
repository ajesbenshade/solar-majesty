using UnityEngine;

namespace SolarMajesty
{
    // Earth — open air, rain and a living biosphere.
    public static partial class PlanetArchitecture
    {
        static ArchStyle StyleEarth() =>
            new ArchStyle
            {
                Name = "Solarpunk arcology",
                Rationale = "Open air, rain and a living biosphere: glass, rooftop shrubs, wind power, nothing to hide from.",
                Hull = new Color(0.93f, 0.94f, 0.91f), Trim = new Color(0.10f, 0.64f, 0.60f),
                Dark = new Color(0.30f, 0.27f, 0.23f), Glass = new Color(0.62f, 0.86f, 0.88f, 0.42f),
                Glow = new Color(1f, 0.86f, 0.62f), GlowEmission = new Color(1.3f, 0.95f, 0.55f),
                Shell = new Color(0.62f, 0.58f, 0.50f), Plant = new Color(0.24f, 0.56f, 0.26f),
                Foil = new Color(0.86f, 0.70f, 0.30f), Ice = new Color(0.8f, 0.9f, 1f, 0.35f),
                Steam = new Color(1f, 1f, 1f, 0.3f),
                DustColor = new Color(0.34f, 0.30f, 0.22f), DustAmount = 0.06f, WearAmount = 0.10f
            };


        static void Earth(Builder p, ArchArchetype a)
        {
            float w = p.W, d = p.D, h = p.H;
            if (a != ArchArchetype.Pad)
            {
                // Warm window bands and rooftop shrubs.
                for (int i = 0; i < 3 + p.Rng(3); i++)
                    p.Sphere("Shrub" + i, ArchRole.Plant,
                        new Vector3(p.Range(-w * 0.28f, w * 0.28f), h + 0.28f, p.Range(-d * 0.28f, d * 0.28f)),
                        Vector3.one * p.Range(0.35f, 0.6f));
                p.WindowBands(h * 0.62f, ArchRole.Glow);
            }
            p.CornerPlanters(ArchRole.Dark, ArchRole.Plant);

            switch (a)
            {
                case ArchArchetype.Dwelling:
                    p.GlassPavilion(new Vector3(-w * 0.12f, h + 0.14f, 0), w * 0.38f, 0.95f, d * 0.38f);
                    break;
                case ArchArchetype.Hub:
                    p.Sphere("Atrium", ArchRole.Glass, new Vector3(0, h + 0.1f, 0), new Vector3(w * 0.5f, w * 0.44f, d * 0.5f));
                    p.Cyl("AtriumRing", ArchRole.Trim, new Vector3(0, h + 0.16f, 0), new Vector3(w * 0.52f, 0.12f, d * 0.52f));
                    p.Sphere("AtriumTree", ArchRole.Plant, new Vector3(0, h + 0.7f, 0), new Vector3(w * 0.22f, w * 0.2f, d * 0.22f));
                    break;
                case ArchArchetype.Power:
                    p.WindTurbine(new Vector3(w * 0.34f, 0, -d * 0.34f), h + 3.2f);
                    p.WindTurbine(new Vector3(-w * 0.34f, 0, -d * 0.34f), h + 2.6f);
                    break;
                case ArchArchetype.Extractor:
                    p.Box("RainCanopy", ArchRole.Glass, new Vector3(0, h + 0.7f, 0), new Vector3(w * 0.9f, 0.06f, d * 0.6f), new Vector3(-12, 0, 0));
                    p.Cyl("Cistern", ArchRole.Glass, new Vector3(w * 0.36f, 0.8f, -d * 0.36f), new Vector3(1.1f, 1.6f, 1.1f));
                    p.Cyl("CisternCap", ArchRole.Trim, new Vector3(w * 0.36f, 1.64f, -d * 0.36f), new Vector3(1.16f, 0.08f, 1.16f));
                    break;
                case ArchArchetype.Workshop:
                    for (int i = 0; i < 3; i++)
                        p.Box("Skylight" + i, ArchRole.Glass,
                            new Vector3((i - 1) * w * 0.24f, h + 0.42f, 0), new Vector3(w * 0.2f, 0.05f, d * 0.6f), new Vector3(0, 0, 30));
                    break;
                case ArchArchetype.Defense:
                    p.PerimeterBerm(ArchRole.Plant, 0.7f, 0.55f, 0.95f);
                    p.Mast(new Vector3(w * 0.38f, 0, d * 0.38f), h + 2.2f, ArchRole.Trim, ArchRole.Glow);
                    break;
                case ArchArchetype.Pad:
                    p.EdgeLights(ArchRole.Glow, 12);
                    break;
                case ArchArchetype.Wonder:
                    for (int t = 0; t < 4; t++)
                    {
                        float r = w * (0.46f - t * 0.09f);
                        float y = h + t * 1.1f;
                        p.Cyl("Tier" + t, ArchRole.Glass, new Vector3(0, y + 0.5f, 0), new Vector3(r, 1f, r));
                        p.Cyl("TierGarden" + t, ArchRole.Plant, new Vector3(0, y + 1.02f, 0), new Vector3(r * 1.05f, 0.1f, r * 1.05f));
                    }
                    break;
            }
        }
    }
}
