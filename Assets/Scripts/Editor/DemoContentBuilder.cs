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
            var harvPrefab = SaveUnitPrefab("Unit_HarvesterBot", UnitPlaceholderFactory.BuildHarvester);
            var survPrefab = SaveUnitPrefab("Unit_SurveyorBot", UnitPlaceholderFactory.BuildSurveyor);
            var terraPrefab = SaveUnitPrefab("Unit_TerraformerBot", UnitPlaceholderFactory.BuildTerraformer);
            var courPrefab = SaveUnitPrefab("Unit_CourierBot", UnitPlaceholderFactory.BuildCourier);
            var geoPrefab = SaveUnitPrefab("Unit_GeologistBot", UnitPlaceholderFactory.BuildGeologist);
            var sentPrefab = SaveUnitPrefab("Unit_SentinelMech", UnitPlaceholderFactory.BuildSentinel);
            SaveUnitPrefab("Unit_DustStalker", UnitPlaceholderFactory.BuildDustStalker);
            SaveUnitPrefab("Unit_RegolithMite", UnitPlaceholderFactory.BuildRegolithMite);
            SaveUnitPrefab("Unit_WattLeech", UnitPlaceholderFactory.BuildWattLeech);
            SaveUnitPrefab("Unit_IceWisp", UnitPlaceholderFactory.BuildIceWisp);
            SaveUnitPrefab("Unit_RockTick", UnitPlaceholderFactory.BuildRockTick);
            SaveUnitPrefab("Unit_SoilCreeper", UnitPlaceholderFactory.BuildSoilCreeper);
            SaveUnitPrefab("Unit_AshHopper", UnitPlaceholderFactory.BuildAshHopper);

            var scout = WriteSpecialist("Specialist_ScoutDrone", GameLoop.CreateScout(), scoutPrefab);
            var eng = WriteSpecialist("Specialist_EngineerBot", GameLoop.CreateEngineer(), engPrefab);
            var def = WriteSpecialist("Specialist_DefenseMech", GameLoop.CreateDefense(), defPrefab);
            WriteSpecialist("Specialist_Medic", GameLoop.CreateMedic(), medicPrefab);
            WriteSpecialist("Specialist_HarvesterBot", GameLoop.CreateHarvester(), harvPrefab);
            WriteSpecialist("Specialist_SurveyorBot", GameLoop.CreateSurveyor(), survPrefab);
            WriteSpecialist("Specialist_TerraformerBot", GameLoop.CreateTerraformer(), terraPrefab);
            WriteSpecialist("Specialist_CourierBot", GameLoop.CreateCourier(), courPrefab);
            WriteSpecialist("Specialist_GeologistBot", GameLoop.CreateGeologist(), geoPrefab);
            WriteSpecialist("Specialist_SentinelMech", GameLoop.CreateSentinel(), sentPrefab);

            WriteFlag("Flag_Explore", FlagType.Explore, "Explore", 40, 0.08f, 4f, new Color(0.3f, 0.85f, 1f));
            WriteFlag("Flag_ClearThreat", FlagType.ClearThreat, "Clear Threat", 80, 0.4f, 6f, new Color(1f, 0.3f, 0.25f));
            WriteFlag("Flag_Build", FlagType.Build, "Build Here", 70, 0.1f, 8f, new Color(1f, 0.65f, 0.15f));
            WriteFlag("Flag_Extract", FlagType.Extract, "Extract", 55, 0.12f, 7f, new Color(0.55f, 0.9f, 0.35f));
            WriteFlag("Flag_DefendArea", FlagType.DefendArea, "Defend Area", 65, 0.25f, 9f, new Color(0.95f, 0.48f, 0.18f));
            WriteFlag("Flag_ResearchSite", FlagType.ResearchSite, "Research Site", 50, 0.1f, 6f, new Color(0.45f, 0.72f, 1f));
            WriteFlag("Flag_EstablishOutpost", FlagType.EstablishOutpost, "Establish Outpost", 75, 0.22f, 10f, new Color(0.22f, 0.82f, 0.78f));
            WriteFlag("Flag_Terraform", FlagType.Terraform, "Terraform", 70, 0.14f, 11f, new Color(0.42f, 0.88f, 0.38f));

            WriteBuilding("Building_Commons", "Colony Commons", BuildingCategory.Commons, 70, 10, 18f, 6, 6);
            WriteBuilding("Building_LandingPad", "Landing Pad", BuildingCategory.LandingPad, 40, 5, 10f, 6, 6);
            WriteBuilding("Building_HAB1", "Hab Module (HAB-1)", BuildingCategory.Habitat, 50, 8, 12f, 4, 4);
            WriteBuilding("Building_PWR1", "Power Node (PWR-1)", BuildingCategory.Power, 35, 0, 8f, 4, 4);
            WriteBuilding("Building_OPS1", "Ops Unit (OPS-1)", BuildingCategory.Mining, 45, 6, 14f, 4, 4);
            WriteBuilding("Building_LAB1", "Lab Module (LAB-1)", BuildingCategory.Laboratory, 55, 10, 14f, 4, 4);
            WriteBuilding("Building_CMD1", "Defense Battery", BuildingCategory.Defense, 60, 8, 16f, 4, 4);
            WriteBuilding("Building_GuildHall", "Guild Hall", BuildingCategory.GuildHall, 56, 6, 14f, 4, 4);
            WriteBuilding("Building_HarvesterWorkshop", "Harvester Workshop", BuildingCategory.HarvesterWorkshop, 40, 5, 12f, 4, 4);
            WriteBuilding("Building_SurveyorWorkshop", "Surveyor Workshop", BuildingCategory.SurveyorWorkshop, 38, 4, 12f, 4, 4);
            WriteBuilding("Building_TerraformerWorkshop", "Terraformer Workshop", BuildingCategory.TerraformerWorkshop, 42, 5, 12f, 4, 4);
            WriteBuilding("Building_CourierWorkshop", "Courier Workshop", BuildingCategory.CourierWorkshop, 36, 4, 12f, 4, 4);
            WriteBuilding("Building_GeologistWorkshop", "Geologist Workshop", BuildingCategory.GeologistWorkshop, 38, 4, 12f, 4, 4);
            WriteBuilding("Building_SentinelWorkshop", "Sentinel Workshop", BuildingCategory.SentinelWorkshop, 40, 5, 12f, 4, 4);
            WriteBuilding("Building_ClimateLoom", "Climate Loom", BuildingCategory.ClimateLoom, 92, 12, 18f, 6, 6);
            WriteBuilding("Building_AegisSpire", "Aegis Spire", BuildingCategory.AegisSpire, 100, 14, 18f, 6, 6);
            WriteBuilding("Building_DeepArchive", "Deep Archive", BuildingCategory.DeepArchive, 88, 10, 16f, 6, 6);
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
            asset.stronglyAttracts = SpecialistPersonality.Attracts(type);
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
            asset.powerDraw = cat == BuildingCategory.Power ? 0 : (power > 0 ? 2 : 0);
            asset.powerGen = cat != BuildingCategory.Power
                ? 0
                : (display.IndexOf("Solar", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 8 : 6);
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

        /// <summary>
        /// Batch smoke: Resources.Load SM_Hero_* + Earth grade + workshop kit.
        /// Unity -batchmode -executeMethod SolarMajesty.EditorTools.DemoContentBuilder.SmokeHeroKits
        /// </summary>
        public static void SmokeHeroKits()
        {
            var cats = new[]
            {
                BuildingCategory.Habitat, BuildingCategory.Commons, BuildingCategory.Power,
                BuildingCategory.Farm, BuildingCategory.RegolithCamp, BuildingCategory.Mine,
                BuildingCategory.Defense, BuildingCategory.LandingPad, BuildingCategory.GuildHall,
                BuildingCategory.Mining, BuildingCategory.Laboratory, BuildingCategory.ClimateLoom,
                BuildingCategory.AegisSpire, BuildingCategory.DeepArchive, BuildingCategory.Inn,
                BuildingCategory.EngineerWorkshop, BuildingCategory.DefenseWorkshop
            };
            int ok = 0;
            int miss = 0;
            for (int i = 0; i < cats.Length; i++)
            {
                var cat = cats[i];
                var prefab = BuildingVisualCatalog.LoadHeroKit(cat);
                if (prefab == null)
                {
                    miss++;
                    Debug.LogWarning("[Smoke] hero FBX miss " + cat);
                    continue;
                }

                var inst = Object.Instantiate(prefab);
                int rends = inst.GetComponentsInChildren<Renderer>(true).Length;
                Object.DestroyImmediate(inst);
                if (rends <= 0)
                {
                    miss++;
                    Debug.LogWarning("[Smoke] hero FBX empty " + cat + " " + prefab.name);
                }
                else
                {
                    ok++;
                    Debug.Log("[Smoke] hero FBX ok " + cat + " " + prefab.name + " renderers=" + rends);
                }
            }

            var earth = CelestialBodyCatalog.Earth();
            Debug.Log("[Smoke] Earth SkyTop=" + earth.SkyTop + " GroundLight=" + earth.GroundLight +
                      " fogEnd=" + earth.FogEnd);

            var shop = new GameObject("Smoke_Workshop");
            HeroBuildingKits.BuildWorkshop(shop.transform, 6f, 6f, new Color(1f, 0.65f, 0.2f), false);
            int shopR = shop.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(shop);
            var inn = new GameObject("Smoke_Inn");
            HeroBuildingKits.BuildInn(inn.transform, 6f, 6f);
            int innR = inn.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(inn);
            var hab = new GameObject("Smoke_HAB");
            HeroBuildingKits.BuildHabitat(hab.transform, 6f, 6f, Color.white);
            int habR = hab.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(hab);
            var commons = new GameObject("Smoke_Commons");
            HeroBuildingKits.BuildCommons(commons.transform, 9f, 9f, Color.white);
            int comR = commons.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(commons);
            var lab = new GameObject("Smoke_LAB");
            HeroBuildingKits.BuildLaboratory(lab.transform, 6f, 6f, Color.white);
            int labR = lab.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(lab);
            var pwr = new GameObject("Smoke_Power");
            HeroBuildingKits.BuildSolarField(pwr.transform, 6f, 6f, Color.white);
            int pwrR = pwr.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(pwr);
            var pad = new GameObject("Smoke_Pad");
            HeroBuildingKits.BuildLandingPad(pad.transform, 9f, 9f, Color.white);
            int padR = pad.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(pad);
            var guild = new GameObject("Smoke_Guild");
            HeroBuildingKits.BuildGuildHall(guild.transform, 6f, 6f, Color.white);
            int guildR = guild.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(guild);
            var ops = new GameObject("Smoke_OPS");
            HeroBuildingKits.BuildOpsUnit(ops.transform, 6f, 6f, Color.white);
            int opsR = ops.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(ops);
            var farm = new GameObject("Smoke_Farm");
            HeroBuildingKits.BuildWaterExtractor(farm.transform, 6f, 6f, Color.white);
            int farmR = farm.GetComponentsInChildren<Renderer>().Length;
            Object.DestroyImmediate(farm);
            Debug.Log("[Smoke] workshop kit renderers=" + shopR + " inn kit renderers=" + innR +
                      " hab=" + habR + " commons=" + comR + " lab=" + labR +
                      " power=" + pwrR + " pad=" + padR +
                      " guild=" + guildR + " ops=" + opsR + " farm=" + farmR);
            Debug.Log("[Smoke] hero FBX ok=" + ok + " miss=" + miss +
                      (miss == 0 ? " SMOKE_OK" : " SMOKE_PARTIAL"));
        }
    }
}
#endif
