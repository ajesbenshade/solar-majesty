#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Writes demo ScriptableObjects + unit placeholder prefabs from the same numbers as GameLoop factories.
    /// Menu: Solar Majesty → Build Demo Content Assets
    /// </summary>
    public static class DemoContentBuilder
    {
        private const string DataRoot = "Assets/Data";
        private const string ResourcesDemo = "Assets/Resources/DemoContent";
        private const string UnitsRoot = "Assets/Resources/Units";

        [MenuItem("Solar Majesty/Build Demo Content Assets")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Solar Majesty",
                "Demo content written under Assets/Data and Assets/Resources/DemoContent (+ Units).",
                "OK");
        }

        /// <summary>Unity -batchmode -executeMethod SolarMajesty.EditorTools.DemoContentBuilder.Build</summary>
        public static void Build()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Specialists");
            EnsureFolder("Assets/Data/Flags");
            EnsureFolder("Assets/Data/Buildings");
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/DemoContent");
            EnsureFolder("Assets/Resources/DemoContent/Specialists");
            EnsureFolder("Assets/Resources/DemoContent/Flags");
            EnsureFolder("Assets/Resources/DemoContent/Buildings");
            EnsureFolder("Assets/Resources/Units");

            var scoutPrefab = SaveUnitPrefab("Unit_ScoutDrone", UnitPlaceholderFactory.BuildScout);
            var engPrefab = SaveUnitPrefab("Unit_EngineerBot", UnitPlaceholderFactory.BuildEngineer);
            var defPrefab = SaveUnitPrefab("Unit_DefenseMech", UnitPlaceholderFactory.BuildDefense);
            var medicPrefab = SaveUnitPrefab("Unit_Medic", UnitPlaceholderFactory.BuildMedic);
            SaveUnitPrefab("Unit_DustStalker", UnitPlaceholderFactory.BuildDustStalker);

            var scout = WriteSpecialist("Specialist_ScoutDrone", GameLoop.CreateScout(), scoutPrefab);
            var eng = WriteSpecialist("Specialist_EngineerBot", GameLoop.CreateEngineer(), engPrefab);
            var def = WriteSpecialist("Specialist_DefenseMech", GameLoop.CreateDefense(), defPrefab);
            WriteSpecialist("Specialist_Medic", GameLoop.CreateMedic(), medicPrefab);

            WriteFlag("Flag_Explore", FlagType.Explore, "Explore", 40, 0.08f, 4f, new Color(0.3f, 0.85f, 1f));
            WriteFlag("Flag_ClearThreat", FlagType.ClearThreat, "Clear Threat", 80, 0.4f, 6f, new Color(1f, 0.3f, 0.25f));
            WriteFlag("Flag_Build", FlagType.Build, "Build Here", 70, 0.1f, 8f, new Color(1f, 0.65f, 0.15f));
            WriteFlag("Flag_Extract", FlagType.Extract, "Extract", 55, 0.12f, 7f, new Color(0.55f, 0.9f, 0.35f));
            WriteFlag("Flag_DefendArea", FlagType.DefendArea, "Defend Area", 65, 0.25f, 9f, new Color(0.85f, 0.35f, 1f));

            WriteBuilding("Building_LandingPad", "Landing Pad", BuildingCategory.LandingPad, 40, 5, 10f, 6, 6);
            WriteBuilding("Building_HAB1", "Hab Module (HAB-1)", BuildingCategory.Habitat, 50, 8, 12f, 4, 3);
            WriteBuilding("Building_PWR1", "Power Node (PWR-1)", BuildingCategory.Power, 35, 0, 8f, 3, 3);
            WriteBuilding("Building_OPS1", "Ops Unit (OPS-1)", BuildingCategory.Mining, 45, 6, 14f, 3, 3);
            WriteBuilding("Building_LAB1", "Lab Module (LAB-1)", BuildingCategory.Laboratory, 55, 10, 14f, 3, 2);
            WriteBuilding("Building_CMD1", "Command (CMD-1)", BuildingCategory.Defense, 60, 8, 16f, 4, 4);
            WriteBuilding(
                "Building_SolarArray",
                "Solar Array",
                BuildingCategory.Power,
                30,
                0,
                9f,
                3,
                4,
                BuildingVisualCatalog.LoadSolarArray());

            // Keep authored Data copies in sync with Resources (Resources are what Play loads).
            MirrorAsset(scout, $"{DataRoot}/Specialists/Specialist_ScoutDrone.asset");
            MirrorAsset(eng, $"{DataRoot}/Specialists/Specialist_EngineerBot.asset");
            MirrorAsset(def, $"{DataRoot}/Specialists/Specialist_DefenseMech.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Solar Majesty] Demo content assets ready.");
        }

        private static GameObject SaveUnitPrefab(string name, System.Func<GameObject> builder)
        {
            string path = $"{UnitsRoot}/{name}.prefab";
            var temp = builder();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static SpecialistData WriteSpecialist(string fileName, SpecialistData src, GameObject prefab)
        {
            string resPath = $"{ResourcesDemo}/Specialists/{fileName}.asset";
            var asset = LoadOrCreate<SpecialistData>(resPath);
            EditorUtility.CopySerialized(src, asset);
            asset.prefab = prefab;
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(src);
            return asset;
        }

        private static void WriteFlag(string fileName, FlagType type, string display, int bounty, float risk, float work, Color color)
        {
            string resPath = $"{ResourcesDemo}/Flags/{fileName}.asset";
            var asset = LoadOrCreate<FlagData>(resPath);
            asset.flagType = type;
            asset.displayName = display;
            asset.defaultBounty = bounty;
            asset.minBounty = 5;
            asset.maxBounty = 500;
            asset.baseRisk = risk;
            asset.workRequired = work;
            asset.bannerColor = color;
            EditorUtility.SetDirty(asset);

            MirrorAsset(asset, $"{DataRoot}/Flags/{fileName}.asset");
        }

        private static void WriteBuilding(
            string fileName,
            string display,
            BuildingCategory cat,
            int metals,
            int power,
            float time,
            int fw,
            int fh,
            GameObject prefabOverride = null)
        {
            string resPath = $"{ResourcesDemo}/Buildings/{fileName}.asset";
            var asset = LoadOrCreate<BuildingData>(resPath);
            asset.displayName = display;
            asset.category = cat;
            asset.footprintWidth = fw;
            asset.footprintHeight = fh;
            asset.buildTimeSeconds = time;
            asset.housingSlots = cat == BuildingCategory.Habitat ? 3 : 0;
            asset.powerDraw = power > 0 ? 2 : 0;
            asset.buildCost = power > 0
                ? new[]
                {
                    new ResourceAmount(ResourceId.Metals, metals),
                    new ResourceAmount(ResourceId.Power, power)
                }
                : new[] { new ResourceAmount(ResourceId.Metals, metals) };
            asset.prefab = prefabOverride != null ? prefabOverride : BuildingVisualCatalog.LoadPrefab(cat);
            EditorUtility.SetDirty(asset);

            MirrorAsset(asset, $"{DataRoot}/Buildings/{fileName}.asset");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void MirrorAsset(Object source, string destPath)
        {
            if (source == null) return;
            var existing = AssetDatabase.LoadAssetAtPath<Object>(destPath);
            if (existing == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), destPath);
            }
            else if (source is ScriptableObject soSrc && existing is ScriptableObject soDst)
            {
                EditorUtility.CopySerialized(soSrc, soDst);
                EditorUtility.SetDirty(soDst);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
