#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Builds LunarOutpost_Sandbox with GameLoop + framed Main Camera. Menu or -executeMethod.
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

        [MenuItem("Solar Majesty/Open Demo Scene")]
        public static void OpenDemoScene()
        {
            if (!System.IO.File.Exists(ScenePath))
                Build();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Solar Majesty] Opened " + scene.path + " — press Play.");
        }

        /// <summary>Unity -batchmode -executeMethod SolarMajesty.EditorTools.DemoSceneBuilder.Build</summary>
        public static void Build()
        {
            EnsureFolders();
            ConfigurePlayerSettings();
            EnsureUrpPipeline();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Keep a Main Camera in the scene so opening the file isn't an empty Game view.
            // GameLoop.ConfigureCamera repositions it on Play.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ColonyLayout.CameraOrthoSize;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<IsometricCameraController>();
            Vector3 focus = ColonyLayout.CameraFocus;
            camGo.transform.position = focus + new Vector3(-18f, 22f, -18f);
            camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

            var loopGo = new GameObject("GameLoop");
            var loop = loopGo.AddComponent<GameLoop>();
            // Assign camera via SerializedObject so the scene reference sticks.
            var so = new SerializedObject(loop);
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

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
