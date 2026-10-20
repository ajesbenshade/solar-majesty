using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// <c>-beatshots &lt;dir&gt;</c>: frame sequences of the launch and victory camera beats for review.
    /// Lays out a working colony (as the store-shot harness does), then plays the victory beat, the
    /// launch beat (its handoff is replaced so the run stays on this world), the launch again with
    /// Reduce motion, and a skipped victory beat, and checks the player's camera comes back exactly.
    /// Like the other harnesses it starts New Games, so it needs <c>-saveDir</c> and <c>-profileDir</c>.
    /// Optional: <c>-beatshotBody Mars</c>. Quits when done (exit 0 when every restore was exact).
    /// </summary>
    public sealed class BeatShotHarness : MonoBehaviour
    {
        public const string Arg = "-beatshots";

        private string _dir;
        private int _shots;
        private bool _allExact = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string dir = ArgValue(Arg);
            if (string.IsNullOrEmpty(dir)) return;
            if (FindAnyObjectByType<BeatShotHarness>() != null) return;
            var go = new GameObject("[BeatShots]");
            DontDestroyOnLoad(go);
            go.AddComponent<BeatShotHarness>()._dir = dir;
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(SaveSystem.DirectoryOverride) || !GamePrefs.IsIsolated)
            {
                Debug.LogError("[BeatShots] Refusing to run without -saveDir and -profileDir: it starts New Games.");
                Quit(2);
                yield break;
            }

            Directory.CreateDirectory(_dir);
            Application.runInBackground = true;
            CameraBeatDirector.EnabledOverride = true;
            if (!Enum.TryParse(ArgValue("-beatshotBody") ?? "Mars", true, out CelestialBodyId body))
                body = CelestialBodyId.Mars;

            GameLoop loop = FindAnyObjectByType<GameLoop>();
            if (loop == null) { Quit(1); yield break; }
            DemoSettings.SetFirstHourDemo(false);
            DemoSettings.MarkTutorialDone();
            loop.StartNewGameOn(body);
            yield return null;
            yield return null;
            float giveUp = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < giveUp)
            {
                loop = FindAnyObjectByType<GameLoop>();
                if (loop != null && loop.Screen == DemoScreen.Playing && loop.ActiveBody == body) break;
                yield return null;
            }
            if (loop == null || loop.ActiveBody != body) { Quit(1); yield break; }

            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            if (loop.IsTutorialActive) loop.SkipTutorial();
            loop.StampPhase4DenseCampus();
            StillCaptureHold.Disarm();
            loop.SpawnShowcaseCrew();
            LaunchSite.EnsureReady(null, heavyShip: true);
            SimSpeed.Set(4);
            loop.ApplySimSpeed();

            float settle = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < settle) { Dismiss(loop); yield return null; }

            var cam = FindAnyObjectByType<IsometricCameraController>();
            if (cam != null)
            {
                cam.FocusOn(ColonyLayout.CampusOrigin + new Vector3(4f, 0f, -2f), 16f);
                cam.SnapToTarget();
            }
            yield return Wait(1f);
            yield return Shot("00_player_view");

            // Victory: slow orbit and pull-back.
            DemoSettings.ReduceMotion = false;
            var before = CameraSnapshot.Capture(Camera.main);
            CameraBeatDirector.PlayVictory(loop, ColonyLayout.CampusOrigin, Loc.F("{0} CONQUERED", body.ToString().ToUpperInvariant()));
            yield return Sequence("victory", new[] { 0.3f, 1.0f, 1.8f, 2.8f, 3.8f, 4.8f, 5.8f, 6.6f });
            yield return WaitIdle();
            yield return Wait(0.3f);
            Check("victory", before);
            yield return Shot("victory_9_restored");

            // Launch: ease in, ignition hold, climb. The handoff is a no-op so the run stays here.
            var pad = LaunchSite.PadWorld;
            before = CameraSnapshot.Capture(Camera.main);
            bool handed = false;
            CameraBeatDirector.PlayLaunch(loop, LaunchSite.Craft, pad, Loc.F("LIFTOFF  ·  BOUND FOR {0}", "EUROPA"),
                () => LaunchSite.PlayDeparture(pad), () => handed = true);
            yield return Sequence("launch", new[] { 0.2f, 0.9f, 1.6f, 2.3f, 3.0f, 3.7f, 4.4f, 5.1f, 5.8f, 6.5f, 7.1f });
            yield return WaitIdle();
            yield return Wait(0.3f);
            Debug.Log($"[BeatShots] launch handoff ran: {handed}");
            Check("launch", before);

            // Reduce motion: a still wide shot, no shake.
            DemoSettings.ReduceMotion = true;
            before = CameraSnapshot.Capture(Camera.main);
            CameraBeatDirector.PlayLaunch(loop, LaunchSite.Craft, pad, Loc.S("LIFTOFF"), null, null);
            yield return Sequence("launch_reduced", new[] { 0.3f, 1.4f, 2.6f });
            yield return WaitIdle();
            Check("launch_reduced", before);
            before = CameraSnapshot.Capture(Camera.main);
            CameraBeatDirector.PlayVictory(loop, ColonyLayout.CampusOrigin, Loc.F("{0} CONQUERED", body.ToString().ToUpperInvariant()));
            yield return Sequence("victory_reduced", new[] { 0.3f, 1.8f });
            yield return WaitIdle();
            Check("victory_reduced", before);
            DemoSettings.ReduceMotion = false;

            // Skip: Esc's path, one second in.
            before = CameraSnapshot.Capture(Camera.main);
            CameraBeatDirector.PlayVictory(loop, ColonyLayout.CampusOrigin, null);
            yield return Wait(1f);
            CameraBeatDirector.SkipCurrent();
            yield return null;
            Check("victory_skipped", before);
            yield return Shot("skip_restored");

            // End to end, through the game's own path: the mission's win (gates forced, QA only),
            // the verdict once the orbit ends, then CONTINUE: launch beat, departure, the next world.
            yield return Flow(loop);

            Debug.Log($"[BeatShots] {_shots} frame(s) in {_dir}; restores exact: {_allExact}; flow ok: {_flowOk}");
            Quit(_allExact && _flowOk && _shots > 0 ? 0 : 1);
        }

        private bool _flowOk;

        private IEnumerator Flow(GameLoop loop)
        {
            var mission = loop.Mission;
            if (mission == null || !ForceWin(mission))
            {
                Debug.LogError("[BeatShots] flow: could not latch the win.");
                yield break;
            }
            yield return Wait(0.6f);
            Debug.Log($"[BeatShots] flow: won {mission.IsWon}, beat {CameraBeatDirector.Active}, timeScale {Time.timeScale:F2}");
            bool beatRan = CameraBeatDirector.Active;
            yield return Shot("flow_1_victory_beat");
            yield return WaitIdle();
            yield return Wait(0.6f);
            Debug.Log($"[BeatShots] flow: verdict up, timeScale {Time.timeScale:F2} (0 = victory hold)");
            yield return Shot("flow_2_verdict");

            CelestialBodyId from = loop.ActiveBody;
            loop.ContinueAfterVictory(); // the verdict's CONTINUE button
            bool launchRan = CameraBeatDirector.Active;
            yield return Wait(2.6f);
            yield return Shot("flow_3_launch_ignition");
            yield return Wait(2.2f);
            yield return Shot("flow_4_launch_climb");

            float giveUp = Time.realtimeSinceStartup + 60f;
            GameLoop next = null;
            while (Time.realtimeSinceStartup < giveUp)
            {
                next = FindAnyObjectByType<GameLoop>();
                if (next != null && next.ActiveBody != from && next.Screen == DemoScreen.Playing) break;
                yield return null;
            }
            if (next == null || next.ActiveBody == from)
            {
                Debug.LogError("[BeatShots] flow: never reached the next world.");
                yield break;
            }
            yield return Wait(3f);
            Dismiss(next);
            Debug.Log($"[BeatShots] flow: {from} -> {next.ActiveBody}, beat {CameraBeatDirector.Active}, " +
                      $"timeScale {Time.timeScale:F2}, input blocked {CameraBeatDirector.BlocksInput}");
            yield return Shot("flow_5_next_world");
            _flowOk = beatRan && launchRan && !CameraBeatDirector.Active && !CameraBeatDirector.BlocksInput;
        }

        /// <summary>QA only: meet the three gates and run the mission's own win (fanfare, save, beat).</summary>
        private static bool ForceWin(MissionController m)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance |
                                                      System.Reflection.BindingFlags.Public |
                                                      System.Reflection.BindingFlags.NonPublic;
            var t = typeof(MissionController);
            t.GetProperty("DensCleared", F)?.SetValue(m, true);
            var hold = t.GetField("sustainHoldSeconds", F);
            var elapsed = t.GetField("_sustainElapsed", F);
            if (hold != null && elapsed != null) elapsed.SetValue(m, hold.GetValue(m));
            m.SetLaunchReady(true);
            t.GetMethod("EnterWon", F)?.Invoke(m, null);
            return m.IsWon;
        }

        private IEnumerator Sequence(string name, float[] times)
        {
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < times.Length; i++)
            {
                while (Time.realtimeSinceStartup - t0 < times[i]) yield return null;
                yield return Shot($"{name}_{i + 1}_{times[i]:0.0}s");
            }
        }

        private static IEnumerator WaitIdle()
        {
            float giveUp = Time.realtimeSinceStartup + 15f;
            while (CameraBeatDirector.Active && Time.realtimeSinceStartup < giveUp) yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
            _shots++;
            var cam = Camera.main;
            Debug.Log($"[BeatShots] {name}: camera {(cam != null ? cam.transform.position.ToString("F1") : "-")} " +
                      $"euler {(cam != null ? cam.transform.eulerAngles.ToString("F0") : "-")} -> {path}");
        }

        private void Check(string beat, CameraSnapshot before)
        {
            var now = CameraSnapshot.Capture(Camera.main);
            bool exact = now.Position == before.Position && now.Rotation == before.Rotation &&
                         now.Orthographic == before.Orthographic &&
                         Mathf.Approximately(now.OrthographicSize, before.OrthographicSize) &&
                         Mathf.Approximately(now.FieldOfView, before.FieldOfView);
            // The controller resumes its own smoothing after handoff; compare the frame the beat returned.
            Debug.Log($"[BeatShots] {beat}: restored exactly = {exact} (pos {before.Position} -> {now.Position})");
            _allExact &= exact;
        }

        private static void Dismiss(GameLoop loop)
        {
            for (int i = 0; i < 8 && loop.TryPeekCutscene(out _); i++)
                loop.DismissCutscene();
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
    }
}
