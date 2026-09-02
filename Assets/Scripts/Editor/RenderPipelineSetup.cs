using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Configures the URP asset and renderer for the M2 look target. Idempotent: safe to re-run.
    ///
    /// The three defects visible in the Mars campus stills are all fixed here rather than in art:
    /// no ambient occlusion (everything floats), no tonemapping (highlights clip and hue-shift),
    /// and a depth texture that SSAO and decals both need but which was switched off.
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string RendererPath = "Assets/Settings/URP-SolarMajesty-Renderer.asset";
        private const string PipelinePath = "Assets/Settings/URP-SolarMajesty.asset";

        private const string PostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Solar Majesty/Render/Configure URP For Look Target", priority = 100)]
        public static void Configure()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            if (rendererData == null || pipeline == null)
            {
                Debug.LogError($"[RenderSetup] Missing {RendererPath} or {PipelinePath}.");
                return;
            }

            ConfigurePipeline(pipeline);
            EnsurePostProcessData(rendererData);
            EnsureFeature<ScreenSpaceAmbientOcclusion>(rendererData, "SSAO", ConfigureSsao);
            EnsureFeature<DecalRendererFeature>(rendererData, "Decals", null);
            EnsureAlwaysIncludedShaders();

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RenderSetup] URP configured: depth+opaque textures, MSAA 4x, SSAO, decals, soft shadows.");
        }

        /// <summary>
        /// Shaders reached only through Shader.Find are stripped from a player build unless they are
        /// listed here. Without this the hull materials silently fall back to magenta outside the editor.
        /// </summary>
        private static void EnsureAlwaysIncludedShaders()
        {
            string[] required = { "SolarMajesty/Hull", "SolarMajesty/PlanetGround" };

            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics == null || graphics.Length == 0)
            {
                Debug.LogWarning("[RenderSetup] Could not open GraphicsSettings.");
                return;
            }

            var so = new SerializedObject(graphics[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            for (int r = 0; r < required.Length; r++)
            {
                var shader = Shader.Find(required[r]);
                if (shader == null) continue;

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log($"[RenderSetup] Always-included shader: {required[r]}");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePipeline(UniversalRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);

            // SSAO and decals both sample depth; the ground dust shader wants the opaque texture.
            SetBool(so, "m_RequireDepthTexture", true);
            SetBool(so, "m_RequireOpaqueTexture", true);

            // Hard edges on white hulls read as aliasing at isometric distance without MSAA.
            SetInt(so, "m_MSAA", 4);
            SetBool(so, "m_SupportsHDR", true);

            // Four cascades over a shorter distance puts the resolution where the campus actually is.
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetFloat(so, "m_ShadowDistance", 120f);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 1024);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePostProcessData(UniversalRendererData rendererData)
        {
            var so = new SerializedObject(rendererData);
            var prop = so.FindProperty("postProcessData");
            if (prop == null || prop.objectReferenceValue != null) return;

            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (data == null)
            {
                Debug.LogWarning("[RenderSetup] Could not find URP PostProcessData; post effects may not render.");
                return;
            }

            prop.objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[RenderSetup] Assigned PostProcessData (was unset, which disables bloom/tonemapping).");
        }

        private static void EnsureFeature<T>(
            UniversalRendererData rendererData,
            string featureName,
            System.Action<T> configure)
            where T : ScriptableRendererFeature
        {
            List<ScriptableRendererFeature> existing = rendererData.rendererFeatures;
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] is T already)
                {
                    configure?.Invoke(already);
                    EditorUtility.SetDirty(already);
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = featureName;
            configure?.Invoke(feature);

            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.SaveAssets();

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                Debug.LogError($"[RenderSetup] Could not resolve a file id for {featureName}.");
                return;
            }

            var so = new SerializedObject(rendererData);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[RenderSetup] Added renderer feature: {featureName}");
        }

        /// <summary>
        /// Tuned for an isometric camera: a short radius so contact darkening reads under modules
        /// and rocks without smearing a grey halo across the open ground.
        /// </summary>
        private static void ConfigureSsao(ScreenSpaceAmbientOcclusion ssao)
        {
            var so = new SerializedObject(ssao);
            var settings = so.FindProperty("m_Settings");
            if (settings == null)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            SetIfPresent(settings, "AOMethod", 1);              // Blue Noise
            SetIfPresent(settings, "Intensity", 1.1f);
            SetIfPresent(settings, "Radius", 0.35f);
            SetIfPresent(settings, "Falloff", 80f);
            SetIfPresent(settings, "Samples", 1);               // Medium
            SetIfPresent(settings, "Downsample", false);
            SetIfPresent(settings, "AfterOpaque", false);
            SetIfPresent(settings, "BlurQuality", 0);
            SetIfPresent(settings, "NormalSamples", 2);         // Reconstruct from depth, 3 taps

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIfPresent(SerializedProperty parent, string name, object value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return;

            switch (value)
            {
                case bool b when p.propertyType == SerializedPropertyType.Boolean:
                    p.boolValue = b;
                    break;
                case int i when p.propertyType == SerializedPropertyType.Enum ||
                                p.propertyType == SerializedPropertyType.Integer:
                    p.intValue = i;
                    break;
                case float f when p.propertyType == SerializedPropertyType.Float:
                    p.floatValue = f;
                    break;
            }
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }
    }
}
