using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>What a marked build site is for (Majesty 2 trading-post zones and temple places).</summary>
    public enum BuildZoneKind
    {
        None = 0,
        TradePost = 1,
        Temple = 2,
        /// <summary>Around an ore deposit, far from the base (Majesty: mines are a risk you walk out to).</summary>
        Mine = 3
    }

    /// <summary>One marked site. A zone holds a single building and is taken while one stands in it.</summary>
    public struct BuildZone
    {
        public BuildZoneKind Kind;
        public Vector3 Center;
        public float Radius;
    }

    /// <summary>A distance band (metres from the Commons) for placing one zone.</summary>
    [Serializable]
    public struct ZoneBand
    {
        [Min(0f)] public float minMeters;
        [Min(0f)] public float maxMeters;
    }

    /// <summary>
    /// Majesty 2 builds trading posts and temples only in marked zones. This holds the rule, the
    /// zone layout (a spread of distances, so a far zone is a real risk/reward choice) and the
    /// pure checks. Held by GameLoop; the world generator places the zones.
    /// </summary>
    [Serializable]
    public sealed class BuildZoneTuning
    {
        [Tooltip("Off = free placement for everything.")]
        public bool enabled = true;
        [Tooltip("Radius of a zone in metres. The building's centre must stand inside it.")]
        [Min(1f)] public float zoneRadius = 7f;
        [Tooltip("Buildings that can only be placed in a trade-post zone (Majesty trading post; our landing pad).")]
        public BuildingCategory[] tradePostBuildings =
        {
            BuildingCategory.LandingPad
        };
        [Tooltip("Buildings that can only be placed around an ore deposit (our nuclear mine).")]
        public BuildingCategory[] mineBuildings =
        {
            BuildingCategory.Mine
        };
        [Tooltip("Buildings that can only be placed in a temple zone.")]
        public BuildingCategory[] templeBuildings =
        {
            BuildingCategory.ClimateLoom, BuildingCategory.AegisSpire, BuildingCategory.DeepArchive
        };
        [Tooltip("One trade-post zone per band, near to far.")]
        public ZoneBand[] tradePostBands =
        {
            new ZoneBand { minMeters = 24f, maxMeters = 40f },
            new ZoneBand { minMeters = 40f, maxMeters = 60f },
            new ZoneBand { minMeters = 60f, maxMeters = 85f },
            new ZoneBand { minMeters = 85f, maxMeters = 115f },
            new ZoneBand { minMeters = 105f, maxMeters = 140f },
            new ZoneBand { minMeters = 125f, maxMeters = 165f },
        };
        [Tooltip("One mine deposit per band, metres from the Commons. Far enough that raiders are a real threat; each gets an ore node and a zone ring.")]
        public ZoneBand[] mineBands =
        {
            new ZoneBand { minMeters = 55f, maxMeters = 75f },
            new ZoneBand { minMeters = 80f, maxMeters = 105f },
            new ZoneBand { minMeters = 110f, maxMeters = 140f },
            new ZoneBand { minMeters = 140f, maxMeters = 170f },
        };
        [Tooltip("One temple zone per band.")]
        public ZoneBand[] templeBands =
        {
            new ZoneBand { minMeters = 28f, maxMeters = 55f },
            new ZoneBand { minMeters = 50f, maxMeters = 85f },
            new ZoneBand { minMeters = 80f, maxMeters = 120f },
        };

        public BuildZoneKind KindFor(BuildingCategory cat)
        {
            if (!enabled) return BuildZoneKind.None;
            if (tradePostBuildings != null && Array.IndexOf(tradePostBuildings, cat) >= 0) return BuildZoneKind.TradePost;
            if (templeBuildings != null && Array.IndexOf(templeBuildings, cat) >= 0) return BuildZoneKind.Temple;
            if (mineBuildings != null && Array.IndexOf(mineBuildings, cat) >= 0) return BuildZoneKind.Mine;
            return BuildZoneKind.None;
        }

        public bool IsZoneBound(BuildingCategory cat) => KindFor(cat) != BuildZoneKind.None;

        /// <summary>
        /// True if a building of this kind may stand at <paramref name="point"/>: inside a zone of
        /// its kind that no other building of that kind already holds. Zone-free categories pass.
        /// </summary>
        public bool Allows(
            BuildZoneKind kind, Vector3 point, IReadOnlyList<BuildZone> zones, IReadOnlyList<Vector3> takenBy)
        {
            if (kind == BuildZoneKind.None) return true;
            if (zones == null) return false;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.Kind != kind) continue;
                if (Flat(point, z.Center) > z.Radius) continue;
                if (IsTaken(z, takenBy)) continue;
                return true;
            }
            return false;
        }

        /// <summary>A zone is taken while a bound building's centre sits inside it.</summary>
        public static bool IsTaken(in BuildZone z, IReadOnlyList<Vector3> takenBy)
        {
            if (takenBy == null) return false;
            for (int i = 0; i < takenBy.Count; i++)
                if (Flat(takenBy[i], z.Center) <= z.Radius) return true;
            return false;
        }

        /// <summary>Why a bound building cannot go here, for the colony log.</summary>
        public string Reason(BuildZoneKind kind, bool anyZoneFree)
        {
            switch (kind)
            {
                case BuildZoneKind.TradePost:
                    return anyZoneFree ? "Landing pads only fit inside a gold trade ring." : "Every trade zone is taken.";
                case BuildZoneKind.Mine:
                    return anyZoneFree ? "Mines can only be built at an ore deposit, out in the wilds." : "Every ore deposit has a mine.";
                default:
                    return anyZoneFree ? "Temples can only be built on a marked temple site." : "Every temple site is taken.";
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
