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
        public const string MedicPath = "DemoContent/Specialists/Specialist_Medic";

        public const string ExploreFlagPath = "DemoContent/Flags/Flag_Explore";
        public const string ClearThreatFlagPath = "DemoContent/Flags/Flag_ClearThreat";
        public const string BuildFlagPath = "DemoContent/Flags/Flag_Build";
        public const string ExtractFlagPath = "DemoContent/Flags/Flag_Extract";
        public const string DefendFlagPath = "DemoContent/Flags/Flag_DefendArea";

        public const string LandingPadPath = "DemoContent/Buildings/Building_LandingPad";
        public const string HabPath = "DemoContent/Buildings/Building_HAB1";
        public const string PowerPath = "DemoContent/Buildings/Building_PWR1";
        public const string OpsPath = "DemoContent/Buildings/Building_OPS1";
        public const string LabPath = "DemoContent/Buildings/Building_LAB1";
        public const string CmdPath = "DemoContent/Buildings/Building_CMD1";
        public const string SolarPath = "DemoContent/Buildings/Building_SolarArray";

        public const string ScoutPrefabPath = "Units/Unit_ScoutDrone";
        public const string EngineerPrefabPath = "Units/Unit_EngineerBot";
        public const string DefensePrefabPath = "Units/Unit_DefenseMech";
        public const string StalkerPrefabPath = "Units/Unit_DustStalker";

        public static SpecialistData LoadScout() => Resources.Load<SpecialistData>(ScoutPath);
        public static SpecialistData LoadEngineer() => Resources.Load<SpecialistData>(EngineerPath);
        public static SpecialistData LoadDefense() => Resources.Load<SpecialistData>(DefensePath);
        public static SpecialistData LoadMedic() => Resources.Load<SpecialistData>(MedicPath);

        public static FlagData LoadExploreFlag() => Resources.Load<FlagData>(ExploreFlagPath);
        public static FlagData LoadClearThreatFlag() => Resources.Load<FlagData>(ClearThreatFlagPath);
        public static FlagData LoadBuildFlag() => Resources.Load<FlagData>(BuildFlagPath);
        public static FlagData LoadExtractFlag() => Resources.Load<FlagData>(ExtractFlagPath);
        public static FlagData LoadDefendFlag() => Resources.Load<FlagData>(DefendFlagPath);

        public static BuildingData[] LoadStarterBuildings()
        {
            var pad = Resources.Load<BuildingData>(LandingPadPath);
            var hab = Resources.Load<BuildingData>(HabPath);
            var pwr = Resources.Load<BuildingData>(PowerPath);
            var ops = Resources.Load<BuildingData>(OpsPath);
            var lab = Resources.Load<BuildingData>(LabPath);
            var cmd = Resources.Load<BuildingData>(CmdPath);
            var solar = Resources.Load<BuildingData>(SolarPath);
            if (pad == null || hab == null || pwr == null || ops == null)
                return null;

            if (lab != null && cmd != null && solar != null)
                return new[] { pad, hab, pwr, ops, lab, cmd, solar };
            if (lab != null && cmd != null)
                return new[] { pad, hab, pwr, ops, lab, cmd };
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
