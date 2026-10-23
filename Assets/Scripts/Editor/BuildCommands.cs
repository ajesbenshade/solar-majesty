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

        /// <summary>
        /// CI (game-ci unity-builder <c>buildMethod</c>): target from <c>-customBuildTarget</c>
        /// (or <c>-buildTarget</c>), player path from <c>-customBuildPath</c>, version from
        /// <c>-smVersion</c> / <c>-buildVersion</c> (see <see cref="ReleaseStamp"/>).
        /// </summary>
        public static void BuildFromCommandLine()
        {
            string targetName = ArgValue("-customBuildTarget") ?? ArgValue("-buildTarget");
            bool ok = TryParseTarget(targetName, out BuildTarget target, out string folder, out string artifact);
            if (!ok)
            {
                Debug.LogError($"[Build] Unknown or missing build target '{targetName}'.");
            }
            else
            {
                string custom = ArgValue("-customBuildPath");
                string outputPath = string.IsNullOrEmpty(custom)
                    ? Path.Combine(ArgValue("-smBuildPath") ?? DefaultOutputRoot, folder, artifact)
                    : WithPlayerExtension(target, custom);
                ok = RunAt(target, outputPath);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void RunOrExit(BuildTarget target, string folder, string artifact)
        {
            bool ok = Run(target, folder, artifact);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(BuildTarget target, string folder, string artifact)
        {
            string root = ArgValue("-smBuildPath") ?? DefaultOutputRoot;
            return RunAt(target, Path.Combine(root, folder, artifact));
        }

        private static bool RunAt(BuildTarget target, string outputPath)
        {
            ShaderVariantKeeper.Create(); // runtime glass / glow must survive variant stripping
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? DefaultOutputRoot);

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

            // Version and icons are applied for this build only; the version is put back after so
            // a local build leaves ProjectSettings untouched. build-info.json is written by
            // BuildInfoPostprocess while the version is still applied.
            ReleaseStamp.ApplyIcons();
            ReleaseStamp.Snapshot previous = ReleaseStamp.ApplyVersion(ReleaseStamp.ResolveVersion());

            Debug.Log($"[Build] {target} -> {outputPath} ({scenes.Length} scene(s))");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                previous.Restore();
            }
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string folderPath = Path.GetDirectoryName(outputPath);
                if (target == BuildTarget.StandaloneOSX)
                    PlaytestHandoff.WriteMac(folderPath);
                else if (target == BuildTarget.StandaloneWindows64)
                    PlaytestHandoff.WriteWindows(folderPath);

                WriteSteamAppId(folderPath);

                Debug.Log(
                    $"[Build] Succeeded: {summary.totalSize / (1024 * 1024)} MB in " +
                    $"{summary.totalTime.TotalSeconds:F1}s -> {outputPath}");
                return true;
            }

            Debug.LogError($"[Build] {summary.result} with {summary.totalErrors} error(s).");
            return false;
        }

        /// <summary>
        /// steam_appid.txt lets a build started outside Steam (QA, a zip) reach the Steam client.
        /// It holds SteamConfig.AppId, today Valve's 480 Spacewar test app, not a real app ID.
        /// It is gitignored, and the SteamPipe script leaves it out of the depot.
        /// </summary>
        private static void WriteSteamAppId(string folder)
        {
            try
            {
                File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), SteamConfig.AppId.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not write steam_appid.txt: " + e.Message);
            }
        }

        private static bool TryParseTarget(string name, out BuildTarget target, out string folder, out string artifact)
        {
            folder = artifact = null;
            target = BuildTarget.NoTarget;
            switch (name)
            {
                case "StandaloneOSX":
                case "OSXUniversal":
                case "OSX":
                    target = BuildTarget.StandaloneOSX;
                    folder = "macOS";
                    artifact = "SolarMajesty.app";
                    return true;
                case "StandaloneWindows64":
                case "Win64":
                    target = BuildTarget.StandaloneWindows64;
                    folder = "Windows";
                    artifact = "SolarMajesty.exe";
                    return true;
                case "StandaloneLinux64":
                case "Linux64":
                    target = BuildTarget.StandaloneLinux64;
                    folder = "Linux";
                    artifact = "SolarMajesty.x86_64";
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>game-ci passes the player path without .app on macOS; Unity wants the bundle name.</summary>
        private static string WithPlayerExtension(BuildTarget target, string path)
        {
            if (target == BuildTarget.StandaloneOSX && !path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return path + ".app";
            if (target == BuildTarget.StandaloneWindows64 && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return path + ".exe";
            return path;
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
