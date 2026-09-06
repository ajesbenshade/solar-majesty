using System;
using System.IO;
using System.Reflection;
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
        private const string PendingOutKey = "SM_CaptureStill_Out";
        private const string PendingBodyKey = "SM_CaptureStill_Body";

        [MenuItem("Solar Majesty/Render/Capture Mars Still", priority = 110)]
        public static void CaptureMars() => Begin(CelestialBodyId.Mars, DefaultOut);

        /// <summary>CI/manual: -executeMethod SolarMajesty.EditorTools.CaptureStill.Run</summary>
        public static void Run()
        {
            // Phase 4 stills default to Mars. Only -smBody overrides (debug other worlds).
            var body = CelestialBodyId.Mars;
            string outPath = DefaultOut;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-smBody" && Enum.TryParse(args[i + 1], true, out CelestialBodyId parsed))
                    body = parsed;
                if (args[i] == "-smOut")
                    outPath = args[i + 1];
            }

            Begin(body, outPath);
        }

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

            ForceGameViewScaleOne();
            // GameLoop.Awake clamps BodySeed to Earth when the body is campaign-locked.
            // Mars stills need the spine open or the shutter logs Mars while the world is Earth.
            CampaignProgress.DebugUnlockAll();
            // Don't ApplySave an Earth continue over a Mars BodySeed.
            DemoSettings.ClearSave();
            DemoSettings.MarkTutorialDone();
            BodySeed.SetBody(body);
            BodySeed.Ensure(body, 0);
            DemoSettings.RequestBootIntoPlay();
            SessionState.SetString(PendingOutKey, outPath);
            SessionState.SetString(PendingBodyKey, body.ToString());

            Vector2 size = GetMainGameViewSize();
            float scale = ReadGameViewScale();
            Debug.Log(
                $"[Capture] Begin body={body} unlockedThrough={CampaignProgress.HighestUnlocked} scale={scale:0.###}x gameView={size.x:0}x{size.y:0} out={outPath}");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _state = 0;
            _frames = 0;
            _outPath = outPath;
            _body = body;
            EditorApplication.update += Pump;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterDomainReload()
        {
            string pending = SessionState.GetString(PendingOutKey, "");
            if (string.IsNullOrEmpty(pending)) return;

            // A hung capture leaves this key set. If the editor is not already entering Play
            // for that capture, drop it — otherwise the user's next Play is force-exited.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.EraseString(PendingOutKey);
                SessionState.EraseString(PendingBodyKey);
                return;
            }

            _outPath = pending;
            string bodyRaw = SessionState.GetString(PendingBodyKey, CelestialBodyId.Mars.ToString());
            if (!Enum.TryParse(bodyRaw, true, out _body))
                _body = CelestialBodyId.Mars;

            // Domain reload can drop BodySeed / GameView zoom — re-assert before the shutter.
            ForceGameViewScaleOne();
            CampaignProgress.DebugUnlockAll();
            BodySeed.SetBody(_body);
            BodySeed.Ensure(_body, 0);

            _state = 0;
            _frames = 0;
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
            Debug.Log($"[Capture] Resume after reload body={_body} scale={ReadGameViewScale():0.###}x");
        }

        private static int _state;
        private static int _frames;
        private static string _outPath;
        private static CelestialBodyId _body = CelestialBodyId.Mars;

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
                    ForceGameViewScaleOne();
                    BodySeed.SetBody(_body);
                    _state = 1;
                    _frames = 0;
                    return;

                case 1:
                    // Let world gen, dressing, and the camera snap settle before the shutter.
                    if (++_frames < 180) return;
                    ForceGameViewScaleOne();
                    Directory.CreateDirectory(Path.GetDirectoryName(_outPath) ?? "Docs");
                    Vector2 size = GetMainGameViewSize();
                    float scale = ReadGameViewScale();
                    ScreenCapture.CaptureScreenshot(_outPath, 1);
                    Debug.Log(
                        $"[Capture] Shutter body={_body} scale={scale:0.###}x " +
                        $"gameView={size.x:0}x{size.y:0} -> {_outPath}");
                    _state = 2;
                    _frames = 0;
                    return;

                case 2:
                    if (++_frames < 60) return;
                    EditorApplication.update -= Pump;
                    SessionState.EraseString(PendingOutKey);
                    SessionState.EraseString(PendingBodyKey);
                    EditorApplication.ExitPlaymode();
                    Debug.Log("[Capture] Done.");
                    if (Application.isBatchMode)
                        EditorApplication.Exit(0);
                    return;
            }
        }

        /// <summary>
        /// Game tab Scale 1.5x crops Phase 4 stills. Force 1x via GameView zoom area reflection
        /// (Unity 6 internal API — best-effort; logs if the layout changes).
        /// </summary>
        private static void ForceGameViewScaleOne()
        {
            try
            {
                Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null)
                {
                    Debug.LogWarning("[Capture] GameView type missing — cannot force Scale 1x");
                    return;
                }

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView == null) return;

                // Prefer public/internal scale API when present (varies by Unity minor).
                PropertyInfo scaleProp = gameViewType.GetProperty(
                    "scale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (scaleProp != null && scaleProp.CanWrite && scaleProp.PropertyType == typeof(float))
                {
                    scaleProp.SetValue(gameView, 1f);
                    gameView.Repaint();
                    return;
                }

                FieldInfo zoomAreaField = gameViewType.GetField(
                    "m_ZoomArea", BindingFlags.Instance | BindingFlags.NonPublic);
                if (zoomAreaField == null)
                {
                    Debug.LogWarning("[Capture] GameView.m_ZoomArea missing — cannot force Scale 1x");
                    return;
                }

                object zoomArea = zoomAreaField.GetValue(gameView);
                if (zoomArea == null) return;

                Type zoomType = zoomArea.GetType();
                FieldInfo scaleField = zoomType.GetField(
                    "m_Scale", BindingFlags.Instance | BindingFlags.NonPublic);
                if (scaleField != null)
                {
                    if (scaleField.FieldType == typeof(Vector2))
                        scaleField.SetValue(zoomArea, Vector2.one);
                    else if (scaleField.FieldType == typeof(float))
                        scaleField.SetValue(zoomArea, 1f);
                }

                MethodInfo setScale = zoomType.GetMethod(
                    "SetScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setScale != null)
                {
                    ParameterInfo[] ps = setScale.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(float))
                        setScale.Invoke(zoomArea, new object[] { 1f });
                    else if (ps.Length == 1 && ps[0].ParameterType == typeof(Vector2))
                        setScale.Invoke(zoomArea, new object[] { Vector2.one });
                }

                gameView.Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Capture] ForceGameViewScaleOne failed: {ex.Message}");
            }
        }

        private static float ReadGameViewScale()
        {
            try
            {
                Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null) return -1f;
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView == null) return -1f;

                PropertyInfo scaleProp = gameViewType.GetProperty(
                    "scale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (scaleProp != null && scaleProp.PropertyType == typeof(float))
                    return (float)scaleProp.GetValue(gameView);

                FieldInfo zoomAreaField = gameViewType.GetField(
                    "m_ZoomArea", BindingFlags.Instance | BindingFlags.NonPublic);
                object zoomArea = zoomAreaField?.GetValue(gameView);
                FieldInfo scaleField = zoomArea?.GetType().GetField(
                    "m_Scale", BindingFlags.Instance | BindingFlags.NonPublic);
                if (scaleField == null) return -1f;
                object val = scaleField.GetValue(zoomArea);
                if (val is Vector2 v) return v.x;
                if (val is float f) return f;
            }
            catch
            {
                // ignore
            }

            return -1f;
        }

        private static Vector2 GetMainGameViewSize()
        {
            try
            {
                Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                MethodInfo getSize = gameViewType?.GetMethod(
                    "GetSizeOfMainGameView", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (getSize != null)
                    return (Vector2)getSize.Invoke(null, null);

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView != null)
                    return gameView.position.size;
            }
            catch
            {
                // ignore
            }

            return Vector2.zero;
        }
    }
}
