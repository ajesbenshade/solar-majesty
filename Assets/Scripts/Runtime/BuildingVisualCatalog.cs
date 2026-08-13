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
                { BuildingCategory.Farm, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.Mine, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.RegolithCamp, "Buildings/SM_PWR1_PowerNode" },
                { BuildingCategory.Inn, "Buildings/SM_CMD1_CommandBuilding" },
                { BuildingCategory.ScoutWorkshop, "Buildings/SM_LAB1_LaboratoryModule" },
                { BuildingCategory.EngineerWorkshop, "Buildings/SM_OPS1_OperationsUnit" },
                { BuildingCategory.DefenseWorkshop, "Buildings/SM_CMD1_CommandBuilding" },
            };

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static GameObject LoadPrefab(BuildingCategory category)
        {
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
                Cache[resourcesPath] = go;
            return go;
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
