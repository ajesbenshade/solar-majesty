using UnityEngine;

namespace SolarMajesty
{
    // Europa — −160 °C, Jupiter's radiation and an ocean under the ice.
    public static partial class PlanetArchitecture
    {
        static ArchStyle StyleEuropa() =>
            new ArchStyle
            {
                Name = "Ice-shielded cryo base",
                Rationale = "−160 °C, Jupiter's radiation and an ocean under the crust: shield with water ice, vent waste heat, drill down.",
                Hull = new Color(0.86f, 0.92f, 0.96f), Trim = new Color(0.18f, 0.78f, 0.92f),
                Dark = new Color(0.16f, 0.20f, 0.26f), Glass = new Color(0.55f, 0.80f, 0.95f, 0.45f),
                Glow = new Color(0.35f, 0.95f, 1f), GlowEmission = new Color(0.30f, 1.60f, 2.20f),
                Shell = new Color(0.72f, 0.84f, 0.92f), Plant = new Color(0.3f, 0.5f, 0.3f),
                Foil = new Color(0.86f, 0.70f, 0.30f), Ice = new Color(0.66f, 0.86f, 0.98f, 0.42f),
                Steam = new Color(0.95f, 0.98f, 1f, 0.32f),
                DustColor = new Color(0.90f, 0.96f, 1f), DustAmount = 0.34f, WearAmount = 0.08f
            };


        static void Europa(Builder p, ArchArchetype a)
        {
            float w = p.W, d = p.D, h = p.H;
            if (a != ArchArchetype.Pad)
            {
                // Water ice stops radiation; the base is heated, so it vents; cyan light in the dark.
                p.IceWalls(Mathf.Min(h * 0.7f, 1.8f));
                p.HeatVent(new Vector3(w * 0.38f, 0, -d * 0.38f), h + 1.6f);
                p.WindowBands(h * 0.5f, ArchRole.Glow);
            }

            switch (a)
            {
                case ArchArchetype.Dwelling:
                    // Ice cap over the roof: metres of water ice are the radiation shield.
                    p.Sphere("IceDome", ArchRole.Ice, new Vector3(0, h, 0), new Vector3(w * 0.78f, h * 0.9f, d * 0.78f));
                    p.Cyl("IceDomeRing", ArchRole.Shell, new Vector3(0, h + 0.04f, 0), new Vector3(w * 0.82f, 0.12f, d * 0.82f));
                    break;
                case ArchArchetype.Hub:
                    p.Sphere("IceShield", ArchRole.Ice, new Vector3(0, h, 0), new Vector3(w * 0.8f, h * 1.3f, d * 0.8f));
                    p.Cyl("IceShieldRing", ArchRole.Shell, new Vector3(0, h + 0.04f, 0), new Vector3(w * 0.84f, 0.14f, d * 0.84f));
                    p.Sphere("CoreLight", ArchRole.Glow, new Vector3(0, h + 0.3f, 0), new Vector3(w * 0.3f, 0.6f, d * 0.3f));
                    p.HeatVent(new Vector3(-w * 0.38f, 0, -d * 0.38f), h + 2.1f);
                    break;
                case ArchArchetype.Power:
                    // Sunlight is 1/25 of Earth's: radioisotope/fission with glowing radiator fins.
                    Vector3 at = new Vector3(-w * 0.28f, 0, d * 0.25f);
                    p.Cyl("RtgCore", ArchRole.Dark, at + new Vector3(0, 0.9f, 0), new Vector3(0.7f, 1.8f, 0.7f));
                    for (int i = 0; i < 8; i++)
                        p.Box("RtgFin" + i, ArchRole.Glow, at + new Vector3(0, 0.9f, 0), new Vector3(0.04f, 1.6f, 1.3f), new Vector3(0, i * 22.5f, 0));
                    break;
                case ArchArchetype.Extractor:
                    // Cryobot drill derrick melting down toward the ocean.
                    Vector3 c = new Vector3(w * 0.3f, 0, d * 0.3f);
                    float top = h + 3.2f;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 leg = new Vector3(i % 2 == 0 ? -0.55f : 0.55f, 0, i < 2 ? -0.55f : 0.55f);
                        p.Box("DerrickLeg" + i, ArchRole.Dark, c + leg * 0.5f + new Vector3(0, top * 0.5f, 0), new Vector3(0.1f, top, 0.1f), Tilt(leg.normalized, -7f));
                    }
                    p.Box("DerrickHead", ArchRole.Trim, c + new Vector3(0, top, 0), new Vector3(0.6f, 0.3f, 0.6f));
                    p.Cyl("BoreGlow", ArchRole.Glow, c + new Vector3(0, 0.02f, 0), new Vector3(1.3f, 0.04f, 1.3f));
                    break;
                case ArchArchetype.Workshop:
                    // Hangar for the under-ice submersible: an ice-clad tube with a lit moon pool.
                    p.Cyl("SubHangar", ArchRole.Ice, new Vector3(0, h + 0.35f, 0), new Vector3(1.5f, w * 0.6f, 1.5f), new Vector3(0, 0, 90));
                    p.Cyl("SubHangarFrame", ArchRole.Hull, new Vector3(0, h + 0.35f, 0), new Vector3(1.1f, w * 0.64f, 1.1f), new Vector3(0, 0, 90));
                    p.Cyl("MoonPool", ArchRole.Glow, new Vector3(-w * 0.3f, 0.02f, d * 0.3f), new Vector3(1.1f, 0.04f, 1.1f));
                    break;
                case ArchArchetype.Defense:
                    p.IceWalls(2.2f, outset: 0.5f);
                    p.Cyl("WatchPylon", ArchRole.Ice, new Vector3(-w * 0.3f, h + 1.5f, d * 0.3f), new Vector3(0.9f, 3f, 0.9f));
                    p.Sphere("WatchEye", ArchRole.Glow, new Vector3(-w * 0.3f, h + 3.2f, d * 0.3f), Vector3.one * 0.6f);
                    break;
                case ArchArchetype.Pad:
                    p.EdgeLights(ArchRole.Glow, 14);
                    p.Cyl("FrostRing", ArchRole.Shell, new Vector3(0, 0.02f, 0), new Vector3(p.R * 2.3f, 0.03f, p.R * 2.3f));
                    break;
                case ArchArchetype.Wonder:
                    p.Cyl("IceSpire", ArchRole.Ice, new Vector3(0, h + 3f, 0), new Vector3(1.4f, 6f, 1.4f));
                    p.Cyl("SpireCore", ArchRole.Glow, new Vector3(0, h + 3f, 0), new Vector3(0.35f, 5.8f, 0.35f));
                    break;
            }
        }

        /// <summary>Euler that leans a vertical part outward along <paramref name="dir"/> by <paramref name="deg"/>.</summary>
        static Vector3 Tilt(Vector3 dir, float deg) => new Vector3(dir.z * deg, 0, -dir.x * deg);
    }
}
