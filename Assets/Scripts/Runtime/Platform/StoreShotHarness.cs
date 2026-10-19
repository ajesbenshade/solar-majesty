using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// <c>-storeshots &lt;dir&gt;</c>: Steam store screenshots from the release player (development
    /// builds stamp a watermark). For each world it lays out a working colony, fabricates a crew,
    /// posts bounties on the nearest dens so robots head out, lets it run, then frames:
    /// the colony with the HUD, the colony and a wide view without it, and the robot furthest
    /// afield (usually mid-job). Quits when done.
    ///
    /// It starts New Games, so like the benchmark it refuses to run without <c>-saveDir</c> and
    /// <c>-profileDir</c>. Tools/qa/store_screenshots.ps1 sets everything up.
    /// Optional: <c>-storeshotBodies Earth,Mars</c>, <c>-storeshotRun 30</c> (seconds of play).
    /// </summary>
    public sealed class StoreShotHarness : MonoBehaviour
    {
        public const string Arg = "-storeshots";

        private string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string dir = ArgValue(Arg);
            if (string.IsNullOrEmpty(dir)) return;
            if (FindAnyObjectByType<StoreShotHarness>() != null) return;
            var go = new GameObject("[StoreShots]");
            DontDestroyOnLoad(go);
            go.AddComponent<StoreShotHarness>()._dir = dir;
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(SaveSystem.DirectoryOverride) || !GamePrefs.IsIsolated)
            {
                Debug.LogError("[StoreShots] Refusing to run without -saveDir and -profileDir: it starts New Games.");
                Quit(2);
                yield break;
            }

            Directory.CreateDirectory(_dir);
            Application.runInBackground = true;
            string[] bodies = (ArgValue("-storeshotBodies") ?? "Earth,Luna,Mars,Belt,Europa").Split(',');
            float run = 30f;
            float.TryParse(ArgValue("-storeshotRun"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out run);
            if (run <= 0f) run = 30f;

            int shots = 0;
            foreach (string raw in bodies)
            {
                if (!Enum.TryParse(raw.Trim(), true, out CelestialBodyId body)) continue;
                yield return Shoot(body, run, n => shots += n);
            }

            Debug.Log($"[StoreShots] {shots} screenshot(s) in {_dir}");
            Quit(shots > 0 ? 0 : 1);
        }

        private IEnumerator Shoot(CelestialBodyId body, float run, Action<int> counted)
        {
            GameLoop loop = FindAnyObjectByType<GameLoop>();
            if (loop == null) yield break;
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
            if (loop == null || loop.ActiveBody != body)
            {
                Debug.LogError("[StoreShots] " + body + " never reached play.");
                yield break;
            }

            // Best look the game has, at the store's native size.
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            if (loop.IsTutorialActive) loop.SkipTutorial();

            // A colony a few hours in: the full campus layout and one robot of every class.
            loop.StampPhase4DenseCampus();
            StillCaptureHold.Disarm(); // the stamp holds the sim for stills; these shots want it running
            // A treasury a few hours in, so the bounties below can actually be posted.
            loop.Resources?.Add(ResourceId.Metals, 6000);
            int crew = loop.SpawnShowcaseCrew();
            PostBounties(loop);
            SimSpeed.Set(6); // 2x: robots reach their jobs within the run
            Time.timeScale = SimSpeed.TimeScale;
            Debug.Log($"[StoreShots] {body}: crew {crew}, running {run:F0}s");

            float t = 0f;
            while (t < run)
            {
                t += Time.unscaledDeltaTime;
                DismissPopups(loop);
                yield return null;
            }

            var cam = FindAnyObjectByType<IsometricCameraController>();
            int n = 0;
            string world = body.ToString().ToLowerInvariant();

            // 1: the colony at work, HUD on (what a player sees).
            yield return Frame(cam, loop, ColonyLayout.CampusOrigin + new Vector3(3f, 0f, -2f), 15f, false, $"{world}_1_colony_hud", () => n++);
            // 2: the same colony without HUD, a little closer.
            yield return Frame(cam, loop, ColonyLayout.CampusOrigin + new Vector3(-2f, 0f, 2f), 12f, true, $"{world}_2_colony", () => n++);
            // 3: the world around it (wide enough for the pad and yards, short of the map edge).
            yield return Frame(cam, loop, ColonyLayout.CampusOrigin + new Vector3(6f, 0f, 4f), 21f, true, $"{world}_3_wide", () => n++);
            // 4: a den and its creatures (plus any robots taking its bounty), HUD on.
            if (TryDenShot(loop, out Vector3 den))
                yield return Frame(cam, loop, den, 10f, false, $"{world}_4_den_hud", () => n++);
            // 5: the crew up close: the densest group of robots, without HUD or tags.
            if (TryCrewShot(out Vector3 crewAt))
                yield return Frame(cam, loop, crewAt, 7.5f, true, $"{world}_5_crew", () => n++);

            counted(n);
        }

        private static System.Collections.Generic.List<Vector3> PostBounties(GameLoop loop)
        {
            var posted = new System.Collections.Generic.List<Vector3>();
            var input = loop.FlagInput;
            var world = loop.World;
            if (input == null || world == null) return posted;

            // The two nearest dens get a Clear Threat bounty generous enough that robots take it.
            var lairs = new System.Collections.Generic.List<StalkerLair>(world.Lairs);
            lairs.RemoveAll(l => l == null);
            // Charted dens show their models; unscouted ones are only a fog disc, which reads as nothing.
            foreach (var l in lairs) l.MarkScouted();
            lairs.Sort((a, b) =>
                (a.WorldPosition - ColonyLayout.CampusOrigin).sqrMagnitude.CompareTo(
                (b.WorldPosition - ColonyLayout.CampusOrigin).sqrMagnitude));
            for (int i = 0; i < lairs.Count && i < 2; i++)
            {
                lairs[i].MarkScouted();
                if (input.ClearThreatFlag != null &&
                    input.PostFlagAt(input.ClearThreatFlag, lairs[i].WorldPosition, 1800f) != null)
                    posted.Add(lairs[i].WorldPosition);
            }
            if (input.ExploreFlag != null)
                input.PostFlagAt(input.ExploreFlag, ColonyLayout.CampusOrigin + new Vector3(-26f, 0f, 18f), 900f);
            return posted;
        }

        /// <summary>The den with the most life within 12 m: its creatures, and robots on its bounty.</summary>
        private static bool TryDenShot(GameLoop loop, out Vector3 at)
        {
            at = Vector3.zero;
            if (loop.World == null) return false;
            var fauna = FindObjectsByType<DustStalkerAgent>(FindObjectsSortMode.None);
            var robots = FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None);
            int best = 0;
            foreach (var lair in loop.World.Lairs)
            {
                if (lair == null || lair.IsCleared) continue;
                Vector3 c = lair.WorldPosition;
                int life = 0;
                Vector3 sum = c;
                int count = 1;
                foreach (var f in fauna)
                {
                    if (f == null || (f.transform.position - c).sqrMagnitude > 144f) continue;
                    life++; sum += f.transform.position; count++;
                }
                foreach (var r in robots)
                {
                    if (r == null || (r.transform.position - c).sqrMagnitude > 144f) continue;
                    life += 2; sum += r.transform.position; count++; // a robot on the job is the story
                }
                if (life > best)
                {
                    best = life;
                    at = Vector3.Lerp(c, sum / count, 0.5f);
                }
            }
            return best >= 2;
        }

        /// <summary>The robot with the most others within 8 m, framed on the group's centre.</summary>
        private static bool TryCrewShot(out Vector3 at)
        {
            at = Vector3.zero;
            var robots = FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None);
            int best = 0;
            foreach (var a in robots)
            {
                if (a == null) continue;
                int near = 0;
                Vector3 sum = Vector3.zero;
                foreach (var b in robots)
                {
                    if (b == null || (b.transform.position - a.transform.position).sqrMagnitude > 64f) continue;
                    near++; sum += b.transform.position;
                }
                if (near > best)
                {
                    best = near;
                    at = sum / near;
                }
            }
            return best >= 2;
        }

        private static void DismissPopups(GameLoop loop)
        {
            for (int i = 0; i < 8 && loop.TryPeekCutscene(out _); i++)
                loop.DismissCutscene();
        }

        private IEnumerator Frame(IsometricCameraController cam, GameLoop loop, Vector3 at, float ortho, bool clean,
            string name, Action shot)
        {
            DismissPopups(loop);
            OverseerHud.HideForCapture = clean;
            SpecialistStatusDisplay.HideForCapture = clean;
            if (cam != null)
            {
                cam.FocusOn(at, ortho);
                cam.SnapToTarget();
            }
            float settle = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            DismissPopups(loop);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return null;
            yield return null;
            OverseerHud.HideForCapture = false;
            SpecialistStatusDisplay.HideForCapture = false;
            shot();
            Debug.Log("[StoreShots] " + path);
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
