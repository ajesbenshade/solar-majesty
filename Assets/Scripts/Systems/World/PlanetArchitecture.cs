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
        Steam,
        /// <summary>Bare structural metal: trusses, anchors, gantries, pipework.</summary>
        Metal,
        /// <summary>Photovoltaic cells (dark blue with a faint glow).</summary>
        Solar,
        /// <summary>Opaque frost, packed snow or rime on roofs and ledges.</summary>
        Frost,
        /// <summary>Earth, soil and loose regolith heaped against walls.</summary>
        Soil,
        /// <summary>Open water: ponds, cisterns, moon pools (translucent).</summary>
        Water,
        /// <summary>Aviation / hazard beacon light (usually blinking).</summary>
        Beacon,
        /// <summary>The world's signature accent light (Earth teal, Europa cyan…).</summary>
        Signal
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

        /// <summary>None = static (merged with its role's other parts into one mesh).</summary>
        public ArchMotion Motion;
        /// <summary>Parts sharing a group move together about <see cref="MotionPivot"/>.</summary>
        public string MotionGroup;
        public Vector3 MotionPivot;
        public Vector3 MotionAxis;
        /// <summary>Spin: degrees/second. Sweep: half-arc degrees. Bob: metres.</summary>
        public float MotionAmount;
        /// <summary>Seconds per cycle for Sweep, Blink, Pulse and Bob.</summary>
        public float MotionPeriod;

        /// <summary>True = not geometry: a vent or spark point for ambient effects at <see cref="Position"/>.</summary>
        public bool IsEmitter;
        public EmitterKind EmitKind;
    }

    /// <summary>How an architecture part moves (see KitLife in Runtime).</summary>
    public enum ArchMotion
    {
        None,
        Spin,
        Sweep,
        Blink,
        Pulse,
        Bob
    }

    /// <summary>A world's building palette and surface weathering.</summary>
    public struct ArchStyle
    {
        public string Name;
        /// <summary>Why the buildings look like this (local conditions), for docs and tooltips.</summary>
        public string Rationale;
        public Color Hull, Trim, Dark, Glass, Glow, GlowEmission, Shell, Plant, Foil, Ice, Steam;
        /// <summary>Optional roles; left default (alpha 0) they fall back to sensible colours.</summary>
        public Color Metal, Solar, Frost, Soil, Water, Beacon, BeaconEmission, Signal, SignalEmission;
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
            ArchRole.Metal => Metal.a > 0f ? Metal : new Color(0.42f, 0.44f, 0.48f),
            ArchRole.Solar => Solar.a > 0f ? Solar : new Color(0.10f, 0.16f, 0.30f),
            ArchRole.Frost => Frost.a > 0f ? Frost : new Color(0.90f, 0.95f, 1f),
            ArchRole.Soil => Soil.a > 0f ? Soil : Shell,
            ArchRole.Water => Water.a > 0f ? Water : new Color(0.20f, 0.42f, 0.52f, 0.7f),
            ArchRole.Beacon => Beacon.a > 0f ? Beacon : new Color(1f, 0.22f, 0.12f),
            ArchRole.Signal => Signal.a > 0f ? Signal : Trim,
            _ => Hull
        };

        /// <summary>HDR emission for glowing roles, or black.</summary>
        public Color RoleEmission(ArchRole r) => r switch
        {
            ArchRole.Glow => GlowEmission,
            ArchRole.Beacon => BeaconEmission.maxColorComponent > 0.01f ? BeaconEmission : new Color(2.4f, 0.35f, 0.18f),
            ArchRole.Signal => SignalEmission.maxColorComponent > 0.01f ? SignalEmission : RoleColor(ArchRole.Signal) * 2.2f,
            ArchRole.Solar => new Color(0.05f, 0.12f, 0.32f),
            _ => Color.black
        };

        public static bool IsGlowing(ArchRole r) => r == ArchRole.Glow || r == ArchRole.Beacon || r == ArchRole.Signal;

        public static bool IsTranslucent(ArchRole r) =>
            r == ArchRole.Glass || r == ArchRole.Ice || r == ArchRole.Steam || r == ArchRole.Water;
    }

    /// <summary>
    /// Each world builds for its own conditions. Pure data: a palette, a remap of the shared kit
    /// colours, and a set of adaptation parts added to every building sized to its footprint and
    /// height. Doorway lanes (the four face centres) are always kept clear.
    ///
    /// Earth — open air, rain, a living biosphere: solarpunk glass atria, rooftop shrubs, wind.
    /// Luna — vacuum, radiation, micrometeoroids, ±150 °C: regolith berms and shield caps,
    ///        white radiators, gold foil, polar sun-tracking towers, red beacons.
    /// Mars — thin CO₂, dust storms, −60 °C: 3D-printed regolith (layered strata), inflatable
    ///        pressure domes, printed windbreaks, amber heater glow, compact fission power.
    /// Belt — microgravity rock, no air, weak sun: anchor stilts and tethers, truss spars,
    ///        spin-gravity rings, huge solar wings, hazard stripes and floodlights.
    /// Europa — −160 °C, Jupiter's radiation, an ocean under the ice: water-ice shield domes and
    ///        walls, heat-vent stacks with steam, cryobot drill derricks, cyan light, frost.
    /// </summary>
    public static partial class PlanetArchitecture
    {
        // Shared kit colours (HeroBuildingKits) that each world re-dresses.
        static readonly Color KitWhite = new Color(0.88f, 0.82f, 0.74f);
        static readonly Color KitTan = new Color(0.78f, 0.62f, 0.46f);
        static readonly Color KitOrange = new Color(0.96f, 0.42f, 0.08f);
        static readonly Color KitCarbon = new Color(0.26f, 0.24f, 0.22f);
        static readonly Color KitYellow = new Color(0.95f, 0.82f, 0.12f);

        public static ArchStyle Style(CelestialBodyId id) => id switch
        {
            CelestialBodyId.Earth => StyleEarth(),
            CelestialBodyId.Luna => StyleLuna(),
            CelestialBodyId.Mars => StyleMars(),
            CelestialBodyId.Belt => StyleBelt(),
            CelestialBodyId.Europa => StyleEuropa(),
            _ => StyleLuna()
        };

        /// <summary>
        /// Roof shells a world lays over a building's hull (Mars pressure dome and printed vault,
        /// Earth atrium…). Kits that carry their own detailed roof skip these, or the shell
        /// buries the roof.
        /// </summary>
        public static bool IsRoofShell(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return false;
            foreach (var prefix in RoofShellPrefixes)
                if (partName.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
            return false;
        }

        static readonly string[] RoofShellPrefixes =
        {
            "PressureDome", "DomeRib", "RingStrata", "PrintedVault", "VaultCourse", "VaultDoor",
            "Shrub", "Atrium", "Skylight", "Pavilion"
        };

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
        /// Adaptation parts for one building, fitted to the kit's own description of itself
        /// (<see cref="KitShape"/>: hull bounds, real roof surfaces, doorways). Positions are in the
        /// building's local space with the ground at y 0. <paramref name="seed"/> varies small
        /// details between neighbours. Each world lives in its own file (PlanetArchitecture.Earth.cs…).
        /// </summary>
        public static List<ArchPart> Adapt(CelestialBodyId id, ArchArchetype a, KitShape shape, int seed)
        {
            var p = new Builder(shape, seed);
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

        /// <summary>
        /// Stand-in box of <paramref name="h"/> with a flat roof over the hull and face-centre
        /// doorways — offline previews and tests.
        /// </summary>
        public static List<ArchPart> Adapt(CelestialBodyId id, ArchArchetype a, float w, float d, float h, int seed) =>
            Adapt(id, a, KitShape.FlatBox(RepresentativeOf(a), w, d, Mathf.Max(0.8f, h)), seed);

        /// <summary>A category that stands for an archetype, for stand-in shapes.</summary>
        public static BuildingCategory RepresentativeOf(ArchArchetype a) => a switch
        {
            ArchArchetype.Dwelling => BuildingCategory.Habitat,
            ArchArchetype.Hub => BuildingCategory.Commons,
            ArchArchetype.Power => BuildingCategory.Power,
            ArchArchetype.Extractor => BuildingCategory.Mine,
            ArchArchetype.Workshop => BuildingCategory.EngineerWorkshop,
            ArchArchetype.Defense => BuildingCategory.Defense,
            ArchArchetype.Pad => BuildingCategory.LandingPad,
            _ => BuildingCategory.ClimateLoom
        };

        // ------------------------------------------------------------------ builder

        sealed class Builder
        {
            public readonly List<ArchPart> Parts = new List<ArchPart>(48);
            /// <summary>Footprint (x, z) and the top of the kit's main hull.</summary>
            public readonly float W, D, H;
            /// <summary>Half the smaller footprint side.</summary>
            public readonly float R;
            /// <summary>The kit's hull bounds, real roof surfaces, doorways and vents.</summary>
            public readonly KitShape Shape;
            public BuildingCategory Category => Shape.Category;
            readonly System.Random _rng;

            public Builder(KitShape shape, int seed)
            {
                Shape = shape;
                W = shape.W; D = shape.D; H = Mathf.Max(0.8f, shape.BodyHeight); R = Mathf.Min(W, D) * 0.5f;
                _rng = new System.Random(seed);
            }

            public int Rng(int n) => _rng.Next(n);
            public float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

            ArchPart _motion;
            bool _inMotion;

            public void Add(string n, ArchShape s, ArchRole r, Vector3 pos, Vector3 size, Vector3 euler)
            {
                var part = new ArchPart { Name = n, Shape = s, Role = r, Position = pos, Size = size, Euler = euler };
                if (_inMotion)
                {
                    part.Motion = _motion.Motion; part.MotionGroup = _motion.MotionGroup; part.MotionPivot = _motion.MotionPivot;
                    part.MotionAxis = _motion.MotionAxis; part.MotionAmount = _motion.MotionAmount; part.MotionPeriod = _motion.MotionPeriod;
                }
                Parts.Add(part);
            }

            /// <summary>
            /// Parts added until <see cref="EndMotion"/> move together as <paramref name="group"/>:
            /// spinning rotors, sweeping dishes, blinking beacons. Group names must be unique per building.
            /// </summary>
            public void BeginMotion(string group, ArchMotion motion, Vector3 pivot, Vector3 axis, float amount, float period = 2f)
            {
                _motion = new ArchPart
                {
                    Motion = motion, MotionGroup = group, MotionPivot = pivot,
                    MotionAxis = axis == default ? Vector3.up : axis.normalized, MotionAmount = amount, MotionPeriod = period
                };
                _inMotion = true;
            }

            public void EndMotion() => _inMotion = false;

            /// <summary>A vent or spark point for ambient effects (steam, smoke, sparks, heat shimmer).</summary>
            public void Emit(string n, EmitterKind kind, Vector3 pos, Vector3 dir = default, float scale = 1f) =>
                Parts.Add(new ArchPart
                {
                    Name = n, IsEmitter = true, EmitKind = kind, Position = pos, Size = Vector3.one * Mathf.Max(0.05f, scale),
                    Euler = dir == default ? Vector3.up : dir.normalized, Role = ArchRole.Steam
                });

            /// <summary>True when a box (centre, full size) would stand in one of the kit's doorway lanes.</summary>
            public bool BlocksDoor(Vector3 center, Vector3 size) => Shape.BlocksDoor(center, size);
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
