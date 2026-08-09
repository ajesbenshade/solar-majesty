using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Loads authored demo ScriptableObjects from Resources/DemoContent (mirrors Assets/Data).
    /// </summary>
    public static class DemoContentCatalog
    {
        public const string ScoutPath = "DemoContent/Specialists/Specialist_ScoutDrone";
        public const string EngineerPath = "DemoContent/Specialists/Specialist_EngineerBot";
        public const string DefensePath = "DemoContent/Specialists/Specialist_DefenseMech";

        public const string ExploreFlagPath = "DemoContent/Flags/Flag_Explore";
        public const string ClearThreatFlagPath = "DemoContent/Flags/Flag_ClearThreat";
        public const string BuildFlagPath = "DemoContent/Flags/Flag_Build";

        public const string LandingPadPath = "DemoContent/Buildings/Building_LandingPad";
        public const string HabPath = "DemoContent/Buildings/Building_HAB1";
        public const string PowerPath = "DemoContent/Buildings/Building_PWR1";
        public const string OpsPath = "DemoContent/Buildings/Building_OPS1";

        public const string ScoutPrefabPath = "Units/Unit_ScoutDrone";
        public const string EngineerPrefabPath = "Units/Unit_EngineerBot";
        public const string DefensePrefabPath = "Units/Unit_DefenseMech";
        public const string StalkerPrefabPath = "Units/Unit_DustStalker";

        public static SpecialistData LoadScout() => Resources.Load<SpecialistData>(ScoutPath);
        public static SpecialistData LoadEngineer() => Resources.Load<SpecialistData>(EngineerPath);
        public static SpecialistData LoadDefense() => Resources.Load<SpecialistData>(DefensePath);

        public static FlagData LoadExploreFlag() => Resources.Load<FlagData>(ExploreFlagPath);
        public static FlagData LoadClearThreatFlag() => Resources.Load<FlagData>(ClearThreatFlagPath);
        public static FlagData LoadBuildFlag() => Resources.Load<FlagData>(BuildFlagPath);

        public static BuildingData[] LoadStarterBuildings()
        {
            var pad = Resources.Load<BuildingData>(LandingPadPath);
            var hab = Resources.Load<BuildingData>(HabPath);
            var pwr = Resources.Load<BuildingData>(PowerPath);
            var ops = Resources.Load<BuildingData>(OpsPath);
            if (pad == null || hab == null || pwr == null || ops == null)
                return null;
            return new[] { pad, hab, pwr, ops };
        }

        public static GameObject LoadUnitPrefab(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return Resources.Load<GameObject>(ScoutPrefabPath);
                case SpecialistClass.EngineerBot: return Resources.Load<GameObject>(EngineerPrefabPath);
                case SpecialistClass.DefenseMech: return Resources.Load<GameObject>(DefensePrefabPath);
                default: return null;
            }
        }

        public static GameObject LoadStalkerPrefab() => Resources.Load<GameObject>(StalkerPrefabPath);
    }
}
