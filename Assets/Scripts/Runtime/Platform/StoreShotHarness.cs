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
    /// Optional: <c>-storeshotBodies Earth,Mars</c>, <c>-storeshotRun 30</c> (seconds of play),
    /// <c>-storeshotKeyart</c> (also oversized, HUD-free key-art renders; <c>-storeshotKeyartWidth 5760</c>).
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

            if (HasArg(KeyartArg))
                yield return KeyArt(cam, loop, world, c => n += c);

            counted(n);
        }

        // ---- Key art ---------------------------------------------------------------------------
        // -storeshotKeyart adds clean, oversized renders for capsules and the library hero
        // (Tools/steam/make_store_art.py composes them). The camera renders straight into a large
        // render texture, so the IMGUI HUD never appears and the size is not limited by the window.

        public const string KeyartArg = "-storeshotKeyart";

        private struct KeyPose
        {
            public string Name;
            public Vector3 Focus;
            public float Yaw, Pitch, Distance, Fov;
            /// <summary>Output aspect (width / height).</summary>
            public float Aspect;
            /// <summary>Also render this framing in the low afternoon sun.</summary>
            public bool Golden;
            public KeyPose(string name, Vector3 focus, float yaw, float pitch, float distance, float fov,
                float aspect = 16f / 9f, bool golden = true)
            {
                Name = name; Focus = focus; Yaw = yaw; Pitch = pitch; Distance = distance; Fov = fov;
                Aspect = aspect; Golden = golden;
            }
        }

        /// <summary>
        /// Focus point that puts <paramref name="subject"/> <paramref name="shift"/> of the frame
        /// width right of centre (negative = left), for a camera at <paramref name="yaw"/>.
        /// </summary>
        private static Vector3 Offset(Vector3 subject, float yaw, float distance, float vfov, float aspect, float shift)
        {
            float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad) * aspect);
            float width = 2f * distance * Mathf.Tan(hfov * 0.5f);
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            return subject - right * (width * shift);
        }

        private IEnumerator KeyArt(IsometricCameraController ctl, GameLoop loop, string world, Action<int> counted)
        {
            int width = 5760;
            if (int.TryParse(ArgValue("-storeshotKeyartWidth"), out int w) && w >= 1920 && w <= 8192)
                width = w;
            Camera cam = ctl != null ? ctl.GetComponent<Camera>() : Camera.main;
            if (cam == null) yield break;

            int n = 0;
            OverseerHud.HideForCapture = true;
            SpecialistStatusDisplay.HideForCapture = true;
            DismissPopups(loop);

            // Map labels and bounty beams read as clutter in a still with no HUD to explain them.
            var hidden = new System.Collections.Generic.List<Renderer>();
            foreach (var tm in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if (tm != null && tm.TryGetComponent(out Renderer r) && r.enabled) { r.enabled = false; hidden.Add(r); }
            foreach (var f in FindObjectsByType<FlagMarker>(FindObjectsSortMode.None))
                if (f != null)
                    foreach (var r in f.GetComponentsInChildren<Renderer>())
                        if (r.enabled) { r.enabled = false; hidden.Add(r); }

            // 1: the classic overseer view, oversized.
            if (ctl != null)
            {
                ctl.FocusOn(ColonyLayout.CampusOrigin + new Vector3(5f, 0f, -1f), 15f);
                ctl.SnapToTarget();
            }
            yield return Settle(1.0f);
            if (RenderTo(cam, Path.Combine(_dir, $"{world}_keyart_ortho.png"), width, Mathf.RoundToInt(width * 9f / 16f))) n++;

            // 2+: perspective poses. Diorama mode sets up the perspective fog; then the controller
            // is paused so the harness owns the pose. Pitches stay above ~22 degrees so the frame
            // is all ground; only "side" shows the horizon.
            bool tilt = DemoSettings.TiltShift;
            bool diorama = DemoSettings.DioramaCamera;
            DemoSettings.TiltShift = false; // no miniature blur on key art
            DemoSettings.DioramaCamera = true;
            yield return Settle(1.0f);
            if (ctl != null) ctl.enabled = false;

            Vector3 o = ColonyLayout.CampusOrigin;
            Vector3 colony = o + new Vector3(6f, 0f, 0f); // between the Commons and the pad
            var poses = new[]
            {
                new KeyPose("low",       o + new Vector3(3f, 0f, 0f), 45f, 22f, 58f, 30f),
                new KeyPose("hero",      o + new Vector3(7f, 0f, 1f), 45f, 27f, 62f, 30f),
                new KeyPose("close",     o + new Vector3(2f, 0f, -1f), 50f, 25f, 44f, 30f),
                new KeyPose("band",      o + new Vector3(6f, 0f, 2f), 40f, 30f, 88f, 26f),
                new KeyPose("side",      o + new Vector3(6f, 0f, 0f), 15f, 14f, 70f, 30f, golden: false),
                // Capsule framings: the colony right of centre, open ground on the left for the logo.
                new KeyPose("right",     Offset(colony, 45f, 84f, 30f, 16f / 9f, 0.225f), 45f, 27f, 84f, 30f),
                new KeyPose("right21",   Offset(colony, 45f, 82f, 26f, 2.2f, 0.245f), 45f, 27f, 82f, 26f, 2.2f),
                // Portrait (vertical and library capsules): the Commons in front, the Colony Lifter behind.
                new KeyPose("portrait",  o + new Vector3(11f, 0f, 0f), 90f, 30f, 70f, 40f, 2f / 3f),
                // Library hero: very wide, the colony in the middle third.
                new KeyPose("ultrawide", o + new Vector3(6f, 0f, 1f), 45f, 27f, 100f, 20f, 3.1f),
            };

            // Tuned light first, then the same framings in the low afternoon sun of the day cycle.
            for (int pass = 0; pass < 2; pass++)
            {
                bool golden = pass == 1;
                if (golden)
                {
                    SunCycle.CaptureElapsed = LowSunTime(loop.BodyProfile);
                    yield return Settle(0.5f);
                }
                foreach (var p in poses)
                {
                    if (golden && !p.Golden) continue;
                    int rw = width;
                    int rh = Mathf.RoundToInt(width / p.Aspect);
                    if (rh > 6000)
                    {
                        rh = 6000;
                        rw = Mathf.RoundToInt(rh * p.Aspect);
                    }
                    Quaternion rot = Quaternion.Euler(p.Pitch, p.Yaw, 0f);
                    Vector3 pos = p.Focus - rot * Vector3.forward * p.Distance;
                    float ground = TerrainDataBake.GroundHeight(pos.x, pos.z);
                    pos.y = Mathf.Max(pos.y, ground + DioramaRig.MinClearance);
                    cam.orthographic = false;
                    cam.fieldOfView = p.Fov;
                    cam.nearClipPlane = 0.5f;
                    cam.transform.SetPositionAndRotation(pos, rot);
                    DemoAtmosphere.SyncFog(cam);
                    yield return Settle(1.2f);
                    DismissPopups(loop);
                    cam.transform.SetPositionAndRotation(pos, rot);
                    DemoAtmosphere.SyncFog(cam);
                    string file = $"{world}_keyart_{p.Name}{(golden ? "_golden" : "")}.png";
                    if (RenderTo(cam, Path.Combine(_dir, file), rw, rh)) n++;
                }
            }
            SunCycle.CaptureElapsed = null;

            foreach (var r in hidden)
                if (r != null) r.enabled = true;
            if (ctl != null) ctl.enabled = true;
            DemoSettings.DioramaCamera = diorama;
            DemoSettings.TiltShift = tilt;
            yield return Settle(0.5f);
            OverseerHud.HideForCapture = false;
            SpecialistStatusDisplay.HideForCapture = false;
            counted(n);
        }

        /// <summary>Mission time of the afternoon when the sun has sunk to about 16 degrees.</summary>
        private static double LowSunTime(CelestialBodyProfile body)
        {
            const int steps = 960;
            float prev = SunPath.Evaluate(body, 0.0).Euler.x;
            for (int i = 1; i < steps; i++)
            {
                double t = i * (double)SunPath.CycleSeconds / steps;
                SunState s = SunPath.Evaluate(body, t);
                if (s.Night <= 0f && s.Euler.x < prev && s.Euler.x <= 16f) return t;
                prev = s.Euler.x;
            }
            return 0.0;
        }

        private static IEnumerator Settle(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>Renders <paramref name="cam"/> into a width x height PNG. False if nothing was written.</summary>
        private static bool RenderTo(Camera cam, string path, int width, int height)
        {
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
            };
            Texture2D tex = null;
            try
            {
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[StoreShots] {path} ({width}x{height})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[StoreShots] key art render failed: " + e.Message);
                return false;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Destroy(rt);
                if (tex != null) Destroy(tex);
            }
        }

        private static bool HasArg(string name)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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
