using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Demo campus layout: Majesty-readable scale + coherent clustering.
    /// Phase 5C: Campus A (primary) + Campus B (second body) for multi-body framing.
    /// </summary>
    public static class ColonyLayout
    {
        /// <summary>World-space campus A center (ground).</summary>
        public static readonly Vector3 CampusOrigin = new Vector3(24f, 0f, 22f);

        /// <summary>World-space campus B center (NE outpost).</summary>
        public static readonly Vector3 CampusBOrigin = new Vector3(54f, 0f, 48f);

        /// <summary>Modules (HAB/LAB/CMD/OPS/PWR/Dome/connectors).</summary>
        public const float ModuleScale = 0.36f;

        /// <summary>Landing pad — still a landmark, not a 40m plate.</summary>
        public const float PadScale = 0.18f;

        /// <summary>Starship placeholder height landmark.</summary>
        public const float ShipScale = 0.18f;

        public static float ScaleForPath(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath)) return ModuleScale;
            if (resourcesPath.Contains("LandingPad")) return PadScale;
            if (resourcesPath.Contains("Starship")) return ShipScale;
            return ModuleScale;
        }

        public static float ScaleForCategory(BuildingCategory category)
        {
            return category == BuildingCategory.LandingPad ? PadScale : ModuleScale;
        }

        /// <summary>Plaza where specialists gather (between dome and south power yard).</summary>
        public static Vector3 PartySpawn => CampusOrigin + new Vector3(0f, 0f, -8f);

        /// <summary>Waystation inn — disconnected outpost south of campus. Not on the tube graph.</summary>
        public static Vector3 InnOutpost => CampusOrigin + new Vector3(0f, 0f, -18f);

        public static Vector3 PartySpawnB => CampusBOrigin + new Vector3(0f, 0f, -6f);

        /// <summary>Camera look-at for campus A.</summary>
        public static Vector3 CameraFocus => CampusOrigin + new Vector3(0f, 0f, -2f);

        public static Vector3 CameraFocusB => CampusBOrigin + new Vector3(0f, 0f, -1f);

        public static Vector3 GroundCenter => (CampusOrigin + CampusBOrigin) * 0.5f;

        /// <summary>Original sandbox was 64 cells (96 m). 256 cells at 1.5 m = 384 m → 16× area.</summary>
        public const int MapCells = 256;

        public const float CameraOrthoSize = 16f;

        /// <summary>Distance within which stalkers contribute to a specialist's bodyDanger.</summary>
        public const float LocalThreatRadius = 16f;

        /// <summary>0 = Campus A (primary), 1 = Campus B (outpost).</summary>
        public static int NearestCampusIndex(Vector3 world)
        {
            float da = FlatDistSq(world, CampusOrigin);
            float db = FlatDistSq(world, CampusBOrigin);
            return db < da ? 1 : 0;
        }

        public static Vector3 CampusOriginFor(int campusIndex) =>
            campusIndex <= 0 ? CampusOrigin : CampusBOrigin;

        public static string CampusLabel(int campusIndex) =>
            campusIndex <= 0 ? "Campus A" : "Campus B";

        private static float FlatDistSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Campus A — axis-aligned (yaw 0) so HAB / plus / LAB / CMD / pad dock on cardinals.
        /// Inn is spawned separately and is not in this graph.
        /// </summary>
        public static readonly ShowcasePiece[] Showcase =
        {
            new ShowcasePiece("Buildings/SM_CommandDome_CentralHub", new Vector3(0f, 0f, 0f), 0f, 6, 6),
            new ShowcasePiece("Buildings/SM_HAB1_HabitatModule", new Vector3(-12f, 0f, 0f), 0f, 4, 4),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(-6.5f, 0f, 0f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_LAB1_LaboratoryModule", new Vector3(-21f, 0f, 0f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(-16.5f, 0f, 0f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_CMD1_CommandBuilding", new Vector3(0f, 0f, 12f), 0f, 4, 4),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(0f, 0f, 6.5f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_OPS1_OperationsUnit", new Vector3(10f, 0f, 12f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(5f, 0f, 12f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_PWR1_PowerNode", new Vector3(0f, 0f, -12f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(0f, 0f, -6.5f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_PWR1_SolarArray", new Vector3(8f, 0f, -12f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_PWR1_SolarArray", new Vector3(-8f, 0f, -12f), 0f, 3, 3),
            new ShowcasePiece("Environment/SM_LandingPad", new Vector3(16f, 0f, 0f), 0f, 6, 6, PadScale),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(8.5f, 0f, 0f), 0f, 2, 2),
            new ShowcasePiece("Environment/SM_Starship_Placeholder", new Vector3(16f, 0f, 0f), 0f, 0, 0, ShipScale),
        };

        /// <summary>Smaller second-body outpost — also yaw 0, cardinal docks only.</summary>
        public static readonly ShowcasePiece[] ShowcaseB =
        {
            new ShowcasePiece("Buildings/SM_CommandDome_CentralHub", new Vector3(0f, 0f, 0f), 0f, 6, 6),
            new ShowcasePiece("Buildings/SM_HAB1_HabitatModule", new Vector3(-10.5f, 0f, 0f), 0f, 4, 4),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(-5.5f, 0f, 0f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_PWR1_PowerNode", new Vector3(0f, 0f, -9f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(0f, 0f, -5.5f), 0f, 2, 2),
            new ShowcasePiece("Buildings/SM_PWR1_SolarArray", new Vector3(8f, 0f, -9f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_OPS1_OperationsUnit", new Vector3(0f, 0f, 9f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(0f, 0f, 5.5f), 0f, 2, 2),
        };

        public readonly struct ShowcasePiece
        {
            public readonly string ResourcesPath;
            public readonly Vector3 LocalOffset;
            public readonly float YawDegrees;
            public readonly float Scale;
            public readonly int FootprintW;
            public readonly int FootprintH;

            public ShowcasePiece(
                string path,
                Vector3 localOffset,
                float yawDegrees,
                int footprintW,
                int footprintH,
                float scale = 0f)
            {
                ResourcesPath = path;
                LocalOffset = localOffset;
                YawDegrees = yawDegrees;
                FootprintW = footprintW;
                FootprintH = footprintH;
                Scale = scale;
            }

            public Vector3 WorldPosition => WorldPositionAt(CampusOrigin);

            public Vector3 WorldPositionAt(Vector3 campusOrigin) => campusOrigin + LocalOffset;

            public float ResolveScale() => Scale > 0f ? Scale : ScaleForPath(ResourcesPath);

            public bool ReservesCells => FootprintW > 0 && FootprintH > 0;
        }
    }
}
