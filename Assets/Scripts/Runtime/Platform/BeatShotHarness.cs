using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// <c>-beatshots &lt;dir&gt;</c>: frame sequences of the launch and victory camera beats
    /// (<see cref="CameraDirector"/>) for review. Saves and preferences are redirected into
    /// <c>&lt;dir&gt;/profile</c> before the first scene loads, so a run never touches the player's
    /// own colony or settings. It starts a New Game (Earth unless <c>-beatshotBody Luna|Mars</c>), then:
    /// plays the victory beat and checks the player's camera comes back exactly; plays it again and
    /// ends it early (the skip path); and finally runs the game's own flow: the mission's win (gates
    /// forced, QA only) with its victory beat, a CONTINUE press during the beat (must be ignored),
    /// the verdict, CONTINUE, the launch beat, and the next world.
    /// Quits when done: exit 0 when every restore was exact and the flow reached the next world.
    /// </summary>
    public sealed class BeatShotHarness : MonoBehaviour
    {
        public const string Arg = "-beatshots";

        private string _dir;
        private int _shots;
        private bool _allExact = true;
        private bool _flowOk;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Isolate()
        {
            string dir = ArgValue(Arg);
            if (string.IsNullOrEmpty(dir)) return;
            string profile = Path.Combine(dir, "profile");
            Directory.CreateDirectory(Path.Combine(profile, "saves"));
            SaveSystem.DirectoryOverride = Path.Combine(profile, "saves");
            GamePrefs.DirectoryOverride = Path.Combine(profile, "prefs");
            Directory.CreateDirectory(GamePrefs.DirectoryOverride);
            IntroLaunch.MarkSeen(); // the first-launch intro would otherwise hold the title
            Debug.Log($"[BeatShots] Saves and preferences isolated under {profile}.");
        }

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
            Directory.CreateDirectory(_dir);
            Application.runInBackground = true;
            CameraDirector.EnabledOverride = true;
            DemoSettings.ReduceMotion = false;
            if (!Enum.TryParse(ArgValue("-beatshotBody") ?? "Earth", true, out CelestialBodyId body) ||
                !DemoSlice.ShowBody(body))
                body = CelestialBodyId.Earth;

            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return Wait(2f);
            GameLoop loop = FindAnyObjectByType<GameLoop>();
            if (loop == null) { Fail("no GameLoop in the boot scene"); yield break; }
            DemoSettings.SetFirstHourDemo(false);
            DemoSettings.MarkTutorialDone();
            loop.StartNewGameOn(body);
            loop = null;
            float giveUp = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < giveUp)
            {
                loop = FindAnyObjectByType<GameLoop>();
                if (loop != null && loop.Screen == DemoScreen.Playing && loop.ActiveBody == body) break;
                yield return null;
            }
            if (loop == null || loop.Screen != DemoScreen.Playing || loop.ActiveBody != body)
            {
                Fail("never reached a playing colony");
                yield break;
            }

            if (loop.IsTutorialActive) loop.SkipTutorial();
            LaunchSite.EnsureReady(null, heavyShip: body != CelestialBodyId.Earth);
            float settle = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < settle) { Dismiss(loop); yield return null; }

            var rig = FindAnyObjectByType<IsometricCameraController>();
            if (rig == null) { Fail("no camera rig"); yield break; }
            rig.FocusOn(ColonyLayout.CampusOrigin + new Vector3(4f, 0f, -2f), 16f);
            rig.SnapToTarget();
            yield return Wait(1f);
            yield return Shot("00_player_view");

            // Victory: slow pull-back and part orbit over the Commons, start to finish.
            Pose before = Capture();
            string won = body.ToString().ToUpperInvariant() + " SECURED";
            if (!CameraDirector.TryPlayVictory(rig, ColonyLayout.CampusOrigin, won))
            {
                Fail("victory beat refused to start");
                yield break;
            }
            yield return Sequence("01_victory", new[] { 0.3f, 1.2f, 2.4f, 3.6f, 4.8f, 6.0f, 7.0f });
            yield return WaitIdle();
            Check("victory", before);
            yield return Shot("02_victory_restored");

            // Skip: end the beat a second in (the path any key / click / pad button takes).
            before = Capture();
            CameraDirector.TryPlayVictory(rig, ColonyLayout.CampusOrigin, won);
            yield return Wait(1f);
            yield return Shot("03_skip_mid_beat");
            if (rig.TryGetComponent(out CameraDirector director)) director.Stop();
            bool blockedAfterSkip = CameraDirector.BlocksInput;
            yield return null;
            Check("victory_skipped", before);
            Debug.Log($"[BeatShots] skip: input swallowed on the skip frame = {blockedAfterSkip}");
            _allExact &= blockedAfterSkip;
            yield return Shot("04_skip_restored");

            yield return Flow(loop);

            Debug.Log($"[BeatShots] {_shots} frame(s) in {_dir}; restores exact: {_allExact}; flow ok: {_flowOk}");
            Quit(_allExact && _flowOk && _shots > 0 ? 0 : 1);
        }

        /// <summary>The game's own path: win, victory beat, verdict, CONTINUE, launch beat, next world.</summary>
        private IEnumerator Flow(GameLoop loop)
        {
            var mission = loop.Mission;
            if (mission == null || !ForceWin(mission))
            {
                Debug.LogError("[BeatShots] flow: could not latch the win.");
                yield break;
            }
            yield return Wait(0.6f);
            bool beatRan = CameraDirector.Active;
            Debug.Log($"[BeatShots] flow: won {mission.IsWon}, beat {beatRan}, hud hidden {CameraDirector.HidesHud}, " +
                      $"timeScale {Time.timeScale:F2}");
            yield return Shot("10_flow_victory_beat");

            CelestialBodyId from = loop.ActiveBody;
            loop.ContinueAfterVictory(); // must be ignored while the beat plays
            bool guardHeld = CameraDirector.Active && loop.ActiveBody == from;
            Debug.Log($"[BeatShots] flow: CONTINUE during the beat ignored = {guardHeld}");
            yield return Wait(2f);
            yield return Shot("11_flow_victory_orbit");
            yield return WaitIdle();
            yield return Wait(1f);
            Debug.Log($"[BeatShots] flow: verdict up, timeScale {Time.timeScale:F2} (0 = victory hold)");
            yield return Shot("12_flow_verdict");

            loop.ContinueAfterVictory(); // the verdict's CONTINUE button
            yield return null;
            bool launchRan = CameraDirector.Active;
            Debug.Log($"[BeatShots] flow: launch beat {launchRan}");
            yield return Sequence("13_flow_launch", new[] { 0.4f, 1.0f, 1.8f, 2.6f, 3.4f, 4.2f });

            float giveUp = Time.realtimeSinceStartup + 90f;
            GameLoop next = null;
            while (Time.realtimeSinceStartup < giveUp)
            {
                next = FindAnyObjectByType<GameLoop>();
                if (next != null && next != loop && next.Screen == DemoScreen.Playing) break;
                next = null;
                yield return null;
            }
            if (next == null)
            {
                Debug.LogError("[BeatShots] flow: never reached the next world.");
                yield break;
            }
            yield return Wait(3f);
            Dismiss(next);
            Debug.Log($"[BeatShots] flow: {from} -> {next.ActiveBody}, beat {CameraDirector.Active}, " +
                      $"input blocked {CameraDirector.BlocksInput}");
            yield return Shot("14_flow_next_world");
            _flowOk = beatRan && guardHeld && launchRan && !CameraDirector.Active && !CameraDirector.BlocksInput;
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

        private struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Size;
        }

        private static Pose Capture()
        {
            var cam = Camera.main;
            if (cam == null) return default;
            return new Pose
            {
                Position = cam.transform.position,
                Rotation = cam.transform.rotation,
                Size = cam.orthographic ? cam.orthographicSize : cam.fieldOfView
            };
        }

        private void Check(string beat, Pose before)
        {
            Pose now = Capture();
            bool exact = (now.Position - before.Position).sqrMagnitude < 1e-6f &&
                         Quaternion.Angle(now.Rotation, before.Rotation) < 0.01f &&
                         Mathf.Abs(now.Size - before.Size) < 1e-3f;
            Debug.Log($"[BeatShots] {beat}: restored exactly = {exact} (pos {before.Position:F3} -> {now.Position:F3})");
            _allExact &= exact;
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
            while (CameraDirector.Active && Time.realtimeSinceStartup < giveUp) yield return null;
            yield return null; // the handoff frame
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

        private static void Dismiss(GameLoop loop)
        {
            for (int i = 0; i < 8 && loop.TryPeekCutscene(out _); i++)
                loop.DismissCutscene();
        }

        private void Fail(string why)
        {
            Debug.LogError("[BeatShots] " + why);
            Quit(1);
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
