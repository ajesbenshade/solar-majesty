using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Thin runtime lookup for colony blockout meshes under Resources/Buildings and Resources/Environment.
    /// GameLoop / BuildingPlacementInput use this so placed buildings look like the Blender kit.
    /// </summary>
    public static class BuildingVisualCatalog
    {
        private static readonly Dictionary<BuildingCategory, string> CategoryResource =
            new Dictionary<BuildingCategory, string>
            {
                { BuildingCategory.LandingPad, "Environment/SM_LandingPad" },
                { BuildingCategory.Habitat, "Buildings/SM_HAB1_HabitatModule" },
                { BuildingCategory.Power, "Buildings/SM_PWR1_PowerNode" },
                { BuildingCategory.Mining, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.Defense, "Buildings/SM_CMD1_CommandBuilding" },
                { BuildingCategory.Utility, "Buildings/SM_ModularTubeConnector" },
                { BuildingCategory.Laboratory, "Buildings/SM_LAB1_LaboratoryModule" },
                // Unique modular kits (Farm / Mine / Camp / Workshops) are composed at runtime.
                { BuildingCategory.Farm, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.Mine, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.RegolithCamp, "Buildings/SM_PWR1_PowerNode" },
                { BuildingCategory.Inn, "Buildings/SM_CMD1_CommandBuilding" },
                { BuildingCategory.ScoutWorkshop, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.EngineerWorkshop, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.DefenseWorkshop, "Buildings/SM_CMD1_CommandBuilding" },
                { BuildingCategory.MedicWorkshop, "Buildings/SM_HAB1_HabitatModule" },
                { BuildingCategory.HarvesterWorkshop, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.SurveyorWorkshop, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.TerraformerWorkshop, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.CourierWorkshop, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.GeologistWorkshop, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.SentinelWorkshop, "Buildings/SM_CMD1_CommandBuilding" },
                { BuildingCategory.GuildHall, "Buildings/SM_CMD1_CommandBuilding" },
                { BuildingCategory.ClimateLoom, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.AegisSpire, "Buildings/SM_CommandDome_CentralHub" },
                { BuildingCategory.DeepArchive, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.Commons, "Buildings/SM_CommandDome_CentralHub" },
            };

        /// <summary>
        /// Phase 4 hero FBX (sheet-matched HAB cylinder / Commons dome / solar field /
        /// extractors / bunker / pad / workshop hangar / tall hangar / Inn / CMD-1 guild /
        /// OPS-1 annex / wonders). Play Mode prefers these;
        /// <see cref="HeroBuildingKits"/> is the procedural fallback.
        /// </summary>
        private static readonly Dictionary<BuildingCategory, string> HeroResource =
            new Dictionary<BuildingCategory, string>
            {
                { BuildingCategory.Habitat, "Buildings/SM_Hero_HAB" },
                { BuildingCategory.Commons, "Buildings/SM_Hero_Commons" },
                { BuildingCategory.Power, "Buildings/SM_Hero_Power" },
                { BuildingCategory.Farm, "Buildings/SM_Hero_Farm" },
                { BuildingCategory.RegolithCamp, "Buildings/SM_Hero_Camp" },
                { BuildingCategory.Mine, "Buildings/SM_Hero_Mine" },
                { BuildingCategory.Defense, "Buildings/SM_Hero_Defense" },
                { BuildingCategory.LandingPad, "Buildings/SM_Hero_LandingPad" },
                { BuildingCategory.GuildHall, "Buildings/SM_Hero_GuildHall" },
                { BuildingCategory.Mining, "Buildings/SM_Hero_OPS" },
                { BuildingCategory.Laboratory, "Buildings/SM_Hero_LAB" },
                { BuildingCategory.ClimateLoom, "Buildings/SM_Hero_ClimateLoom" },
                { BuildingCategory.AegisSpire, "Buildings/SM_Hero_AegisSpire" },
                { BuildingCategory.DeepArchive, "Buildings/SM_Hero_DeepArchive" },
                { BuildingCategory.Inn, "Buildings/SM_Hero_Inn" },
                { BuildingCategory.ScoutWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.EngineerWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.MedicWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.HarvesterWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.SurveyorWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.TerraformerWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.CourierWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.GeologistWorkshop, "Buildings/SM_Hero_Workshop" },
                { BuildingCategory.DefenseWorkshop, "Buildings/SM_Hero_WorkshopTall" },
                { BuildingCategory.SentinelWorkshop, "Buildings/SM_Hero_WorkshopTall" },
            };

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();

        public static GameObject LoadPrefab(BuildingCategory category)
        {
            GameObject hero = LoadHeroKit(category);
            if (hero != null)
                return hero;
            if (!CategoryResource.TryGetValue(category, out string path))
                return null;
            return LoadByPath(path);
        }

        public static GameObject LoadByPath(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
                return null;
            if (Cache.TryGetValue(resourcesPath, out GameObject cached) && cached != null)
                return cached;

            GameObject go = Resources.Load<GameObject>(resourcesPath);
            if (go != null)
            {
                Cache[resourcesPath] = go;
                return go;
            }

            if (MissingLogged.Add(resourcesPath) &&
                resourcesPath.IndexOf("SM_Hero_", System.StringComparison.Ordinal) >= 0)
            {
                Debug.LogWarning(
                    "[BuildingVisualCatalog] Hero kit missing at Resources/" + resourcesPath +
                    " — using procedural HeroBuildingKits. Reimport Assets/Resources/Buildings if the FBX is on disk.");
            }
            return null;
        }

        public static GameObject LoadHeroKit(BuildingCategory category)
        {
            if (!HeroResource.TryGetValue(category, out string path))
                return null;
            return LoadByPath(path);
        }

        /// <summary>Optional showcase pieces (dome, starship) for greybox colony look.</summary>
        public static GameObject LoadCommandDome() => LoadByPath("Buildings/SM_CommandDome_CentralHub");
        public static GameObject LoadStarship() => LoadByPath("Environment/SM_Starship_Placeholder");
        public static GameObject LoadSolarArray() => LoadByPath("Buildings/SM_PWR1_SolarArray");
        public static GameObject LoadConnector() => LoadByPath("Buildings/SM_ModularTubeConnector");
        public static GameObject LoadLaboratory() => LoadByPath("Buildings/SM_LAB1_LaboratoryModule");

        public static GameObject LoadCrater(int sizeClass)
        {
            switch (Mathf.Clamp(sizeClass, 0, 2))
            {
                case 0: return LoadByPath("Environment/SM_Crater_Small");
                case 1: return LoadByPath("Environment/SM_Crater_Medium");
                default: return LoadByPath("Environment/SM_Crater_Large");
            }
        }
    }
}
