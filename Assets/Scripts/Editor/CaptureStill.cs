using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Enters play mode, lets the world settle, and writes a PNG. This is the evidence loop for the
    /// Phase 4 exit review: every look milestone ends with a fresh still compared against the mockup,
    /// captured the same way each time rather than by hand at an arbitrary camera.
    /// </summary>
    public static class CaptureStill
    {
        private const string ScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";
        private const string DefaultOut = "Docs/Roadmap/SM_Capture.png";

        [MenuItem("Solar Majesty/Render/Capture Mars Still", priority = 110)]
        public static void CaptureMars() => Begin(CelestialBodyId.Mars, DefaultOut);

        /// <summary>CI/manual: -executeMethod SolarMajesty.EditorTools.CaptureStill.Run</summary>
        public static void Run()
        {
            var body = CelestialBodyId.Mars;
            string outPath = DefaultOut;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-smBody" && System.Enum.TryParse(args[i + 1], true, out CelestialBodyId parsed))
                    body = parsed;
                if (args[i] == "-smOut")
                    outPath = args[i + 1];
            }

            Begin(body, outPath);
        }

        // Entering play mode triggers a domain reload that wipes statics, so the pending capture
        // is parked in SessionState and picked up again on the other side.
        private const string PendingKey = "SM_CaptureStill_Out";

        private static void Begin(CelestialBodyId body, string outPath)
        {
            // Unity's batch mode cannot reliably drive play mode to a rendered frame; it stalls
            // after the domain reload instead of failing. Refuse rather than hang a CI job.
            if (Application.isBatchMode)
            {
                Debug.LogError(
                    "[Capture] Still capture needs an interactive editor. " +
                    "Use Solar Majesty > Render > Capture Mars Still.");
                EditorApplication.Exit(1);
                return;
            }

            BodySeed.SetBody(body);
            DemoSettings.RequestBootIntoPlay();
            SessionState.SetString(PendingKey, outPath);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _state = 0;
            _frames = 0;
            _outPath = outPath;
            EditorApplication.update += Pump;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterDomainReload()
        {
            string pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending)) return;

            // A hung capture leaves this key set. If the editor is not already entering Play
            // for that capture, drop it — otherwise the user's next Play is force-exited.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.EraseString(PendingKey);
                return;
            }

            _outPath = pending;
            _state = 0;
            _frames = 0;
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }

        private static int _state;
        private static int _frames;
        private static string _outPath;

        private static void Pump()
        {
            if (string.IsNullOrEmpty(_outPath))
            {
                EditorApplication.update -= Pump;
                return;
            }

            switch (_state)
            {
                case 0:
                    if (!EditorApplication.isPlaying) return;
                    _state = 1;
                    _frames = 0;
                    return;

                case 1:
                    // Let world gen, dressing, and the camera snap settle before the shutter.
                    if (++_frames < 180) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(_outPath) ?? "Docs");
                    ScreenCapture.CaptureScreenshot(_outPath, 1);
                    Debug.Log($"[Capture] Requested still -> {_outPath}");
                    _state = 2;
                    _frames = 0;
                    return;

                case 2:
                    if (++_frames < 60) return;
                    EditorApplication.update -= Pump;
                    SessionState.EraseString(PendingKey);
                    EditorApplication.ExitPlaymode();
                    Debug.Log("[Capture] Done.");
                    if (Application.isBatchMode)
                        EditorApplication.Exit(0);
                    return;
            }
        }
    }
}
