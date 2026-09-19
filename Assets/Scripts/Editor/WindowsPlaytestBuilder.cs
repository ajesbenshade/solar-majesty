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
            PlayerSettings.productName = "Solar Majesty Demo";
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

            File.WriteAllText(Path.Combine(outDir, "PLAYTEST.txt"), PlaytestReadme);
            Debug.Log("[Solar Majesty] Windows playtest ready: " + outDir
                      + " bytes=" + report.summary.totalSize);
            return true;
        }

        private const string PlaytestReadme =
            "Solar Majesty — Windows playtest\n" +
            "================================\n" +
            "Early overseer-loop demo (not a finished game). No Unity install needed.\n" +
            "\n" +
            "How to run\n" +
            "----------\n" +
            "1. Unzip the whole folder. Do not run the .exe from inside the zip.\n" +
            "2. Double-click SolarMajesty.exe. Keep it next to SolarMajesty_Data.\n" +
            "3. Windows SmartScreen may warn (unsigned build). More info → Run anyway.\n" +
            "4. Alt+Enter toggles fullscreen. Esc pauses (Resume / Settings / Title / Quit).\n" +
            "\n" +
            "What to try (Earth tutorial)\n" +
            "----------------------------\n" +
            "Title → click Earth (or another world). Empty drop, no starter robots.\n" +
            "B, key 1: Colony Commons on the orange claim disc.\n" +
            "Airlock Junction on a Commons face, then HAB + a workshop on airlock ends.\n" +
            "Workshop finishes → a robot fabricates. G to post a flag. T for research.\n" +
            "You never click-to-move units. Robots take (or ignore) bounties on their own.\n" +
            "\n" +
            "Controls\n" +
            "--------\n" +
            "Esc          Pause\n" +
            "WASD         Pan camera     Q / E zoom out / in (mouse does not pan or zoom)\n" +
            "B            Build catalog  G flag catalog     Tab cycle     T research\n" +
            "1-9 / 0      Pick a building while Build is open\n" +
            "F1 Explore   F2 Clear Threat   F3 Build   F4 Extract   F5 Defend\n" +
            "LMB          Place / inspect     RMB on a flag: cancel + refund MET\n" +
            "+ / -        Raise / lower bounty\n" +
            "P            Form a party (max 4)     [ disband\n" +
            "\n" +
            "Notes for testers\n" +
            "-----------------\n" +
            "- Continue restores campus + stockpile + research + open flags + fauna + specialist HP on that world.\n" +
            "- Engineer ignores a cheap Build flag; raise $ with + until they take it.\n" +
            "- Shift+F10 (debug) hops bodies. Shift+F10 with Shift held unlocks the campaign.\n" +
            "- Greybox / blockout art. Please note crashes, unreadable UI, and \"I didn't know what to do\".\n";
    }
}
#endif
