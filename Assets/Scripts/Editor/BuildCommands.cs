using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Headless build entry points. CI calls these with -executeMethod; the menu items are the
    /// same code path so a local build cannot drift from the one the pipeline produces.
    /// </summary>
    public static class BuildCommands
    {
        private const string DefaultOutputRoot = "Builds";

        [MenuItem("Solar Majesty/Build/macOS (Apple Silicon)", priority = 200)]
        public static void BuildMacMenu() => Run(BuildTarget.StandaloneOSX, "macOS", "SolarMajesty.app");

        [MenuItem("Solar Majesty/Build/Windows 64-bit", priority = 201)]
        public static void BuildWindowsMenu() => Run(BuildTarget.StandaloneWindows64, "Windows", "SolarMajesty.exe");

        [MenuItem("Solar Majesty/Build/Linux 64-bit", priority = 202)]
        public static void BuildLinuxMenu() => Run(BuildTarget.StandaloneLinux64, "Linux", "SolarMajesty.x86_64");

        /// <summary>CI: -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildMac</summary>
        public static void BuildMac() => RunOrExit(BuildTarget.StandaloneOSX, "macOS", "SolarMajesty.app");

        public static void BuildWindows() => RunOrExit(BuildTarget.StandaloneWindows64, "Windows", "SolarMajesty.exe");

        public static void BuildLinux() => RunOrExit(BuildTarget.StandaloneLinux64, "Linux", "SolarMajesty.x86_64");

        private static void RunOrExit(BuildTarget target, string folder, string artifact)
        {
            bool ok = Run(target, folder, artifact);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(BuildTarget target, string folder, string artifact)
        {
            string root = ArgValue("-smBuildPath") ?? DefaultOutputRoot;
            string outputPath = Path.Combine(root, folder, artifact);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? root);

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No enabled scenes in Build Settings.");
                return false;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None
            };

            Debug.Log($"[Build] {target} -> {outputPath} ({scenes.Length} scene(s))");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[Build] Succeeded: {summary.totalSize / (1024 * 1024)} MB in " +
                    $"{summary.totalTime.TotalSeconds:F1}s -> {outputPath}");
                return true;
            }

            Debug.LogError($"[Build] {summary.result} with {summary.totalErrors} error(s).");
            return false;
        }

        private static string ArgValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                    return args[i + 1];
            }
            return null;
        }
    }
}
