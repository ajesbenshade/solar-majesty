using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Development player for HUD reviews. The build runs under its own product name so its
    /// PlayerPrefs and saves stay apart from the real game's. Run it with
    /// <c>-smHudShot &lt;dir&gt;</c> and <see cref="HudShotHarness"/> writes one still per HUD state.
    /// </summary>
    public static class HudPreviewBuild
    {
        private const string DefaultOut = "Builds/HudPreview/SolarMajestyHudPreview.app";

        [MenuItem("Solar Majesty/Render/Build HUD Preview Player (macOS)", priority = 112)]
        public static void BuildMenu() => Build();

        /// <summary>CI/manual: -executeMethod SolarMajesty.EditorTools.HudPreviewBuild.Run [-smBuildPath path.app]</summary>
        public static void Run()
        {
            bool ok = Build();
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build()
        {
            string outPath = ArgValue("-smBuildPath") ?? DefaultOut;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? "Builds");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[HudPreview] No enabled scenes in Build Settings.");
                return false;
            }

            string product = PlayerSettings.productName;
            try
            {
                PlayerSettings.productName = product + " HUD Preview";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.Development
                });
                bool ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log($"[HudPreview] {report.summary.result} → {outPath}");
                return ok;
            }
            finally
            {
                PlayerSettings.productName = product;
            }
        }

        private static string ArgValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }
    }
}
