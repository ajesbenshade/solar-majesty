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
        public const string HarvesterPath = "DemoContent/Specialists/Specialist_HarvesterBot";
        public const string SurveyorPath = "DemoContent/Specialists/Specialist_SurveyorBot";
        public const string TerraformerPath = "DemoContent/Specialists/Specialist_TerraformerBot";
        public const string CourierPath = "DemoContent/Specialists/Specialist_CourierBot";
        public const string GeologistPath = "DemoContent/Specialists/Specialist_GeologistBot";
        public const string SentinelPath = "DemoContent/Specialists/Specialist_SentinelMech";

        public const string ExploreFlagPath = "DemoContent/Flags/Flag_Explore";
        public const string ClearThreatFlagPath = "DemoContent/Flags/Flag_ClearThreat";
        public const string BuildFlagPath = "DemoContent/Flags/Flag_Build";
        public const string ExtractFlagPath = "DemoContent/Flags/Flag_Extract";
        public const string DefendFlagPath = "DemoContent/Flags/Flag_DefendArea";
        public const string ResearchSiteFlagPath = "DemoContent/Flags/Flag_ResearchSite";
        public const string OutpostFlagPath = "DemoContent/Flags/Flag_EstablishOutpost";
        public const string TerraformFlagPath = "DemoContent/Flags/Flag_Terraform";

        public const string PalacePath = "DemoContent/Buildings/Building_Palace";
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
        public const string MedicPrefabPath = "Units/Unit_Medic";
        public const string HarvesterPrefabPath = "Units/Unit_HarvesterBot";
        public const string SurveyorPrefabPath = "Units/Unit_SurveyorBot";
        public const string TerraformerPrefabPath = "Units/Unit_TerraformerBot";
        public const string CourierPrefabPath = "Units/Unit_CourierBot";
        public const string GeologistPrefabPath = "Units/Unit_GeologistBot";
        public const string SentinelPrefabPath = "Units/Unit_SentinelMech";
        public const string StalkerPrefabPath = "Units/Unit_DustStalker";

        public static SpecialistData LoadScout() => Resources.Load<SpecialistData>(ScoutPath);
        public static SpecialistData LoadEngineer() => Resources.Load<SpecialistData>(EngineerPath);
        public static SpecialistData LoadDefense() => Resources.Load<SpecialistData>(DefensePath);
        public static SpecialistData LoadMedic() => Resources.Load<SpecialistData>(MedicPath);
        public static SpecialistData LoadHarvester() => Resources.Load<SpecialistData>(HarvesterPath);
        public static SpecialistData LoadSurveyor() => Resources.Load<SpecialistData>(SurveyorPath);
        public static SpecialistData LoadTerraformer() => Resources.Load<SpecialistData>(TerraformerPath);
        public static SpecialistData LoadCourier() => Resources.Load<SpecialistData>(CourierPath);
        public static SpecialistData LoadGeologist() => Resources.Load<SpecialistData>(GeologistPath);
        public static SpecialistData LoadSentinel() => Resources.Load<SpecialistData>(SentinelPath);

        public static FlagData LoadExploreFlag() => Resources.Load<FlagData>(ExploreFlagPath);
        public static FlagData LoadClearThreatFlag() => Resources.Load<FlagData>(ClearThreatFlagPath);
        public static FlagData LoadBuildFlag() => Resources.Load<FlagData>(BuildFlagPath);
        public static FlagData LoadExtractFlag() => Resources.Load<FlagData>(ExtractFlagPath);
        public static FlagData LoadDefendFlag() => Resources.Load<FlagData>(DefendFlagPath);
        public static FlagData LoadResearchSiteFlag() => Resources.Load<FlagData>(ResearchSiteFlagPath);
        public static FlagData LoadOutpostFlag() => Resources.Load<FlagData>(OutpostFlagPath);
        public static FlagData LoadTerraformFlag() => Resources.Load<FlagData>(TerraformFlagPath);

        public static BuildingData[] LoadStarterBuildings()
        {
            var palace = Resources.Load<BuildingData>(PalacePath);
            var pad = Resources.Load<BuildingData>(LandingPadPath);
            var hab = Resources.Load<BuildingData>(HabPath);
            var pwr = Resources.Load<BuildingData>(PowerPath);
            var ops = Resources.Load<BuildingData>(OpsPath);
            var lab = Resources.Load<BuildingData>(LabPath);
            var cmd = Resources.Load<BuildingData>(CmdPath);
            var solar = Resources.Load<BuildingData>(SolarPath);
            if (pad == null || hab == null || pwr == null || ops == null)
                return null;

            // Palace is injected by GameLoop.EnsurePalaceFirst when the asset is missing.
            if (lab != null && cmd != null && solar != null)
                return palace != null
                    ? new[] { palace, hab, pwr, ops, lab, pad, cmd, solar }
                    : new[] { pad, hab, pwr, ops, lab, cmd, solar };
            if (lab != null && cmd != null)
                return palace != null
                    ? new[] { palace, hab, pwr, ops, lab, pad, cmd }
                    : new[] { pad, hab, pwr, ops, lab, cmd };
            return palace != null
                ? new[] { palace, hab, pwr, ops, pad }
                : new[] { pad, hab, pwr, ops };
        }

        public static GameObject LoadUnitPrefab(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return Resources.Load<GameObject>(ScoutPrefabPath);
                case SpecialistClass.EngineerBot: return Resources.Load<GameObject>(EngineerPrefabPath);
                case SpecialistClass.DefenseMech: return Resources.Load<GameObject>(DefensePrefabPath);
                case SpecialistClass.Medic: return Resources.Load<GameObject>(MedicPrefabPath);
                case SpecialistClass.HarvesterBot: return Resources.Load<GameObject>(HarvesterPrefabPath);
                case SpecialistClass.SurveyorBot: return Resources.Load<GameObject>(SurveyorPrefabPath);
                case SpecialistClass.TerraformerBot: return Resources.Load<GameObject>(TerraformerPrefabPath);
                case SpecialistClass.CourierBot: return Resources.Load<GameObject>(CourierPrefabPath);
                case SpecialistClass.GeologistBot: return Resources.Load<GameObject>(GeologistPrefabPath);
                case SpecialistClass.SentinelMech: return Resources.Load<GameObject>(SentinelPrefabPath);
                default: return null;
            }
        }

        public static GameObject LoadStalkerPrefab() => Resources.Load<GameObject>(StalkerPrefabPath);
    }
}
