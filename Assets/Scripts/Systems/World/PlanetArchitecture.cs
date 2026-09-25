using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>What a building is for, as far as architecture cares.</summary>
    public enum ArchArchetype
    {
        Dwelling,
        Hub,
        Power,
        Extractor,
        Workshop,
        Defense,
        Pad,
        Wonder
    }

    /// <summary>Material role of an architecture part; each world colours roles its own way.</summary>
    public enum ArchRole
    {
        Hull,
        Trim,
        Dark,
        Glass,
        Glow,
        /// <summary>Local protective mass: sintered regolith, printed Mars concrete, ice blocks…</summary>
        Shell,
        Plant,
        /// <summary>Gold multi-layer insulation foil.</summary>
        Foil,
        /// <summary>Translucent water-ice shielding.</summary>
        Ice,
        /// <summary>Translucent vapour.</summary>
        Steam
    }

    public enum ArchShape
    {
        Box,
        Cylinder,
        Sphere
    }

    /// <summary>
    /// One piece of planet-specific architecture, in the building's local space (y up from the
    /// ground, footprint centred on the origin). <see cref="Size"/> is the true extent in metres:
    /// box = x/y/z, cylinder = diameter x / height y / diameter z, sphere = diameters.
    /// </summary>
    public struct ArchPart
    {
        public string Name;
        public ArchShape Shape;
        public ArchRole Role;
        public Vector3 Position;
        public Vector3 Size;
        public Vector3 Euler;
    }

    /// <summary>A world's building palette and surface weathering.</summary>
    public struct ArchStyle
    {
        public string Name;
        /// <summary>Why the buildings look like this (local conditions), for docs and tooltips.</summary>
        public string Rationale;
        public Color Hull, Trim, Dark, Glass, Glow, GlowEmission, Shell, Plant, Foil, Ice, Steam;
        /// <summary>Hull shader weathering: dust on Mars, grey regolith on Luna, frost on Europa, soot in the Belt.</summary>
        public Color DustColor;
        public float DustAmount;
        public float WearAmount;

        public Color RoleColor(ArchRole r) => r switch
        {
            ArchRole.Hull => Hull,
            ArchRole.Trim => Trim,
            ArchRole.Dark => Dark,
            ArchRole.Glass => Glass,
            ArchRole.Glow => Glow,
            ArchRole.Shell => Shell,
            ArchRole.Plant => Plant,
            ArchRole.Foil => Foil,
            ArchRole.Ice => Ice,
            ArchRole.Steam => Steam,
            _ => Hull
        };

        public static bool IsTranslucent(ArchRole r) => r == ArchRole.Glass || r == ArchRole.Ice || r == ArchRole.Steam;
    }

    /// <summary>
    /// Each world builds for its own conditions. Pure data: a palette, a remap of the shared kit
    /// colours, and a set of adaptation parts added to every building sized to its footprint and
    /// height. Doorway lanes (the four face centres) are always kept clear.
    ///
    /// Earth — open air, rain, a living biosphere: solarpunk green roofs, glass atria, wind.
    /// Luna — vacuum, radiation, micrometeoroids, ±150 °C: regolith berms and shield caps,
    ///        white radiators, gold foil, polar sun-tracking towers, red beacons.
    /// Mars — thin CO₂, dust storms, −60 °C: 3D-printed regolith (layered strata), inflatable
    ///        pressure domes, printed windbreaks, amber heater glow, compact fission power.
    /// Belt — microgravity rock, no air, weak sun: anchor stilts and tethers, truss spars,
    ///        spin-gravity rings, huge solar wings, hazard stripes and floodlights.
    /// Europa — −160 °C, Jupiter's radiation, an ocean under the ice: water-ice shield domes and
    ///        walls, heat-vent stacks with steam, cryobot drill derricks, cyan light, frost.
    /// </summary>
    public static class PlanetArchitecture
    {
        // Shared kit colours (HeroBuildingKits) that each world re-dresses.
        static readonly Color KitWhite = new Color(0.88f, 0.82f, 0.74f);
        static readonly Color KitTan = new Color(0.78f, 0.62f, 0.46f);
        static readonly Color KitOrange = new Color(0.96f, 0.42f, 0.08f);
        static readonly Color KitCarbon = new Color(0.26f, 0.24f, 0.22f);
        static readonly Color KitYellow = new Color(0.95f, 0.82f, 0.12f);

        public static ArchStyle Style(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Earth:
                    return new ArchStyle
                    {
                        Name = "Solarpunk arcology",
                        Rationale = "Open air, rain and a living biosphere: glass, green roofs, wind power, nothing to hide from.",
                        Hull = new Color(0.93f, 0.94f, 0.91f), Trim = new Color(0.10f, 0.64f, 0.60f),
                        Dark = new Color(0.30f, 0.27f, 0.23f), Glass = new Color(0.62f, 0.86f, 0.88f, 0.42f),
                        Glow = new Color(1f, 0.86f, 0.62f), GlowEmission = new Color(1.3f, 0.95f, 0.55f),
                        Shell = new Color(0.62f, 0.58f, 0.50f), Plant = new Color(0.24f, 0.56f, 0.26f),
                        Foil = new Color(0.86f, 0.70f, 0.30f), Ice = new Color(0.8f, 0.9f, 1f, 0.35f),
                        Steam = new Color(1f, 1f, 1f, 0.3f),
                        DustColor = new Color(0.34f, 0.30f, 0.22f), DustAmount = 0.06f, WearAmount = 0.10f
                    };
                case CelestialBodyId.Luna:
                    return new ArchStyle
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
                case CelestialBodyId.Mars:
                    return new ArchStyle
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
                case CelestialBodyId.Belt:
                    return new ArchStyle
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
                case CelestialBodyId.Europa:
                    return new ArchStyle
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
                default:
                    return Style(CelestialBodyId.Luna);
            }
        }

        public static ArchArchetype ArchetypeOf(BuildingCategory c) => c switch
        {
            BuildingCategory.Habitat or BuildingCategory.Inn or BuildingCategory.AidStation => ArchArchetype.Dwelling,
            BuildingCategory.Commons or BuildingCategory.GuildHall or BuildingCategory.Market => ArchArchetype.Hub,
            BuildingCategory.Power => ArchArchetype.Power,
            BuildingCategory.Farm or BuildingCategory.Mine or BuildingCategory.RegolithCamp or BuildingCategory.Mining => ArchArchetype.Extractor,
            BuildingCategory.Defense or BuildingCategory.Watchtower or BuildingCategory.AegisSpire => ArchArchetype.Defense,
            BuildingCategory.LandingPad => ArchArchetype.Pad,
            BuildingCategory.ClimateLoom or BuildingCategory.DeepArchive => ArchArchetype.Wonder,
            _ => ArchArchetype.Workshop
        };

        /// <summary>
        /// Re-dress a shared kit colour for this world (orange trim becomes Luna gold foil, Europa
        /// cyan, Belt hazard yellow, Earth teal; white shells follow the world's hull). False = keep.
        /// </summary>
        public static bool TryRemap(CelestialBodyId id, Color c, out Color result)
        {
            result = c;
            if (id == CelestialBodyId.Mars) return false; // Mars is the tuned reference look
            var s = Style(id);
            if (Near(c, KitOrange)) { result = s.Trim; return true; }
            if (Near(c, KitWhite) || Near(c, KitTan)) { result = s.Hull; return true; }
            if (Near(c, KitYellow) && id == CelestialBodyId.Europa) { result = s.Trim; return true; }
            if (Near(c, KitCarbon) && id == CelestialBodyId.Belt) { result = s.Dark; return true; }
            return false;
        }

        /// <summary>
        /// Same re-dress keyed by the shared art-library material name (<c>SM_Art_WhiteHull</c>,
        /// <c>SM_Art_Orange</c>, <c>SM_Art_BlackCarbon</c>…), which is what kit prims carry once
        /// IndustrialArtDressing has run. Falls back to <see cref="TryRemap(CelestialBodyId, Color, out Color)"/>.
        /// </summary>
        public static bool TryRemapMaterial(CelestialBodyId id, string materialName, Color current, out Color result)
        {
            result = current;
            if (id == CelestialBodyId.Mars) return false;
            if (!string.IsNullOrEmpty(materialName) && materialName.StartsWith("SM_Art_"))
            {
                var s = Style(id);
                string slot = materialName.Substring(7);
                if (slot.StartsWith("WhiteHull")) { result = s.Hull; return true; }
                if (slot.StartsWith("Orange")) { result = s.Trim; return true; }
                if (slot.StartsWith("BlackCarbon") && id == CelestialBodyId.Belt) { result = s.Dark; return true; }
                return false; // other library slots (steel, glass, solar, cyan…) read right everywhere
            }
            return TryRemap(id, current, out result);
        }

        static bool Near(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return dr * dr + dg * dg + db * db < 0.012f;
        }

        /// <summary>
        /// Adaptation parts for one building. <paramref name="h"/> is the height of the building's
        /// own hull; <paramref name="seed"/> varies small details between neighbours.
        /// </summary>
        public static List<ArchPart> Adapt(CelestialBodyId id, ArchArchetype a, float w, float d, float h, int seed)
        {
            var p = new Builder(w, d, Mathf.Max(0.8f, h), seed);
            switch (id)
            {
                case CelestialBodyId.Earth: Earth(p, a); break;
                case CelestialBodyId.Luna: Luna(p, a); break;
                case CelestialBodyId.Mars: Mars(p, a); break;
                case CelestialBodyId.Belt: Belt(p, a); break;
                case CelestialBodyId.Europa: Europa(p, a); break;
            }
            return p.Parts;
        }

        // ------------------------------------------------------------------ Earth

        static void Earth(Builder p, ArchArchetype a)
        {
            float w = p.W, d = p.D, h = p.H;
            if (a != ArchArchetype.Pad)
            {
                // Green roof and warm window bands.
                p.Box("GreenRoof", ArchRole.Plant, new Vector3(0, h + 0.07f, 0), new Vector3(w * 0.72f, 0.14f, d * 0.72f));
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

        // ------------------------------------------------------------------ Luna

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

        // ------------------------------------------------------------------ Mars

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

        // ------------------------------------------------------------------ Belt

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

        // ------------------------------------------------------------------ Europa

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

        // ------------------------------------------------------------------ builder

        sealed class Builder
        {
            public readonly List<ArchPart> Parts = new List<ArchPart>(48);
            public readonly float W, D, H;
            /// <summary>Half the smaller footprint side.</summary>
            public readonly float R;
            readonly System.Random _rng;

            public Builder(float w, float d, float h, int seed)
            {
                W = w; D = d; H = h; R = Mathf.Min(w, d) * 0.5f;
                _rng = new System.Random(seed);
            }

            public int Rng(int n) => _rng.Next(n);
            public float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

            public void Add(string n, ArchShape s, ArchRole r, Vector3 pos, Vector3 size, Vector3 euler) =>
                Parts.Add(new ArchPart { Name = n, Shape = s, Role = r, Position = pos, Size = size, Euler = euler });
            public void Box(string n, ArchRole r, Vector3 pos, Vector3 size, Vector3 euler = default) => Add(n, ArchShape.Box, r, pos, size, euler);
            public void Cyl(string n, ArchRole r, Vector3 pos, Vector3 size, Vector3 euler = default) => Add(n, ArchShape.Cylinder, r, pos, size, euler);
            public void Sphere(string n, ArchRole r, Vector3 pos, Vector3 size, Vector3 euler = default) => Add(n, ArchShape.Sphere, r, pos, size, euler);

            /// <summary>True when an angle (degrees, 0 = +Z) points at a doorway lane.</summary>
            static bool InPortLane(float deg, float halfWidth = 16f)
            {
                float m = ((deg % 90f) + 90f) % 90f;
                return m < halfWidth || m > 90f - halfWidth;
            }

            /// <summary>Mounds around the footprint, leaving the four doorway lanes clear.</summary>
            public void PerimeterBerm(ArchRole role, float height, float depth, float length, float outset = 0.35f)
            {
                float rx = W * 0.5f + outset, rz = D * 0.5f + outset;
                float perim = 2f * Mathf.PI * Mathf.Sqrt((rx * rx + rz * rz) * 0.5f);
                int n = Mathf.Max(8, Mathf.RoundToInt(perim / (length * 0.8f)));
                for (int i = 0; i < n; i++)
                {
                    float deg = i * 360f / n + 45f / n;
                    if (InPortLane(deg)) continue;
                    float rad = deg * Mathf.Deg2Rad;
                    // Square-ish ring hugging a rectangle.
                    float sx = Mathf.Sin(rad), cz = Mathf.Cos(rad);
                    float k = 1f / Mathf.Max(Mathf.Abs(sx) / rx, Mathf.Abs(cz) / rz);
                    Vector3 at = new Vector3(sx * k, height * 0.28f, cz * k);
                    // Keep the whole mound, not just its centre, off the lane axes.
                    if (Mathf.Min(Mathf.Abs(at.x), Mathf.Abs(at.z)) < 0.8f + length * 0.5f) continue;
                    Sphere("Berm" + i, role, at, new Vector3(length, height, depth * (0.9f + 0.2f * (float)_rng.NextDouble())), new Vector3(0, deg + 90f, 0));
                }
            }

            /// <summary>Glowing window strips on the ±X faces, above the port height.</summary>
            public void WindowBands(float y, ArchRole role, bool slit = false)
            {
                float t = slit ? 0.06f : 0.12f;
                // Split either side of the doorway (0.71 m half-width).
                float z0 = 0.95f, z1 = D * 0.44f;
                if (z1 - z0 < 0.3f) return;
                float zc = (z0 + z1) * 0.5f, len = z1 - z0;
                for (int s = -1; s <= 1; s += 2)
                {
                    Box("WindowA" + s, role, new Vector3(s * (W * 0.5f + 0.02f), y, -zc), new Vector3(0.04f, t, len));
                    Box("WindowB" + s, role, new Vector3(s * (W * 0.5f + 0.02f), y, zc), new Vector3(0.04f, t, len));
                }
            }

            public void CornerPlanters(ArchRole box, ArchRole plant)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * (W * 0.5f + 0.45f), z = (i < 2 ? -1 : 1) * (D * 0.5f + 0.45f);
                    Box("Planter" + i, box, new Vector3(x, 0.2f, z), new Vector3(0.7f, 0.4f, 0.7f));
                    Sphere("PlanterGreen" + i, plant, new Vector3(x, 0.55f, z), new Vector3(0.75f, 0.6f, 0.75f));
                }
            }

            public void GlassPavilion(Vector3 at, float w, float h, float d)
            {
                Box("Pavilion", ArchRole.Glass, at + new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -0.5f : 0.5f) * w, z = (i < 2 ? -0.5f : 0.5f) * d;
                    Box("PavilionPost" + i, ArchRole.Trim, at + new Vector3(x, h * 0.5f, z), new Vector3(0.06f, h, 0.06f));
                }
                Box("PavilionRoof", ArchRole.Trim, at + new Vector3(0, h, 0), new Vector3(w + 0.08f, 0.05f, d + 0.08f));
            }

            public void WindTurbine(Vector3 at, float height)
            {
                Cyl("TurbineMast", ArchRole.Hull, at + new Vector3(0, height * 0.5f, 0), new Vector3(0.14f, height, 0.14f));
                // Vertical-axis helix: three tall curved blades around the top.
                for (int i = 0; i < 3; i++)
                {
                    Vector3 o = Quaternion.Euler(0, i * 120f, 0) * new Vector3(0.45f, 0, 0);
                    Box("TurbineBlade" + i, ArchRole.Hull, at + o + new Vector3(0, height - 0.9f, 0), new Vector3(0.08f, 1.6f, 0.22f), new Vector3(0, i * 120f, 12f));
                }
                Cyl("TurbineCap", ArchRole.Trim, at + new Vector3(0, height, 0), new Vector3(0.3f, 0.12f, 0.3f));
            }

            public void Mast(Vector3 at, float height, ArchRole mast, ArchRole beacon)
            {
                Cyl("Mast", mast, at + new Vector3(0, height * 0.5f, 0), new Vector3(0.08f, height, 0.08f));
                Sphere("Beacon", beacon, at + new Vector3(0, height + 0.08f, 0), Vector3.one * 0.22f);
            }

            public void EdgeLights(ArchRole role, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 dir = Quaternion.Euler(0, i * 360f / count, 0) * Vector3.forward;
                    Cyl("EdgeLight" + i, role, dir * (R * 0.94f) + new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.1f, 0.2f));
                }
            }

            /// <summary>White radiator fins on the rear corners, edge-on to the sun.</summary>
            public void Radiators(float h, int pairs)
            {
                for (int i = 0; i < pairs; i++)
                {
                    float s = i == 0 ? -1f : 1f;
                    Vector3 at = new Vector3(s * (W * 0.5f + 0.9f), h * 0.6f, -D * 0.3f);
                    Box("RadiatorStrut" + i, ArchRole.Dark, new Vector3(s * (W * 0.5f + 0.35f), h * 0.6f, -D * 0.3f), new Vector3(0.7f, 0.08f, 0.08f));
                    Box("Radiator" + i, ArchRole.Hull, at, new Vector3(0.05f, h * 0.9f, D * 0.36f));
                }
            }

            public void Dish(Vector3 at, float size)
            {
                Cyl("DishPylon", ArchRole.Dark, at + new Vector3(0, 0.45f, 0), new Vector3(0.14f, 0.9f, 0.14f));
                Sphere("Dish", ArchRole.Hull, at + new Vector3(0, 1.1f, 0), new Vector3(size, 0.25f, size), new Vector3(-45f, 0, 0));
                Sphere("DishFeed", ArchRole.Glow, at + new Vector3(0, 1.45f, 0.35f), Vector3.one * 0.14f);
            }

            /// <summary>3D-printed regolith wall: stacked strata, each layer a touch inset.</summary>
            /// <summary>Layered printed-regolith wall along X, split by <paramref name="gap"/> metres at its middle.</summary>
            public void PrintedWall(Vector3 at, float length, float height, float thick, float gap = 0f)
            {
                int layers = Mathf.Max(4, Mathf.RoundToInt(height / 0.22f));
                float lh = height / layers;
                float half = (length - gap) * 0.5f;
                for (int side = gap > 0f ? -1 : 0; side <= (gap > 0f ? 1 : 0); side += gap > 0f ? 2 : 1)
                {
                    float len = gap > 0f ? half : length;
                    float cx = gap > 0f ? side * (gap + half) * 0.5f : 0f;
                    if (len < 0.3f) continue;
                    string tag = side < 0 ? "L" : side > 0 ? "R" : "";
                    for (int i = 0; i < layers; i++)
                    {
                        float inset = (i % 2 == 0 ? 0f : 0.03f) + i * 0.01f;
                        Box("Strata" + tag + i, ArchRole.Shell, at + new Vector3(cx, lh * (i + 0.5f), 0),
                            new Vector3(len - i * 0.04f, lh * 0.92f, thick - inset));
                    }
                }
            }

            /// <summary>Printed tapering tower (conical stack of strata) with a glowing window slit.</summary>
            public void PrintedTower(Vector3 at, float baseDiameter, float height, int layers)
            {
                float lh = height / layers;
                for (int i = 0; i < layers; i++)
                {
                    float t = i / (float)layers;
                    float dia = Mathf.Lerp(baseDiameter, baseDiameter * 0.45f, t * t);
                    Cyl("TowerStrata" + i, ArchRole.Shell, at + new Vector3(0, lh * (i + 0.5f), 0), new Vector3(dia, lh * 0.94f, dia));
                }
                Box("TowerSlit", ArchRole.Glow, at + new Vector3(0, height * 0.55f, baseDiameter * 0.36f), new Vector3(0.1f, height * 0.5f, 0.06f));
            }

            /// <summary>Printed ring wall at the base of a dome.</summary>
            public void PrintedRing(Vector3 at, float diameter, float height, int layers)
            {
                float lh = height / layers;
                for (int i = 0; i < layers; i++)
                    Cyl("RingStrata" + i, ArchRole.Shell, at + new Vector3(0, lh * (i + 0.5f), 0), new Vector3(diameter + 0.2f - i * 0.05f, lh * 0.94f, diameter + 0.2f - i * 0.05f));
            }

            /// <summary>Truss spars along the roof edges with cross members.</summary>
            public void TrussFrame(float y)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Box("TrussX" + s, ArchRole.Dark, new Vector3(0, y, s * D * 0.5f), new Vector3(W * 1.04f, 0.1f, 0.1f));
                    Box("TrussZ" + s, ArchRole.Dark, new Vector3(s * W * 0.5f, y, 0), new Vector3(0.1f, 0.1f, D * 1.04f));
                }
                Box("TrussDiagA", ArchRole.Dark, new Vector3(0, y, 0), new Vector3(0.07f, 0.07f, Mathf.Sqrt(W * W + D * D)), new Vector3(0, Mathf.Atan2(W, D) * Mathf.Rad2Deg, 0));
                Box("TrussDiagB", ArchRole.Dark, new Vector3(0, y, 0), new Vector3(0.07f, 0.07f, Mathf.Sqrt(W * W + D * D)), new Vector3(0, -Mathf.Atan2(W, D) * Mathf.Rad2Deg, 0));
            }

            /// <summary>Vertical spin-gravity ring (segments of a torus) on a hub strut.</summary>
            public void SpinRing(Vector3 center, float radius, int segments, float yaw = 0f)
            {
                // Yawed onto the diagonal so the rim never swings across a doorway lane.
                Quaternion turn = Quaternion.Euler(0, yaw, 0);
                Cyl("SpinHub", ArchRole.Dark, center, new Vector3(0.6f, 0.5f, 0.6f), new Vector3(90, yaw, 0));
                Cyl("SpinStrut", ArchRole.Dark, new Vector3(center.x, center.y * 0.5f + 0.2f, center.z), new Vector3(0.18f, center.y - 0.4f, 0.18f));
                float seg = 2f * Mathf.PI * radius / segments * 1.08f;
                for (int i = 0; i < segments; i++)
                {
                    float deg = i * 360f / segments;
                    Vector3 o = turn * (Quaternion.Euler(0, 0, deg) * new Vector3(0, radius, 0));
                    Box("SpinSeg" + i, i % 4 == 0 ? ArchRole.Trim : ArchRole.Hull, center + o, new Vector3(seg, 0.34f, 0.5f), new Vector3(0, yaw, deg));
                }
                for (int i = 0; i < 4; i++)
                {
                    float deg = i * 90f + 45f;
                    Vector3 o = turn * (Quaternion.Euler(0, 0, deg) * new Vector3(0, radius * 0.5f, 0));
                    Box("SpinSpoke" + i, ArchRole.Dark, center + o, new Vector3(0.08f, radius, 0.08f), new Vector3(0, yaw, deg));
                }
            }

            /// <summary>Ice-block shield walls around the base, doorway lanes left open.</summary>
            public void IceWalls(float height, float outset = 0.3f)
            {
                float bw = 0.9f;
                for (int side = 0; side < 4; side++)
                {
                    bool alongX = side < 2;
                    float sign = side % 2 == 0 ? -1f : 1f;
                    float span = alongX ? W : D;
                    int n = Mathf.Max(2, Mathf.FloorToInt(span / bw));
                    for (int i = 0; i < n; i++)
                    {
                        float u = -span * 0.5f + (i + 0.5f) * span / n;
                        if (Mathf.Abs(u) < 0.9f) continue; // doorway lane
                        float hh = height * (0.75f + 0.25f * (float)_rng.NextDouble());
                        Vector3 at = alongX
                            ? new Vector3(u, hh * 0.5f, sign * (D * 0.5f + outset))
                            : new Vector3(sign * (W * 0.5f + outset), hh * 0.5f, u);
                        Box("IceBlock" + side + "_" + i, ArchRole.Ice, at,
                            alongX ? new Vector3(span / n * 0.94f, hh, 0.5f) : new Vector3(0.5f, hh, span / n * 0.94f));
                    }
                }
            }

            /// <summary>Heat vent stack with a glowing collar and a vapour plume.</summary>
            public void HeatVent(Vector3 at, float height)
            {
                Cyl("VentStack", ArchRole.Hull, at + new Vector3(0, height * 0.5f, 0), new Vector3(0.45f, height, 0.45f));
                Cyl("VentCollar", ArchRole.Glow, at + new Vector3(0, height - 0.15f, 0), new Vector3(0.52f, 0.1f, 0.52f));
                for (int i = 0; i < 3; i++)
                    Sphere("Vapour" + i, ArchRole.Steam, at + new Vector3(0.1f * i, height + 0.35f + i * 0.45f, 0.05f * i),
                        Vector3.one * (0.55f + i * 0.28f));
            }
        }
    }
}
