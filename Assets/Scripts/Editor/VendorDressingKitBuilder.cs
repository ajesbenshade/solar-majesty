#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Wires Asset Store packs into Resources/Dressing/VendorDressingKit so runtime
    /// can instantiate Kevin Iglesias walkers and Polytope Earth nature.
    /// </summary>
    public static class VendorDressingKitBuilder
    {
        public const string AssetPath = "Assets/Resources/Dressing/VendorDressingKit.asset";

        private const string DummyM =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/Prefabs/Human_BasicMotionsDummy_M.prefab";
        private const string DummyF =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/Prefabs/Human_BasicMotionsDummy_F.prefab";
        private const string Controller =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/AnimatorControllers/HumanBasicMotionsScene.controller";
        private const string IdleM =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
        private const string WalkM =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string IdleF =
            "Assets/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx";
        private const string WalkF =
            "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk/HumanF@Walk01_Forward.fbx";
        private const string ModelM =
            "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string ModelF =
            "Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx";

        private static readonly string[] EarthTrees =
        {
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_apples.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_pears.prefab"
        };

        private static readonly string[] EarthRocks =
        {
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Generic_Rock_01.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_River_Rock_Pile_02.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Ore_Rock_01.prefab"
        };

        private static readonly string[] EarthShrubs =
        {
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Shrubs/PT_Generic_Shrub_01_green.prefab"
        };

        private static readonly string[] EarthGrass =
        {
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Plants/PT_Grass_02.prefab"
        };

        private static readonly string[] EarthFlowers =
        {
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Flowers/PT_Poppy_02.prefab"
        };

        [InitializeOnLoadMethod]
        private static void AutoBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                BuildIfStale();
            };
        }

        [MenuItem("Solar Majesty/Build Vendor Dressing Kit")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Solar Majesty",
                "Vendor dressing kit written to Assets/Resources/Dressing/VendorDressingKit.asset",
                "OK");
        }

        public static void BuildIfStale()
        {
            var kit = AssetDatabase.LoadAssetAtPath<VendorDressingKit>(AssetPath);
            if (kit != null && kit.HasWalker && kit.HasEarthNature) return;
            Build();
        }

        public static void Build()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Dressing");

            var kit = AssetDatabase.LoadAssetAtPath<VendorDressingKit>(AssetPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<VendorDressingKit>();
                AssetDatabase.CreateAsset(kit, AssetPath);
            }

            kit.dummyMale = Load<GameObject>(DummyM);
            kit.dummyFemale = Load<GameObject>(DummyF);
            kit.motionsController = Load<RuntimeAnimatorController>(Controller);
            kit.idleMale = LoadClip(IdleM);
            kit.walkMale = LoadClip(WalkM);
            kit.idleFemale = LoadClip(IdleF);
            kit.walkFemale = LoadClip(WalkF);
            kit.maleAvatar = LoadAvatar(ModelM);
            kit.femaleAvatar = LoadAvatar(ModelF);
            kit.earthTrees = LoadAll(EarthTrees);
            kit.earthRocks = LoadAll(EarthRocks);
            kit.earthShrubs = LoadAll(EarthShrubs);
            kit.earthGrass = LoadAll(EarthGrass);
            kit.earthFlowers = LoadAll(EarthFlowers);

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[VendorDressing] kit walker=" + kit.HasWalker +
                " earthTrees=" + (kit.earthTrees != null ? kit.earthTrees.Length : 0));
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static AnimationClip LoadClip(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null) return null;
            AnimationClip best = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    if (best == null || clip.name.IndexOf("Walk", System.StringComparison.Ordinal) >= 0 ||
                        clip.name.IndexOf("Idle", System.StringComparison.Ordinal) >= 0)
                        best = clip;
                }
            }
            return best;
        }

        private static Avatar LoadAvatar(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null) return null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar av) return av;
            }
            return null;
        }

        private static GameObject[] LoadAll(string[] paths)
        {
            var list = new System.Collections.Generic.List<GameObject>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                var go = Load<GameObject>(paths[i]);
                if (go != null) list.Add(go);
            }
            return list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
