#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Builds LunarOutpost_Sandbox with a single GameLoop object. Menu or -executeMethod.
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";
        private const string UrpAssetPath = "Assets/Settings/URP-SolarMajesty.asset";
        private const string UrpRendererPath = "Assets/Settings/URP-SolarMajesty-Renderer.asset";

        [MenuItem("Solar Majesty/Build Demo Scene")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog("Solar Majesty", "Demo scene saved:\n" + ScenePath, "OK");
        }

        /// <summary>Unity -batchmode -executeMethod SolarMajesty.EditorTools.DemoSceneBuilder.Build</summary>
        public static void Build()
        {
            EnsureFolders();
            ConfigurePlayerSettings();
            EnsureUrpPipeline();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Strip default Main Camera — GameLoop creates/configures isometric camera.
            var existingCam = Object.FindFirstObjectByType<Camera>();
            if (existingCam != null)
                Object.DestroyImmediate(existingCam.gameObject);

            // Remove default light if present; GameLoop/ground will add simple lighting.
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                Object.DestroyImmediate(light.gameObject);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var loopGo = new GameObject("GameLoop");
            loopGo.AddComponent<GameLoop>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Solar Majesty] Demo scene ready: " + ScenePath);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "SolarMajesty";
            PlayerSettings.productName = "Solar Majesty Demo";
        }

        /// <summary>Assign a basic URP pipeline so FBX/Lit materials render (not pink).</summary>
        private static void EnsureUrpPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, UrpAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Solar Majesty] Created URP assets under Assets/Settings/");
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
