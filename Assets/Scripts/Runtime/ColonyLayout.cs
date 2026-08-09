using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Demo campus layout: Majesty-readable scale (not 1:1 meters) + coherent clustering.
    /// Real Blender meters are too large next to capsule specialists; we shrink for silhouette clarity.
    /// </summary>
    public static class ColonyLayout
    {
        /// <summary>World-space campus center (ground).</summary>
        public static readonly Vector3 CampusOrigin = new Vector3(24f, 0f, 22f);

        /// <summary>Modules (HAB/LAB/CMD/OPS/PWR/Dome/connectors).</summary>
        public const float ModuleScale = 0.42f;

        /// <summary>Landing pad — still a landmark, not a 40m plate.</summary>
        public const float PadScale = 0.22f;

        /// <summary>Starship placeholder height landmark.</summary>
        public const float ShipScale = 0.22f;

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

        /// <summary>Plaza where specialists gather (south of dome).</summary>
        public static Vector3 PartySpawn => CampusOrigin + new Vector3(0f, 0f, -10f);

        /// <summary>Camera look-at for first Play frame.</summary>
        public static Vector3 CameraFocus => CampusOrigin + new Vector3(0f, 0f, -2f);

        public const float CameraOrthoSize = 14f;

        /// <summary>
        /// Ordered showcase pieces: path, local offset from CampusOrigin, yaw, footprint cells (W×H).
        /// Footprints reserve BuildingPlacer cells so player placement cannot overlap the campus.
        /// </summary>
        public static readonly ShowcasePiece[] Showcase =
        {
            // Core hub
            new ShowcasePiece("Buildings/SM_CommandDome_CentralHub", new Vector3(0f, 0f, 0f), 0f, 6, 6),

            // Habitat spine (west)
            new ShowcasePiece("Buildings/SM_HAB1_HabitatModule", new Vector3(-11f, 0f, 0f), 0f, 4, 3),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(-6.2f, 0f, 0f), 0f, 2, 1),
            new ShowcasePiece("Buildings/SM_LAB1_LaboratoryModule", new Vector3(-18.5f, 0f, 0f), 0f, 3, 2),
            new ShowcasePiece("Buildings/SM_ModularTubeConnector", new Vector3(-14.2f, 0f, 0f), 0f, 2, 1),

            // Command / ops (north)
            new ShowcasePiece("Buildings/SM_CMD1_CommandBuilding", new Vector3(-4f, 0f, 10f), 0f, 4, 4),
            new ShowcasePiece("Buildings/SM_OPS1_OperationsUnit", new Vector3(6f, 0f, 10f), 0f, 3, 3),

            // Power yard (south)
            new ShowcasePiece("Buildings/SM_PWR1_PowerNode", new Vector3(2f, 0f, -11f), 0f, 3, 3),
            new ShowcasePiece("Buildings/SM_PWR1_SolarArray", new Vector3(8.5f, 0f, -11f), 0f, 3, 4),

            // Landing complex (east) — ship shares pad footprint (skip duplicate reserve)
            new ShowcasePiece("Environment/SM_LandingPad", new Vector3(16f, 0f, 0f), 0f, 6, 6, PadScale),
            new ShowcasePiece("Environment/SM_Starship_Placeholder", new Vector3(16f, 0f, 0f), 0f, 0, 0, ShipScale),
        };

        public readonly struct ShowcasePiece
        {
            public readonly string ResourcesPath;
            public readonly Vector3 LocalOffset;
            public readonly float YawDegrees;
            public readonly float Scale; // 0 = use ScaleForPath
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

            public Vector3 WorldPosition => CampusOrigin + LocalOffset;

            public float ResolveScale() => Scale > 0f ? Scale : ScaleForPath(ResourcesPath);

            public bool ReservesCells => FootprintW > 0 && FootprintH > 0;
        }
    }
}
