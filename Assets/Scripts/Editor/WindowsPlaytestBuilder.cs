#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Standalone Windows player for a friend playtest (no Unity install required).
    /// Menu: Solar Majesty → Build Windows Playtest
    /// CLI: -buildTarget Win64 -executeMethod SolarMajesty.EditorTools.WindowsPlaytestBuilder.Build
    /// </summary>
    public static class WindowsPlaytestBuilder
    {
        private const string ScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";
        private const string OutputDir = "Builds/WindowsPlaytest";
        private const string ExeName = "SolarMajesty.exe";

        [MenuItem("Solar Majesty/Build Windows Playtest")]
        public static void BuildFromMenu()
        {
            bool ok = BuildInternal();
            EditorUtility.DisplayDialog(
                "Solar Majesty",
                ok
                    ? "Windows playtest built:\n" + Path.GetFullPath(OutputDir)
                    : "Windows playtest build failed. See Console / Editor.log.",
                "OK");
        }

        /// <summary>Unity -batchmode -quit -buildTarget Win64 -executeMethod SolarMajesty.EditorTools.WindowsPlaytestBuilder.Build</summary>
        public static void Build()
        {
            bool ok = BuildInternal();
            if (!ok && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static bool BuildInternal()
        {
            if (!File.Exists(ScenePath))
                DemoSceneBuilder.Build();

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.LogWarning("[Solar Majesty] SwitchActiveBuildTarget returned false; continuing with -buildTarget Win64.");
            }

            PlayerSettings.companyName = "SolarMajesty";
            // Keep ProjectSettings productName so telemetry lands in Solar Majesty/Playtest/.
            PlayerSettings.productName = "Solar Majesty";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(projectRoot, OutputDir);
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            string exePath = Path.Combine(outDir, ExeName);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.CompressWithLz4HC
            };

            Debug.Log("[Solar Majesty] Building Windows playtest → " + exePath);
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    "[Solar Majesty] Windows playtest failed: " + report.summary.result
                    + " errors=" + report.summary.totalErrors);
                return false;
            }

            PlaytestHandoff.WriteWindows(outDir);
            Debug.Log("[Solar Majesty] Windows playtest ready: " + outDir
                      + " bytes=" + report.summary.totalSize);
            return true;
        }
    }
}
#endif
