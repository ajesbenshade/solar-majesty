using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Gives each quality level its own URP asset. Before this every level except Ultra fell back to
    /// the same pipeline (4x MSAA, 4096 shadows in 4 cascades to 120 m, soft shadows, SSAO), so
    /// "Low" saved next to nothing on an integrated GPU.
    ///
    /// Ultra keeps <c>URP-SolarMajesty</c> untouched (the look target). The other tiers are copies
    /// with the expensive parts stepped down; Medium and Low use a renderer without SSAO. Idempotent:
    /// re-run after retuning Ultra and the tiers are regenerated from it.
    /// </summary>
    public static class QualityTiersSetup
    {
        private const string UltraPath = "Assets/Settings/URP-SolarMajesty.asset";
        private const string RendererPath = "Assets/Settings/URP-SolarMajesty-Renderer.asset";
        private const string LiteRendererPath = "Assets/Settings/URP-SolarMajesty-Renderer-Lite.asset";
        private const string HighPath = "Assets/Settings/URP-SolarMajesty-High.asset";
        private const string MediumPath = "Assets/Settings/URP-SolarMajesty-Medium.asset";
        private const string LowPath = "Assets/Settings/URP-SolarMajesty-Low.asset";

        // Matches the level order in ProjectSettings/QualitySettings.asset.
        private static readonly string[] LevelAssets = { LowPath, LowPath, MediumPath, HighPath, HighPath, UltraPath };

        [MenuItem("Solar Majesty/Render/Create Quality Tiers", priority = 101)]
        public static void Create()
        {
            var ultra = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UltraPath);
            if (ultra == null)
            {
                Debug.LogError("[QualityTiers] Missing " + UltraPath + ". Run Configure URP For Look Target first.");
                return;
            }

            var lite = CopyFrom(RendererPath, LiteRendererPath) as UniversalRendererData;
            if (lite != null)
            {
                foreach (var feature in lite.rendererFeatures)
                {
                    if (feature is ScreenSpaceAmbientOcclusion) feature.SetActive(false);
                }
                EditorUtility.SetDirty(lite);
            }

            Tier(HighPath, msaa: 2, renderScale: 1f, upscaling: 0, shadowRes: 2048, cascades: 2, distance: 100f,
                soft: true, softQuality: 2, extraLightShadows: true, renderer: null);
            Tier(MediumPath, msaa: 1, renderScale: 1f, upscaling: 0, shadowRes: 2048, cascades: 2, distance: 80f,
                soft: true, softQuality: 1, extraLightShadows: false, renderer: lite);
            // FSR keeps edges readable when rendering below native resolution.
            Tier(LowPath, msaa: 1, renderScale: 0.8f, upscaling: 3, shadowRes: 1024, cascades: 1, distance: 60f,
                soft: false, softQuality: 1, extraLightShadows: false, renderer: lite);

            AssignLevels();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QualityTiers] Low/Medium/High URP assets created and assigned; Ultra unchanged.");
        }

        /// <summary>CI / batch entry point.</summary>
        public static void CreateAndExit()
        {
            Create();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static Object CopyFrom(string source, string target)
        {
            if (AssetDatabase.LoadMainAssetAtPath(target) != null)
                AssetDatabase.DeleteAsset(target);
            if (!AssetDatabase.CopyAsset(source, target))
            {
                Debug.LogError($"[QualityTiers] Could not copy {source} to {target}.");
                return null;
            }
            return AssetDatabase.LoadMainAssetAtPath(target);
        }

        private static void Tier(string path, int msaa, float renderScale, int upscaling, int shadowRes, int cascades,
            float distance, bool soft, int softQuality, bool extraLightShadows, UniversalRendererData renderer)
        {
            var asset = CopyFrom(UltraPath, path) as UniversalRenderPipelineAsset;
            if (asset == null) return;

            var so = new SerializedObject(asset);
            SetInt(so, "m_MSAA", msaa);
            SetFloat(so, "m_RenderScale", renderScale);
            SetInt(so, "m_UpscalingFilter", upscaling);
            SetInt(so, "m_MainLightShadowmapResolution", shadowRes);
            SetInt(so, "m_ShadowCascadeCount", cascades);
            SetFloat(so, "m_ShadowDistance", distance);
            SetBool(so, "m_SoftShadowsSupported", soft);
            SetInt(so, "m_SoftShadowQuality", softQuality);
            SetBool(so, "m_AdditionalLightShadowsSupported", extraLightShadows);
            if (renderer != null)
            {
                var list = so.FindProperty("m_RendererDataList");
                if (list != null && list.arraySize > 0)
                    list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void AssignLevels()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (settings == null || settings.Length == 0) return;
            var so = new SerializedObject(settings[0]);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null) return;
            for (int i = 0; i < levels.arraySize && i < LevelAssets.Length; i++)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LevelAssets[i]);
                var prop = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                if (prop != null && pipeline != null) prop.objectReferenceValue = pipeline;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
            else Debug.LogWarning("[QualityTiers] No property " + name);
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning("[QualityTiers] No property " + name);
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning("[QualityTiers] No property " + name);
        }
    }
}
