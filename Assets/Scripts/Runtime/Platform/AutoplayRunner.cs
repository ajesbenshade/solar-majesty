using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// <c>-autoplay</c>: plays the whole campaign, Earth to Europa, the way a player would, and fails
    /// loudly if the game throws, logs an error, stops making progress, or runs out of time.
    ///
    /// Everything goes through the player's own controls: buildings are committed through the same
    /// path as a click on a valid ghost (<see cref="BuildingPlacementInput.TryPlaceAt"/>), bounties
    /// through the flag tool (<see cref="FlagPlacementInput.PostFlagAt"/> and its +/− nudge),
    /// research through the tech panel's <see cref="ResearchManager.TrySelect"/>, and the
    /// pause / settings / title / save / load / continue / victory buttons call the methods those
    /// buttons call. Nothing is granted: no resources, no unlocks, no cleared dens. The only dev
    /// help is <see cref="SimSpeed.HarnessBoost"/>, which runs the world faster than 4×.
    ///
    /// On every world it also pauses and opens Settings, saves to a slot and loads it back, and goes
    /// to the title and Continues, checking the colony comes back the same each time. With
    /// <c>-autoplayRelaunchOn Mars</c> it quits the game mid-world (the QUIT button) and expects to be
    /// started again with <c>-autoplayResume</c>, which Continues from the title.
    ///
    /// It starts a New Game, so like the benchmark it refuses to run without <c>-saveDir</c> and
    /// <c>-profileDir</c>. Tools/qa/run_autoplay.ps1 sets everything up and reads the summary.
    ///
    ///   SolarMajesty.exe -autoplay -saveDir X -profileDir Y -nosteam -noai
    ///       [-autoplayOut summary.json] [-autoplayBoost 6] [-autoplayTimeout 150] [-autoplayStall 15]
    ///       [-autoplayRelaunchOn Mars] [-autoplayResume] [-autoplayFailFast]
    /// </summary>
    public sealed class AutoplayRunner : MonoBehaviour
    {
        public const string Arg = "-autoplay";
        private const string Tag = "[Autoplay] ";

        /// <summary>The fastest player speed (4×). The harness boost multiplies on top of it.</summary>
        private const int TopSpeedIndex = 8;
        private const float DecideEvery = 1.5f;
        private const float StatusEvery = 60f;
        private const int SaveSlot = 2;

        // ---------------------------------------------------------------- options
        private float _boost = 6f;
        private float _timeoutMinutes = 150f;
        private float _stallMinutes = 15f;
        private string _outPath;
        private string _statePath;
        private CelestialBodyId? _relaunchOn;
        private bool _resume;
        private bool _failFast;

        // ---------------------------------------------------------------- run state
        private RunState _state = new RunState();
        private BodyRun _body;
        private readonly object _gate = new object();
        private readonly List<string> _errors = new List<string>();
        private int _errorCount;
        private string _failure;
        private bool _finished;
        private float _realStart;
        private GameLoop _loop;

        [Serializable]
        private sealed class RunState
        {
            public string version;
            public string startedUtc;
            public float realSecondsBefore;
            public int relaunches;
            public bool relaunchPending;
            public Snapshot beforeRelaunch;
            public List<BodyRun> bodies = new List<BodyRun>();
        }

        [Serializable]
        private sealed class BodyRun
        {
            public string body;
            public float simSeconds;
            public float realSeconds;
            public float winSimSeconds;
            public int restarts;
            public bool settingsChecked;
            public bool saveLoadChecked;
            public bool titleContinueChecked;
            public bool relaunchChecked;
            public bool won;
            public int buildingsPlaced;
            public int bountiesPosted;
            public int techsStarted;
            public int densCleared;
            public int robotsLost;
            public int refabs;
            public string winHeadline;
            /// <summary>The world's summary-screen tallies and verdict at the win.</summary>
            public string tallies;
            public List<string> milestones = new List<string>();
        }

        [Serializable]
        private sealed class Summary
        {
            public bool passed;
            public string failure;
            public string version;
            public string startedUtc;
            public float realSeconds;
            public int relaunches;
            public int errorCount;
            public List<string> errors;
            /// <summary>The game's own persistent error log for this session (ErrorLog), with full stacks.</summary>
            public string errorLogFile;
            public List<BodyRun> bodies;
            public bool conqueredEveryWorld;
            public bool solarConquestAchievement;
            /// <summary>Every achievement held by the throwaway profile at the end.</summary>
            public List<string> achievements;
            public string verdict;
        }

        /// <summary>What must survive a save and load. Compared field by field after every reload.</summary>
        [Serializable]
        private sealed class Snapshot
        {
            public string body;
            public int seed;
            public int metals;
            public int ice;
            public string buildings;
            public string techs;
            public int activeTech;
            public int lairsCleared;
            public int lairsScouted;
            public int flags;
            public int robots;
            public int hiresWaiting;
            public int refabs;
            public bool launchReady;
            public int conquered;
            public int fauna;
        }

        // ---------------------------------------------------------------- boot

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!HasArg(Arg)) return;
            if (FindAnyObjectByType<AutoplayRunner>() != null) return;
            var go = new GameObject("[Autoplay]");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoplayRunner>();
        }

        private void Awake()
        {
            Application.logMessageReceivedThreaded += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            SimSpeed.HarnessBoost = 1f;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            lock (_gate)
            {
                _errorCount++;
                if (_errors.Count < 40)
                {
                    string where = string.IsNullOrEmpty(stackTrace) ? "" : " @ " + FirstLine(stackTrace);
                    _errors.Add($"{type}: {condition}{where}");
                }
            }
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(SaveSystem.DirectoryOverride) || !GamePrefs.IsIsolated)
            {
                Debug.LogError(Tag + "Refusing to run without -saveDir and -profileDir: it starts a New Game " +
                               "and would overwrite the player's campaign. Use Tools/qa/run_autoplay.ps1.");
                Quit(2);
                yield break;
            }

            _realStart = Time.realtimeSinceStartup;
            _boost = ParseFloat(ArgValue("-autoplayBoost"), _boost);
            _timeoutMinutes = ParseFloat(ArgValue("-autoplayTimeout"), _timeoutMinutes);
            _stallMinutes = ParseFloat(ArgValue("-autoplayStall"), _stallMinutes);
            _resume = HasArg("-autoplayResume");
            _failFast = HasArg("-autoplayFailFast");
            _quality = (int)ParseFloat(ArgValue("-autoplayQuality"), -1f);
            if (Enum.TryParse(ArgValue("-autoplayFrom") ?? "", true, out CelestialBodyId from) && CampaignProgress.IsOnSpine(from))
                _from = from;
            if (Enum.TryParse(ArgValue("-autoplayRelaunchOn") ?? "", true, out CelestialBodyId relaunch))
                _relaunchOn = relaunch;
            string saveRoot = SaveSystem.DirectoryOverride;
            _statePath = Path.Combine(saveRoot, "autoplay-state.json");
            _outPath = ArgValue("-autoplayOut") ?? Path.Combine(saveRoot, "autoplay-summary.json");

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            SimSpeed.HarnessBoost = _boost;

            if (_resume && File.Exists(_statePath))
            {
                try { _state = JsonUtility.FromJson<RunState>(File.ReadAllText(_statePath)) ?? new RunState(); }
                catch (Exception e) { Fail("could not read the relaunch state: " + e.Message); }
                _state.relaunches++;
            }
            else
            {
                _state = new RunState
                {
                    version = Application.version,
                    startedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
            }

            Debug.Log(Tag + $"start · boost {_boost:0.#}× on top of 4× · timeout {_timeoutMinutes:0} min · " +
                      $"stall {_stallMinutes:0} sim-min" + (_resume ? " · resumed after relaunch" : "") +
                      (_relaunchOn.HasValue ? $" · relaunch on {_relaunchOn}" : ""));

            yield return WaitFor(() => Loop != null && Loop.Screen == DemoScreen.Title, 120f, "the title screen");
            if (_failure != null) { Finish(); yield break; }
            yield return WaitReal(1f);

            if (_resume)
            {
                if (!_state.relaunchPending)
                    Fail("resumed without a pending relaunch in " + _statePath);
                else if (!DemoSettings.SaveExists)
                    Fail("after relaunch the title has no Continue (no save on disk)");
                if (_failure != null) { Finish(); yield break; }
                _state.relaunchPending = false;
                Milestone("relaunched: title → Continue");
                Loop.ContinueGame(); // the CONTINUE button
            }
            else if (_from != CampaignProgress.NewGameBody)
            {
                // Title: a new campaign dropped on a later world (what the orrery offers once the
                // inner worlds are open). For iterating on one world; a release check starts on Earth.
                Milestone($"title → New Campaign on {_from} (slot 1)");
                Loop.StartNewGameOn(_from, 1);
            }
            else
            {
                Milestone("title → New Campaign (slot 1)");
                Loop.StartNewGame(CampaignProgress.NewGameBody, 1); // NEW CAMPAIGN → START IN SLOT 1
            }

            while (_failure == null && !_finished)
            {
                yield return WaitFor(() => Loop != null && Loop.IsPlaying, 120f, "the colony to load");
                if (_failure != null) break;

                if (_resume && _state.beforeRelaunch != null)
                {
                    Compare(_state.beforeRelaunch, Capture(Loop), "relaunch + Continue");
                    _state.beforeRelaunch = null;
                    _body = FindBodyRun(Loop.ActiveBody);
                    if (_body != null) _body.relaunchChecked = true;
                    _resume = false;
                }

                yield return PlayBody();
            }

            Finish();
        }

        // ---------------------------------------------------------------- one world

        private IEnumerator PlayBody()
        {
            var loop = Loop;
            CelestialBodyId bodyId = loop.ActiveBody;
            _body = FindBodyRun(bodyId);
            // The decision clocks and memories run on this world's sim time and positions.
            _lastBuildAt = -99f;
            _flagPostedAt.Clear();
            _refusedSpots.Clear();
            _claimed.Clear();
            _pestSince.Clear();
            if (_body == null)
            {
                _body = new BodyRun { body = bodyId.ToString() };
                _state.bodies.Add(_body);
                Milestone($"arrived on {bodyId} (seed {BodySeed.Current}, treasury {Treasury(loop)} EU, " +
                          $"goal {loop.Mission?.TreasuryGoal ?? 0} EU, {loop.World?.Lairs.Count ?? 0} dens)");
            }

            float lastDecide = -99f, lastStatus = 0f, stallClock = 0f;
            float best = -1f;
            int lastCleared = ClearedLairs(loop), lastTechs = loop.Research?.UnlockedCount ?? 0, lastGates = 0;
            bool lastDens = false;
            int lastRobots = Robots(loop);
            float statusReal = Time.realtimeSinceStartup;
            int frames = 0;
            float saveLoadAfter = 0f;

            while (_failure == null)
            {
                loop = Loop;
                if (loop == null || loop.ActiveBody != bodyId) { yield return null; continue; }
                CheckTimeout();
                if (_failFast && _errorCount > 0) Fail("error logged (fail-fast)");
                if (_failure != null) yield break;

                if (loop.Screen != DemoScreen.Playing)
                {
                    yield return null;
                    continue;
                }

                var mission = loop.Mission;
                if (mission != null && mission.IsWon)
                    break;

                EnsureRunning(loop);
                DismissCutscenes(loop);

                float dt = Time.deltaTime; // scaled: sim seconds
                _body.simSeconds += dt;
                _body.realSeconds += Time.unscaledDeltaTime;
                stallClock += dt;

                if (mission != null && mission.IsLost)
                {
                    _body.restarts++;
                    Milestone($"OUTPOST LOST ({mission.FailDetail}) — RESTART MISSION #{_body.restarts}");
                    if (_body.restarts > 2) { Fail($"{bodyId} lost three times"); yield break; }
                    loop.RestartMission();
                    yield return WaitFor(() => Loop != null && Loop.IsPlaying && Loop.Mission != null && !Loop.Mission.IsLost,
                        120f, "the restarted mission");
                    stallClock = 0f;
                    best = -1f;
                    continue;
                }

                if (loop.NeedsFieldRevive && loop.CanPayYard)
                {
                    Milestone("field party down — FOBOT YARD");
                    loop.PayFobotYard();
                }

                if (_body.simSeconds - lastDecide >= DecideEvery)
                {
                    lastDecide = _body.simSeconds;
                    Decide(loop, bodyId);
                }

                // Milestones the player would notice.
                int cleared = ClearedLairs(loop);
                if (cleared > lastCleared)
                {
                    _body.densCleared += cleared - lastCleared;
                    Milestone($"den cleared ({cleared}/{loop.World?.Lairs.Count ?? 0})");
                }
                lastCleared = cleared;
                int techs = loop.Research?.UnlockedCount ?? 0;
                if (techs > lastTechs) Milestone($"research done ({techs} techs)");
                lastTechs = techs;
                int gates = mission != null ? mission.GatesComplete : 0;
                if (gates != lastGates && mission != null)
                    Milestone($"gates {gates}/3 — dens {(mission.DensCleared ? "✓" : "·")} sustain {(mission.SustainComplete ? "✓" : "·")} launch {(mission.LaunchReady ? "✓" : "·")}");
                lastGates = gates;
                // The dens gate only closes again if something that counts toward it comes back
                // after the last den: a regression of the town-burrow flicker (DensGate).
                bool densNow = mission != null && mission.DensCleared;
                if (lastDens && !densNow && mission != null && mission.UnclearedLairs <= 0)
                    Debug.LogWarning(Tag + $"dens gate reopened after the last den ({mission.StalkersRemaining} den fauna alive)");
                lastDens = densNow;
                int robots = Robots(loop);
                if (robots < lastRobots) _body.robotsLost += lastRobots - robots;
                lastRobots = robots;

                frames++;
                if (_body.simSeconds - lastStatus >= StatusEvery)
                {
                    float realNow = Time.realtimeSinceStartup;
                    float span = Mathf.Max(0.001f, realNow - statusReal);
                    Debug.Log(Tag + Status(loop) + string.Format(CultureInfo.InvariantCulture,
                        " · {0:F0} fps, {1:F1}× real time", frames / span, (_body.simSeconds - lastStatus) / span));
                    Debug.Log(Tag + "  " + Crew(loop));
                    lastStatus = _body.simSeconds;
                    statusReal = realNow;
                    frames = 0;
                }

                // Stall: no forward motion on any front for too long.
                float score = ProgressScore(loop);
                if (score > best + 0.01f)
                {
                    best = score;
                    stallClock = 0f;
                }
                else if (stallClock > _stallMinutes * 60f)
                {
                    Fail($"stalled on {bodyId}: no progress for {_stallMinutes:0} sim-minutes. {Status(loop)} {Crew(loop)}");
                    yield break;
                }

                // The menus and the save round trips, once per world, while the colony is mid-game.
                if (!_body.settingsChecked && _body.simSeconds > 45f)
                {
                    yield return CheckPauseAndSettings();
                    continue;
                }
                if (!_body.saveLoadChecked && _body.simSeconds > 120f && Placed(loop) >= 3)
                {
                    yield return CheckSaveLoad(bodyId);
                    saveLoadAfter = _body.simSeconds;
                    stallClock = 0f;
                    continue;
                }
                if (_body.saveLoadChecked && !_body.titleContinueChecked && _body.simSeconds > saveLoadAfter + 60f)
                {
                    yield return CheckTitleContinue(bodyId);
                    stallClock = 0f;
                    continue;
                }
                if (_relaunchOn == bodyId && !_body.relaunchChecked && _body.titleContinueChecked &&
                    _state.relaunches == 0)
                {
                    yield return QuitForRelaunch();
                    yield break;
                }

                yield return null;
            }

            if (_failure != null) yield break;
            yield return Victory(bodyId);
        }

        private IEnumerator Victory(CelestialBodyId bodyId)
        {
            var loop = Loop;
            var mission = loop.Mission;
            _body.won = true;
            _body.winSimSeconds = _body.simSeconds;
            _body.winHeadline = mission.WinHeadline;
            Milestone($"VICTORY on {bodyId} — \"{mission.WinHeadline}\" after {_body.simSeconds / 60f:F1} sim-min " +
                      $"({_body.realSeconds / 60f:F1} real-min), rating {loop.CurrentRating.Letter}");
            var rows = new List<string>();
            foreach (var row in loop.Stats.SummaryRows()) rows.Add($"{row.Label} {row.Value}");
            _body.tallies = string.Join(" · ", rows) + " · \"" + loop.Stats.PlaystyleVerdict() + "\"";
            Milestone("tallies: " + _body.tallies);

            if (!CampaignProgress.HasConquered(bodyId))
                Fail($"{bodyId} won but not marked conquered");
            yield return WaitReal(1.5f);

            if (DemoEndOffer.Applies(bodyId, true, ReplayRules.IsEndless))
            {
                yield return DemoEnd(bodyId);
                yield break;
            }

            var next = CampaignProgress.NextAfter(bodyId);
            var offer = VictoryOffer.For(bodyId, VictoryOffer.NextIsBuilt(bodyId), ReplayRules.IsEndless);
            if (next.HasValue)
            {
                if (!offer.CanContinue)
                {
                    Fail($"victory on {bodyId} offers no way on to {next.Value}");
                    yield break;
                }
                if (!CampaignProgress.IsUnlocked(next.Value))
                    Fail($"victory on {bodyId} did not unlock {next.Value}");
                Milestone($"victory → \"{offer.ContinueLabel}\" (launch)");
                // The Continue to … button. Wait on the next world, not on time: a victory camera
                // beat can hold the panel back and a launch shot can play before the load. A press
                // the game ignored (panel not up yet) is pressed again; one it took is not.
                var won = Loop;
                yield return PressUntil(() => won.ContinueAfterVictory(),
                    () => Loop != null && Loop.IsPlaying && Loop.ActiveBody == next.Value,
                    () => won != null && won.ActiveBody == bodyId && won.Mission != null && won.Mission.IsWon,
                    120f, $"arrival on {next.Value}");
                yield break;
            }

            // The end of the campaign.
            if (mission.WinHeadline != Loc.S("SOLAR CONQUEST COMPLETE"))
                Fail("last world's headline is \"" + mission.WinHeadline + "\"");
            if (!CampaignProgress.ConqueredEveryWorld)
                Fail("campaign over but not every world is marked conquered (mask " + CampaignProgress.ConqueredMask + ")");
            yield return WaitReal(2.5f); // achievements are evaluated every two seconds of play
            if (!Achievements.IsUnlocked(AchievementId.SolarConquest))
                Fail("Solar Conquest achievement not earned at the end of the campaign");
            if (offer.CanContinue)
                Fail("the last world offers a launch to nowhere");
            Milestone("campaign complete — victory → Main Menu");
            var last = Loop;
            yield return PressUntil(() => last.ExitVictoryToTitle(), // the Main Menu button
                () => Loop != null && Loop.Screen == DemoScreen.Title,
                () => last != null && last.IsPlaying,
                60f, "the title after the campaign");
            yield return WaitReal(1f);
            // The finished campaign still Continues, into its won world.
            Loop.ContinueGame();
            yield return WaitFor(() => Loop != null && Loop.IsPlaying, 60f, "Continue after the campaign");
            if (_failure == null && (Loop.ActiveBody != bodyId || Loop.Mission == null || !Loop.Mission.IsWon))
                Fail($"Continue after the campaign landed on {Loop.ActiveBody}, won={Loop.Mission?.IsWon}");
            else
                Milestone("Continue after the campaign lands on the won world");
            yield return WaitReal(2f);
            _finished = true;
        }

        /// <summary>
        /// Steam demo: Earth's win must end on the demo screen, not a launch. Checks nothing past
        /// Earth opened, captures the screen next to the report, then presses KEEP PLAYING EARTH
        /// and checks the colony runs on.
        /// </summary>
        private IEnumerator DemoEnd(CelestialBodyId bodyId)
        {
            var loop = Loop;
            Milestone("demo build: Earth won → expecting the end-of-demo screen");
            var next = CampaignProgress.NextAfter(bodyId);
            if (next.HasValue && CampaignProgress.IsUnlocked(next.Value))
                Fail($"demo victory unlocked {next.Value}");
            if (VictoryOffer.NextIsBuilt(bodyId))
                Fail("demo victory offers a launch");
            yield return WaitFor(() => Loop == loop && loop.VictoryHeld && !CameraBeatDirector.Active, 60f,
                "the end-of-demo screen");
            if (_failure != null) yield break;
            yield return WaitReal(2.5f); // the HUD draws it; achievements are evaluated every two seconds
            if (Achievements.IsUnlocked(AchievementId.SolarConquest))
                Fail("Solar Conquest unlocked in the demo");

            string dir = Path.GetDirectoryName(_outPath);
            if (!string.IsNullOrEmpty(dir))
            {
                string shot = Path.Combine(dir, Path.GetFileNameWithoutExtension(_outPath) + "-demo-end.png");
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex != null)
                {
                    // With the Windows desktop locked the player renders nothing (OnGUI is not even
                    // called), and the capture is one flat grey. Say so instead of passing it off as evidence.
                    if (IsFlat(tex))
                        Debug.LogWarning(Tag + "end-of-demo screenshot is a blank frame (desktop locked or window " +
                                         "minimized?); not saved. Rerun with the desktop unlocked to see the screen.");
                    else
                    {
                        File.WriteAllBytes(shot, tex.EncodeToPNG());
                        Milestone("end-of-demo screen captured → " + shot);
                    }
                    Destroy(tex);
                }
            }

            Milestone("end-of-demo → \"" + DemoEndOffer.Label(DemoEndOffer.Choice.KeepPlaying) + "\"");
            loop.ContinueAfterDemoEnd(); // the KEEP PLAYING EARTH button
            yield return WaitFor(() => !loop.VictoryHeld && Time.timeScale > 0f, 20f, "the colony to run after the demo end");
            if (_failure != null) yield break;
            float simStart = Time.time;
            yield return WaitFor(() => Time.time - simStart > 20f, 60f, "20 sim-seconds of Earth after the demo end");
            if (_failure == null && (Loop != loop || !loop.IsPlaying || loop.ActiveBody != bodyId))
                Fail($"after KEEP PLAYING the game is on {Loop?.ActiveBody}, playing={Loop?.IsPlaying}");
            else if (_failure == null)
                Milestone("demo complete — Earth keeps running after the end screen");
            _finished = true;
        }

        /// <summary>True when a sampled grid of pixels is all one colour (nothing was rendered).</summary>
        private static bool IsFlat(Texture2D tex)
        {
            Color first = tex.GetPixel(0, 0);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                Color c = tex.GetPixel(x * tex.width / 8 + tex.width / 16, y * tex.height / 8 + tex.height / 16);
                if (Mathf.Abs(c.r - first.r) + Mathf.Abs(c.g - first.g) + Mathf.Abs(c.b - first.b) > 0.02f)
                    return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the player's decisions

        private float _lastBuildAt = -99f;
        private readonly Dictionary<FlagHandle, float> _flagPostedAt = new Dictionary<FlagHandle, float>();

        private void Decide(GameLoop loop, CelestialBodyId body)
        {
            if (loop.IsTutorialActive && Placed(loop) >= 1)
            {
                Milestone("tutorial — SKIP (step " + loop.TutorialStep + ")");
                loop.SkipTutorial(); // the tutorial bar's SKIP
            }
            Refab(loop);
            ChooseResearch(loop, body);
            Build(loop);
            PostBounties(loop);
        }

        /// <summary>A workshop with a wreck to re-fab and no re-fab under way.</summary>
        private static bool WreckWaiting(GameLoop loop)
        {
            var village = loop.Village;
            if (village == null) return false;
            foreach (var st in village.Structures)
            {
                if (st == null || !st.IsAlive || !st.IsWorkshop) continue;
                var cls = ColonyStructure.RobotClassForWorkshop(st.Category);
                if (cls.HasValue && loop.HasWreckFor(cls.Value) && loop.RefabAt(st) == null) return true;
            }
            return false;
        }

        /// <summary>
        /// A scrapped robot leaves a wreck, and the scrap pile raises junk bots every few seconds
        /// until it is gone (Europa: a colony that never re-fabbed lost every robot). The workshop
        /// panel's RE-FAB chip builds a level-1 robot from the wreck; press it as soon as it is
        /// affordable, before anything else is bought.
        /// </summary>
        private void Refab(GameLoop loop)
        {
            var village = loop.Village;
            if (village == null) return;
            foreach (var st in village.Structures)
            {
                if (st == null || !st.IsAlive || !st.IsWorkshop) continue;
                var cls = ColonyStructure.RobotClassForWorkshop(st.Category);
                if (!cls.HasValue || !loop.HasWreckFor(cls.Value) || loop.RefabAt(st) != null) continue;
                int met = OverseerRules.RefabMetals(st.SourceData);
                if (Treasury(loop) < met) continue;
                if (loop.TryRefabRookie(st))
                {
                    _body.refabs++;
                    Milestone($"RE-FAB {ColonyStructure.ClassLabel(cls.Value)} at {st.DisplayName} ({met} EU)");
                }
            }
        }

        private void ChooseResearch(GameLoop loop, CelestialBodyId body)
        {
            var r = loop.Research;
            if (r == null || r.ActiveTech != TechId.None) return;
            TechId pick = TechId.None;
            TechId launch = r.LaunchTechId(body);
            if (launch != TechId.None && !r.IsUnlocked(launch))
                pick = FirstSelectableTowards(r, launch, new HashSet<TechId>());
            if (pick == TechId.None)
            {
                // Then whatever the panel recommends, skipping the costly secret projects. A tech
                // with an EU bill on completion takes that money the moment it is there, so it waits
                // until the treasury has more than this world's goal (bounties come first).
                int goal = loop.Mission != null ? loop.Mission.TreasuryGoal : 5000;
                bool flush = Treasury(loop) > goal;
                foreach (var t in TechCatalog.All)
                {
                    if (t.SecretProject || !r.CanSelect(t.Id)) continue;
                    if (!flush && t.CompleteCost != null && t.CompleteCost.Length > 0) continue;
                    pick = t.Id;
                    break;
                }
            }
            if (pick == TechId.None) return;
            if (r.TrySelect(pick))
            {
                _body.techsStarted++;
                Milestone("research → " + pick);
            }
        }

        private static TechId FirstSelectableTowards(ResearchManager r, TechId goal, HashSet<TechId> seen)
        {
            if (!seen.Add(goal) || r.IsUnlocked(goal)) return TechId.None;
            var def = TechCatalog.Get(goal);
            if (def == null) return TechId.None;
            if (def.Prerequisites != null)
            {
                foreach (var p in def.Prerequisites)
                {
                    var t = FirstSelectableTowards(r, p, seen);
                    if (t != TechId.None) return t;
                }
            }
            return r.CanSelect(goal) ? goal : TechId.None;
        }

        /// <summary>
        /// A sensible build order: a builder, fighters and a scout, research, food, the pad, then
        /// the shops and more fighters. Each entry: category, how many to own, and whether it is
        /// worth spending the bounty money on (the core) or only spare money (the rest).
        /// </summary>
        private static readonly (BuildingCategory cat, int count, bool core)[] Plan =
        {
            (BuildingCategory.EngineerWorkshop, 1, true),
            (BuildingCategory.DefenseWorkshop, 1, true),
            (BuildingCategory.Farm, 1, true),
            (BuildingCategory.DefenseWorkshop, 2, true),
            (BuildingCategory.LandingPad, 1, true),
            (BuildingCategory.Laboratory, 1, false),
            (BuildingCategory.Market, 1, false),
            (BuildingCategory.ScoutWorkshop, 1, false),
            (BuildingCategory.MedicWorkshop, 1, false),
            (BuildingCategory.Watchtower, 1, false),
            (BuildingCategory.SentinelWorkshop, 1, false),
            (BuildingCategory.Blacksmith, 1, false),
            (BuildingCategory.Laboratory, 2, false),
            (BuildingCategory.Farm, 2, false),
            (BuildingCategory.Mine, 1, false),
            (BuildingCategory.DefenseWorkshop, 3, false),
            (BuildingCategory.Defense, 1, false),
        };

        /// <summary>Money a player keeps back for den bounties while dens are still open.</summary>
        private const int BountyFund = 1500;

        private void Build(GameLoop loop)
        {
            var input = loop.BuildInput;
            if (input == null || loop.Grid == null || loop.Placer == null) return;
            if (_body.simSeconds - _lastBuildAt < 4f) return;
            if (WreckWaiting(loop)) return; // the re-fab is bought first
            var mission = loop.Mission;
            int treasury = Treasury(loop);
            int goal = mission != null ? mission.TreasuryGoal : 5000;
            bool densOpen = DensOpen(loop);
            // Once the dens are quiet and the craft is staged, the only thing left is the treasury:
            // stop building and let it fill.
            if (!densOpen && mission != null && mission.LaunchReady && treasury < goal + 1500) return;
            // Save up for the launch tech's completion bill once the tree reaches it.
            int reserve = LaunchBill(loop);

            var visible = input.VisibleIndices;
            foreach (var step in Plan)
            {
                if (Owned(loop, step.cat) >= step.count) continue;
                BuildingData data = null;
                foreach (int i in visible)
                {
                    var d = input.Catalog[i];
                    if (d != null && d.category == step.cat) { data = d; break; }
                }
                if (data == null || !loop.IsBuildingUnlocked(step.cat)) continue; // not on the menu yet
                int cost = Cost(loop, data);
                int keep = step.core ? reserve : reserve + (densOpen ? BountyFund : 0) + 500;
                if (treasury - cost < keep) return; // wait for this one rather than skipping ahead
                if (TryPlace(loop, data, out string why))
                {
                    _lastBuildAt = _body.simSeconds;
                    _body.buildingsPlaced++;
                    Milestone($"build {data.displayName} ({cost} EU)");
                }
                else
                {
                    Debug.Log(Tag + $"could not place {data.displayName}: {why}");
                    _lastBuildAt = _body.simSeconds + 20f; // a player would look for room, then try again later
                }
                return;
            }
        }

        /// <summary>
        /// Dens left to clear, or den fauna the objective still names. Town burrow pests no longer
        /// hold the gate (DensGate), so the gate itself is the answer.
        /// </summary>
        private static bool DensOpen(GameLoop loop)
        {
            var mission = loop.Mission;
            return mission == null || !mission.DensCleared;
        }

        private static int LaunchBill(GameLoop loop)
        {
            var r = loop.Research;
            if (r == null) return 0;
            var launch = r.LaunchTechId(loop.ActiveBody);
            if (launch == TechId.None || r.IsUnlocked(launch)) return 0;
            var def = TechCatalog.Get(launch);
            if (def?.CompleteCost == null) return 0;
            int metals = 0;
            foreach (var c in def.CompleteCost)
                if (c.resource == ResourceId.Metals) metals += c.amount;
            return metals;
        }

        private static int Cost(GameLoop loop, BuildingData data)
        {
            var cost = loop.Placer.CostFor(data);
            int metals = 0;
            if (cost != null)
                foreach (var c in cost)
                    if (c.resource == ResourceId.Metals) metals += c.amount;
            // A workshop is only worth it with the money to hire its robot when it is done.
            var cls = ColonyStructure.RobotClassForWorkshop(data.category);
            if (cls.HasValue) metals += MajestyEconomy.HireCost(cls.Value);
            return metals;
        }

        private bool TryPlace(GameLoop loop, BuildingData data, out string why)
        {
            why = "no open ground";
            var grid = loop.Grid;
            int w = Mathf.Max(1, data.footprintWidth), h = Mathf.Max(1, data.footprintHeight);
            Vector3 half = new Vector3((w - 1) * 0.5f * grid.CellSize, 0f, (h - 1) * 0.5f * grid.CellSize);
            var kind = loop.ZoneRules.KindFor(data.category);
            var zones = loop.BuildZones;
            Vector3 hub = ColonyLayout.CampusOrigin;

            var centres = new List<Vector3>();
            if (kind != BuildZoneKind.None)
            {
                // A zone-bound building goes in the nearest free marked site of its kind.
                var mine = new List<BuildZone>();
                if (zones != null)
                    for (int i = 0; i < zones.Count; i++)
                        if (zones[i].Kind == kind && !loop.IsZoneTaken(i)) mine.Add(zones[i]);
                mine.Sort((a, b) => Flat(a.Center, hub).CompareTo(Flat(b.Center, hub)));
                foreach (var z in mine)
                {
                    centres.Add(z.Center);
                    for (float r = 1.5f; r < z.Radius; r += 1.5f)
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k * Mathf.PI / 4f;
                            centres.Add(z.Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                        }
                }
            }
            else
            {
                // Free buildings: rings round the Commons, leaving a lane of dirt between them and
                // staying out of marked sites so the pad and mines keep their ground.
                for (float r = 10f; r <= 46f; r += 3f)
                {
                    int n = Mathf.Max(8, Mathf.RoundToInt(r * 0.7f));
                    for (int k = 0; k < n; k++)
                    {
                        float a = (k / (float)n) * Mathf.PI * 2f + r * 0.37f;
                        centres.Add(hub + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                    }
                }
            }

            foreach (var c in centres)
            {
                Vector2Int origin = grid.WorldToCell(c - half);
                if (kind == BuildZoneKind.None)
                {
                    if (!loop.Placer.CanFitRect(new Vector2Int(origin.x - 1, origin.y - 1), w + 2, h + 2)) continue;
                    if (InAnyZone(zones, PlacementCursor.Center(origin, w, h, grid.CellSize, grid.Origin), Mathf.Max(w, h) * grid.CellSize)) continue;
                }
                if (loop.PlacementBlockReason(data, origin) != null) continue;
                if (loop.BuildInput.TryPlaceAt(data, origin, out why)) return true;
            }
            return false;
        }

        private static bool InAnyZone(IReadOnlyList<BuildZone> zones, Vector3 p, float pad)
        {
            if (zones == null) return false;
            for (int i = 0; i < zones.Count; i++)
                if (Flat(zones[i].Center, p) < zones[i].Radius + pad * 0.5f) return true;
            return false;
        }

        private void PostBounties(GameLoop loop)
        {
            var input = loop.FlagInput;
            var world = loop.World;
            var flags = loop.Flags;
            if (input == null || world == null || flags == null) return;

            // Forget completed / cancelled poles.
            var gone = new List<FlagHandle>();
            foreach (var kv in _flagPostedAt)
            {
                bool open = false;
                foreach (var f in flags.Flags) if (ReferenceEquals(f, kv.Key)) { open = true; break; }
                if (!open) gone.Add(kv.Key);
            }
            foreach (var g in gone) _flagPostedAt.Remove(g);
            // Poles that came back with a load are watched from now on.
            foreach (var f in flags.Flags)
                if (f != null && !_flagPostedAt.ContainsKey(f)) _flagPostedAt[f] = _body.simSeconds;

            int fighters = 0, scouts = 0;
            foreach (var a in loop.Agents)
            {
                if (a == null || a.IsIncapacitated || a.Data == null) continue;
                var cls = a.Data.specialistClass;
                if (cls == SpecialistClass.DefenseMech || cls == SpecialistClass.SentinelMech) fighters++;
                if (cls == SpecialistClass.ScoutDrone || cls == SpecialistClass.SurveyorBot) scouts++;
            }

            var lairs = new List<StalkerLair>();
            foreach (var l in world.Lairs) if (l != null && !l.IsCleared) lairs.Add(l);
            Vector3 hub = ColonyLayout.CampusOrigin;
            lairs.Sort((a, b) => Flat(a.WorldPosition, hub).CompareTo(Flat(b.WorldPosition, hub)));

            int denPoles = 0, pestPoles = 0, explorePoles = 0;
            foreach (var f in flags.Flags)
            {
                if (f?.Data == null) continue;
                if (f.Data.flagType == FlagType.Explore) explorePoles++;
                if (f.Data.flagType == FlagType.DefendArea) { pestPoles++; continue; }
                if (f.Data.flagType != FlagType.ClearThreat) continue;
                bool onDen = false;
                foreach (var l in lairs)
                    if (Flat(l.WorldPosition, f.WorldPosition) < OverseerRules.ClearThreatSameDenMeters) { onDen = true; break; }
                if (onDen) denPoles++; else pestPoles++;
            }

            // The objective says "Clear dens — F2 Clear Threat": the nearest den or two get a
            // Clear Threat bounty as soon as there is someone to fight. A scout, if there is one,
            // charts the next den out so the fight there is shorter.
            // A wreck waiting for its re-fab raises junk bots until it is gone: that money comes first.
            bool wreck = WreckWaiting(loop);
            foreach (var lair in lairs)
            {
                if (fighters == 0 || denPoles >= 2 || wreck) break;
                if (HasFlagNear(flags, FlagType.ClearThreat, lair.WorldPosition, OverseerRules.ClearThreatSameDenMeters)) continue;
                if (Post(loop, FlagType.ClearThreat, lair.WorldPosition, 900f, $"den {Flat(lair.WorldPosition, hub):F0} m out"))
                    denPoles++;
            }

            // Pests in the yard: leeches on the power node, mites at the purses and a halted
            // village bleed the treasury (the advisor says to post a flag). Robots only hunt what
            // is within reach of where they stand, so a pest across the yard waits for a price.
            // One cheap pole at a time; with too little for a den bounty this comes first, since
            // a bled treasury never gets there.
            // Only a pest that has been in the yard a while: most are hunted for free by the robot
            // at home, and a pole on every creeper the farm attracts costs more than it steals.
            var seenNow = new HashSet<DustStalkerAgent>();
            foreach (var s in loop.Stalkers)
                if (s != null && s.IsAlive && Flat(s.transform.position, hub) < 30f)
                {
                    seenNow.Add(s);
                    if (!_pestSince.ContainsKey(s)) _pestSince[s] = _body.simSeconds;
                }
            foreach (var gonePest in new List<DustStalkerAgent>(_pestSince.Keys))
                if (!seenNow.Contains(gonePest)) _pestSince.Remove(gonePest);
            // Junk bots come first and at once: they bite robots and take EU from them or the
            // treasury every couple of seconds ("Junk bots rising from the scrap pile — post Clear Threat").
            bool junkInYard = false;
            foreach (var s in seenNow)
                if (DustStalkerAgent.IsJunk(s.Kind)) { junkInYard = true; break; }
            if (fighters > 0 && pestPoles == 0 && (lairs.Count > 0 || junkInYard))
            {
                DustStalkerAgent pest = null;
                float bestPest = 30f;
                foreach (var s in seenNow)
                {
                    bool junk = DustStalkerAgent.IsJunk(s.Kind);
                    if (junkInYard && !junk) continue;
                    if (!junk && _body.simSeconds - _pestSince[s] < 60f) continue;
                    float d = Flat(s.transform.position, hub);
                    if (d >= bestPest) continue;
                    if (HasFlagNear(flags, FlagType.ClearThreat, s.transform.position, 14f) ||
                        HasFlagNear(flags, FlagType.DefendArea, s.transform.position, 14f)) continue;
                    bestPest = d;
                    pest = s;
                }
                // Creepers, mites and ticks answer to Defend Area (the advisor says so; the pole
                // itself wears them down while a robot holds it); the rest to Clear Threat.
                var kind = pest != null && DustStalkerAgent.UsesDefendCounter(pest.Kind) ? FlagType.DefendArea : FlagType.ClearThreat;
                if (pest != null && Post(loop, kind, pest.transform.position, 300f,
                        $"{pest.RoleLabel} {bestPest:F0} m from the Commons"))
                    pestPoles++;
            }
            foreach (var lair in lairs)
            {
                if (scouts == 0 || explorePoles >= 1) break;
                if (lair.IsScouted || HasFlagNear(flags, FlagType.Explore, lair.WorldPosition, 10f)) continue;
                // Scouts only look about 75 m out (the pole says "too far" beyond that).
                if (IsRefusedSpot(lair.WorldPosition) || Flat(lair.WorldPosition, hub) > 75f) continue;
                if (Post(loop, FlagType.Explore, lair.WorldPosition, 500f, $"uncharted den {Flat(lair.WorldPosition, hub):F0} m out"))
                    explorePoles++;
            }

            // Dens quiet but the objective still names fauna: a price on the nearest stragglers.
            var mission = loop.Mission;
            bool stragglers = lairs.Count == 0 && mission != null && !mission.DensCleared;
            if (fighters > 0 && stragglers && pestPoles < 2)
            {
                // Only fauna that hold the gate: town spawns keep coming after the dens are quiet
                // and never do (DensGate), and yard pests that are already scattering leave on
                // their own. The yard ones are left to the robots at home.
                DustStalkerAgent nearest = null;
                float best = float.MaxValue;
                foreach (var s in loop.Stalkers)
                {
                    if (s == null || !s.IsAlive || s.IsTownSpawn || s.IsRetreating) continue;
                    float d = Flat(s.transform.position, hub);
                    if (d < 30f) continue;
                    if (d >= best || HasFlagNear(flags, FlagType.ClearThreat, s.transform.position, 14f)) continue;
                    best = d;
                    nearest = s;
                }
                if (nearest != null)
                    Post(loop, FlagType.ClearThreat, nearest.transform.position, 900f,
                        $"straggler {nearest.RoleLabel} {best:F0} m from the Commons");
            }

            // Nobody on it after a minute: read the pole. Out of everyone's reach, take it down
            // (right-click); otherwise raise it, the way a player taps + on the pole.
            foreach (var kv in new List<KeyValuePair<FlagHandle, float>>(_flagPostedAt))
            {
                var f = kv.Key;
                if (f != null && f.ClaimCount > 0 && _claimed.Add(f))
                    Milestone($"{f.Data.flagType} pole {Flat(f.WorldPosition, hub):F0} m out taken ({f.CurrentBounty:N0} EU)");
                if (f == null || f.ClaimCount > 0) continue;
                if (_body.simSeconds - kv.Value < 60f) continue;
                string pole = f.InterestLabel ?? "";
                bool tooFar = f.InterestCount == 0 && pole.IndexOf("too far", StringComparison.OrdinalIgnoreCase) >= 0;
                // A pole on a yard pest that has moved off or died has nothing left to clear.
                bool stalePest = (f.Data.flagType == FlagType.ClearThreat || f.Data.flagType == FlagType.DefendArea) &&
                                 !NearDen(world, f.WorldPosition) && !FaunaNear(loop, f.WorldPosition, 15f);
                if (tooFar || stalePest)
                {
                    if (tooFar) _refusedSpots.Add(f.WorldPosition);
                    _flagPostedAt.Remove(f);
                    loop.CancelFlag(f);
                    Milestone($"took down {f.Data.flagType} pole: \"{(stalePest ? "nothing left there" : pole)}\"");
                    continue;
                }
                // Somebody is tempted, or the pole says they are hurt, tired or busy: the price is
                // not the problem. Only "raise +" / "won't take the walk" asks for more money.
                if (f.InterestCount > 0) continue;
                if (pole.IndexOf("raise", StringComparison.OrdinalIgnoreCase) < 0 &&
                    pole.IndexOf("won't take", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (f.CurrentBounty >= 3000f || Treasury(loop) < 600) continue;
                float before = f.CurrentBounty;
                input.SelectPosted(f);
                for (int i = 0; i < 4; i++) input.NudgeBounty(MajestyEconomy.FlagBountyStep);
                input.ClearPostedSelection();
                _flagPostedAt[f] = _body.simSeconds;
                if (f.CurrentBounty > before)
                    Milestone($"raised {f.Data.flagType} bounty {Flat(f.WorldPosition, hub):F0} m out to {f.CurrentBounty:N0} EU (pole: \"{pole}\")");
            }
        }

        private static bool NearDen(PlanetaryWorldGen world, Vector3 at)
        {
            foreach (var l in world.Lairs)
                if (l != null && !l.IsCleared && Flat(l.WorldPosition, at) < OverseerRules.ClearThreatSameDenMeters) return true;
            return false;
        }

        private static bool FaunaNear(GameLoop loop, Vector3 at, float r)
        {
            foreach (var s in loop.Stalkers)
                if (s != null && s.IsAlive && Flat(s.transform.position, at) < r) return true;
            return false;
        }

        private readonly List<Vector3> _refusedSpots = new List<Vector3>();
        private readonly HashSet<FlagHandle> _claimed = new HashSet<FlagHandle>();
        private readonly Dictionary<DustStalkerAgent, float> _pestSince = new Dictionary<DustStalkerAgent, float>();

        private bool IsRefusedSpot(Vector3 at)
        {
            foreach (var p in _refusedSpots) if (Flat(p, at) < 10f) return true;
            return false;
        }

        private bool Post(GameLoop loop, FlagType type, Vector3 at, float bounty, string what)
        {
            var input = loop.FlagInput;
            var data = input != null ? input.FlagFor(type) : null;
            if (data == null) return false;
            if (Treasury(loop) < bounty + 200f) return false;
            var handle = input.PostFlagAt(data, at, bounty);
            if (handle == null) return false;
            _flagPostedAt[handle] = _body.simSeconds;
            _body.bountiesPosted++;
            Milestone($"bounty {type} {bounty:N0} EU on {what}");
            return true;
        }

        private static bool HasFlagNear(FlagManager flags, FlagType type, Vector3 at, float r)
        {
            foreach (var f in flags.Flags)
                if (f?.Data != null && f.Data.flagType == type && Flat(f.WorldPosition, at) < r) return true;
            return false;
        }

        // ---------------------------------------------------------------- menu and save checks

        private IEnumerator CheckPauseAndSettings()
        {
            var loop = Loop;
            loop.PausePlay(); // Esc
            yield return WaitReal(0.3f);
            if (Loop.Screen != DemoScreen.Paused || Time.timeScale != 0f)
                Fail($"Esc did not pause (screen {Loop.Screen}, timeScale {Time.timeScale})");
            Loop.OpenSettings(); // pause menu → Settings
            yield return WaitReal(0.5f);
            if (Loop.Screen != DemoScreen.Settings) Fail("Settings did not open from the pause menu");
            Loop.CloseSettings(); // BACK
            yield return WaitReal(0.3f);
            if (Loop.Screen != DemoScreen.Paused) Fail("closing Settings did not return to the pause menu");
            Loop.ResumePlay(); // Resume
            yield return WaitReal(0.3f);
            if (Loop.Screen != DemoScreen.Playing || Time.timeScale <= 0f)
                Fail("Resume did not resume the world");
            _body.settingsChecked = true;
            Milestone("pause → Settings → back → Resume ok");
        }

        private IEnumerator CheckSaveLoad(CelestialBodyId body)
        {
            var old = Loop;
            var before = Capture(old);
            if (!old.SaveToSlot(SaveSlot)) { Fail("SAVE to slot " + SaveSlot + " failed"); yield break; }
            Milestone($"saved to slot {SaveSlot}; loading it back");
            if (!old.LoadFromSlot(SaveSlot)) { Fail("LOAD of slot " + SaveSlot + " failed"); yield break; }
            // The scene reloads next frame; until then the old colony is still alive (and ticking).
            yield return WaitFor(() => old == null && Loop != null && Loop.IsPlaying && Loop.ActiveBody == body, 120f, "the loaded slot");
            if (_failure != null) yield break;
            yield return null;
            Compare(before, Capture(Loop), "save → load");
            _body.saveLoadChecked = true;
        }

        private IEnumerator CheckTitleContinue(CelestialBodyId body)
        {
            var before = Capture(Loop);
            Loop.PausePlay();
            Loop.ReturnToTitle(); // pause menu → Title
            yield return WaitReal(0.5f);
            if (Loop.Screen != DemoScreen.Title) { Fail("Title did not open from the pause menu"); yield break; }
            if (!DemoSettings.SaveExists) { Fail("title shows no Continue after leaving a colony"); yield break; }
            yield return WaitReal(1f);
            Loop.ContinueGame(); // CONTINUE
            yield return WaitFor(() => Loop != null && Loop.IsPlaying && Loop.ActiveBody == body, 60f, "Continue from the title");
            if (_failure != null) yield break;
            yield return null;
            Compare(before, Capture(Loop), "title → Continue");
            _body.titleContinueChecked = true;
        }

        private IEnumerator QuitForRelaunch()
        {
            var loop = Loop;
            loop.PausePlay();
            yield return WaitReal(0.3f);
            _state.beforeRelaunch = Capture(loop);
            _state.relaunchPending = true;
            _state.realSecondsBefore += Time.realtimeSinceStartup - _realStart;
            WriteState();
            Milestone("pause menu → QUIT (the run continues in a relaunched game)");
            SimSpeed.HarnessBoost = 1f;
            loop.QuitDemo(); // QUIT: saves and closes the game
            _finished = true;
            enabled = false;
        }

        private Snapshot Capture(GameLoop loop)
        {
            var s = new Snapshot
            {
                body = loop.ActiveBody.ToString(),
                seed = BodySeed.Current,
                metals = loop.Resources != null ? loop.Resources.Get(ResourceId.Metals) : 0,
                ice = loop.Resources != null ? loop.Resources.Get(ResourceId.WaterIce) : 0,
                activeTech = loop.Research != null ? (int)loop.Research.ActiveTech : 0,
                lairsCleared = ClearedLairs(loop),
                flags = loop.Flags != null ? loop.Flags.Flags.Count : 0,
                robots = Robots(loop),
                hiresWaiting = loop.AwaitingHireCount,
                launchReady = loop.Mission != null && loop.Mission.LaunchReady,
                conquered = CampaignProgress.ConqueredMask
            };
            if (loop.Placer != null)
                foreach (var o in loop.Placer.Orders)
                    if (o != null && o.IsRefab && !o.IsComplete) s.refabs++;
            foreach (var f in loop.Stalkers)
                if (f != null && f.IsAlive) s.fauna++;
            if (loop.World != null)
                foreach (var l in loop.World.Lairs)
                    if (l != null && l.IsScouted) s.lairsScouted++;

            var counts = new SortedDictionary<string, int>();
            if (loop.Placer != null)
                foreach (var p in loop.Placer.Pieces)
                {
                    string k = p.Category.ToString();
                    counts.TryGetValue(k, out int n);
                    counts[k] = n + 1;
                }
            var sb = new StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            s.buildings = sb.ToString().Trim();

            var techs = new List<int>();
            if (loop.Research != null)
                foreach (var t in loop.Research.Unlocked) techs.Add((int)t);
            techs.Sort();
            s.techs = string.Join(",", techs);
            return s;
        }

        private void Compare(Snapshot a, Snapshot b, string what)
        {
            var diffs = new List<string>();
            void Check(string field, object x, object y)
            {
                if (!Equals(x, y)) diffs.Add($"{field} {x} → {y}");
            }
            Check("world", a.body, b.body);
            Check("seed", a.seed, b.seed);
            Check("EU", a.metals, b.metals);
            Check("ICE", a.ice, b.ice);
            Check("buildings", a.buildings, b.buildings);
            Check("techs", a.techs, b.techs);
            Check("active tech", (TechId)a.activeTech, (TechId)b.activeTech);
            Check("dens cleared", a.lairsCleared, b.lairsCleared);
            Check("dens scouted", a.lairsScouted, b.lairsScouted);
            Check("bounties", a.flags, b.flags);
            Check("robots", a.robots, b.robots);
            Check("workshops waiting to hire", a.hiresWaiting, b.hiresWaiting);
            Check("re-fabs under way", a.refabs, b.refabs);
            Check("launch ready", a.launchReady, b.launchReady);
            Check("conquered", a.conquered, b.conquered);
            // Continue deliberately puts back at most OverseerRules.MaxFaunaPerDen per den, so fewer
            // fauna can be right; more than were saved never is.
            if (b.fauna > a.fauna) diffs.Add($"fauna {a.fauna} → {b.fauna}");
            if (diffs.Count == 0)
                Milestone($"{what}: colony restored intact ({a.buildings}; {a.metals} EU; {a.robots} robots; " +
                          $"{a.flags} bounties; fauna {a.fauna} → {b.fauna})");
            else
                Debug.LogError(Tag + $"{what} lost state on {a.body}: " + string.Join("; ", diffs));
        }

        // ---------------------------------------------------------------- plumbing

        private GameLoop Loop
        {
            get
            {
                if (_loop == null)
                {
                    _loop = FindAnyObjectByType<GameLoop>();
                    // Every scene load re-applies the profile's graphics settings; keep the requested tier.
                    if (_loop != null && _quality >= 0 && _quality < QualitySettings.names.Length &&
                        QualitySettings.GetQualityLevel() != _quality)
                        QualitySettings.SetQualityLevel(_quality, true);
                }
                return _loop;
            }
        }

        private int _quality = -1;
        private CelestialBodyId _from = CelestialBodyId.Earth;

        private void EnsureRunning(GameLoop loop)
        {
            // Continue and Load hold the world (HOLD); a player presses the speed button to go on.
            if (SimSpeed.Index != TopSpeedIndex || SimSpeed.IsPaused)
            {
                SimSpeed.Set(TopSpeedIndex);
                loop.OnSpeedChanged();
            }

            // Keep each frame's slice of world time small: on a slow frame (another game on the
            // GPU, a loading hitch) the boost backs off instead of moving robots and fauna a whole
            // second per frame while the fixed-step economy drops the excess.
            _frameDt = Mathf.Lerp(_frameDt, Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.25f), 0.05f);
            float topScale = SimSpeed.Multipliers[TopSpeedIndex] * SimSpeed.BasePace;
            float want = Mathf.Clamp(MaxSimStep / (topScale * _frameDt), 1f, _boost);
            if (Mathf.Abs(want - SimSpeed.HarnessBoost) > 0.25f)
            {
                SimSpeed.HarnessBoost = want;
                loop.ApplySimSpeed();
            }
        }

        /// <summary>Most world time one frame may take (four 20 Hz steps).</summary>
        private const float MaxSimStep = 0.2f;
        private float _frameDt = 1f / 60f;

        private static void DismissCutscenes(GameLoop loop)
        {
            for (int i = 0; i < 8 && loop.TryPeekCutscene(out _); i++)
                loop.DismissCutscene(); // CONTINUE · SPACE
        }

        private IEnumerator WaitFor(Func<bool> done, float realSeconds, string what)
        {
            float until = Time.realtimeSinceStartup + realSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                bool ok;
                try { ok = done(); }
                catch (Exception) { ok = false; }
                if (ok) yield break;
                yield return null;
            }
            Fail($"timed out after {realSeconds:0} s waiting for {what}");
        }

        /// <summary>
        /// Press a button, then wait for the state it leads to. If the game is still in the state
        /// where the press should have counted (<paramref name="stillThere"/>) after a while, the
        /// press was not taken (a camera beat held the panel back) and it is pressed again. The
        /// gap is longer than a launch shot, so a press that was taken is never doubled.
        /// </summary>
        private IEnumerator PressUntil(Action press, Func<bool> done, Func<bool> stillThere, float realSeconds, string what)
        {
            const float Repress = 12f;
            float until = Time.realtimeSinceStartup + realSeconds;
            float next = 0f;
            while (Time.realtimeSinceStartup < until)
            {
                bool ok;
                try { ok = done(); }
                catch (Exception) { ok = false; }
                if (ok) yield break;
                bool again;
                try { again = stillThere(); }
                catch (Exception) { again = false; }
                if (again && Time.realtimeSinceStartup >= next)
                {
                    if (next > 0f) Milestone("press again — " + what);
                    press();
                    next = Time.realtimeSinceStartup + Repress;
                }
                yield return null;
            }
            Fail($"timed out after {realSeconds:0} s waiting for {what}");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private void CheckTimeout()
        {
            float real = _state.realSecondsBefore + Time.realtimeSinceStartup - _realStart;
            if (real > _timeoutMinutes * 60f)
                Fail($"timed out after {_timeoutMinutes:0} real minutes");
        }

        private void Fail(string why)
        {
            if (_failure != null) return;
            _failure = why;
            Debug.LogError(Tag + "FAIL: " + why);
        }

        private void Milestone(string line)
        {
            float t = _body != null ? _body.simSeconds : 0f;
            string body = _body != null ? _body.body : "-";
            string entry = string.Format(CultureInfo.InvariantCulture, "{0} {1,6:F0}s  {2}", body, t, line);
            _body?.milestones.Add(entry);
            Debug.Log(Tag + entry);
        }

        private string Status(GameLoop loop)
        {
            var m = loop.Mission;
            var r = loop.Research;
            var set = loop.Settlement;
            string tech = r == null ? "-" : r.ActiveTech == TechId.None ? "idle"
                : string.Format(CultureInfo.InvariantCulture, "{0} {1:F0}/{2:F0}{3}", r.ActiveTech, r.ActiveProgress, r.ActiveCost,
                    r.LastEvent == "awaiting_stockpile" ? " (awaiting EU)" : "");
            return string.Format(CultureInfo.InvariantCulture,
                "{0} t={1:F0}s · EU {2}/{3} · income {4:F0}/{5}/min · sustain {6:F0}/{7:F0}s · dens {8}/{9} left (fauna {10}) · " +
                "launch {11} · tech {12} [{13} done] · robots {14} · buildings {15} · bounties {16} · till share {18} EU · objective \"{17}\"",
                _body?.body, _body?.simSeconds ?? 0f, Treasury(loop), m?.TreasuryGoal ?? 0,
                set?.NetMetalsPerMin ?? 0f, set?.IncomeGoalPerMin ?? 0, m?.SustainElapsed ?? 0f, m?.SustainRequired ?? 0f,
                m?.UnclearedLairs ?? 0, m?.LairCount ?? 0, m?.StalkersRemaining ?? 0,
                m != null && m.LaunchReady ? "ready" : "no", tech, r?.UnlockedCount ?? 0,
                Robots(loop), loop.Placer?.Pieces.Count ?? 0, loop.Flags?.Flags.Count ?? 0, m?.ObjectiveLabel,
                loop.Stats != null ? loop.Stats.TitheCollected : 0);
        }

        /// <summary>Every robot (what it is doing and where) and the fauna by kind and distance.</summary>
        private static string Crew(GameLoop loop)
        {
            Vector3 hub = ColonyLayout.CampusOrigin;
            var sb = new StringBuilder("robots[");
            foreach (var a in loop.Agents)
            {
                if (a == null || a.Data == null) continue;
                var nav = a.GetComponent<UnityEngine.AI.NavMeshAgent>();
                string path = nav == null || !nav.enabled ? "noagent"
                    : !nav.isOnNavMesh ? "offmesh"
                    : nav.pathPending ? "pending"
                    : !nav.hasPath ? "nopath"
                    : nav.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete ? "path"
                    : nav.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial ? "partial" : "invalid";
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}:{1}/{2} f{3:F2} h{4:F2} {5:F0}m {6}{7}{8}",
                    ColonyStructure.ClassLabel(a.Data.specialistClass), a.CurrentAction, a.LastReason,
                    a.Fatigue, a.HealthNormalized, Flat(a.transform.position, hub), path,
                    a.IsIncapacitated ? " DOWN" : "", RouteToTarget(loop, a));
            }
            sb.Append(" ] fauna[");
            var kinds = new SortedDictionary<string, (int n, float near, float far)>();
            foreach (var s in loop.Stalkers)
            {
                if (s == null || !s.IsAlive) continue;
                string k = s.Kind.ToString();
                float d = Flat(s.transform.position, hub);
                kinds.TryGetValue(k, out var v);
                kinds[k] = v.n == 0 ? (1, d, d) : (v.n + 1, Mathf.Min(v.near, d), Mathf.Max(v.far, d));
            }
            foreach (var kv in kinds)
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}×{1} {2:F0}-{3:F0}m", kv.Key, kv.Value.n, kv.Value.near, kv.Value.far);
            sb.Append(" ]");
            return sb.ToString();
        }

        /// <summary>
        /// For a robot on a Clear Threat walk: what lies between it and the target. The longest run
        /// of water on the straight line, and whether the NavMesh has a dry path there.
        /// </summary>
        private static string RouteToTarget(GameLoop loop, SpecialistAgent a)
        {
            if (loop.World == null) return "";
            var f = a.ActiveFlag;
            Vector3 from = a.transform.position, to;
            if (f?.Data != null && f.Data.flagType == FlagType.ClearThreat)
            {
                to = f.WorldPosition;
                var lair = loop.World.FindNearestLair(to, 12f);
                if (lair != null) to = lair.WorldPosition;
            }
            else if (a.LastReason == "clear_threat_yield")
            {
                StalkerLair nearest = null;
                foreach (var l in loop.World.Lairs)
                    if (l != null && !l.IsCleared && (nearest == null || Flat(l.WorldPosition, from) < Flat(nearest.WorldPosition, from)))
                        nearest = l;
                if (nearest == null) return "";
                to = nearest.WorldPosition;
            }
            else return "";
            float len = Flat(from, to), wetRun = 0f, longest = 0f;
            for (float s = 0f; s <= len; s += 1f)
            {
                Vector3 p = Vector3.Lerp(from, to, len > 0f ? s / len : 1f);
                if (loop.World.IsOverWater(p, 0.35f)) { wetRun += 1f; longest = Mathf.Max(longest, wetRun); }
                else wetRun = 0f;
            }
            string dry = "?";
            var path = new UnityEngine.AI.NavMeshPath();
            if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(from.x, 0.02f, from.z), out var hs, 8f, UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.SamplePosition(new Vector3(to.x, 0.02f, to.z), out var he, 16f, UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.CalculatePath(hs.position, he.position, UnityEngine.AI.NavMesh.AllAreas, path))
                dry = path.status.ToString();
            return string.Format(CultureInfo.InvariantCulture, " →target {0:F0}m wet {1:F0}m dry:{2}", len, longest, dry);
        }

        private float _treasuryHigh;

        private float ProgressScore(GameLoop loop)
        {
            var m = loop.Mission;
            var r = loop.Research;
            int t = Treasury(loop);
            if (t > _treasuryHigh) _treasuryHigh = t;
            float score = 0f;
            if (m != null) score += m.GatesComplete * 1000f + Mathf.Floor(m.SustainElapsed / 5f) * 5f;
            // Fewer fauna than ever before on this world is progress toward the dens gate (the
            // straggler hunt after the last den can take a while).
            if (m != null && m.StalkersRemaining < _faunaLow) _faunaLow = m.StalkersRemaining;
            score += Mathf.Max(0, 300 - _faunaLow) * 5f;
            score += ClearedLairs(loop) * 200f;
            if (loop.World != null)
                foreach (var l in loop.World.Lairs) if (l != null && l.IsScouted) score += 40f;
            if (r != null) score += r.UnlockedCount * 100f + Mathf.Floor(r.ActiveProgress / 10f);
            // Buildings and treasury only count up to what the win needs: a colony that keeps
            // growing and banking while a gate never opens is stalled, not progressing.
            score += Mathf.Min(loop.Placer?.Pieces.Count ?? 0, 30) * 20f;
            float goal = m != null ? m.TreasuryGoal : 5000f;
            score += Mathf.Floor(Mathf.Min(_treasuryHigh, goal) / 250f);
            return score;
        }

        private BodyRun FindBodyRun(CelestialBodyId id)
        {
            _treasuryHigh = 0f;
            _faunaLow = int.MaxValue;
            foreach (var b in _state.bodies)
                if (b.body == id.ToString()) return b;
            return null;
        }

        private int _faunaLow = int.MaxValue;

        private static int Treasury(GameLoop loop) => loop.Resources != null ? loop.Resources.Get(ResourceId.Metals) : 0;

        private static int ClearedLairs(GameLoop loop)
        {
            int n = 0;
            if (loop.World != null)
                foreach (var l in loop.World.Lairs) if (l != null && l.IsCleared) n++;
            return n;
        }

        private static int Robots(GameLoop loop)
        {
            int n = 0;
            foreach (var a in loop.Agents) if (a != null && a.Data != null) n++;
            return n;
        }

        private static int Owned(GameLoop loop, BuildingCategory cat)
        {
            int n = 0;
            foreach (var p in loop.Placer.Pieces) if (p.Category == cat) n++;
            return n;
        }

        /// <summary>Buildings the player placed (not the Commons or the drop HAB, not villager houses).</summary>
        private static int Placed(GameLoop loop)
        {
            int n = 0;
            if (loop.Placer == null) return 0;
            foreach (var p in loop.Placer.Pieces)
                if (p.Category != BuildingCategory.Commons && p.Category != BuildingCategory.Habitat &&
                    p.Category != BuildingCategory.Power) n++;
            return n;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void WriteState()
        {
            try { File.WriteAllText(_statePath, JsonUtility.ToJson(_state, true), new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogError(Tag + "could not write state: " + e.Message); }
        }

        private void Finish()
        {
            if (enabled == false && _state.relaunchPending) return; // quitting for a relaunch
            int errors;
            List<string> errorList;
            lock (_gate)
            {
                errors = _errorCount;
                errorList = new List<string>(_errors);
            }
            if (_failure == null && errors > 0)
                _failure = $"{errors} error(s)/exception(s) logged during the run";
            if (_failure == null)
            {
                foreach (var b in _state.bodies)
                {
                    if (!b.won) _failure = $"{b.body} was never won";
                    else if (!b.settingsChecked || !b.saveLoadChecked || !b.titleContinueChecked)
                        _failure = $"{b.body} won before its menu/save checks ran " +
                                   $"(settings {b.settingsChecked}, save/load {b.saveLoadChecked}, title/continue {b.titleContinueChecked})";
                    if (_failure != null) break;
                }
                // The Steam demo is Earth only and ends on its own screen (DemoEnd).
                int expected = DemoBuild.Active ? 1 : (int)CelestialBodyCatalog.Last - (int)_from + 1;
                if (_failure == null && _state.bodies.Count < expected)
                    _failure = $"only {_state.bodies.Count} of {expected} worlds were played";
                if (_failure == null && _relaunchOn.HasValue && _state.relaunches == 0)
                    _failure = "the relaunch on " + _relaunchOn + " never happened";
            }

            bool passed = _failure == null;
            float real = _state.realSecondsBefore + Time.realtimeSinceStartup - _realStart;
            var summary = new Summary
            {
                passed = passed,
                failure = _failure ?? "",
                version = Application.version,
                startedUtc = _state.startedUtc,
                realSeconds = real,
                relaunches = _state.relaunches,
                errorCount = errors,
                errors = errorList,
                errorLogFile = ErrorLog.Count > 0 ? ErrorLog.CurrentFile ?? "" : "",
                bodies = _state.bodies,
                conqueredEveryWorld = CampaignProgress.ConqueredEveryWorld,
                solarConquestAchievement = Achievements.IsUnlocked(AchievementId.SolarConquest),
                achievements = new List<string>(),
                verdict = Loop != null ? Loop.Stats.PlaystyleVerdict() : ""
            };
            foreach (var def in Achievements.Definitions)
                if (Achievements.IsUnlocked(def.Id)) summary.achievements.Add(def.Title);
            try
            {
                string dir = Path.GetDirectoryName(_outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_outPath, JsonUtility.ToJson(summary, true), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "could not write summary: " + e.Message);
            }

            var sb = new StringBuilder();
            sb.Append(Tag).Append(passed ? "PASSED" : "FAILED: " + _failure).Append('\n');
            sb.AppendFormat(CultureInfo.InvariantCulture, "  total {0:F1} real-min, {1} relaunch(es), {2} error(s)\n",
                real / 60f, _state.relaunches, errors);
            foreach (var b in _state.bodies)
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  {0,-7} {1} in {2,5:F1} sim-min / {3,5:F1} real-min · {4} buildings · {5} bounties · {6} dens · {7} restarts · checks S{8} L{9} T{10}{11}\n",
                    b.body, b.won ? "won " : "open", b.simSeconds / 60f, b.realSeconds / 60f, b.buildingsPlaced,
                    b.bountiesPosted, b.densCleared, b.restarts, b.settingsChecked ? "+" : "-",
                    b.saveLoadChecked ? "+" : "-", b.titleContinueChecked ? "+" : "-", b.relaunchChecked ? " R+" : "");
            sb.Append("  achievements: ").Append(string.Join(", ", summary.achievements)).Append('\n');
            foreach (var e in errorList) sb.Append("  ! ").Append(e).Append('\n');
            sb.Append("  summary: ").Append(_outPath);
            Debug.Log(sb.ToString());

            _finished = true;
            if (File.Exists(_statePath))
            {
                try { File.Delete(_statePath); } catch { /* leave it */ }
            }
            SimSpeed.HarnessBoost = 1f;
            Quit(passed ? 0 : 1);
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        private static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            return (i < 0 ? s : s.Substring(0, i)).Trim();
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
