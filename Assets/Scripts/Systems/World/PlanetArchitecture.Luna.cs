using UnityEngine;

namespace SolarMajesty
{
    // Luna — vacuum, radiation, micrometeoroids and ±150 °C swings.
    public static partial class PlanetArchitecture
    {
        static ArchStyle StyleLuna() =>
            new ArchStyle
            {
                Name = "Regolith-shielded outpost",
                Rationale = "No air, hard radiation, micrometeoroids and ±150 °C swings: bury it under regolith, shed heat through radiators, wrap it in gold foil.",
                Hull = new Color(0.86f, 0.87f, 0.88f), Trim = new Color(0.86f, 0.66f, 0.22f),
                Dark = new Color(0.18f, 0.18f, 0.20f), Glass = new Color(0.30f, 0.42f, 0.55f, 0.55f),
                Glow = new Color(1f, 0.25f, 0.18f), GlowEmission = new Color(2.2f, 0.35f, 0.22f),
                Shell = new Color(0.47f, 0.46f, 0.44f), Plant = new Color(0.3f, 0.5f, 0.3f),
                Foil = new Color(0.90f, 0.70f, 0.24f), Ice = new Color(0.8f, 0.9f, 1f, 0.35f),
                Steam = new Color(1f, 1f, 1f, 0.25f),
                DustColor = new Color(0.55f, 0.54f, 0.52f), DustAmount = 0.18f, WearAmount = 0.16f
            };


        static void Luna(Builder p, ArchArchetype a)
        {
            float w = p.W, d = p.D, h = p.H;
            if (a != ArchArchetype.Pad)
            {
                // Regolith berm against radiation and micrometeoroids, a sintered shield cap,
                // gold MLI foil under it and white radiator fins shedding heat.
                p.PerimeterBerm(ArchRole.Shell, 0.9f, 0.75f, 1.35f);
                if (a == ArchArchetype.Dwelling)
                    p.Sphere("RegolithMound", ArchRole.Shell, new Vector3(0, h, 0), new Vector3(w * 0.9f, 0.95f, d * 0.9f));
                else
                    p.Box("ShieldCap", ArchRole.Shell, new Vector3(0, h + 0.2f, 0), new Vector3(w * 0.92f, 0.4f, d * 0.92f));
                p.Box("FoilBand", ArchRole.Foil, new Vector3(0, h - 0.04f, 0), new Vector3(w * 0.94f, 0.1f, d * 0.94f));
                p.Radiators(h, 2);
                p.Mast(new Vector3(-w * 0.4f, 0, -d * 0.4f), h + 1.3f, ArchRole.Dark, ArchRole.Glow);
            }

            switch (a)
            {
                case ArchArchetype.Hub:
                    p.Dish(new Vector3(w * 0.22f, h + 0.4f, -d * 0.2f), 1.9f);
                    break;
                case ArchArchetype.Power:
                    // Polar sun-tracking tower: sunlight skims the horizon at the lunar poles.
                    p.Cyl("SunTowerMast", ArchRole.Hull, new Vector3(w * 0.3f, (h + 5f) * 0.5f, -d * 0.3f), new Vector3(0.28f, h + 5f, 0.28f));
                    p.Box("SunTowerPanel", ArchRole.Dark, new Vector3(w * 0.3f, h + 4f, -d * 0.3f), new Vector3(1.9f, 2.6f, 0.08f));
                    p.Box("SunTowerFrame", ArchRole.Foil, new Vector3(w * 0.3f, h + 4f, -d * 0.3f - 0.06f), new Vector3(2.0f, 2.7f, 0.04f));
                    break;
                case ArchArchetype.Extractor:
                    p.Cyl("IceTank", ArchRole.Foil, new Vector3(-w * 0.3f, 0.75f, d * 0.3f), new Vector3(1.2f, 1.5f, 1.2f), new Vector3(0, 0, 90));
                    break;
                case ArchArchetype.Defense:
                    // Sintered-regolith blast blocks, split around the -Z doorway lane.
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i < 2 ? -1f : 1f) * (i % 2 == 0 ? w * 0.42f : w * 0.22f);
                        p.Box("SinterBlock" + i, ArchRole.Shell, new Vector3(x, 0.45f, -d * 0.58f), new Vector3(w * 0.18f, 0.9f, 0.6f));
                    }
                    break;
                case ArchArchetype.Pad:
                    p.PerimeterBerm(ArchRole.Shell, 1.1f, 0.9f, 1.6f, outset: 0.8f); // blast berm
                    p.EdgeLights(ArchRole.Glow, 12);
                    break;
                case ArchArchetype.Wonder:
                    p.Cyl("ShieldedTower", ArchRole.Shell, new Vector3(0, h + 2.2f, 0), new Vector3(w * 0.34f, 4.4f, d * 0.34f));
                    p.Cyl("TowerFoil", ArchRole.Foil, new Vector3(0, h + 4.5f, 0), new Vector3(w * 0.36f, 0.2f, d * 0.36f));
                    p.Sphere("TowerBeacon", ArchRole.Glow, new Vector3(0, h + 4.8f, 0), Vector3.one * 0.4f);
                    break;
            }
        }
    }
}
