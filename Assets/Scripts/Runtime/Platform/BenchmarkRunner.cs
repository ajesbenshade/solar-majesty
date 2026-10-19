using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace SolarMajesty
{
    /// <summary>
    /// <c>-benchmark</c>: starts a fresh colony on each world, runs the simulation at speed while the
    /// camera sweeps the map, and writes frame-time statistics to <c>Logs/benchmark-*.json</c>. Then quits.
    ///
    /// This is the measured basis for the store's system requirements and the "60 fps" target, and a
    /// soak: any exception during the run fails it (exit code 1).
    ///
    /// It starts New Games, so it refuses to run unless both <c>-saveDir</c> and <c>-profileDir</c>
    /// point somewhere disposable. Tools/qa/run_benchmark.ps1 sets everything up.
    ///
    ///   SolarMajesty.exe -benchmark -saveDir X -profileDir Y [-benchmarkBodies Earth,Mars]
    ///                    [-benchmarkSeconds 45] [-benchmarkSpeed 7] -nosteam -noai
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        public const string Arg = "-benchmark";

        private const float BootTimeout = 90f;
        private const float Warmup = 6f;
        private const float CameraHop = 3f;
        private const float NotDrawnMs = 1f;

        private readonly List<WorldResult> _results = new List<WorldResult>();
        private string _outPath;

        [Serializable]
        private sealed class WorldResult
        {
            public string world;
            public float seconds;
            public int frames;
            public float avgFps;
            public float p50Ms;
            public float p95Ms;
            public float p99Ms;
            public float maxMs;
            public float pctOver33Ms;
            public float pctOver16Ms;
            public long monoUsedMb;
            public long totalAllocatedMb;
            public int robots;
            public int fauna;
            public int errors;
            /// <summary>False when frame times show the OS was not presenting the window; fps is then meaningless.</summary>
            public bool drawn = true;
            /// <summary>The worst frames: when they happened and what else happened that frame.</summary>
            public List<Spike> spikes = new List<Spike>();
            public string note;
        }

        [Serializable]
        private sealed class Spike
        {
            public float atSeconds;
            public float ms;
            public float saveMs;
            public bool saveInFrame;
            public int gcCount;
            /// <summary>Development builds only: where the frame went, in ms, for the systems that usually own a spike.</summary>
            public string breakdown;
        }

        // Profiler markers are only recorded in development builds; in release they read zero.
        private static readonly string[] SpikeMarkers =
        {
            "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "FixedUpdate.ScriptRunBehaviourFixedUpdate", "FixedUpdate.PhysicsFixedUpdate",
            "GC.Collect", "Update.ScriptRunDelayedTasks", "PreUpdate.AIUpdatePostScript",
            "PostLateUpdate.FinishFrameRendering", "Loading.ReadObject", "Destroy"
        };
        private readonly List<ProfilerRecorder> _recorders = new List<ProfilerRecorder>();

        [Serializable]
        private sealed class Report
        {
            public string version;
            public string unity;
            public string os;
            public string cpu;
            public int cpuCores;
            public string gpu;
            public int gpuMemoryMb;
            public int ramMb;
            public string resolution;
            public string quality;
            public string startedUtc;
            public bool passed;
            public List<WorldResult> worlds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!HasArg(Arg)) return;
            if (FindAnyObjectByType<BenchmarkRunner>() != null) return;
            var go = new GameObject("[Benchmark]");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchmarkRunner>();
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(SaveSystem.DirectoryOverride) || !GamePrefs.IsIsolated)
            {
                Debug.LogError("[Benchmark] Refusing to run without -saveDir and -profileDir: it starts New Games " +
                               "and would overwrite the player's campaign. Use Tools/qa/run_benchmark.ps1.");
                Quit(2);
                yield break;
            }

            Application.runInBackground = true; // a benchmark behind another window still has to run
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            string[] bodies = (ArgValue("-benchmarkBodies") ?? "Earth,Luna,Mars,Belt,Europa").Split(',');
            float seconds = ParseFloat(ArgValue("-benchmarkSeconds"), 45f);
            int speedIndex = Mathf.Clamp((int)ParseFloat(ArgValue("-benchmarkSpeed"), 7f), 1, SimSpeed.Multipliers.Length - 1);

            System.IO.Directory.CreateDirectory(ErrorLog.Directory);
            _outPath = Path.Combine(ErrorLog.Directory, $"benchmark-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            Debug.Log($"[Benchmark] {string.Join(",", bodies)} · {seconds:F0}s each · speed {SimSpeed.LabelFor(speedIndex)} → {_outPath}");

            foreach (string raw in bodies)
            {
                if (!Enum.TryParse(raw.Trim(), true, out CelestialBodyId body))
                {
                    Debug.LogWarning("[Benchmark] Unknown world " + raw);
                    continue;
                }
                yield return RunWorld(body, seconds, speedIndex);
                WriteReport();
            }

            bool passed = _results.Count > 0 && _results.TrueForAll(r => r.errors == 0 && string.IsNullOrEmpty(r.note));
            WriteReport();
            Debug.Log("[Benchmark] " + (passed ? "PASSED" : "FAILED") + " — " + _outPath);
            Quit(passed ? 0 : 1);
        }

        private IEnumerator RunWorld(CelestialBodyId body, float seconds, int speedIndex)
        {
            var result = new WorldResult { world = body.ToString() };
            _results.Add(result);

            GameLoop loop = null;
            float waited = 0f;
            while ((loop = FindAnyObjectByType<GameLoop>()) == null && waited < BootTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (loop == null) { result.note = "no GameLoop"; yield break; }

            DemoSettings.SetFirstHourDemo(false); // the guided start keeps players on Earth; the benchmark visits every world
            loop.StartNewGameOn(body); // reloads the scene
            yield return null;
            yield return null;

            waited = 0f;
            while (waited < BootTimeout)
            {
                loop = FindAnyObjectByType<GameLoop>();
                if (loop != null && loop.Screen == DemoScreen.Playing && loop.ActiveBody == body) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (loop == null || loop.Screen != DemoScreen.Playing || loop.ActiveBody != body)
            {
                result.note = $"did not reach play on {body} within {BootTimeout:F0}s";
                Debug.LogError("[Benchmark] " + result.note);
                yield break;
            }

            if (loop.IsTutorialActive) loop.SkipTutorial();
            ApplyRequestedResolution(); // the game re-applies the player's saved display settings on boot
            SimSpeed.Set(speedIndex);
            Time.timeScale = SimSpeed.TimeScale;

            var cam = FindAnyObjectByType<IsometricCameraController>();
            Vector3 centre = loop.Grid != null
                ? loop.Grid.Origin + new Vector3(loop.Grid.Width * loop.Grid.CellSize * 0.5f, 0f, loop.Grid.Height * loop.Grid.CellSize * 0.5f)
                : Vector3.zero;
            float reach = loop.Grid != null ? Mathf.Min(loop.Grid.Width, loop.Grid.Height) * loop.Grid.CellSize * 0.35f : 40f;

            float t = 0f;
            while (t < Warmup) { t += Time.unscaledDeltaTime; yield return null; }

            int errorsBefore = ErrorLog.Count;
            var frames = new List<float>(Mathf.CeilToInt(seconds * 120f));
            float elapsed = 0f, nextHop = 0f;
            int hop = 0;
            var spikes = new List<Spike>();
            int gcBefore = GC.CollectionCount(0);
            if (Debug.isDebugBuild && _recorders.Count == 0)
            {
                foreach (string m in SpikeMarkers)
                    _recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, m, 4));
            }
            while (elapsed < seconds)
            {
                if (cam != null && elapsed >= nextHop)
                {
                    // Alternate a near pass and a far pass around the map: worst case for ground cover and props.
                    float a = hop * 2.399963f; // golden angle spreads the stops
                    float r = reach * Mathf.Sqrt((hop % 7 + 1) / 7f);
                    Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    cam.FocusOn(p, hop % 2 == 0 ? 9f : 22f);
                    hop++;
                    nextHop += CameraHop;
                }
                yield return null;
                float dt = Time.unscaledDeltaTime;
                frames.Add(dt * 1000f);
                elapsed += dt;
                if (dt * 1000f > 33.4f)
                {
                    int gc = GC.CollectionCount(0);
                    spikes.Add(new Spike { atSeconds = elapsed, ms = dt * 1000f, saveMs = GameLoop.LastSaveMs, saveInFrame = GameLoop.LastSaveFrame >= Time.frameCount - 1, gcCount = gc - gcBefore, breakdown = Breakdown() });
                    gcBefore = gc;
                }
            }

            spikes.Sort((a, b) => b.ms.CompareTo(a.ms));
            if (spikes.Count > 8) spikes.RemoveRange(8, spikes.Count - 8);
            result.spikes = spikes;
            foreach (var s in spikes)
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Benchmark] {0} spike {1:F0} ms at {2:F1}s (save in frame {3}, last save {4:F0} ms, gen0 GCs {5}) {6}",
                    result.world, s.ms, s.atSeconds, s.saveInFrame, s.saveMs, s.gcCount, s.breakdown));
            frames.Sort();
            int n = frames.Count;
            float sum = 0f;
            int over33 = 0, over16 = 0;
            for (int i = 0; i < n; i++)
            {
                sum += frames[i];
                if (frames[i] > 33.4f) over33++;
                if (frames[i] > 16.8f) over16++;
            }
            result.seconds = elapsed;
            result.frames = n;
            result.avgFps = n > 0 ? 1000f * n / sum : 0f;
            result.p50Ms = Pct(frames, 0.50f);
            result.p95Ms = Pct(frames, 0.95f);
            result.p99Ms = Pct(frames, 0.99f);
            result.maxMs = n > 0 ? frames[n - 1] : 0f;
            result.pctOver33Ms = n > 0 ? 100f * over33 / n : 0f;
            result.pctOver16Ms = n > 0 ? 100f * over16 / n : 0f;
            result.monoUsedMb = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            result.totalAllocatedMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            result.robots = FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None).Length;
            result.fauna = FindObjectsByType<DustStalkerAgent>(FindObjectsSortMode.None).Length;
            result.errors = ErrorLog.Count - errorsBefore;
            if (result.p50Ms < NotDrawnMs)
            {
                // No real frame of this game renders in under a millisecond; the OS was not presenting
                // the window (locked screen, minimised, covered on some drivers). Say so rather than
                // publish a fantasy frame rate.
                result.drawn = false;
                Debug.LogWarning("[Benchmark] " + result.world + ": frames under 1 ms — the window was not being drawn (locked or minimised). " +
                                 "Errors still count; rerun with the desktop unlocked for real frame rates.");
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Benchmark] {0}: {1:F1} fps avg · p50 {2:F1} ms · p99 {3:F1} ms · max {4:F0} ms · {5:F1}% > 33 ms · {6} errors",
                result.world, result.avgFps, result.p50Ms, result.p99Ms, result.maxMs, result.pctOver33Ms, result.errors));
        }

        /// <summary>Time per marker in the frame that just ended, which is the slow one being reported.</summary>
        private string Breakdown()
        {
            if (_recorders.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < _recorders.Count; i++)
            {
                var r = _recorders[i];
                if (!r.Valid || r.Count == 0) continue;
                double ms = r.LastValue / 1e6;
                if (ms >= 2.0) sb.Append(SpikeMarkers[i]).Append('=').Append(ms.ToString("F0", CultureInfo.InvariantCulture)).Append(' ');
            }
            return sb.ToString().Trim();
        }

        private void OnDestroy()
        {
            foreach (var r in _recorders) r.Dispose();
            _recorders.Clear();
        }

        private static void ApplyRequestedResolution()
        {
            int w = (int)ParseFloat(ArgValue("-screen-width"), 0f);
            int h = (int)ParseFloat(ArgValue("-screen-height"), 0f);
            if (w <= 0 || h <= 0) return;
            bool full = ArgValue("-screen-fullscreen") == "1";
            Screen.SetResolution(w, h, full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        private static float Pct(List<float> sorted, float q)
        {
            if (sorted.Count == 0) return 0f;
            int i = Mathf.Clamp(Mathf.CeilToInt(q * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[i];
        }

        private void WriteReport()
        {
            var report = new Report
            {
                version = Application.version,
                unity = Application.unityVersion,
                os = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType,
                cpuCores = SystemInfo.processorCount,
                gpu = SystemInfo.graphicsDeviceName,
                gpuMemoryMb = SystemInfo.graphicsMemorySize,
                ramMb = SystemInfo.systemMemorySize,
                resolution = $"{Screen.width}x{Screen.height}",
                quality = QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "",
                startedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                passed = _results.Count > 0 && _results.TrueForAll(r => r.errors == 0 && string.IsNullOrEmpty(r.note)),
                worlds = _results
            };
            try
            {
                File.WriteAllText(_outPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogError("[Benchmark] Could not write report: " + e.Message);
            }
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        private static bool HasArg(string name)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static float ParseFloat(string s, float fallback) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }
}
