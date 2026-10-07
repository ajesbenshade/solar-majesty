using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Windows development player for world reviews. Runs under its own product name so its
    /// PlayerPrefs and saves stay apart from the real game's. Run it with
    /// <c>-smWorldShot &lt;dir&gt;</c> and <see cref="WorldShotHarness"/> photographs the map.
    /// CLI: -executeMethod SolarMajesty.EditorTools.WorldPreviewBuild.Run [-smBuildPath path.exe]
    /// </summary>
    public static class WorldPreviewBuild
    {
        private const string DefaultOut = "Builds/WorldPreview/SolarMajestyWorldPreview.exe";

        [MenuItem("Solar Majesty/Render/Build World Preview Player (Windows)", priority = 113)]
        public static void BuildMenu() => Build();

        public static void Run()
        {
            bool ok = Build();
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build()
        {
            ShaderVariantKeeper.Create();
            string outPath = ArgValue("-smBuildPath") ?? DefaultOut;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? "Builds");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WorldPreview] No enabled scenes in Build Settings.");
                return false;
            }

            string product = PlayerSettings.productName;
            try
            {
                PlayerSettings.productName = product + " World Preview";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                bool ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log($"[WorldPreview] {report.summary.result} -> {outPath}");
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
