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

        /// <summary>Command-line switch for <see cref="BuildFromCommandLine"/>: build the Steam demo flavour.</summary>
        public const string DemoArg = "-smDemo";

        [MenuItem("Solar Majesty/Build/Windows 64-bit Demo (Next Fest)", priority = 210)]
        public static void BuildWindowsDemoMenu() =>
            Run(BuildTarget.StandaloneWindows64, "Windows", DemoBuild.ExeName, demo: true);

        /// <summary>
        /// The Steam demo: Earth only, its own App ID, product name and save folders.
        /// CI: -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindowsDemo -smBuildPath Builds\Demo
        /// (or <see cref="BuildFromCommandLine"/> with <c>-smDemo</c>, as the release workflow does).
        /// </summary>
        public static void BuildWindowsDemo()
        {
            bool ok = Run(BuildTarget.StandaloneWindows64, "Windows", DemoBuild.ExeName, demo: true);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// CI (game-ci unity-builder <c>buildMethod</c>): target from <c>-customBuildTarget</c>
        /// (or <c>-buildTarget</c>), player path from <c>-customBuildPath</c>, version from
        /// <c>-smVersion</c> / <c>-buildVersion</c> (see <see cref="ReleaseStamp"/>).
        /// <c>-smDemo</c> builds the Steam demo flavour instead of the full game.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            string targetName = ArgValue("-customBuildTarget") ?? ArgValue("-buildTarget");
            bool demo = HasArg(DemoArg);
            bool ok = TryParseTarget(targetName, out BuildTarget target, out string folder, out string artifact);
            if (!ok)
            {
                Debug.LogError($"[Build] Unknown or missing build target '{targetName}'.");
            }
            else
            {
                if (demo && target == BuildTarget.StandaloneWindows64)
                    artifact = DemoBuild.ExeName;
                string custom = ArgValue("-customBuildPath");
                string outputPath = string.IsNullOrEmpty(custom)
                    ? Path.Combine(ArgValue("-smBuildPath") ?? DefaultOutputRoot, folder, artifact)
                    : WithPlayerExtension(target, custom);
                ok = RunAt(target, outputPath, demo);
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

        private static bool Run(BuildTarget target, string folder, string artifact, bool demo = false)
        {
            string root = ArgValue("-smBuildPath") ?? DefaultOutputRoot;
            return RunAt(target, Path.Combine(root, folder, artifact), demo);
        }

        /// <summary>
        /// The demo flavour: the SM_DEMO define goes to this build only (extraScriptingDefines, not
        /// ProjectSettings), and the product name and bundle id are swapped for the build and put
        /// back afterwards, so the full game's settings never change.
        /// </summary>
        private static bool RunAt(BuildTarget target, string outputPath, bool demo)
        {
            if (!demo)
                return BuildAt(target, outputPath, false);

            var named = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            string product = PlayerSettings.productName;
            string bundle = PlayerSettings.GetApplicationIdentifier(named);
            try
            {
                PlayerSettings.productName = DemoBuild.ProductName;
                PlayerSettings.SetApplicationIdentifier(named, DemoBuild.BundleId);
                ReleaseStamp.BuildingDemo = true;
                return BuildAt(target, outputPath, true);
            }
            finally
            {
                ReleaseStamp.BuildingDemo = false;
                PlayerSettings.productName = product;
                PlayerSettings.SetApplicationIdentifier(named, bundle);
            }
        }

        private static bool BuildAt(BuildTarget target, string outputPath, bool demo)
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
                options = BuildOptions.None,
                extraScriptingDefines = demo ? new[] { DemoBuild.Define } : null
            };

            // Version and icons are applied for this build only; the version is put back after so
            // a local build leaves ProjectSettings untouched. build-info.json is written by
            // BuildInfoPostprocess while the version is still applied.
            ReleaseStamp.ApplyIcons();
            ReleaseStamp.Snapshot previous = ReleaseStamp.ApplyVersion(ReleaseStamp.ResolveVersion());

            Debug.Log($"[Build] {target} -> {outputPath} ({scenes.Length} scene(s))" +
                      (demo ? $" · DEMO ({DemoBuild.Define}, \"{PlayerSettings.productName}\", app {SteamConfig.AppIdFor(true)})" : ""));
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

                WriteSteamAppId(folderPath, demo);
                CopyNotices(folderPath);

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
        /// It holds the flavour's App ID (SteamConfig.AppIdFor), today Valve's 480 Spacewar test app, not a real app ID.
        /// It is gitignored, and the SteamPipe script leaves it out of the depot.
        /// </summary>
        private static void WriteSteamAppId(string folder, bool demo)
        {
            try
            {
                File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), SteamConfig.AppIdFor(demo).ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not write steam_appid.txt: " + e.Message);
            }
        }

        /// <summary>MIT and SDK notices for code compiled into the player must ship with it.</summary>
        private static void CopyNotices(string folder)
        {
            const string source = "Tools/release/THIRD-PARTY-NOTICES.txt";
            try
            {
                if (File.Exists(source))
                    File.Copy(source, Path.Combine(folder, "THIRD-PARTY-NOTICES.txt"), overwrite: true);
                else
                    Debug.LogWarning("[Build] " + source + " is missing; the build ships without license notices.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not copy notices: " + e.Message);
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

        private static bool HasArg(string flag) =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

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
