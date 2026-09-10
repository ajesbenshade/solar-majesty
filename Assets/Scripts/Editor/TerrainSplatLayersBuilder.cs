#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Wires BOXOPHOBIC Terrain Data Baker demo albedo tiles into
    /// Resources/Dressing/TerrainSplatLayers so Play Mode can splat them.
    /// </summary>
    public static class TerrainSplatLayersBuilder
    {
        public const string AssetPath = "Assets/Resources/Dressing/TerrainSplatLayers.asset";

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

        [MenuItem("Solar Majesty/Build Terrain Splat Layers")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Solar Majesty",
                "Terrain splat layers written to Assets/Resources/Dressing/TerrainSplatLayers.asset",
                "OK");
        }

        public static void BuildIfStale()
        {
            var kit = AssetDatabase.LoadAssetAtPath<TerrainSplatLayers>(AssetPath);
            if (kit != null && kit.HasSand && kit.HasGrass) return;
            Build();
        }

        public static void Build()
        {
            var kit = AssetDatabase.LoadAssetAtPath<TerrainSplatLayers>(AssetPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<TerrainSplatLayers>();
                AssetDatabase.CreateAsset(kit, AssetPath);
            }

            kit.sand = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainSplatLayers.SandAssetPath);
            kit.grass = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainSplatLayers.GrassAssetPath);
            kit.snow = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainSplatLayers.SnowAssetPath);

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[TerrainSplat] sand=" + (kit.sand != null) +
                " grass=" + (kit.grass != null) +
                " snow=" + (kit.snow != null));
        }
    }
}
#endif
