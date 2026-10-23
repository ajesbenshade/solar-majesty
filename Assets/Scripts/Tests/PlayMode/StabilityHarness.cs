using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Shared plumbing for the soak, round-trip and perf PlayMode tests: temp save/log roots, the
    /// validation-profile gate, booting a world, a starter colony, fast-forwarding the sim with a
    /// fixed frame step, and watching the log for errors from any thread.
    /// </summary>
    internal static class StabilityHarness
    {
        public const string Scene = "LunarOutpost_Sandbox";

        public static readonly CelestialBodyId[] Worlds =
        {
            CelestialBodyId.Earth, CelestialBodyId.Luna, CelestialBodyId.Mars, CelestialBodyId.Belt, CelestialBodyId.Europa
        };

        public static void RequireValidationProfile()
        {
            if (Application.companyName != "SolarMajestyValidation" && !ValidationGate.IsCi)
                Assert.Ignore("Requires the isolated validation profile (or CI=true); resets that profile's campaign.");
        }

        public static float EnvFloat(string name, float fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }

        public static int EnvInt(string name, int fallback) => Mathf.RoundToInt(EnvFloat(name, fallback));

        /// <summary>Saves, telemetry and the session error log all go to one temp folder.</summary>
        public sealed class TempRoots
        {
            private string _root;
            private float _captureDelta;
            private float _timeScale;
            private bool _ignoreFailing;

            public string Root => _root;

            public void SetUp()
            {
                _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sm-stability-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(_root);
                SaveSystem.DirectoryOverride = _root;
                PlaytestTelemetry.DirectoryOverride = System.IO.Path.Combine(_root, "Playtest");
                ErrorLog.DirectoryOverride = System.IO.Path.Combine(_root, "Logs");
                DeveloperPrefsShield.Capture();
                _captureDelta = Time.captureDeltaTime;
                _timeScale = Time.timeScale;
                // The tests gather every error and fail once with a summary instead of on the first line.
                _ignoreFailing = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
            }

            public void TearDown()
            {
                LogAssert.ignoreFailingMessages = _ignoreFailing;
                Time.captureDeltaTime = _captureDelta;
                Time.timeScale = _timeScale;
                DeveloperPrefsShield.Restore();
                SaveSystem.DirectoryOverride = null;
                PlaytestTelemetry.DirectoryOverride = null;
                ErrorLog.DirectoryOverride = null;
                try
                {
                    if (!string.IsNullOrEmpty(_root) && System.IO.Directory.Exists(_root))
                        System.IO.Directory.Delete(_root, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Stability] Could not delete {_root}: {e.Message}");
                }
            }
        }

        /// <summary>Errors, exceptions and asserts from any thread, plus a warning count.</summary>
        public sealed class LogWatch : IDisposable
        {
            private readonly object _gate = new object();
            private readonly List<string> _errors = new List<string>();
            private readonly Dictionary<string, int> _seen = new Dictionary<string, int>(StringComparer.Ordinal);
            private int _warnings;
            private bool _attached;

            public LogWatch()
            {
                Application.logMessageReceivedThreaded += OnLog;
                _attached = true;
            }

            public int ErrorCount
            {
                get { lock (_gate) return _errors.Count; }
            }

            public int WarningCount
            {
                get { lock (_gate) return _warnings; }
            }

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                lock (_gate)
                {
                    if (type == LogType.Warning)
                    {
                        _warnings++;
                        return;
                    }
                    if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                    string key = type + ": " + condition;
                    _seen.TryGetValue(key, out int n);
                    _seen[key] = n + 1;
                    if (n == 0)
                    {
                        string firstFrame = FirstLine(stackTrace);
                        _errors.Add(string.IsNullOrEmpty(firstFrame) ? key : key + "\n      at " + firstFrame);
                    }
                }
            }

            /// <summary>Distinct error lines with repeat counts, or empty.</summary>
            public string Summary(int max = 25)
            {
                lock (_gate)
                {
                    if (_errors.Count == 0) return "";
                    var sb = new StringBuilder();
                    for (int i = 0; i < _errors.Count && i < max; i++)
                    {
                        string key = _errors[i];
                        int nl = key.IndexOf('\n');
                        int count = _seen[nl >= 0 ? key.Substring(0, nl) : key];
                        sb.Append("  - ").Append(key);
                        if (count > 1) sb.Append("  (x").Append(count).Append(')');
                        sb.Append('\n');
                    }
                    if (_errors.Count > max) sb.Append("  … ").Append(_errors.Count - max).Append(" more distinct errors\n");
                    return sb.ToString();
                }
            }

            public void Dispose()
            {
                if (!_attached) return;
                _attached = false;
                Application.logMessageReceivedThreaded -= OnLog;
            }

            private static string FirstLine(string text)
            {
                if (string.IsNullOrEmpty(text)) return "";
                int nl = text.IndexOf('\n');
                return (nl >= 0 ? text.Substring(0, nl) : text).Trim();
            }
        }

        /// <summary>Coroutine result holder (Unity coroutines cannot return values).</summary>
        public sealed class Booted
        {
            public GameLoop Loop;
        }

        /// <summary>
        /// Fresh campaign with every world unlocked, boot into Earth, then hop to <paramref name="body"/>.
        /// Leaves the world playing (not paused).
        /// </summary>
        public static IEnumerator BootWorld(CelestialBodyId body, Booted result)
        {
            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);
            CampaignProgress.UnlockThrough(CelestialBodyId.Europa);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync(Scene);
            yield return null;
            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.IsNotNull(loop, "GameLoop missing after boot");
            if (body != BodySeed.Body)
            {
                var old = loop;
                loop.SelectBody(body);
                loop = null;
                for (int frame = 0; frame < 600 && loop == null; frame++)
                {
                    yield return null;
                    var found = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                    if (found != null && found != old && BodySeed.Body == body && found.Settlement != null)
                        loop = found;
                }
                Assert.IsNotNull(loop, $"{body}: no GameLoop after the world hop");
                yield return null;
            }
            Assert.AreEqual(body, BodySeed.Body);
            Assert.IsTrue(loop.Settlement != null && loop.Settlement.HasCommons, $"{body} must be playable on arrival");
            result.Loop = loop;
        }

        /// <summary>
        /// Arrival worlds have a Commons and no robots. Stand up an Engineer and a Defense workshop,
        /// an inn and a market the way the village raises buildings, fabricate their robots, and
        /// fund the treasury so the economy has something to do. Returns how many robots exist.
        /// </summary>
        public static int EnsureStarterColony(GameLoop loop, int metals = 1500)
        {
            loop.Resources.Set(ResourceId.Metals, Mathf.Max(metals, loop.Resources.Get(ResourceId.Metals)));
            if (CountLiving(loop) >= 2) return CountLiving(loop);
            var wanted = new[]
            {
                BuildingCategory.EngineerWorkshop, BuildingCategory.DefenseWorkshop,
                BuildingCategory.ScoutWorkshop, BuildingCategory.Inn, BuildingCategory.Market
            };
            int salt = 11;
            foreach (var cat in wanted)
            {
                var data = loop.VillageData(cat);
                if (data == null) continue;
                if (!loop.TryFindVillagePlot(data, salt, out var cell, out var world))
                {
                    salt += 3;
                    continue;
                }
                salt += 3;
                if (!loop.RaiseVillageBuilding(data, cell)) continue;
                var st = loop.Village != null ? loop.Village.FindNear(world, 4f) : null;
                if (st != null && st.IsWorkshop)
                    loop.TryFabricateRobot(st, announce: false, restoreVeteran: false);
            }
            return CountLiving(loop);
        }

        public static int CountLiving(GameLoop loop)
        {
            int n = 0;
            var agents = loop.Agents;
            for (int i = 0; i < agents.Count; i++)
                if (agents[i] != null && agents[i].IsAlive) n++;
            return n;
        }

        /// <summary>
        /// Run the world at the fastest player speed with a fixed frame step, so sim time per frame is
        /// identical on any machine. <c>SM_SOAK_TIMESCALE</c> overrides the time scale.
        /// </summary>
        public static void StartFastForward(GameLoop loop, float frameStep = 0.05f)
        {
            if (!loop.IsPlaying) loop.ResumePlay();
            SimSpeed.Set(SimSpeed.Multipliers.Length - 1);
            loop.ApplySimSpeed();
            Time.captureDeltaTime = frameStep;
            Time.timeScale = DesiredTimeScale();
        }

        public static float DesiredTimeScale() =>
            Mathf.Max(0.1f, EnvFloat("SM_SOAK_TIMESCALE", SimSpeed.Multipliers[SimSpeed.Multipliers.Length - 1] * SimSpeed.BasePace));

        public static void StopFastForward(GameLoop loop)
        {
            Time.captureDeltaTime = 0f;
            if (loop != null && loop.IsPlaying) loop.PausePlay();
        }

        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary>Robots, fauna and the camera; returns a description of each non-finite pose.</summary>
        public static List<string> NonFinitePoses(GameLoop loop)
        {
            var bad = new List<string>();
            var agents = loop.Agents;
            for (int i = 0; i < agents.Count; i++)
                if (agents[i] != null && !IsFinite(agents[i].transform.position))
                    bad.Add($"robot {agents[i].name} at {agents[i].transform.position}");
            var stalkers = loop.Stalkers;
            for (int i = 0; i < stalkers.Count; i++)
                if (stalkers[i] != null && !IsFinite(stalkers[i].transform.position))
                    bad.Add($"fauna {stalkers[i].name} at {stalkers[i].transform.position}");
            var cam = Camera.main;
            if (cam != null && !IsFinite(cam.transform.position))
                bad.Add($"camera at {cam.transform.position}");
            return bad;
        }

        public static int CountGameObjects() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        /// <summary>
        /// Flags a robot as stuck only when it has a live nav path more than 2 m long, is not resting,
        /// down, or scrapped, and has stayed within 1 m of the same spot on every sample for
        /// <see cref="StuckSeconds"/> of sim time. Standing still at work or at the plaza is fine: those
        /// stop the agent and clear its path.
        /// </summary>
        public sealed class StuckWatch
        {
            private sealed class Track
            {
                public Vector3 Anchor;
                public float TravelSince = -1f;
                public bool Reported;
            }

            private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
            public readonly List<string> Stuck = new List<string>();
            public float StuckSeconds;
            public int OffMeshSamples;

            public StuckWatch(float stuckSeconds)
            {
                StuckSeconds = stuckSeconds;
            }

            public void Sample(GameLoop loop, float simTime)
            {
                var agents = loop.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    if (a == null) continue;
                    int id = a.GetEntityId().GetHashCode();
                    Vector3 pos = a.transform.position;
                    if (!_tracks.TryGetValue(id, out var t))
                    {
                        _tracks[id] = new Track { Anchor = pos };
                        continue;
                    }
                    var nav = a.GetComponent<NavMeshAgent>();
                    if (nav != null && nav.enabled && !nav.isOnNavMesh) OffMeshSamples++;
                    bool travelling = a.IsAlive && !a.IsIncapacitated && !a.IsScrapped
                                      && a.CurrentAction != SpecialistAction.Rest
                                      && nav != null && nav.enabled && nav.isOnNavMesh && !nav.isStopped
                                      && (nav.pathPending || (nav.hasPath && nav.remainingDistance > 2f));
                    float dx = pos.x - t.Anchor.x, dz = pos.z - t.Anchor.z;
                    if (dx * dx + dz * dz > 1f)
                    {
                        t.Anchor = pos;
                        t.TravelSince = travelling ? simTime : -1f;
                    }
                    else if (!travelling)
                        t.TravelSince = -1f;
                    else if (t.TravelSince < 0f)
                        t.TravelSince = simTime;
                    else if (!t.Reported && simTime - t.TravelSince >= StuckSeconds)
                    {
                        t.Reported = true;
                        Stuck.Add($"{a.name} ({a.Data?.specialistClass}) stuck {simTime - t.TravelSince:F0}s at {pos} " +
                                  $"action={a.CurrentAction} status={a.Status} dest={nav.destination} remaining={nav.remainingDistance:F1}");
                    }
                }
            }
        }
    }
}
