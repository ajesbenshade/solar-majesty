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
            ShaderVariantKeeper.Create(); // runtime glass / glow must survive variant stripping
            string version = ArgValue("-smVersion");
            if (!string.IsNullOrEmpty(version))
                PlayerSettings.bundleVersion = version;
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
                string folderPath = Path.GetDirectoryName(outputPath);
                WriteSteamAppId(folderPath);
                WriteBuildInfo(folderPath, target);
                CopyNotices(folderPath);
                if (target == BuildTarget.StandaloneOSX)
                    PlaytestHandoff.WriteMac(folderPath);
                else if (target == BuildTarget.StandaloneWindows64)
                    PlaytestHandoff.WriteWindows(folderPath);

                Debug.Log(
                    $"[Build] Succeeded: {summary.totalSize / (1024 * 1024)} MB in " +
                    $"{summary.totalTime.TotalSeconds:F1}s -> {outputPath}");
                return true;
            }

            Debug.LogError($"[Build] {summary.result} with {summary.totalErrors} error(s).");
            return false;
        }

        /// <summary>
        /// steam_appid.txt lets a build started outside Steam (QA, playtesters with the zip) reach
        /// the Steam API. Tools/steam/upload_to_steam.ps1 leaves it out of the depot, because Steam
        /// passes the app ID itself when it launches the game.
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

        /// <summary>Version, commit and time next to the exe, so a bug report can name its build.</summary>
        private static void WriteBuildInfo(string folder, BuildTarget target)
        {
            try
            {
                string commit = Git("rev-parse --short HEAD") ?? "unknown";
                string dirty = string.IsNullOrEmpty(Git("status --porcelain --untracked-files=no")) ? "" : "-dirty";
                File.WriteAllText(Path.Combine(folder, "build_info.txt"),
                    $"version={PlayerSettings.bundleVersion}\n" +
                    $"commit={commit}{dirty}\n" +
                    $"target={target}\n" +
                    $"built_utc={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\n" +
                    $"unity={Application.unityVersion}\n" +
                    $"steam_app={SteamConfig.AppId}\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] Could not write build_info.txt: " + e.Message);
            }
        }

        private static string Git(string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);
                    return p.ExitCode == 0 ? output : null;
                }
            }
            catch
            {
                return null;
            }
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
