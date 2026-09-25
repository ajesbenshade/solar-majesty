using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum OverseerTool
    {
        None = 0,
        Build = 1,
        Flag = 2
    }

    public enum DemoScreen
    {
        Title = 0,
        Playing = 1,
        Paused = 2,
        Settings = 3
    }

    /// <summary>
    /// Vertical-slice bootstrap: owns pure C# systems and thin scene drivers.
    /// Phase 1.5: spawns Scout + Engineer + Defense with distinct personalities.
    /// Player may only place buildings and post flags — never command specialists.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameLoop : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private IsoGrid grid;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform specialistSpawn;
        [SerializeField] private Transform flagRoot;
        [SerializeField] private Transform buildingRoot;

        [Header("Content (optional — runtime defaults if null)")]
        [SerializeField] private SpecialistData scoutData;
        [SerializeField] private SpecialistData engineerData;
        [SerializeField] private SpecialistData defenseData;
        [SerializeField] private SpecialistData medicData;
        [SerializeField] private SpecialistData harvesterData;
        [SerializeField] private SpecialistData surveyorData;
        [SerializeField] private SpecialistData terraformerData;
        [SerializeField] private SpecialistData courierData;
        [SerializeField] private SpecialistData geologistData;
        [SerializeField] private SpecialistData sentinelData;
        [SerializeField] private FlagData exploreFlagData;
        [SerializeField] private FlagData clearThreatFlagData;
        [SerializeField] private FlagData buildFlagData;
        [SerializeField] private FlagData extractFlagData;
        [SerializeField] private FlagData defendFlagData;
        [SerializeField] private FlagData researchSiteFlagData;
        [SerializeField] private FlagData outpostFlagData;
        [SerializeField] private FlagData terraformFlagData;
        [SerializeField] private BuildingData[] starterBuildings;

        [Header("Slice settings")]
        [SerializeField] private OverseerTool activeTool = OverseerTool.None;
        [SerializeField] private Vector3 specialistSpawnOffset = new Vector3(24f, 0f, 12f);
        [SerializeField] private bool seedStartingResources = true;

        public const int MaxPartySize = 4;

        [Header("Phase 1.6 Threat")]
        [SerializeField] private bool spawnDustStalkers = true;
        [SerializeField] private int dustStalkerCount = 2;
        [SerializeField] private float stalkerSpawnRadius = 14f;
        [SerializeField] private bool spawnSecondBody = true;
        [SerializeField] private int campusBStalkerCount = 2;

        [Header("Demo greybox visuals")]
        [SerializeField] private bool spawnGroundPlane = true;
        [Tooltip("Debug: pre-build Campus A/B. Campaign starts empty — map + dens only.")]
        [SerializeField] private bool spawnShowcaseColony = false;
        [Tooltip("Debug: spawn waystation inn mesh. Empty start uses a rest beacon only.")]
        [SerializeField] private bool spawnWaystationInn = false;

        [Header("Procedural world")]
        [SerializeField] private CelestialBodyId celestialBody = CelestialBodyId.Earth;
        [Tooltip("0 = use persisted BodySeed for this world; non-zero forces that seed for this Play.")]
        [SerializeField] private int worldSeedOverride = 0;
        [SerializeField] private bool advanceSeedOnRestart = true;

        // Pure systems
        public ResourceManager Resources { get; private set; }
        public FlagManager Flags { get; private set; }
        public BuildingPlacer Placer { get; private set; }
        public SpecialistBrain Brain { get; private set; }
        public SimpleEconomy Economy { get; private set; }


        // Runtime threat service (not in Systems/)
        public ThreatPressure Threat { get; private set; }

        // Drivers
        public SpecialistAgent Agent { get; private set; } // first / primary (Scout)
        public IReadOnlyList<SpecialistAgent> Agents => _agents;
        public IReadOnlyList<DustStalkerAgent> Stalkers => _stalkers;
        public OverseerTool ActiveTool => activeTool;
        public float FlagBounty => _flagInput != null ? _flagInput.Bounty : 0f;
        public FlagPlacementInput FlagInput => _flagInput;
        public BuildingPlacementInput BuildInput => _buildInput;
        public int FocusedCampus => _focusedCampus;
        public IReadOnlyList<SpecialistAgent> SelectedAgents => _selected;
        public IsoGrid Grid => grid;
        public Settlement Settlement { get; private set; }
        public VillageExpansion Village { get; private set; }
        public ResearchManager Research { get; private set; }
        public GuildBenefitDirector GuildBenefits { get; private set; }
        public IReadOnlyList<HeroParty> Parties => _parties;
        public CelestialBodyId ActiveBody => celestialBody;
        public bool StartsEmpty => !spawnShowcaseColony;
        public bool SpawnWaystationInn => spawnWaystationInn;
        public DemoScreen Screen { get; private set; } = DemoScreen.Title;
        public bool IsPlaying => Screen == DemoScreen.Playing;
        public bool AllowsCamera => Screen == DemoScreen.Playing;
        public bool TitlePointerBlocksWorld =>
            _overseerHud != null && _overseerHud.HitsHudPanels();

        /// <summary>The mouse is over a HUD panel right now (the wheel scrolls it instead of zooming).</summary>
        public bool PointerOverHud =>
            (_overseerHud != null && _overseerHud.HitsHudPanels()) ||
            (_alertView != null && _alertView.PointerOverCards());
        public bool TitleConfirmOpen =>
            _overseerHud != null && _overseerHud.TitleConfirmOpen;

        /// <summary>
        /// True while a colony is in progress (or settings opened from pause). Ironman cannot
        /// be flipped mid-run; the title screen still edits the next New Game preference.
        /// </summary>
        public bool RunConfigLocked =>
            Screen == DemoScreen.Playing
            || Screen == DemoScreen.Paused
            || (Screen == DemoScreen.Settings && _settingsReturn == DemoScreen.Paused);
        public const int TutorialCompleteStep = 6;
        public int TutorialStep { get; private set; }
        private int TutorialGoal =>
            DemoSettings.FirstHourDemo ? FirstHourTutorial.Goal : TutorialCompleteStep;
        public bool IsTutorialActive => !DemoSettings.TutorialDone && TutorialStep < TutorialGoal;
        private bool _tutorialDefendPosted;
        private bool _tutorialPestSpawned;
        public BuildingData[] StarterBuildings => starterBuildings;
        public StillCampusDensity.StampLog LastStillStamp { get; private set; }
        public float LastStillOrtho { get; private set; }
        public CelestialBodyProfile BodyProfile => _body;
        public PlanetaryWorldGen World => _world;
        public int MoonSeedValue => BodySeed.Current;

        public void SetTool(OverseerTool tool) => ApplyTool(tool);

        /// <summary>Bottom-dock toggle: pressing an active tool again closes it (back to inspect),
        /// unless the catalog was minimized — then B/G re-opens the list.</summary>
        public void ToggleTool(OverseerTool tool)
        {
            if (tool == OverseerTool.None)
            {
                ApplyTool(OverseerTool.None);
                return;
            }

            if (activeTool == tool)
            {
                if (_overseerHud != null && _overseerHud.TryExpandMinimizedMenu(tool))
                    return;
                ApplyTool(OverseerTool.None);
                return;
            }

            ApplyTool(tool);
            _overseerHud?.ExpandMenu(tool);
        }

        public void NotifyCatalogPicked() => _overseerHud?.MinimizeActiveMenu();

        /// <summary>True for the rest of this frame after a specialist was selected — skips flag/build place.</summary>
        public bool WorldClickUsedBySelection { get; private set; }
        public float CurrentThreatPressure => Threat != null ? Threat.Current : 0f;

        public OverseerRating CurrentRating => OverseerScore.Evaluate(BuildScoreInput());

        public void ApplyReplayToBrain()
        {
            if (Brain == null) return;
            Brain.ConsiderRange = 80f * Mathf.Clamp(ReplayRules.ConsiderRangeScale, 0.8f, 1.55f);
        }

        private OverseerScoreInput BuildScoreInput()
        {
            var m = _mission;
            float meanHp = 0f;
            int robots = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null) continue;
                robots++;
                meanHp += a.HealthNormalized;
            }
            if (robots > 0) meanHp /= robots;

            bool dens = m != null && m.DensCleared;
            bool sustain = m != null && m.SustainComplete;
            bool launch = m != null && m.LaunchReady;
            return new OverseerScoreInput
            {
                DensCleared = dens,
                UnclearedLairs = m != null ? m.UnclearedLairs : 0,
                LairCount = m != null ? m.LairCount : 0,
                SustainComplete = sustain,
                Sustain01 = m != null && m.SustainRequired > 0.01f
                    ? m.SustainElapsed / m.SustainRequired : 0f,
                LaunchReady = launch,
                Metals = Resources != null ? Resources.Get(ResourceId.Metals) : 0,
                RobotCount = robots,
                MeanHealth = meanHp,
                MissionElapsed = m != null ? m.MissionElapsed : 0f,
                GatesMet = dens && sustain && launch,
                RevivePenalty = _revivePenaltyApplied ? OverseerRules.ReviveRatingPenalty : 0
            };
        }

        /// <summary>Local threat at the camera-focused campus (HUD framing).</summary>
        public float FocusedLocalThreat => LocalThreatAt(ColonyLayout.CampusOriginFor(_focusedCampus));

        /// <summary>Local threat sample at an arbitrary world point (ambient + nearby stalkers).</summary>
        public float LocalThreatAt(Vector3 world)
        {
            float ambient = Threat != null ? Threat.Ambient : 0.18f;
            float peak = 0f;
            float r = ColonyLayout.LocalThreatRadius;
            float rSq = r * r;
            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s == null || !s.IsAlive) continue;
                Vector3 sp = s.transform.position;
                float dx = sp.x - world.x;
                float dz = sp.z - world.z;
                float dSq = dx * dx + dz * dz;
                if (dSq > rSq) continue;
                float d = Mathf.Sqrt(dSq);
                float pressure = s.IsAggro ? 0.55f : 0.08f;
                float falloff = 1f - (d / r);
                peak = Mathf.Max(peak, pressure * falloff);
            }
            return Mathf.Clamp01(ambient + peak);
        }

        public int CountStalkersNearCampus(int campusIndex)
        {
            Vector3 origin = ColonyLayout.CampusOriginFor(campusIndex);
            float r = ColonyLayout.LocalThreatRadius * 1.35f;
            float rSq = r * r;
            int n = 0;
            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s == null || !s.IsAlive) continue;
                Vector3 sp = s.transform.position;
                float dx = sp.x - origin.x;
                float dz = sp.z - origin.z;
                if (dx * dx + dz * dz <= rSq) n++;
            }
            return n;
        }

        /// <summary>True when every living specialist is down, or the roster is empty.</summary>
        public bool IsOutpostOverwhelmed
        {
            get
            {
                int living = 0;
                int down = 0;
                for (int i = 0; i < _agents.Count; i++)
                {
                    var a = _agents[i];
                    if (a == null) continue;
                    living++;
                    if (a.IsIncapacitated) down++;
                }
                return living == 0 || (living > 0 && down == living);
            }
        }

        public int RobotCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _agents.Count; i++)
                {
                    if (_agents[i] != null) n++;
                }
                return n;
            }
        }

        /// <summary>The colony is lost when the Commons (the treasury) falls after it stood.</summary>
        public bool ColonyExtinct =>
            !StillCaptureHold.Active &&
            _commonsEverStood && Settlement != null && !Settlement.HasCommons;

        private bool _commonsEverStood;

        /// <summary>
        /// Dens exist from world gen; campus raids and pests stay off for
        /// <see cref="OverseerRules.FaunaGraceSeconds"/>. Continue with a standing
        /// Commons uses a short re-entry window instead of this empty-drop grace.
        /// </summary>
        public bool InFaunaGrace =>
            !_skipFaunaGrace &&
            (_mission == null || _mission.MissionElapsed < OverseerRules.FaunaGraceSeconds);

        public bool NeedsFieldRevive
        {
            get
            {
                int living = 0;
                int down = 0;
                for (int i = 0; i < _agents.Count; i++)
                {
                    var a = _agents[i];
                    if (a == null) continue;
                    living++;
                    if (a.IsIncapacitated) down++;
                }
                if (living > 0 && down == living) return true;
                return living == 0 && HasScrapCorpse;
            }
        }

        public bool HasScrapCorpse => _corpses.Count > 0;
        public IReadOnlyList<SpecialistRecord> Corpses => _corpses;

        public int FieldReviveMet
        {
            get
            {
                ComputeReviveBill(out int met, out _);
                return met;
            }
        }

        public bool HasFobotYard
        {
            get
            {
                var yard = FindFobotYard();
                return yard != null && yard.IsAlive;
            }
        }

        public bool CanPayYard =>
            HasFobotYard && (HasScrapCorpse || HasDownedRobot);

        public bool HasDownedRobot
        {
            get
            {
                for (int i = 0; i < _agents.Count; i++)
                {
                    if (_agents[i] != null && _agents[i].IsIncapacitated)
                        return true;
                }
                return false;
            }
        }

        public int WreckCount => _corpses.Count;

        public int FieldReviveIce => 0;

        public bool HasRefabInProgress => Placer != null && Placer.HasRefabOrder;

        public bool EmptyRosterFailed { get; private set; }

        public bool PayrollThin =>
            Resources != null && Resources.Get(ResourceId.Metals) < OverseerRules.ThinMetals;

        public float FieldReviveReadyIn => Mathf.Max(0f, _reviveReadyAt - Time.time);

        private readonly List<SpecialistAgent> _agents = new List<SpecialistAgent>();
        private readonly List<SpecialistAgent> _selected = new List<SpecialistAgent>(MaxPartySize);
        private readonly List<DustStalkerAgent> _stalkers = new List<DustStalkerAgent>();
        private readonly List<HeroParty> _parties = new List<HeroParty>(4);
        private int _nextPartyId = 1;
        private FlagPlacementInput _flagInput;
        private BuildingPlacementInput _buildInput;
        private IsometricCameraController _isoCam;
        private string _stillLeftoverNote = "none";
        private Transform _threatRoot;
        private float _constructionTick;
        private readonly SimClock _sim = new SimClock();
        private AdaptiveMusic _music;
        private double _playSeconds;
        private DebugHud _debugHud;
        private OverseerHud _overseerHud;
        private OverseerAlertView _alertView;
        private int _focusedCampus;
        private CampusNavMesh _campusNav;

        /// <summary>Campus NavMesh (tax collectors path on it too).</summary>
        public CampusNavMesh CampusNav => _campusNav;

        /// <summary>Finished workshops waiting for the treasury to afford their Majesty hire fee.</summary>
        private readonly List<ColonyStructure> _awaitingHire = new List<ColonyStructure>(4);
        private float _hireNagAt;

        /// <summary>(time, gold) pairs of gold reaching the treasury — drives the income readout.</summary>
        private readonly Queue<KeyValuePair<float, int>> _treasuryIncome = new Queue<KeyValuePair<float, int>>(64);
        private int _treasuryIncomeSum;

        /// <summary>Gold that reached the treasury over the last two minutes, per minute.</summary>
        public float TreasuryIncomePerMin
        {
            get
            {
                PruneTreasuryIncome();
                return _treasuryIncomeSum / 2f;
            }
        }

        private void PruneTreasuryIncome()
        {
            float cutoff = Time.time - 120f;
            while (_treasuryIncome.Count > 0 && _treasuryIncome.Peek().Key < cutoff)
                _treasuryIncomeSum -= _treasuryIncome.Dequeue().Value;
        }

        public void NoteTreasuryIncome(int gold)
        {
            if (gold <= 0) return;
            _treasuryIncome.Enqueue(new KeyValuePair<float, int>(Time.time, gold));
            _treasuryIncomeSum += gold;
            PruneTreasuryIncome();
        }

        /// <summary>A tax collector emptied its bag at Commons or a Watchtower.</summary>
        public void NoteCollectorDeposit(int amount, Vector3 at)
        {
            if (amount <= 0) return;
            Settlement?.NoteLevyDeposited(amount);
            NoteTreasuryIncome(amount);
            if (!_levyHomeLogged)
            {
                _levyHomeLogged = true;
                LogOverseer(OverseerRules.GrokLevyHome, 6.2f);
                Alerts.Push("levy_home", $"Tax collector walked {amount} EU home", AlertSeverity.Good, Time.unscaledTime, at);
            }
        }

        /// <summary>Half of a hero's earning went into a guild till (Majesty 2 hero tax).</summary>
        public void NoteGuildTax(int amount)
        {
            if (amount > 0) Stats.TitheCollected += amount;
        }

        /// <summary>Caravans and market sales land in the Market's till (Commons if there is no Market).</summary>
        private bool PayTradeGold(int gold)
        {
            if (gold <= 0 || Village == null) return false;
            var pad = Village.NearestByCategory(ColonyLayout.CampusOrigin, 400f, BuildingCategory.LandingPad);
            Vector3 from = pad != null ? pad.WorldPosition : ColonyLayout.CampusOrigin;
            var till = Village.NearestByCategory(from, 400f, BuildingCategory.Market) ?? Village.CommonsHub();
            if (till == null || !till.IsAlive) return false;
            till.AccrueLevy(gold);
            return true;
        }

        /// <summary>Majesty trading post: the longer the pad → market road, the richer the caravan.</summary>
        private void RefreshCaravanValue()
        {
            if (Economy == null || Village == null) return;
            var pad = Village.NearestByCategory(ColonyLayout.CampusOrigin, 400f, BuildingCategory.LandingPad);
            var market = pad != null ? Village.NearestByCategory(pad.WorldPosition, 400f, BuildingCategory.Market) : null;
            float d = pad != null && market != null ? FlatDist(pad.WorldPosition, market.WorldPosition) : 0f;
            Economy.CaravanGold = MajestyEconomy.CaravanGold(d);
        }

        /// <summary>Workshops that finished while the treasury was short hire as soon as it can pay.</summary>
        private void TickAwaitingHires()
        {
            for (int i = _awaitingHire.Count - 1; i >= 0; i--)
            {
                var st = _awaitingHire[i];
                if (st == null || !st.IsAlive || st.RobotFabricated)
                {
                    _awaitingHire.RemoveAt(i);
                    continue;
                }
                if (TryFabricateRobot(st, chargeHire: true))
                    _awaitingHire.RemoveAt(i);
            }
        }
        private MissionController _mission;
        private PlanetaryWorldGen _world;
        private CelestialBodyProfile _body;
        private TechEffects _tech = TechEffects.Neutral;
        private bool _launchCraftStaged;
        private DemoScreen _settingsReturn = DemoScreen.Title;
        private float _autosaveTimer;
        private float _interestTimer;
        private float _ecologyCooldown = 5f;
        private float _techRefreshCooldown;
        private bool _faunaRetreated;
        private bool _skipFaunaGrace;
        private bool _everHadRobot;
        private bool _radWarned;
        private float _glanceCooldown;
        private bool _bodyHopQueued;
        private CelestialBodyId _bodyHopTarget;
        private bool _bodyHopUnlock;
        private GameObject _outpostBeacon;
        private readonly Dictionary<EntityId, float> _extractStamp = new Dictionary<EntityId, float>(8);
        private readonly List<ConstructionOrder> _completedBuilds = new List<ConstructionOrder>(8);
        private float _emptyRosterTimer;
        private float _reviveReadyAt;
        private bool _revivePenaltyApplied;
        private bool _yardBillLogged;
        private readonly GrokSession _grok = new GrokSession();
        private int _lastTithe;
        private float _purseToastAt;
        private readonly List<TimedDisc> _surveys = new List<TimedDisc>(4);
        private readonly List<TimedDisc> _watches = new List<TimedDisc>(4);
        private DustStalkerAgent _batteryLock;
        private float _batteryLockUntil;
        private readonly List<SpecialistRecord> _corpses = new List<SpecialistRecord>(8);
        private readonly List<GameObject> _wreckVisuals = new List<GameObject>(8);
        private readonly List<SpecialistRecord> _rosterSaved = new List<SpecialistRecord>(8);
        private readonly Dictionary<SpecialistClass, SpecialistRecord> _pendingVeterans =
            new Dictionary<SpecialistClass, SpecialistRecord>(8);
        private float _junkTimer;
        private float _lastMechDeathAt = -999f;
        private NarrativeBeatTracker _narrative;
        private string _consumedTravelLog;
        private bool _continuedColony;
        private bool _levyHomeLogged;

        private struct TimedDisc
        {
            public Vector3 Pos;
            public float Until;
            public float Extra;
        }

        public MissionController Mission => _mission;
        public OverseerLog Log { get; } = new OverseerLog();
        public NarrativeBeatTracker Narrative => _narrative;
        public bool ContinuedColony => _continuedColony;

        /// <summary>
        /// Soft camera pan toward a world event. Rate-limited so it never fights the player.
        /// Omit orthoSize to keep the current zoom — fauna must not yank a campus close-up
        /// back out to the empty-drop 16. After a campus snap, cooldown also blocks pan.
        /// </summary>
        public void GlanceAt(Vector3 world, float? orthoSize = null, bool force = false)
        {
            if (!IsPlaying || _isoCam == null) return;
            if (!force && _glanceCooldown > 0f) return;
            bool campusPlaced = Placer != null && Placer.Pieces != null && Placer.Pieces.Count > 0;
            // Fauna / minimap must not yank a campus close-up back out to empty-drop 16.
            if (campusPlaced)
                orthoSize = null;
            _isoCam.GlanceAt(world, orthoSize);
            _glanceCooldown = campusPlaced ? 8f : 5.5f;
        }

        /// <summary>Prioritised, deduplicated notifications. Distinct from the flat Overseer log.</summary>
        public AlertFeed Alerts { get; } = new AlertFeed();

        /// <summary>Per-run tallies for the end-of-run summary and achievement checks.</summary>
        public RunStats Stats { get; } = new RunStats();

        /// <summary>Active ground overlay. Cycled with V.</summary>
        public MapOverlayMode OverlayMode { get; private set; } = MapOverlayMode.None;

        public void LogOverseer(string line) => LogOverseer(line, 4.2f);

        public void LogOverseer(string line, float seconds)
        {
            if (StillCaptureHold.Active) return;
            Log.Push(line);
            _overseerHud?.Notify(line, seconds);
            // Scripted Grok lines are baked; everything else in the log stays text.
            OverseerVoice.SpeakIfScripted(line);
        }

        public bool GrokLessonsOn => GrokAdvisor.TrainingWheels(celestialBody);

        /// <summary>Stalker drank the tank. Failure aside, not a sale.</summary>
        public void NoteIceSiphon() => TryGrok(GrokBeat.StalkerSiphon, true);

        private bool TryGrok(GrokBeat beat, bool condition = true)
        {
            if (StillCaptureHold.Active) return false;
            bool wheels = GrokAdvisor.TrainingWheels(celestialBody);
            if (!_grok.TrySpeak(beat, wheels, condition)) return false;
            LogOverseer(GrokCatalog.Say(beat), 6.2f);
            return true;
        }

        private void TickGrok()
        {
            if (StillCaptureHold.Active) return;
            if (Settlement != null && Settlement.CoreHabs > 0)
                TryGrok(GrokBeat.FirstHab);
            bool rosterTicking = _everHadRobot && RobotCount <= 0 &&
                                 _emptyRosterTimer > 0.05f && !EmptyRosterFailed;
            TryGrok(GrokBeat.EmptyRoster, rosterTicking);
        }

        /// <summary>
        /// Raise an alert as well as logging. Use a stable key so repeats collapse into one row
        /// rather than flooding the feed.
        /// </summary>
        public void RaiseAlert(string key, string message, AlertSeverity severity)
        {
            Alerts.Push(key, message, severity, Time.unscaledTime);
            Log.Push(message);
            DemoAudio.PlayAlert(severity);
            if (severity >= AlertSeverity.Warning)
                OverseerVoice.Speak(message, severity);
        }

        public void RaiseAlert(string key, string message, AlertSeverity severity, Vector3 at)
        {
            Alerts.Push(key, message, severity, Time.unscaledTime, at);
            Log.Push(message);
            DemoAudio.PlayAlert(severity);
            if (severity >= AlertSeverity.Warning)
                OverseerVoice.Speak(message, severity);
        }

        public void NoteLevyDeposited(int amount, Vector3 at)
        {
            if (amount <= 0) return;
            Settlement?.NoteLevyDeposited(amount);
            NoteTreasuryIncome(amount);
            if (!_levyHomeLogged)
            {
                _levyHomeLogged = true;
                LogOverseer(OverseerRules.GrokLevyHome, 6.2f);
            }
            else
                LogOverseer($"Haul deposited {amount} EU at Commons.");
            Alerts.Push("levy_home", $"Levy walked home · {amount} EU", AlertSeverity.Good, Time.unscaledTime, at);
        }

        public void NoteLevyStolen(int amount, Vector3 at, bool fromHab)
        {
            if (amount <= 0) return;
            Settlement?.NoteLevyStolen(amount);
            string line = fromHab
                ? OverseerRules.GrokLevyStolenHab
                : OverseerRules.GrokLevyStolenCourier;
            LogOverseer(line, 6.2f);
            Alerts.Push(
                fromHab ? "levy_stolen_hab" : "levy_stolen_courier",
                line,
                AlertSeverity.Warning,
                Time.unscaledTime,
                at);
            DemoAudio.PlayAlertWarning();
            OverseerVoice.Speak(line, AlertSeverity.Warning);
        }

        private void OnAchievementEarned(AchievementDef def)
        {
            if (def == null) return;
            RaiseAlert($"ach_{def.Id}", $"Achievement — {def.Title}", AlertSeverity.Good);
            PlaytestTelemetry.Record("achievement", "id", def.Id.ToString());
        }

        private void HandleOverlayHotkeys()
        {
            if (InputBindings.TextEntryActive) return; // typing flag orders
            if (Input.GetKeyDown(KeyCode.V))
            {
                // V cycles off → danger → coverage. The power overlay retired with the grid.
                OverlayMode = OverlayMode switch
                {
                    MapOverlayMode.None => MapOverlayMode.Danger,
                    MapOverlayMode.Danger => MapOverlayMode.Coverage,
                    _ => MapOverlayMode.None
                };
                LogOverseer(OverlayMode == MapOverlayMode.None
                    ? "Overlays off."
                    : $"Overlay: {MapOverlay.TitleFor(OverlayMode)}.");
            }

            // Jump to whatever is going most wrong right now.
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                Alert urgent = Alerts.MostUrgentWithPosition();
                if (urgent != null)
                {
                    GlanceAt(urgent.WorldPosition, force: true);
                    Alerts.Acknowledge(urgent);
                }
            }
        }

        /// <summary>
        /// Camera shake for impacts and launches, scaled down with distance from the view centre so
        /// something happening off-screen does not rattle what the player is actually looking at.
        /// </summary>
        public void ShakeCamera(float strength, Vector3 at)
        {
            if (_isoCam == null || !IsPlaying) return;

            float dist = FlatDist(at, mainCamera != null ? mainCamera.transform.position : at);
            float falloff = Mathf.Clamp01(1f - dist / 90f);
            if (falloff <= 0.01f) return;

            _isoCam.AddShake(strength * falloff);
        }

        public void NoteRadiationExposure()
        {
            if (_radWarned) return;
            _radWarned = true;
            LogOverseer("Radiation outside the Commons — robots take damage far from campus.");
        }

        /// <summary>HUD helper for hold timer display.</summary>
        public string FormatHold(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RearmEditorStillHold() => RearmStillHoldIfEditorShutter();
#endif

        private static void RearmStillHoldIfEditorShutter()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(StillCaptureHold.EditorSessionKey, false))
                StillCaptureHold.Arm();
#endif
        }

        private void Awake()
        {
            RearmStillHoldIfEditorShutter();
            DemoSettings.Load();
            DemoSettings.SaveLoadNotice = "";
            SimSpeed.Load();
            PlaytestTelemetry.Begin(Application.version);
            Achievements.BeginRun();
            Achievements.Earned += OnAchievementEarned;
            CampaignProgress.Ensure();
            celestialBody = BodySeed.LoadSavedBody();
            if (SaveSystem.TryRead(SaveSystem.AutosaveSlot, out var bootSave))
            {
                DemoSettings.SaveExists = true;
                CampaignProgress.UnlockThrough((CelestialBodyId)bootSave.highestUnlocked);
                CampaignProgress.UnlockThrough((CelestialBodyId)bootSave.body);
                // An explicit travel/retry request takes precedence over the last active planet.
                if (!DemoSettings.BootStraightIntoPlay)
                    celestialBody = (CelestialBodyId)bootSave.body;
                ReplayRules.Restore(bootSave.replay);
                if (bootSave.rosterBlob != null)
                {
                    DemoSettings.FirstHourDemo = bootSave.firstHourDemo;
                    DemoSettings.TutorialDone = bootSave.tutorialDone;
                }
                else if (celestialBody != CelestialBodyId.Earth)
                    DemoSettings.FirstHourDemo = false;
            }
            else if (SaveSystem.IsNewerVersion(SaveSystem.AutosaveSlot))
            {
                DemoSettings.SaveExists = true;
                DemoSettings.SaveLoadNotice = "This colony was saved by a newer game version. Update the game to Continue; your save is unchanged.";
            }
            else if (SaveSystem.Exists(SaveSystem.AutosaveSlot))
            {
                DemoSettings.SaveExists = true;
                DemoSettings.SaveLoadNotice = "We couldn't read this colony or its backup. Your save files are unchanged.";
            }
            if (!CampaignProgress.IsUnlocked(celestialBody))
                celestialBody = CelestialBodyId.Earth;
            _body = CelestialBodyCatalog.Get(celestialBody);
            ModularBuildingFactory.BindBody(_body);
            IndustrialArtDressing.BindBody(_body);
            BodySeed.Ensure(celestialBody, worldSeedOverride);
            // Choose the world seed before generating its terrain, nodes and lairs.
            if (worldSeedOverride == 0 && SaveSystem.TryReadWorld(celestialBody, out var worldSave))
                BodySeed.SetAndPersist(worldSave.seed);
            else if (worldSeedOverride == 0 && SaveSystem.TryRead(SaveSystem.AutosaveSlot, out var latestSave)
                     && latestSave.body == (int)celestialBody)
                BodySeed.SetAndPersist(latestSave.seed);

            EnsureSceneRefs();
            BuildPureSystems();
            EnsureContent();
            WireInputDrivers();
            ConfigureCamera();
            Village = GetComponent<VillageExpansion>();
            if (Village == null) Village = gameObject.AddComponent<VillageExpansion>();
            Village.Bind(this);
            if (spawnShowcaseColony)
                SpawnShowcaseColony();
            else
                SeedEmptyStartClaim();
            GenerateWorld();
            SpawnParty();
            SpawnThreats();
            EnsureNavMesh();
            DemoAtmosphere.Apply(mainCamera, transform, _body);
            PlanetaryMapDressing.Apply(transform, grid, _body);
            // Vista ponds add NavMesh carve obstacles after the first bake — rebuild once.
            if (_campusNav != null && grid != null)
            {
                _campusNav.Build(grid);
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i]?.BindNavMesh(_campusNav);
            }
            // Dressing owns sky + void fill; re-assert on GameLoop's camera (Awake may run before MainCamera tag resolves).
            if (mainCamera != null && _body != null)
                PlanetaryMapDressing.ApplyCameraVoidFill(mainCamera, _body, RenderSettings.skybox != null);
            KingdomLife.Dress(transform, emptyStart: StartsEmpty);
            CampusDressing.Reset();
            RefreshSuitCrossings();
            EnsureHud();
            TryInit("alerts", () => { _alertView = OverseerAlertView.Ensure(this); });
            TryInit("map overlay", () => MapOverlay.Ensure(this));
            EnsureMission();
            LaunchSite.ClearSession();
            _launchCraftStaged = false;
            BootstrapResearch();
            SyncLaunchGate();
            DemoAudio.Ensure();
            DemoAudio.SetBody(_body);
            DemoAudio.ApplyVolumes();
            DemoAudio.SetCampusAmbient(0);
            TryInit("spatial audio", () => SpatialAudio.Ensure());
            TryInit("character voices", () => { CharacterVoice.Ensure(); _ = CharacterVoice.Bank; });
            // Adaptive music synthesises several seconds of audio on the main thread — do it after
            // the first frame so the title screen is visible while it warms up.

            string travel = CampaignProgress.ConsumeTravelLog();
            _consumedTravelLog = travel;
            if (!StillCaptureHold.Active && !string.IsNullOrEmpty(travel))
                Log.Push(travel);
            // W2 arrival cuts replace stale Earth ArrivalLog and overlay Luna/Mars fauna tees.
            if (!StillCaptureHold.Active &&
                _body != null &&
                !CampaignCutsceneCatalog.TryGetArrival(_body.Id, out _) &&
                !string.IsNullOrEmpty(_body.ArrivalLog))
            {
                Log.Push(_body.ArrivalLog);
            }

            if (DemoSettings.BootStraightIntoPlay)
                EnterPlaying(loadStockpile: DemoSettings.SaveExists);
            else
                EnterTitle();

            Debug.Log($"[GameLoop] Demo ready — {_body.DisplayName} seed={BodySeed.Current}" +
                      (StartsEmpty ? " (empty start)." : "."));
        }

        private IEnumerator Start()
        {
            yield return null;
            TryInit("adaptive music", () => { _music = AdaptiveMusic.Ensure(); });
        }

        private static void TryInit(string label, System.Action action)
        {
            try { action(); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"[GameLoop] Optional init failed ({label}); continuing so Play Mode still shows.");
            }
        }

        private void OnDestroy()
        {
            if (Economy != null)
            {
                Economy.UpkeepApplied -= OnUpkeepTithe;
            }
            Achievements.Earned -= OnAchievementEarned;
            PlaytestTelemetry.RecordQuit(
                Screen.ToString(),
                celestialBody,
                0,
                Placer != null ? Placer.Pieces.Count : 0,
                _playSeconds);
            SolarSystemTitleView.Instance?.Hide();
            Time.timeScale = 1f;
        }

        public void EnterTitle()
        {
            Screen = DemoScreen.Title;
            Time.timeScale = 0f;
            ApplyTool(OverseerTool.None);
            SolarSystemTitleView.Ensure(this, mainCamera)?.Show();
        }

        public void EnterPlaying(bool loadStockpile)
        {
            if (loadStockpile && SaveSystem.IsNewerVersion(SaveSystem.AutosaveSlot))
            {
                DemoSettings.SaveLoadNotice = "This colony was saved by a newer game version. Update the game to Continue; your save is unchanged.";
                EnterTitle();
                return;
            }
            if (loadStockpile && SaveSystem.Exists(SaveSystem.AutosaveSlot) &&
                !SaveSystem.TryRead(SaveSystem.AutosaveSlot, out _))
            {
                DemoSettings.SaveLoadNotice = "We couldn't read this colony or its backup. Your save files are unchanged.";
                EnterTitle();
                return;
            }
            DemoSettings.SaveLoadNotice = "";
            SolarSystemTitleView.Instance?.Hide();
            Screen = DemoScreen.Playing;
            Time.timeScale = SimSpeed.TimeScale;
            int piecesBefore = Placer != null ? Placer.Pieces.Count : 0;
            bool restoredSave = false;
            if (loadStockpile)
            {
                SaveSystem.TryRead(SaveSystem.AutosaveSlot, out SaveGame latest);
                SaveGame save = latest != null && latest.MatchesWorld(celestialBody, BodySeed.Current) ? latest : null;
                if (save == null && SaveSystem.TryReadWorld(celestialBody, out var destination)
                                 && destination.MatchesWorld(celestialBody, BodySeed.Current))
                {
                    save = destination;
                    // Resources and research travel with the campaign; colony state stays on its world.
                    if (latest != null)
                    {
                        save.stockpile = latest.stockpile;
                        save.research = latest.research;
                    }
                }
                if (save != null)
                    restoredSave = ApplySave(save);
                else
                {
                    if (latest != null)
                    {
                        Research?.RestoreFrom(latest.research.unlocked, (TechId)latest.research.activeTech,
                            latest.research.activeProgress, latest.research.bankedScience, latest.research.progress);
                        Resources.Set(ResourceId.Regolith, latest.stockpile.regolith);
                        Resources.Set(ResourceId.WaterIce, latest.stockpile.waterIce);
                        Resources.Set(ResourceId.Metals, latest.stockpile.metals);
                        Resources.Set(ResourceId.Power, latest.stockpile.power);
                    }
                    else
                        DemoSettings.TryLoadStockpile(Resources);
                    LoadRosterMemory();
                    RestoreCampus();
                    RetryUnpaidCorpses();
                }
            }
            if (!restoredSave)
            {
                if (loadStockpile && SaveSystem.TryRead(SaveSystem.AutosaveSlot, out var campaignSave))
                    ReplayRules.ApplyIronmanFromSave(campaignSave.replay);
                else
                    ReplayRules.LatchRun();
            }
            Achievements.IronmanActive = ReplayRules.IronmanRun;
            bool restoredCampus = loadStockpile &&
                                  Placer != null &&
                                  Placer.Pieces.Count > piecesBefore;
            bool continuedColony = restoredCampus &&
                                   Settlement != null &&
                                   Settlement.HasCommons;
            _continuedColony = continuedColony;
            if (continuedColony)
            {
                _skipFaunaGrace = true;
                _ecologyCooldown = OverseerRules.ContinueReentrySeconds;
            }
            else
                _ecologyCooldown = OverseerRules.FaunaGraceSeconds;
            if (!spawnShowcaseColony && !continuedColony)
                PlaceFirstHourShell();
            if (!spawnShowcaseColony && (Settlement == null || !Settlement.HasCommons))
                PlaceDropCommons();
            if (Settlement != null && Settlement.HasCommons)
                SnapCampusCamera();
            PersistSession();
            TutorialStep = DemoSettings.TutorialDone ? TutorialGoal : 0;
            TickTutorial();
            RearmStillHoldIfEditorShutter();
            _overseerHud?.OnSessionPlaying();
            BeginNarrativeSession();
            if (ReplayRules.Mode != ColonyRunMode.Campaign ||
                ReplayRules.Challenge != ChallengeId.None ||
                ReplayRules.Stance != DoctrineStance.Balanced ||
                ReplayRules.IronmanRun)
            {
                LogOverseer($"Replay: {ReplayRules.HudTag}. Doctrine nudges hunger/courage/workshop pull only.");
            }
        }

        public void StartNewGame() => StartNewGame(CampaignProgress.NewGameBody);

        /// <summary>Wipes the continue slot and returns to the solar-system title.</summary>
        public void WipeCampaignToTitle()
        {
            CampaignProgress.ResetCampaign();
            SaveSystem.DeleteAll();
            SimSpeed.Load(); // fresh runs start at the player's preferred speed, never held
            DemoSettings.ClearSave();
            DemoSettings.ResetTutorial();
            ReplayRules.Save();
            ReloadActiveScene();
        }

        /// <summary>Start a fresh campaign on the selected world; Earth is the default.</summary>
        public void StartNewGame(CelestialBodyId drop)
        {
            if (!CampaignProgress.IsOnSpine(drop))
                drop = CampaignProgress.NewGameBody;
            CampaignProgress.BeginNewGame(drop);
            SaveSystem.DeleteAll();
            SimSpeed.Load(); // fresh runs start at the player's preferred speed, never held
            DemoSettings.ClearSave();
            if (drop == CelestialBodyId.Mars)
                DemoSettings.MarkTutorialDone();
            else
                DemoSettings.ResetTutorial();
            ReplayRules.Save();
            DemoSettings.RequestBootIntoPlay();
            BodySeed.SetBody(drop);
            ReloadActiveScene();
        }

        public void StartNewGameOn(CelestialBodyId body)
        {
            ResearchManager.WipeUnlocks();
            CampaignProgress.ResetCampaign();
            CampaignProgress.UnlockThrough(body);
            SaveSystem.DeleteAll();
            SimSpeed.Load(); // fresh runs start at the player's preferred speed, never held
            DemoSettings.ClearSave();
            DemoSettings.ResetTutorial();
            ReplayRules.Save();
            DemoSettings.RequestBootIntoPlay();
            BodySeed.SetBody(body);
            ReloadActiveScene();
        }

        /// <summary>Title orrery click. Never posts flags or specialist orders.</summary>
        public void PlayBodyFromTitle(CelestialBodyId body, bool cheatUnlock)
        {
            if (DemoSettings.FirstHourDemo && body != CelestialBodyId.Earth)
            {
                _overseerHud?.Notify(
                    "This demo is Earth. Settings → Full campaign opens the other worlds.",
                    3.5f);
                return;
            }

            var profile = CelestialBodyCatalog.Get(body);
            var outcome = SolarSystemTitlePick.Resolve(
                body,
                celestialBody,
                DemoSettings.SaveExists,
                CampaignProgress.IsUnlocked(body),
                cheatUnlock);

            switch (outcome)
            {
                case SolarSystemTitlePick.Outcome.Locked:
                    _overseerHud?.Notify(
                        $"{profile.DisplayName} is locked. Conquer the inner worlds first — or Shift+click.",
                        3.5f);
                    return;
                case SolarSystemTitlePick.Outcome.StartNewOnBody:
                    StartNewGameOn(body);
                    return;
                case SolarSystemTitlePick.Outcome.ContinueCurrent:
                    ContinueGame();
                    return;
                case SolarSystemTitlePick.Outcome.SwitchBody:
                    if (cheatUnlock)
                        CampaignProgress.UnlockThrough(body);
                    SelectBody(body, allowLocked: true);
                    return;
            }
        }

        public void ContinueGame()
        {
            if (!DemoSettings.SaveExists)
            {
                EnterPlaying(loadStockpile: false);
                return;
            }
            EnterPlaying(loadStockpile: true);
        }

        public void TogglePause()
        {
            if (Screen == DemoScreen.Title || Screen == DemoScreen.Settings) return;
            if (Screen == DemoScreen.Paused)
                ResumePlay();
            else if (Screen == DemoScreen.Playing)
                PausePlay();
        }

        public void PausePlay()
        {
            if (Screen != DemoScreen.Playing) return;
            Screen = DemoScreen.Paused;
            Time.timeScale = 0f;
            ApplyTool(OverseerTool.None);
            PersistSession();
        }

        public void ResumePlay()
        {
            Screen = DemoScreen.Playing;
            // Leaving the modal pause resumes at the speed the player was running, not always 1x.
            if (SimSpeed.IsPaused)
                SimSpeed.Resume();
            Time.timeScale = SimSpeed.TimeScale;
        }

        public void OpenSettings()
        {
            _settingsReturn = Screen == DemoScreen.Playing ? DemoScreen.Paused : Screen;
            if (Screen == DemoScreen.Playing)
                PausePlay();
            Screen = DemoScreen.Settings;
            Time.timeScale = 0f;
        }

        public void CloseSettings()
        {
            DemoSettings.SaveSettings();
            DemoAudio.ApplyVolumes();
            ApplyReplayToBrain();
            RefreshTechEffects();
            if (_settingsReturn == DemoScreen.Paused)
            {
                Screen = DemoScreen.Paused;
                Time.timeScale = 0f;
            }
            else if (_settingsReturn == DemoScreen.Title)
                EnterTitle();
            else
                ResumePlay();
        }

        public void ReturnToTitle()
        {
            PersistSession();
            EnterTitle();
        }

        public void QuitDemo()
        {
            PersistSession();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SkipTutorial()
        {
            TutorialStep = TutorialGoal;
            DemoSettings.MarkTutorialDone();
        }

        public void RestartTutorial()
        {
            DemoSettings.ResetTutorial();
            TutorialStep = 0;
            _tutorialDefendPosted = false;
            _tutorialPestSpawned = false;
            _overseerHud?.Notify(
                DemoSettings.FirstHourDemo
                    ? "Tutorial reset — workshop, Build at 700, raise the price, then Defend."
                    : "Tutorial reset — Commons, then HAB, workshop, flag, bounty.",
                4f);
        }

        public void SetFirstHourDemo(bool on)
        {
            DemoSettings.SetFirstHourDemo(on);
            ApplyReplayToBrain();
            _overseerHud?.Notify(
                on
                    ? "Earth demo on. Guilds, Belt, and Europa are hidden. New Game to start clean."
                    : "Full campaign on. Other worlds, guilds, and replay rules are back.",
                4f);
        }

        public void NotifyTechOpened()
        {
        }

        public void CancelFlag(FlagHandle handle)
        {
            if (handle == null || Flags == null) return;
            int refund = handle.EscrowMetals;
            Economy?.RefundBountyEscrow(refund);
            Flags.Cancel(handle);
            _overseerHud?.Notify(refund > 0 ? $"Flag cancelled — {refund} EU returned." : "Flag cancelled.", 2.4f);
            Debug.Log("[Flags] Cancelled — metals refunded.");
        }

        public void NotifyFlagPosted(FlagHandle handle)
        {
            RefreshFlagInterest();
            if (handle == null) return;
            BroadcastRefusalChips(handle);

            EnsureNarrative();
            bool wheels = GrokAdvisor.TrainingWheels(celestialBody);

            if (TryGrok(GrokBeat.FirstFlag))
            {
                // first-flag lesson; decree copy waits for the next post
            }
            else if (wheels &&
                     NarrativeBeatTracker.TryResolvePosted(handle, celestialBody, CurrentNarrativeHint(), out var decree) &&
                     _narrative.TryTakePostToast(decree.Id, out var toast))
            {
                LogOverseer(toast.Line, 6.5f);
            }

            if (handle.InterestCount <= 0)
            {
                if (wheels)
                    TryGrok(GrokBeat.GreedAsk);
                else
                    TryGrok(GrokBeat.Refusal);
            }

            string interest = InterestLine(handle);
            if (!string.IsNullOrEmpty(interest))
            {
                LogOverseer(interest);
            }

            if (DemoSettings.FirstHourDemo &&
                TutorialStep == FirstHourTutorial.PestStep &&
                handle.Data != null &&
                handle.Data.flagType == FlagType.DefendArea)
            {
                _tutorialDefendPosted = true;
            }

            PlaytestTelemetry.Record("flag_posted", new[]
            {
                ("type", handle.Data != null ? handle.Data.flagType.ToString() : ""),
                ("bounty", handle.CurrentBounty.ToString("F0")),
                ("escrow", handle.EscrowMetals.ToString()),
                ("interest", handle.InterestCount.ToString())
            });
        }

        public void NotifyFlagClaimed(FlagHandle handle)
        {
            if (handle != null)
            {
                PlaytestTelemetry.Record("flag_claimed", new[]
                {
                    ("type", handle.Data != null ? handle.Data.flagType.ToString() : ""),
                    ("bounty", handle.CurrentBounty.ToString("F0"))
                });
            }

            EnsureNarrative();
            if (!NarrativeBeatTracker.TryResolvePosted(handle, celestialBody, CurrentNarrativeHint(), out var decree))
                return;
            _narrative.NoteClaimed(decree.Id);
            if (_narrative.TryTakeClaimToast(decree.Id, out var toast))
                LogOverseer(toast.Line, 6.5f);
            if (_narrative.TryTakeClaimAside(decree.Id, out string aside))
                Log.Push(aside);
        }

        public void NotifyFlagCompleted(FlagHandle handle)
        {
            EnsureNarrative();
            if (!NarrativeBeatTracker.TryResolvePosted(handle, celestialBody, CurrentNarrativeHint(), out var decree))
            {
                SyncNarrativeCivic();
                return;
            }
            _narrative.NoteCompleted(decree.Id);
            SyncNarrativeCivic();
        }

        private void OnFlagWorkCompleted(FlagHandle handle) => NotifyFlagCompleted(handle);

        private string InterestLine(FlagHandle handle)
        {
            if (_agents.Count == 0)
                return "Flag posted — fabricate a workshop robot before anyone can take it.";
            if (handle.InterestCount <= 0)
                return handle.InterestLabel;
            return $"{handle.InterestCount} tempted: {handle.InterestLabel}";
        }

        private void EnsureNarrative()
        {
            if (_narrative != null) return;
            _narrative = new NarrativeBeatTracker();
        }

        private NarrativeWorldHint CurrentNarrativeHint()
        {
            bool hasPad = Settlement != null && Settlement.HasPad;
            bool padOrder = false;
            bool workshopOrder = false;
            bool padPiece = hasPad;
            if (Placer != null)
            {
                var orders = Placer.Orders;
                for (int i = 0; i < orders.Count; i++)
                {
                    var o = orders[i];
                    if (o == null || o.IsComplete || o.Data == null) continue;
                    if (o.Data.category == BuildingCategory.LandingPad)
                        padOrder = true;
                    if (o.IsRefab || ColonyStructure.IsWorkshopCategory(o.Data.category))
                        workshopOrder = true;
                }

                var pieces = Placer.Pieces;
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (pieces[i].Category == BuildingCategory.LandingPad)
                    {
                        padPiece = true;
                        break;
                    }
                }
            }

            bool launchLive = false;
            if (Research != null)
            {
                var profile = _body != null ? _body : CelestialBodyCatalog.Get(celestialBody);
                TechId launch = profile != null ? profile.LaunchTech : TechId.None;
                if (launch != TechId.None)
                    launchLive = Research.ActiveTech == launch || Research.IsUnlocked(launch);
            }

            return new NarrativeWorldHint
            {
                HasCommons = Settlement != null && Settlement.HasCommons,
                HasHab = Settlement != null && Settlement.CoreHabs > 0,
                HasPad = hasPad,
                HasPower = true,
                HasPadPiece = padPiece,
                HasPadOrder = padOrder,
                HasWorkshopOrder = workshopOrder,
                LaunchPathLive = launchLive
            };
        }

        private void SyncNarrativeCivic()
        {
            if (StillCaptureHold.Active) return;
            EnsureNarrative();
            bool charter = Research != null && Research.IsUnlocked(TechId.GuildCharter);
            _narrative.SyncCivic(celestialBody, CurrentNarrativeHint(), charter, _launchCraftStaged);
        }

        private void BeginNarrativeSession()
        {
            EnsureNarrative();
            if (StillCaptureHold.Active) return;
            SyncNarrativeCivic();

            string arrivalKey = CampaignCutsceneCatalog.ArrivalKey(celestialBody);
            bool hop = !string.IsNullOrEmpty(_consumedTravelLog);
            bool freshDrop = !_continuedColony;
            if (!string.IsNullOrEmpty(arrivalKey) &&
                (hop || freshDrop) &&
                !CampaignProgress.WasCutShown(arrivalKey))
            {
                _narrative.EnqueueCut(arrivalKey);
            }

            string travelKey = AdvisorToastCatalog.TravelKeyForArrival(celestialBody);
            bool announce = hop || freshDrop;
            if (announce && celestialBody == CelestialBodyId.Luna && TryGrok(GrokBeat.Drop))
            {
                // Luna training-wheels drop. W2 arrival copy stays in the catalog.
            }
            else if (announce &&
                     GrokAdvisor.TrainingWheels(celestialBody) &&
                     !string.IsNullOrEmpty(travelKey) &&
                     _narrative.TryTakeTravelToast(travelKey, out var toast))
            {
                LogOverseer(toast.Line, 6.8f);
            }
        }

        public bool TryPeekCutscene(out CampaignCutscene cut)
        {
            cut = default;
            if (StillCaptureHold.Active) return false;
            if (_mission != null && _mission.IsLost) return false;
            EnsureNarrative();
            while (_narrative.PeekCut(out string id))
            {
                if (CampaignProgress.WasCutShown(id) ||
                    !CampaignCutsceneCatalog.TryGet(id, out cut) ||
                    cut.Kind != CutsceneKind.Modal)
                {
                    _narrative.DismissCut();
                    continue;
                }
                return true;
            }
            return false;
        }

        public void DismissCutscene()
        {
            EnsureNarrative();
            if (_narrative.PeekCut(out string id))
            {
                CampaignProgress.NoteCutShown(id);
                _narrative.DismissCut();
            }
        }

        private void TickFlagInterest(float dt)
        {
            _interestTimer += dt;
            if (_interestTimer < 0.45f) return;
            _interestTimer = 0f;
            RefreshFlagInterest();
        }

        public void RefreshFlagInterest()
        {
            if (Flags == null || Brain == null) return;
            var flags = Flags.Flags;
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (flag == null) continue;
                int n = 0;
                string names = "";
                SpecialistAgent nearestMatch = null;
                float nearestMatchD = 999f;
                float bestPref = -1f;
                for (int a = 0; a < _agents.Count; a++)
                {
                    var agent = _agents[a];
                    if (agent == null || agent.IsIncapacitated || agent.Data == null) continue;
                    if (Brain.WouldTakeFlag(agent.PeekContext(), flag, agent.BodyDanger, out _))
                    {
                        n++;
                        string label = ColonyStructure.ClassLabel(agent.Data.specialistClass);
                        if (names.Length == 0) names = label;
                        else if (names.IndexOf(label, System.StringComparison.Ordinal) < 0)
                            names += " · " + label;
                    }

                    float pref = agent.Data.GetPreference(flag.Data != null ? flag.Data.flagType : FlagType.Explore);
                    float d = FlatDist(agent.transform.position, flag.WorldPosition);
                    if (pref > bestPref || (Mathf.Abs(pref - bestPref) < 0.01f && d < nearestMatchD))
                    {
                        bestPref = pref;
                        nearestMatchD = d;
                        nearestMatch = agent;
                    }
                }
                flag.InterestCount = n;
                if (n > 0)
                    flag.InterestLabel = $"{n} tempted · {names}";
                else if (_agents.Count == 0)
                    flag.InterestLabel = "no robots yet";
                else
                    flag.InterestLabel = RefusalSubtitle(flag, nearestMatch);
            }
        }

        private string RefusalSubtitle(FlagHandle flag, SpecialistAgent agent)
        {
            if (flag == null || agent == null || Brain == null)
                return "Ignored — raise bounty (+)";
            var kind = Brain.ExplainFlag(agent.PeekContext(), flag, agent.BodyDanger);
            string cls = ColonyStructure.ClassLabel(agent.Data != null ? agent.Data.specialistClass : SpecialistClass.ScoutDrone);
            switch (kind)
            {
                case FlagRefusalKind.Greed:
                    return $"{cls} wants {agent.HireMin} — raise +";
                case FlagRefusalKind.TooFar:
                    return $"too far for {cls}";
                case FlagRefusalKind.Hurt:
                    return $"{cls} is hurt — wait";
                case FlagRefusalKind.NotMyJob:
                    return $"not a {cls} job";
                default:
                    return "Ignored — raise bounty (+)";
            }
        }

        private void BroadcastRefusalChips(FlagHandle flag)
        {
            if (flag == null || Brain == null) return;
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent == null || agent.IsIncapacitated || agent.Data == null) continue;
                var ctx = agent.PeekContext();
                var kind = Brain.ExplainFlag(ctx, flag, agent.BodyDanger);
                if (kind == FlagRefusalKind.WouldTake) continue;
                float dist = FlatDist(agent.transform.position, flag.WorldPosition);
                float consider = 40f + agent.Data.explorePreference * 35f;
                if (Brain.ConsiderRange > 0f)
                    consider = Mathf.Max(consider, Brain.ConsiderRange * 0.7f);
                if (dist > consider) continue;
                string chip = kind switch
                {
                    FlagRefusalKind.Greed => "TOO CHEAP",
                    FlagRefusalKind.TooFar => "TOO FAR",
                    FlagRefusalKind.Hurt => "NOT NOW",
                    FlagRefusalKind.NotMyJob => "NO",
                    FlagRefusalKind.Hunting => "HUNTING",
                    FlagRefusalKind.Orders => "ORDERS",
                    _ => null
                };
                if (!string.IsNullOrEmpty(chip))
                {
                    agent.ShowRefusal(chip);
                    if (kind == FlagRefusalKind.Orders)
                        CharacterVoice.Cue(agent, VoiceCue.Refused, HeroNarrator.Report(agent, NarrationKind.Refused, flag));
                    PlaytestTelemetry.Record("flag_refused", new[]
                    {
                        ("class", agent.Data.specialistClass.ToString()),
                        ("kind", kind.ToString()),
                        ("bounty", flag.CurrentBounty.ToString("F0"))
                    });
                }
            }
        }

        private void HandleSessionHotkeys()
        {
            if (InputBindings.TextEntryActive) return; // typing flag orders
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Screen == DemoScreen.Settings)
                    CloseSettings();
                else if (Screen == DemoScreen.Title)
                    return;
                else
                    TogglePause();
            }
        }

        private void TickTutorial()
        {
            if (DemoSettings.TutorialDone || TutorialStep >= TutorialGoal) return;
            if (DemoSettings.FirstHourDemo)
            {
                TickFirstHourTutorial();
                return;
            }

            for (int n = 0; n < TutorialCompleteStep; n++)
            {
                int before = TutorialStep;
                if (TutorialStep == 0 && Settlement != null && Settlement.HasCommons)
                    AdvanceTutorial();
                else if (TutorialStep == 1 && Village != null && Village.Collectors.Count > 0)
                    AdvanceTutorial();
                else if (TutorialStep == 2 && Settlement != null && Settlement.Habs > 0)
                    AdvanceTutorial();
                else if (TutorialStep == 3 && HasAnyWorkshop())
                    AdvanceTutorial();
                else if (TutorialStep == 4 && Flags != null && Flags.Flags.Count > 0)
                    AdvanceTutorial();
                else if (TutorialStep == 5 && AnyRobotTempted())
                    AdvanceTutorial();
                if (TutorialStep == before || TutorialStep >= TutorialCompleteStep)
                    break;
            }
        }

        private void TickFirstHourTutorial()
        {
            bool engineer = HasLivingEngineer();
            bool refused = HasBuildFlag(tempted: false);
            bool tempted = HasBuildFlag(tempted: true);
            for (int n = 0; n < FirstHourTutorial.Goal; n++)
            {
                int next = FirstHourTutorial.Advance(
                    TutorialStep, engineer, refused, tempted, _tutorialDefendPosted);
                if (next == TutorialStep) break;
                TutorialStep = next;
                PlaytestTelemetry.Record("tutorial_step", "step", TutorialStep);
                if (TutorialStep == FirstHourTutorial.PestStep)
                    SpawnTutorialPest();
                if (TutorialStep >= FirstHourTutorial.Goal)
                {
                    DemoSettings.MarkTutorialDone();
                    break;
                }
            }
        }

        private bool HasLivingEngineer()
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent == null || !agent.IsAlive || agent.Data == null) continue;
                if (agent.Data.specialistClass == SpecialistClass.EngineerBot)
                    return true;
            }
            return false;
        }

        private bool HasBuildFlag(bool tempted)
        {
            if (Flags == null) return false;
            var list = Flags.Flags;
            for (int i = 0; i < list.Count; i++)
            {
                var flag = list[i];
                if (flag?.Data == null || flag.Data.flagType != FlagType.Build) continue;
                bool interested = flag.InterestCount > 0;
                if (interested == tempted) return true;
            }
            return false;
        }

        private void SpawnTutorialPest()
        {
            if (_tutorialPestSpawned) return;
            Vector3 home = ColonyLayout.CampusOrigin + new Vector3(10f, 0f, 6f);
            if (SpawnFaunaAt(FaunaKind.Creeper, home) == null) return;
            _tutorialPestSpawned = true;
            LogOverseer("Soil creeper on the yard — post Defend (F5).");
        }

        private bool HasAnyWorkshop()
        {
            if (Placer == null) return false;
            var pieces = Placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (ColonyStructure.IsWorkshopCategory(pieces[i].Category))
                    return true;
            }
            return false;
        }

        public bool AnyRobotTempted()
        {
            if (Flags == null) return false;
            var list = Flags.Flags;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].InterestCount > 0)
                    return true;
            }
            return false;
        }

        public bool FirstHourBuildAlreadyTempting =>
            DemoSettings.FirstHourDemo &&
            TutorialStep == 1 &&
            HasBuildFlag(tempted: true) &&
            !HasBuildFlag(tempted: false);

        public bool TutorialWantsPriceLesson =>
            DemoSettings.FirstHourDemo
                ? TutorialStep == 2 && HasBuildFlag(tempted: false) && !HasBuildFlag(tempted: true)
                : TutorialStep == 5 && Flags != null && Flags.Flags.Count > 0 && !AnyRobotTempted();

        private void AdvanceTutorial()
        {
            TutorialStep++;
            PlaytestTelemetry.Record("tutorial_step", "step", TutorialStep);
            if (TutorialStep >= TutorialGoal)
                DemoSettings.MarkTutorialDone();
        }

        private void TickAutosave()
        {
            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer < 20f) return;
            _autosaveTimer = 0f;
            PersistSession();
        }

        private void TryCancelFlagUnderCursor()
        {
            if (!Input.GetMouseButtonUp(1)) return;
            if (_isoCam != null && (_isoCam.IsDragging || _isoCam.SuppressFlagCancel)) return;
            if (_overseerHud != null && _overseerHud.PointerBlocksWorld) return;
            if (mainCamera == null || Flags == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 400f)) return;
            var marker = hit.collider.GetComponentInParent<FlagMarker>();
            if (marker != null && marker.Handle != null)
                CancelFlag(marker.Handle);
        }

        /// <summary>
        /// Empty campaign start: soft claim at Campus A so the first modules must dock there.
        /// Colony Commons is auto-dropped on the claim (not the showcase pack).
        /// </summary>
        private void SeedEmptyStartClaim()
        {
            if (Placer == null || grid == null) return;

            SeedSoftClaim(ColonyLayout.CampusOrigin, campus: true);
            SeedSoftClaim(ColonyLayout.CampusBOrigin, campus: false);
            if (!DemoSettings.SaveExists)
            {
                // Capture stills and non-Earth drops keep the Commons-only claim.
                // The Earth demo also drops a HAB.
                if (!DemoSettings.FirstHourDemo ||
                    celestialBody != CelestialBodyId.Earth ||
                    StillCaptureHold.Active)
                    PlaceDropCommons();
                PlaceFirstHourShell();
            }
        }

        /// <summary>
        /// First-drop Commons on the Campus A claim. Free, complete, and live — player
        /// does not open BLD. Showcase colony stays off; extra modules are still unbuilt.
        /// </summary>
        private void PlaceDropCommons()
        {
            if (spawnShowcaseColony || Placer == null || grid == null) return;
            if (Placer.HasCommonsModule) return;
            if (Settlement != null && Settlement.HasCommons) return;

            var data = DataForCategory(BuildingCategory.Commons);
            if (data == null && starterBuildings != null && starterBuildings.Length > 0)
                data = starterBuildings[0];
            if (data == null || data.category != BuildingCategory.Commons) return;

            int fw = Mathf.Max(1, data.footprintWidth);
            int fh = Mathf.Max(1, data.footprintHeight);
            float cell = grid.CellSize;
            float half = (fw * cell) * 0.5f;
            Vector3 corner = ColonyLayout.CampusOrigin
                - new Vector3(half, 0f, half)
                + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            Vector2Int origin = grid.WorldToCell(corner);
            Vector3 world = FootprintWorldCenter(origin, fw, fh);
            if (!Placer.TryRestore(data, origin, world, 1f, out _))
            {
                Debug.LogWarning("[GameLoop] Drop Commons failed to occupy the Campus A claim.");
                return;
            }

            Transform root = buildingRoot != null ? buildingRoot : transform;
            GameObject go = ModularBuildingFactory.Spawn(
                data.category, world, root, fw, fh, cell);
            go.name = "Bld_ColonyCommons_drop";
            CampusNavMesh.AddObstacle(go);
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
            CampusDressing.DressPlaced(data, go, _body);
            HideDropClaimIfSettled();
            if (_campusNav != null)
                NotifyCampusExpanded();
            SnapCampusCamera();
            bool demoShell = DemoSettings.FirstHourDemo &&
                             celestialBody == CelestialBodyId.Earth &&
                             !StillCaptureHold.Active;
            if (!demoShell)
                Log.Push("Colony Commons is down — build workshops, houses and shops anywhere nearby.");
            Debug.Log("[GameLoop] Auto-placed Colony Commons on the Campus A claim.");
        }

        /// <summary>
        /// Earth demo start: the Commons and one tax house (HAB) are already finished, standing
        /// apart on open ground. The first thing the player builds is the Engineer workshop.
        /// </summary>
        private void PlaceFirstHourShell()
        {
            if (!DemoSettings.FirstHourDemo) return;
            if (celestialBody != CelestialBodyId.Earth) return;
            if (StillCaptureHold.Active || spawnShowcaseColony) return;

            PlaceDropCommons();
            if (Settlement != null && Settlement.CoreHabs > 0)
                return;
            if (!TryCommonsPiece(out var commons)) return;

            var habData = DataForCategory(BuildingCategory.Habitat);
            if (habData == null) return;

            int habW = Mathf.Max(1, habData.footprintWidth);
            int habH = Mathf.Max(1, habData.footprintHeight);
            const int gap = 3;
            // East, west, north, south of the Commons, with a few cells of dirt between them.
            var tries = new[]
            {
                new Vector2Int(commons.Origin.x + commons.Width + gap, commons.Origin.y + (commons.Height - habH) / 2),
                new Vector2Int(commons.Origin.x - gap - habW, commons.Origin.y + (commons.Height - habH) / 2),
                new Vector2Int(commons.Origin.x + (commons.Width - habW) / 2, commons.Origin.y + commons.Height + gap),
                new Vector2Int(commons.Origin.x + (commons.Width - habW) / 2, commons.Origin.y - gap - habH)
            };
            foreach (var habCell in tries)
            {
                if (!RectInBounds(habCell, habW, habH)) continue;
                if (!Placer.CanFitRect(habCell, habW, habH)) continue;
                if (FootprintOverWater(habCell, habW, habH)) continue;
                if (!PlaceComplete(habData, habCell, "Bld_HAB_drop"))
                    continue;

                HideDropClaimIfSettled();
                if (_campusNav != null)
                    NotifyCampusExpanded();
                SnapCampusCamera();
                Log.Push("The Commons and a house are down. Build an Engineer workshop anywhere nearby.");
                Debug.Log("[GameLoop] First-hour shell placed (Commons, HAB).");
                return;
            }

            Debug.LogWarning("[GameLoop] First-hour HAB could not fit beside the Commons.");
        }

        private bool TryCommonsPiece(out BuildingPlacer.CampusPiece piece)
        {
            piece = default;
            if (Placer == null) return false;
            var pieces = Placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Category != BuildingCategory.Commons) continue;
                piece = pieces[i];
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- village growth

        /// <summary>Catalog entry villagers build for this category (house or solar farm).</summary>
        public BuildingData VillageData(BuildingCategory cat) => DataForCategory(cat);

        /// <summary>
        /// Open ground in rings around the Commons, a cell of dirt clear on every side, in bounds,
        /// dry, and off the launch pad. <paramref name="salt"/> turns the ring so the village
        /// spreads around the Commons instead of stacking on one side.
        /// </summary>
        public bool TryFindVillagePlot(BuildingData data, int salt, out Vector2Int origin, out Vector3 world)
        {
            origin = default;
            world = default;
            if (data == null || Placer == null || grid == null) return false;
            if (!TryCommonsPiece(out var commons)) return false;
            int w = Mathf.Max(1, data.footprintWidth), h = Mathf.Max(1, data.footprintHeight);
            Vector3 hub = FootprintWorldCenter(commons.Origin, commons.Width, commons.Height);
            float cs = grid.CellSize;
            Vector3 half = new Vector3((w - 1) * 0.5f * cs, 0f, (h - 1) * 0.5f * cs);
            for (float r = VillageGrowth.MinRing; r <= VillageGrowth.MaxRing; r += 3f)
            {
                int n = Mathf.Max(8, Mathf.RoundToInt(r * 0.6f));
                for (int k = 0; k < n; k++)
                {
                    float a = (k / (float)n + salt * 0.137f) * Mathf.PI * 2f;
                    Vector3 c = hub + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    Vector2Int o = grid.WorldToCell(c - half);
                    var ring = new Vector2Int(o.x - 1, o.y - 1);
                    if (!RectInBounds(ring, w + 2, h + 2)) continue;
                    if (!Placer.CanFitRect(ring, w + 2, h + 2)) continue;
                    if (FootprintOverWater(o, w, h)) continue;
                    Vector3 at = FootprintWorldCenter(o, w, h);
                    if (FlatDist(at, LaunchSite.PadWorld) < 10f) continue;
                    origin = o;
                    world = at;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Villagers only build while no enemy is near the Commons or the site.</summary>
        public bool IsSettlementSafe(Vector3 site)
        {
            if (StillCaptureHold.Active) return true;
            Vector3 hub = Village != null && Village.CommonsHub() != null
                ? Village.CommonsHub().WorldPosition
                : ColonyLayout.CampusOrigin;
            float r = VillageGrowth.SafeRadius;
            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s == null || !s.IsAlive) continue;
                Vector3 p = s.transform.position;
                if (FlatDist(p, hub) < r || FlatDist(p, site) < r) return false;
            }
            return true;
        }

        /// <summary>A villager project is finished: stand the building up and start its tax.</summary>
        public bool RaiseVillageBuilding(BuildingData data, Vector2Int origin)
        {
            if (data == null || Placer == null || grid == null) return false;
            int fw = Mathf.Max(1, data.footprintWidth);
            int fh = Mathf.Max(1, data.footprintHeight);
            Vector3 world = FootprintWorldCenter(origin, fw, fh);
            if (!Placer.TryRestore(data, origin, world, 1f, out _))
                return false;

            Transform root = buildingRoot != null ? buildingRoot : transform;
            GameObject go = ModularBuildingFactory.Spawn(data.category, world, root, fw, fh, grid.CellSize);
            go.name = $"Bld_Village_{data.category}";
            TerrainGrading.LevelUnder(go);
            CampusNavMesh.AddObstacle(go);
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
            CampusDressing.DressPlaced(data, go, _body);
            DemoVfx.BuildComplete(world);
            if (_campusNav != null)
                NotifyCampusExpanded();
            PersistSession();
            return true;
        }

        /// <summary>True when any corner or the centre of the footprint sits on a lake or river.</summary>
        private bool FootprintOverWater(Vector2Int origin, int width, int height)
        {
            if (_world == null || grid == null) return false;
            float cs = grid.CellSize;
            Vector3 corner = grid.CellToWorld(origin) - new Vector3(cs * 0.5f, 0f, cs * 0.5f);
            float w = width * cs, h = height * cs;
            for (int ix = 0; ix <= 2; ix++)
            for (int iz = 0; iz <= 2; iz++)
            {
                var p = corner + new Vector3(w * ix * 0.5f, 0f, h * iz * 0.5f);
                if (_world.IsOverWater(p, 0.2f)) return true;
            }
            return false;
        }

        private bool RectInBounds(Vector2Int origin, int width, int height)
        {
            if (grid == null) return false;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!grid.InBounds(new Vector2Int(origin.x + x, origin.y + y)))
                    return false;
            }
            return true;
        }

        private bool PlaceComplete(BuildingData data, Vector2Int origin, string goName)
        {
            if (data == null || Placer == null || grid == null) return false;
            int fw = Mathf.Max(1, data.footprintWidth);
            int fh = Mathf.Max(1, data.footprintHeight);
            Vector3 world = FootprintWorldCenter(origin, fw, fh);
            if (!Placer.TryRestore(data, origin, world, 1f, out _))
                return false;

            Transform root = buildingRoot != null ? buildingRoot : transform;
            GameObject go = ModularBuildingFactory.Spawn(
                data.category, world, root, fw, fh, grid.CellSize);
            go.name = goName;
            CampusNavMesh.AddObstacle(go);
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
            CampusDressing.DressPlaced(data, go, _body);
            return true;
        }

        private BuildingData DataNamed(BuildingCategory cat, string namePart)
        {
            BuildingData fallback = null;
            if (starterBuildings == null) return null;
            for (int i = 0; i < starterBuildings.Length; i++)
            {
                var data = starterBuildings[i];
                if (data == null || data.category != cat) continue;
                if (!string.IsNullOrEmpty(namePart) &&
                    !string.IsNullOrEmpty(data.displayName) &&
                    data.displayName.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return data;
                if (fallback == null) fallback = data;
            }
            return fallback;
        }

        private void SeedSoftClaim(Vector3 world, bool campus)
        {
            const int footprint = 6;
            float cell = grid.CellSize;
            float half = (footprint * cell) * 0.5f;
            Vector3 corner = world - new Vector3(half, 0f, half) + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            Vector2Int origin = grid.WorldToCell(corner);
            if (campus)
            {
                Placer.SeedCampusClaim(origin, footprint, footprint);
                CampusDressing.DressClaimDisc(
                    buildingRoot != null ? buildingRoot : transform,
                    world,
                    "DropZone_Claim",
                    new Color(0.96f, 0.42f, 0.08f),
                    9.5f);
            }
            else
            {
                Placer.SeedOutpostClaim(origin, footprint, footprint);
                _outpostBeacon = CampusDressing.DressClaimDisc(
                    buildingRoot != null ? buildingRoot : transform,
                    world,
                    "DropZone_Outpost",
                    new Color(0.22f, 0.72f, 0.86f),
                    8.2f);
            }
        }

        private void BootstrapResearch()
        {
            if (Research == null) return;
            if (Research.ActiveTech == TechId.None && Research.CanSelect(TechId.FieldSurvey))
                Research.TrySelect(TechId.FieldSurvey);
            RefreshTechEffects();
        }

        private void Update()
        {
            FlushDebugBodyHop();
            HandleSessionHotkeys();
            HandleBodyHopHotkeys();
            FlushDebugBodyHop();
            if (!IsPlaying) return;

            HandleToolHotkeys();
            HandleSpeedHotkeys();
            HandleOverlayHotkeys();
            HandleSelection();
            TryCancelFlagUnderCursor();
            PushThreatToSpecialists();
            _world?.TickLairs(Placer != null ? Placer.Pieces.Count : 0);
            RefreshPowerBudget();

            _playSeconds += Time.unscaledDeltaTime;
            _music?.Evaluate(this, Time.unscaledDeltaTime);

            Stats.Tick(Time.deltaTime);
            if (Settlement != null && Settlement.HasCommons) _commonsEverStood = true;
            if (Placer != null) Stats.NoteModules(Placer.Pieces.Count);

            // Time.deltaTime is already scaled by SimSpeed, so a fixed-step loop gives the pure
            // systems identical results at any frame rate or game speed.
            int steps = _sim.Advance(Time.deltaTime);
            for (int s = 0; s < steps; s++)
                TickSimulation(_sim.StepSeconds);

            RefreshSustainRates();
            TickCourierPad();
            _mission?.Tick();
            TickTutorial();
            TickAutosave();
            TickJunkYard(Time.deltaTime);
            TickGrok();
            if (_glanceCooldown > 0f)
                _glanceCooldown -= Time.deltaTime;

            // Prune destroyed stalkers / robots from lists
            for (int i = _stalkers.Count - 1; i >= 0; i--)
            {
                if (_stalkers[i] == null)
                    _stalkers.RemoveAt(i);
            }
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                if (_agents[i] == null)
                    _agents.RemoveAt(i);
            }
        }

        /// <summary>
        /// One fixed simulation step. Everything in here must be safe to run several times in a
        /// single frame (catch-up) and zero times in a frame (paused or a very fast frame).
        /// </summary>
        private void TickSimulation(float dt)
        {
            if (Settlement != null)
            {
                Settlement.Tick(dt);
            }
            Village?.Tick(dt);
            TickResearch(dt);
            GuildBenefits?.Tick(dt);
            TickEmptyRosterFail(dt);
            TickFlagInterest(dt);
            TickCampusEcology(dt);
            TickCampusBoard(dt);

            _constructionTick += dt;
            if (_constructionTick >= 0.25f)
            {
                ProcessCompletedConstruction();

                var living = new List<SpecialistData>(_agents.Count);
                for (int i = 0; i < _agents.Count; i++)
                {
                    if (_agents[i] != null && _agents[i].Data != null)
                        living.Add(_agents[i].Data);
                }
                if (Economy != null)
                {
                    RefreshCaravanValue();
                    Economy.Tick(_constructionTick, living);
                }
                TickAwaitingHires();
                _constructionTick = 0f;
            }
        }

        /// <summary>
        /// Space holds the world; comma / period (or − / + outside the flag tool, where they nudge
        /// the bounty) step the speed down and up.
        /// </summary>
        private void HandleSpeedHotkeys()
        {
            if (InputBindings.TextEntryActive) return; // typing flag orders
            bool plusMinus = ActiveTool != OverseerTool.Flag;

            if (Input.GetKeyDown(KeyCode.Space))
                SimSpeed.TogglePause();
            else if (Input.GetKeyDown(KeyCode.Period) ||
                     (plusMinus && (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))))
                SimSpeed.Faster();
            else if (Input.GetKeyDown(KeyCode.Comma) ||
                     (plusMinus && (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))))
                SimSpeed.Slower();
            else
                return;

            OnSpeedChanged();
        }

        /// <summary>Apply a speed change from any control (keys or the HUD) and note it once.</summary>
        public void OnSpeedChanged()
        {
            ApplySimSpeed();
            LogOverseer(SimSpeed.IsPaused ? "World held." : $"Speed {SimSpeed.Label}.");
            PlaytestTelemetry.Record("speed_change", "speed", SimSpeed.Label);
        }

        /// <summary>
        /// Push the chosen speed onto Time.timeScale so NavMesh movement and every deltaTime
        /// consumer scale together. The modal pause screen overrides this while it is open.
        /// </summary>
        public void ApplySimSpeed()
        {
            if (!IsPlaying)
                return;
            Time.timeScale = SimSpeed.TimeScale;
        }

        /// <summary>
        /// Phase 5D: each specialist feels local stalker pressure near their position —
        /// Campus B fauna no longer spikes bodyDanger for the Campus A party until nearby.
        /// SpecialistBrain scoring unchanged; only the bodyDanger input is spatially honest.
        /// </summary>
        private void PushThreatToSpecialists()
        {
            if (Threat == null) return;

            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent == null) continue;

                float danger = LocalThreatAt(agent.transform.position);
                if (HasActiveDefendNear(agent.transform.position))
                    danger = Mathf.Clamp01(danger * 0.55f);
                if (HasCommonsShade(agent.transform.position))
                    danger = Mathf.Clamp01(danger * OverseerRules.CommonsShadeDanger);

                // Fissile nodes hum — light local danger bump without rewriting the brain.
                if (_world != null)
                {
                    var node = _world.FindNearestNodeAny(agent.transform.position, 6f);
                    if (node != null && !node.IsDepleted && node.NodeType == ResourceNodeType.Fissile)
                        danger = Mathf.Clamp01(danger + 0.08f);
                }

                if (_body != null && _body.RadiationDrainPerSecond > 0f)
                {
                    float da = FlatDist(agent.transform.position, ColonyLayout.CampusOrigin);
                    float nearest = da;
                    if (Settlement != null && Settlement.HasOutpost)
                    {
                        float db = FlatDist(agent.transform.position, ColonyLayout.CampusBOrigin);
                        nearest = Mathf.Min(da, db);
                    }
                    if (nearest > _body.RadiationSafeRadius)
                        danger = Mathf.Clamp01(danger + 0.08f);
                }

                agent.SetBodyDanger(danger);
            }
        }

        private bool HasActiveDefendNear(Vector3 world, float radius = 14f)
        {
            if (InDefendWatch(world)) return true;
            if (Flags == null) return false;
            float rSq = radius * radius;
            var list = Flags.Flags;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f?.Data == null || f.Data.flagType != FlagType.DefendArea || f.ClaimCount <= 0)
                    continue;
                Vector3 p = f.WorldPosition;
                float dx = p.x - world.x;
                float dz = p.z - world.z;
                if (dx * dx + dz * dz <= rSq)
                    return true;
            }
            return false;
        }

        private bool HasActiveDefendClaim()
        {
            if (Flags == null) return false;
            var list = Flags.Flags;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f?.Data != null && f.Data.flagType == FlagType.DefendArea && f.ClaimCount > 0)
                    return true;
            }
            return false;
        }

        private void EnsureSceneRefs()
        {
            if (grid == null)
            {
                var go = new GameObject("IsoGrid");
                go.transform.SetParent(transform);
                grid = go.AddComponent<IsoGrid>();
            }
            grid.Resize(ColonyLayout.MapCells, ColonyLayout.MapCells);

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
            {
                var camGo = new GameObject("Main Camera");
                mainCamera = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            if (flagRoot == null)
            {
                var go = new GameObject("Flags");
                go.transform.SetParent(transform);
                flagRoot = go.transform;
            }

            if (buildingRoot == null)
            {
                var go = new GameObject("Buildings");
                go.transform.SetParent(transform);
                buildingRoot = go.transform;
            }

            if (specialistSpawn == null)
            {
                var go = new GameObject("SpecialistSpawn");
                go.transform.SetParent(transform);
                go.transform.position = specialistSpawnOffset;
                specialistSpawn = go.transform;
            }

            if (_threatRoot == null)
            {
                var go = new GameObject("Threats");
                go.transform.SetParent(transform);
                _threatRoot = go.transform;
            }

            FitGroundToGrid();
        }

        private void FitGroundToGrid()
        {
            if (!spawnGroundPlane || grid == null) return;
            var ground = GameObject.Find("GroundPlane");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "GroundPlane";
                ground.transform.SetParent(transform);
            }

            float worldW = grid.WorldWidth;
            float worldH = grid.WorldHeight;

            // Displaced surface replaces the flat 10x10 primitive. Campus pads stay level, so
            // placement, docking, and NavMesh are unaffected.
            Mesh mesh;
            TerrainBake bake = null;
            try
            {
                bake = TerrainDataBake.Generate(worldW, worldH, BodySeed.Current, _body);
                mesh = TerrainMeshBuilder.Build(bake);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                mesh = null;
            }

            if (mesh != null)
            {
                var filter = ground.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = mesh;
                var collider = ground.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    var box = ground.GetComponent<Collider>();
                    if (box != null) Destroy(box);
                    collider = ground.AddComponent<MeshCollider>();
                }
                collider.sharedMesh = mesh;
                var holder = ground.GetComponent<TerrainBakeHolder>();
                if (holder == null) holder = ground.AddComponent<TerrainBakeHolder>();
                holder.Bake = bake;
                // World gen, dressing, units and building pads read the live surface from here.
                TerrainDataBake.Current = bake;
            }

            // The mesh is authored in world units already, so the transform stays identity.
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = Vector3.one;
            // Albedo / grade is owned by PlanetaryMapDressing — do not stamp a flat greybox tint.
        }

        private void BuildPureSystems()
        {
            Resources = new ResourceManager();
            if (seedStartingResources)
                ApplyStarterStockpile();
            if (DemoSettings.BootStraightIntoPlay && DemoSettings.SaveExists)
                DemoSettings.TryLoadStockpile(Resources);

            Flags = new FlagManager();
            Flags.FlagCompleted += OnFlagWorkCompleted;
            Placer = new BuildingPlacer(Resources);
            Placer.HasCommons = () =>
                (Settlement != null && Settlement.HasCommons) || Placer.HasCommonsModule;
            if (grid != null)
            {
                // Reject if any footprint cell is off-map (not only the origin).
                Placer.ExtraPlacementRule = (cell, data) =>
                {
                    if (data == null) return false;
                    for (int x = 0; x < data.footprintWidth; x++)
                    for (int y = 0; y < data.footprintHeight; y++)
                    {
                        if (!grid.InBounds(new Vector2Int(cell.x + x, cell.y + y)))
                            return false;
                    }

                    bool hasCommons = (Settlement != null && Settlement.HasCommons) ||
                                      Placer.HasCommonsModule;

                    // Colony Commons: first civic landmark on the drop claim only.
                    if (data.category == BuildingCategory.Commons)
                    {
                        if (hasCommons) return false;
                        return Placer.OverlapsSoftClaim(cell, data.footprintWidth, data.footprintHeight);
                    }

                    if (data.category == BuildingCategory.Inn)
                        return true;

                    if (!hasCommons)
                        return false;

                    if (!IsBuildingUnlocked(data.category))
                        return false;

                    // Free placement, Majesty style: anywhere on open ground once the Commons
                    // stands. Nothing links buildings together — just keep out of water.
                    return !FootprintOverWater(cell, data.footprintWidth, data.footprintHeight);
                };
            }

            Brain = new SpecialistBrain();
            ApplyReplayToBrain();
            // Mines are the trade posts now; no supply ships land with cargo.
            Economy = new SimpleEconomy(Resources) { ResupplyEnabled = false };
            float resupply = 90f * (_body != null ? Mathf.Max(0.4f, _body.ResupplyIntervalScale) : 1f);
            int fee = _body != null ? Mathf.Max(0, _body.ResupplyDockFee) : 0;
            Economy.ConfigureResupply(resupply, fee);
            Settlement = new Settlement(Resources);
            if (_body != null)
                Settlement.SetBodyYield(_body.MineYieldScale);
            // Rebind after Settlement exists (constructor order).
            Placer.HasCommons = () =>
                (Settlement != null && Settlement.HasCommons) || Placer.HasCommonsModule;
            Research = new ResearchManager(Resources);
            Research.TechUnlocked += OnTechUnlocked;
            GuildBenefits = new GuildBenefitDirector();
            Economy.UpkeepApplied += OnUpkeepTithe;
            // Majesty gold loop: trade and mine gold sit in tills until a tax collector walks it home.
            Economy.TillSink = PayTradeGold;
            Settlement.RouteCampGoldToTills = true;
            Placer.OwnedCount = cat => Village != null ? Village.CountAlive(cat) : 0;
            Threat = new ThreatPressure { Ambient = 0.18f };
        }

        private void ApplyStarterStockpile()
        {
            var body = _body ?? CelestialBodyCatalog.Get(celestialBody);
            float scale = Mathf.Clamp(ReplayRules.StartStockpileScale, 0.35f, 1f);
            // CRED is the only currency; the retired ICE / REG / PWR stocks stay at zero.
            Resources.Set(ResourceId.Regolith, 0);
            Resources.Set(ResourceId.WaterIce, 0);
            Resources.Set(ResourceId.Metals, Mathf.Max(0, Mathf.RoundToInt(body.StartMetals * scale)));
            Resources.Set(ResourceId.Power, 0);
        }

        private void OnTechUnlocked(TechId id)
        {
            RefreshTechEffects();
            SyncLaunchGate();
            DemoAudio.PlayResearch();
            DemoVfx.ClaimRing(ColonyLayout.CampusOrigin, new Color(0.45f, 0.75f, 1f));
            Debug.Log($"[GameLoop] Tech unlocked: {id}");
            var def = TechCatalog.Get(id);
            if (def != null && def.SecretProject)
                LogOverseer($"Secret Project complete: {def.DisplayName}.");
            else if (id == TechId.ExtractBasics && !DemoSettings.FirstHourDemo)
                LogOverseer("Extract Basics. Dock a Market Stall — potions and a regen necklace, paid in EU.");
            else if (id == TechId.OreRefining && !DemoSettings.FirstHourDemo)
                LogOverseer("Ore Refining. Dock a Blacksmith — lodge arms and armor, paid in EU.");
            else if (id == TechId.MedProtocols)
                LogOverseer("Med Protocols. Dock a Fobot Yard — wrecks stand up here, paid in EU.");
            else if (id == TechId.LifeSupport && !DemoSettings.FirstHourDemo)
                LogOverseer("Life Support. Dock an Aid Station — hurt robots pay EU for a patch.");
            else if (id == TechId.HorizonPulse)
                LogOverseer("Horizon Pulse researched. Inspect Horizon Lodge and spend EU to mark dens.");
            else if (id == TechId.AnvilOvertime)
                LogOverseer("Anvil Overtime researched. Inspect Anvil Compact and spend EU to weld faster.");
            else if (id == TechId.AegisWatchfire)
                LogOverseer("Aegis Watchfire researched. Inspect Aegis Lodge and spend EU to harden the roster.");
            else if (id == TechId.TriageFieldAid)
                LogOverseer("Triage Field Aid researched. Inspect Triage Compact and spend EU to patch the dirt.");
            else if (id == TechId.GuildCharter)
            {
                LogOverseer("Guild Charter signed. Dock Horizon Lodge, Anvil Compact, Aegis Lodge, or Triage Compact. Flags near the hall pull that class.");
                EnsureNarrative();
                _narrative.NoteCompleted(FlagDecreeIds.EarthCharterTheHall);
                if (_narrative.TryTakeCompleteToast(AdvisorToastCatalog.CompleteCharterTheHall, out var charterToast))
                    Log.Push(charterToast.Line);
                SyncNarrativeCivic();
            }
            else if (id == TechId.HarvestDoctrine)
                LogOverseer("Harvest Doctrine. Strip Guild is licensed. Mines and haul improve.");
            else if (id == TechId.SurveyDoctrine)
                LogOverseer("Survey Doctrine. Chart Lodge is licensed. Labs tick faster.");
            else if (id == TechId.AegisDoctrine)
                LogOverseer("Aegis Doctrine. Grid draw drops 15%. The rim breathes easier.");
            else if (id == TechId.TerraformCharter)
                LogOverseer("Terraform Charter. Bloom Compact is licensed. Post Terraform (U) on farms.");
            else if (id == TechId.FreightDoctrine)
                LogOverseer("Freight Doctrine. Haul Lodge is licensed — they take Outpost cheap.");
            else if (id == TechId.CoreSampling)
                LogOverseer("Core Sampling. Core Lodge is licensed. Mines tick harder.");
            else if (id == TechId.PerimeterDoctrine)
                LogOverseer("Perimeter Doctrine. Rim Watch is licensed — they take Defend cheap.");
            else if (id == TechId.ClimateLoom)
                LogOverseer("Climate Loom. Farms surge. Place the 6×6 Loom landmark.");
            else if (id == TechId.AegisSpire)
                LogOverseer("Aegis Spire. Grid draw and rim pressure drop. Place the 6×6 Spire.");
            else if (id == TechId.DeepArchive)
                LogOverseer("Deep Archive. Labs remember every sample. Place the 6×6 Archive.");
            if (Research != null && Research.HasLaunchUnlockFor(celestialBody) &&
                Settlement != null && !Settlement.HasPad)
            {
                string craft = Research.LaunchTechLabel(celestialBody);
                LogOverseer($"{craft} researched. Place a Landing Pad to stage the craft.");
            }
        }

        public bool IsBuildingUnlocked(BuildingCategory cat)
        {
            TechId need = TechRequiredFor(cat);
            if (need == TechId.None) return true;
            return Research != null && Research.IsUnlocked(need);
        }

        public static TechId TechRequiredFor(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.GuildHall: return TechId.GuildCharter;
                case BuildingCategory.Market: return TechId.ExtractBasics;
                case BuildingCategory.Blacksmith: return TechId.OreRefining;
                case BuildingCategory.FobotYard: return TechId.MedProtocols;
                case BuildingCategory.AidStation: return TechId.LifeSupport;
                case BuildingCategory.HarvesterWorkshop: return TechId.HarvestDoctrine;
                case BuildingCategory.SurveyorWorkshop: return TechId.SurveyDoctrine;
                case BuildingCategory.TerraformerWorkshop: return TechId.TerraformCharter;
                case BuildingCategory.CourierWorkshop: return TechId.FreightDoctrine;
                case BuildingCategory.GeologistWorkshop: return TechId.CoreSampling;
                case BuildingCategory.SentinelWorkshop: return TechId.PerimeterDoctrine;
                case BuildingCategory.ClimateLoom: return TechId.ClimateLoom;
                case BuildingCategory.AegisSpire: return TechId.AegisSpire;
                case BuildingCategory.DeepArchive: return TechId.DeepArchive;
                default: return TechId.None;
            }
        }

        private void RefreshTechEffects()
        {
            bool loom = HasAliveCategory(BuildingCategory.ClimateLoom);
            bool spire = HasAliveCategory(BuildingCategory.AegisSpire);
            _tech = TechEffects.From(Research, loom, spire);
            if (Settlement != null)
            {
                Settlement.SetTechYieldBonus(_tech.MineYieldBonus);
            }

            if (Economy != null)
            {
                float resupply = 90f * (_body != null ? Mathf.Max(0.4f, _body.ResupplyIntervalScale) : 1f);
                resupply *= Mathf.Max(0.4f, _tech.ResupplyIntervalScale);
                resupply *= Mathf.Max(0.4f, ReplayRules.ResupplyIntervalScale);
                int fee = _body != null ? Mathf.Max(0, _body.ResupplyDockFee) : 0;
                fee = Mathf.Max(0, fee - _tech.ResupplyFeeDiscount);
                fee += Mathf.Max(0, ReplayRules.ExtraDockFee);
                Economy.SetResupplyRules(resupply, fee);
            }
        }

        private void SyncLaunchGate()
        {
            if (_mission == null || Research == null) return;
            bool tech = Research.HasLaunchUnlockFor(celestialBody);
            bool pad = Settlement != null && Settlement.HasPad;
            if (!tech || !pad)
                return;

            bool wasReady = _mission.LaunchReady;
            _mission.SetLaunchReady(true);
            if (!_launchCraftStaged)
            {
                _launchCraftStaged = true;
                bool heavy = celestialBody != CelestialBodyId.Earth;
                LaunchSite.EnsureReady(buildingRoot != null ? buildingRoot : transform, heavy);
                _isoCam?.FocusOn(LaunchSite.PadWorld, ColonyLayout.CameraOrthoSize);
            }

            if (!wasReady)
            {
                string craft = Research.LaunchTechLabel(celestialBody);
                LogOverseer($"{craft} staged on the Landing Pad. Launch gate is open.");
                EnsureNarrative();
                string completeKey = AdvisorToastCatalog.CompleteKeyForCraft(celestialBody);
                if (!string.IsNullOrEmpty(completeKey) &&
                    _narrative.TryTakeCompleteToast(completeKey, out var stagedToast))
                {
                    Log.Push(stagedToast.Line);
                }
                SyncNarrativeCivic();
            }
        }

        private void TickResearch(float dt)
        {
            if (Research == null) return;
            CountLabs(out int labs, out int workers);
            float mult = _body != null ? _body.ResearchRateMultiplier : 1f;
            float archive = HasAliveCategory(BuildingCategory.DeepArchive) ? 0.55f : 0f;
            Research.Tick(dt, labs, workers, mult, archive);
            SyncLaunchGate();
        }

        private void EnsureContent()
        {
            // Prefer authored Resources/DemoContent assets; factories remain Play-safe fallback.
            if (scoutData == null) scoutData = DemoContentCatalog.LoadScout() ?? CreateScout();
            if (engineerData == null) engineerData = DemoContentCatalog.LoadEngineer() ?? CreateEngineer();
            if (defenseData == null) defenseData = DemoContentCatalog.LoadDefense() ?? CreateDefense();
            if (medicData == null) medicData = DemoContentCatalog.LoadMedic() ?? CreateMedic();
            if (harvesterData == null) harvesterData = DemoContentCatalog.LoadHarvester() ?? CreateHarvester();
            if (surveyorData == null) surveyorData = DemoContentCatalog.LoadSurveyor() ?? CreateSurveyor();
            if (terraformerData == null) terraformerData = DemoContentCatalog.LoadTerraformer() ?? CreateTerraformer();
            if (courierData == null) courierData = DemoContentCatalog.LoadCourier() ?? CreateCourier();
            if (geologistData == null) geologistData = DemoContentCatalog.LoadGeologist() ?? CreateGeologist();
            if (sentinelData == null) sentinelData = DemoContentCatalog.LoadSentinel() ?? CreateSentinel();

            BindUnitPrefab(scoutData, SpecialistClass.ScoutDrone);
            BindUnitPrefab(engineerData, SpecialistClass.EngineerBot);
            BindUnitPrefab(defenseData, SpecialistClass.DefenseMech);
            BindUnitPrefab(medicData, SpecialistClass.Medic);
            BindUnitPrefab(harvesterData, SpecialistClass.HarvesterBot);
            BindUnitPrefab(surveyorData, SpecialistClass.SurveyorBot);
            BindUnitPrefab(terraformerData, SpecialistClass.TerraformerBot);
            BindUnitPrefab(courierData, SpecialistClass.CourierBot);
            BindUnitPrefab(geologistData, SpecialistClass.GeologistBot);
            BindUnitPrefab(sentinelData, SpecialistClass.SentinelMech);
            SpecialistPersonality.Apply(scoutData);
            SpecialistPersonality.Apply(engineerData);
            SpecialistPersonality.Apply(defenseData);
            SpecialistPersonality.Apply(medicData);
            SpecialistPersonality.Apply(harvesterData);
            SpecialistPersonality.Apply(surveyorData);
            SpecialistPersonality.Apply(terraformerData);
            SpecialistPersonality.Apply(courierData);
            SpecialistPersonality.Apply(geologistData);
            SpecialistPersonality.Apply(sentinelData);

            if (exploreFlagData == null)
                exploreFlagData = DemoContentCatalog.LoadExploreFlag()
                    ?? CreateFlag(FlagType.Explore, "Explore", 40, 0.08f, 4f, new Color(0.3f, 0.85f, 1f));
            if (clearThreatFlagData == null)
                clearThreatFlagData = DemoContentCatalog.LoadClearThreatFlag()
                    ?? CreateFlag(FlagType.ClearThreat, "Clear Threat", 80, 0.4f, 6f, new Color(1f, 0.3f, 0.25f));
            if (buildFlagData == null)
                buildFlagData = DemoContentCatalog.LoadBuildFlag()
                    ?? CreateFlag(FlagType.Build, "Build Here", 70, 0.1f, 8f, new Color(1f, 0.65f, 0.15f));
            if (extractFlagData == null)
                extractFlagData = DemoContentCatalog.LoadExtractFlag()
                    ?? CreateFlag(FlagType.Extract, "Extract", 55, 0.12f, 7f, new Color(0.55f, 0.9f, 0.35f));
            if (defendFlagData == null)
                defendFlagData = DemoContentCatalog.LoadDefendFlag()
                    ?? CreateFlag(FlagType.DefendArea, "Defend Area", 65, 0.25f, 9f, new Color(0.95f, 0.48f, 0.18f));
            if (researchSiteFlagData == null)
                researchSiteFlagData = DemoContentCatalog.LoadResearchSiteFlag()
                    ?? CreateFlag(FlagType.ResearchSite, "Research Site", 50, 0.1f, 6f, new Color(0.45f, 0.72f, 1f));
            if (outpostFlagData == null)
                outpostFlagData = DemoContentCatalog.LoadOutpostFlag()
                    ?? CreateFlag(FlagType.EstablishOutpost, "Establish Outpost", 75, 0.22f, 10f, new Color(0.22f, 0.82f, 0.78f));
            if (terraformFlagData == null)
                terraformFlagData = DemoContentCatalog.LoadTerraformFlag()
                    ?? CreateFlag(FlagType.Terraform, "Terraform", 70, 0.14f, 11f, new Color(0.42f, 0.88f, 0.38f));
            SpecialistPersonality.ApplyFlagAffinity(exploreFlagData);
            SpecialistPersonality.ApplyFlagAffinity(clearThreatFlagData);
            SpecialistPersonality.ApplyFlagAffinity(buildFlagData);
            SpecialistPersonality.ApplyFlagAffinity(extractFlagData);
            SpecialistPersonality.ApplyFlagAffinity(defendFlagData);
            SpecialistPersonality.ApplyFlagAffinity(researchSiteFlagData);
            SpecialistPersonality.ApplyFlagAffinity(outpostFlagData);
            SpecialistPersonality.ApplyFlagAffinity(terraformFlagData);
            foreach (var f in new[] { exploreFlagData, clearThreatFlagData, buildFlagData, extractFlagData,
                         defendFlagData, researchSiteFlagData, outpostFlagData, terraformFlagData })
                MajestyEconomy.ApplyFlagBounties(f);

            if (starterBuildings == null || starterBuildings.Length == 0)
            {
                starterBuildings = DemoContentCatalog.LoadStarterBuildings();
                if (starterBuildings == null || starterBuildings.Length == 0)
                {
                    starterBuildings = new[]
                    {
                        CreateBuilding("Colony Commons", BuildingCategory.Commons, 70, 10, 18f, 6, 6),
                        CreateBuilding("Hab Module (HAB-1)", BuildingCategory.Habitat, 50, 8, 12f, 4, 4),
                        CreateBuilding("Power Node (PWR-1)", BuildingCategory.Power, 35, 0, 8f, 4, 4),
                        CreateBuilding("OPS Drop-off", BuildingCategory.Mining, 45, 6, 14f, 4, 4),
                        CreateBuilding("Lab Module (LAB-1)", BuildingCategory.Laboratory, 55, 10, 14f, 4, 4),
                        CreateBuilding("Landing Pad", BuildingCategory.LandingPad, 40, 5, 10f, 6, 6),
                        CreateBuilding("Defense Battery", BuildingCategory.Defense, 60, 8, 16f, 4, 4)
                    };
                }
            }

            // Bind Blender blockout meshes from Resources (no Inspector wiring required).
            for (int i = 0; i < starterBuildings.Length; i++)
            {
                if (starterBuildings[i] != null && starterBuildings[i].prefab == null)
                    starterBuildings[i].prefab = BuildingVisualCatalog.LoadPrefab(starterBuildings[i].category);
            }

            starterBuildings = EnsureCommonsFirst(starterBuildings);
            starterBuildings = AppendEconomyBuildings(starterBuildings);
            NormalizeCatalogNames(starterBuildings);
            ForceCardinalFootprints(starterBuildings);
            StripShopCostsToCredits(starterBuildings);
            MajestyEconomy.ApplyBuildingPrices(starterBuildings);
        }

        /// <summary>Colony Commons is always catalog index 0 — Majesty first-build.</summary>
        private static BuildingData[] EnsureCommonsFirst(BuildingData[] current)
        {
            var commons = CreateBuilding("Colony Commons", BuildingCategory.Commons, 70, 10, 18f, 6, 6);
            if (current == null || current.Length == 0)
                return new[] { commons };

            int existing = -1;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != null && current[i].category == BuildingCategory.Commons)
                {
                    existing = i;
                    ForceCommonsDisplayName(current[i]);
                    break;
                }
            }

            if (existing == 0)
                return current;

            if (existing > 0)
            {
                var reorder = new BuildingData[current.Length];
                reorder[0] = current[existing];
                int w = 1;
                for (int i = 0; i < current.Length; i++)
                {
                    if (i == existing) continue;
                    reorder[w++] = current[i];
                }
                return reorder;
            }

            var merged = new BuildingData[current.Length + 1];
            merged[0] = commons;
            current.CopyTo(merged, 1);
            return merged;
        }

        private static void ForceCommonsDisplayName(BuildingData data)
        {
            if (data == null) return;
            data.displayName = "Colony Commons";
        }

        /// <summary>CMD-1 sheet is Guild dress; Defense bunker is not "Command".</summary>
        private static void NormalizeCatalogNames(BuildingData[] buildings)
        {
            if (buildings == null) return;
            for (int i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                if (b == null) continue;
                switch (b.category)
                {
                    case BuildingCategory.Commons:
                        b.displayName = "Colony Commons";
                        break;
                    case BuildingCategory.Defense:
                        b.displayName = "Defense Battery";
                        b.description = "auto-fires 18 m";
                        break;
                    case BuildingCategory.Mining:
                        b.displayName = "OPS Drop-off";
                        b.description = "does not grow EU";
                        break;
                    case BuildingCategory.Mine:
                        b.displayName = "Nuclear Mine";
                        b.description = "more energy farther out";
                        break;
                    case BuildingCategory.Habitat:
                        b.description = $"villagers build · {MajestyEconomy.HouseDailyFlat} EU/day";
                        break;
                    case BuildingCategory.Farm:
                        b.description = $"tax {MajestyEconomy.FarmDailyTax}/day";
                        break;
                    case BuildingCategory.LandingPad:
                        b.description = "launch craft";
                        break;
                    case BuildingCategory.Laboratory:
                        b.description = "research";
                        break;
                    case BuildingCategory.Power:
                        b.displayName = "Solar Farm";
                        b.description = $"villagers build · {MajestyEconomy.SolarFarmDailyTax} EU/day";
                        break;
                    case BuildingCategory.ClimateLoom:
                    case BuildingCategory.AegisSpire:
                    case BuildingCategory.DeepArchive:
                        b.description = "unlock from ★ tech — bonus while standing";
                        break;
                    case BuildingCategory.GuildHall:
                        if (RobotGuildCatalog.TryMatch(b, out var guild))
                        {
                            b.displayName = guild.HallName;
                            b.description = guild.CatalogLine;
                            if (b.preferredOccupants == null || b.preferredOccupants.Length == 0)
                                b.preferredOccupants = guild.Occupants;
                        }
                        else
                            b.description = "Guild Hall — assign a class";
                        break;
                    case BuildingCategory.Market:
                        b.displayName = "Market Stall";
                        b.description = $"tax {MajestyEconomy.MarketDailyTax}/day";
                        break;
                    case BuildingCategory.Blacksmith:
                        b.displayName = "Blacksmith";
                        b.description = "hero gear";
                        break;
                    case BuildingCategory.FobotYard:
                        b.displayName = "Fobot Yard";
                        b.description = "Pay EU here to stand wrecks up.";
                        break;
                    case BuildingCategory.Watchtower:
                        b.displayName = "Watchtower";
                        b.description = "+1 tax collector";
                        break;
                    case BuildingCategory.AidStation:
                        b.displayName = "Aid Station";
                        b.description = "Hurt robots pay EU for a patch. Triage clocks in.";
                        break;
                }
            }
        }

        private static void BindUnitPrefab(SpecialistData data, SpecialistClass cls)
        {
            if (data == null || data.prefab != null) return;
            data.prefab = DemoContentCatalog.LoadUnitPrefab(cls);
        }

        private void WireInputDrivers()
        {
            _flagInput = GetComponent<FlagPlacementInput>();
            if (_flagInput == null) _flagInput = gameObject.AddComponent<FlagPlacementInput>();

            _buildInput = GetComponent<BuildingPlacementInput>();
            if (_buildInput == null) _buildInput = gameObject.AddComponent<BuildingPlacementInput>();

            _isoCam = mainCamera.GetComponent<IsometricCameraController>();
            if (_isoCam == null) _isoCam = mainCamera.gameObject.AddComponent<IsometricCameraController>();

            _flagInput.Initialize(
                Flags, grid, _isoCam,
                exploreFlagData, clearThreatFlagData, buildFlagData,
                extractFlagData, defendFlagData,
                flagRoot,
                researchSiteFlagData, outpostFlagData, terraformFlagData);
            _buildInput.Initialize(Placer, Resources, grid, _isoCam, starterBuildings, buildingRoot);
            // Start in inspect mode so LMB selects specialists; open Flag/Build from the dock.
            activeTool = OverseerTool.None;
            ApplyTool(activeTool);
        }

        private void ConfigureCamera()
        {
            mainCamera.orthographic = true;
            bool frameCommons = !spawnShowcaseColony && !DemoSettings.SaveExists;
            float ortho = frameCommons ? ColonyLayout.CampusOrthoSize : ColonyLayout.CameraOrthoSize;
            mainCamera.orthographicSize = ortho;
            mainCamera.nearClipPlane = 0.3f;
            // Deep horizon floor lives near y=-80; far clip must reach it at max ortho.
            mainCamera.farClipPlane = 2000f;
            if (_body != null && _body.Id == CelestialBodyId.Mars)
            {
                // Dream-loop: salmon haze clear — Skybox was reading as a hard black cut.
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = PlanetaryMapDressing.VoidFillColor(_body);
            }
            else
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                if (_body != null)
                    mainCamera.backgroundColor = PlanetaryMapDressing.VoidFillColor(_body);
            }
            mainCamera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            Vector3 focus = ColonyLayout.CameraFocus;
            mainCamera.transform.position = focus + new Vector3(-18f, 22f, -18f);
            if (mainCamera.GetComponent<AudioListener>() == null)
                mainCamera.gameObject.AddComponent<AudioListener>();

            if (_isoCam != null)
            {
                if (grid != null)
                {
                    float maxX = grid.WorldWidth + 12f;
                    float maxZ = grid.WorldHeight + 12f;
                    _isoCam.SetPanBounds(new Vector2(-8f, -8f), new Vector2(maxX, maxZ));
                }
                _isoCam.FocusOn(focus, ortho);
                _isoCam.SnapToTarget();
            }
        }

        private void SpawnParty()
        {
            _agents.Clear();
            ClearSelection();
            Agent = null;
            // Outdoor robots are fabricated when their workshop finishes construction.
            // Colonists are villagers (VillagerAgent), not specialists — they never take bounties.
            Debug.Log("[GameLoop] No starter robots — build workshops to fabricate outdoor robots.");
        }

        /// <summary>
        /// Fabricate one outdoor robot when a workshop finishes building. With
        /// <paramref name="chargeHire"/> the treasury pays the Majesty 2 recruit fee first; a workshop
        /// the treasury cannot afford yet waits and hires as soon as the gold is there.
        /// </summary>
        public bool TryFabricateRobot(ColonyStructure workshop, bool announce = true, bool restoreVeteran = true,
            bool chargeHire = false)
        {
            if (workshop == null || !workshop.IsAlive || !workshop.IsWorkshop) return false;
            if (workshop.RobotFabricated) return false;

            var cls = ColonyStructure.RobotClassForWorkshop(workshop.Category);
            if (!cls.HasValue && workshop.HasPreferredClass)
                cls = workshop.PreferredClass;
            if (!cls.HasValue) return false;

            SpecialistData data = DataForClass(cls.Value);
            if (data == null) return false;

            Vector3 pos = workshop.WorldPosition + new Vector3(1.6f, 0f, 0.4f);
            if (grid != null)
                pos = grid.SnapToCellCenter(pos);

            if (HasCorpse(cls.Value))
                return false;

            int hire = chargeHire ? MajestyEconomy.HireCost(cls.Value) : 0;
            if (hire > 0 && (Resources == null || !Resources.TrySpend(ResourceId.Metals, hire)))
            {
                if (!_awaitingHire.Contains(workshop)) _awaitingHire.Add(workshop);
                if (Time.time >= _hireNagAt)
                {
                    _hireNagAt = Time.time + 30f;
                    LogOverseer($"{workshop.DisplayName} is ready — hiring its {ColonyStructure.ClassLabel(cls.Value)} costs {hire} EU.");
                }
                return false;
            }

            var agent = SpawnOne(data, pos, TintForClass(cls.Value));
            if (agent == null)
            {
                if (hire > 0) Resources.Add(ResourceId.Metals, hire);
                return false;
            }
            if (chargeHire)
                agent.EarnCredits(MajestyEconomy.HeroStartingPurse, null, taxed: false);

            if (restoreVeteran)
            {
                if (_pendingVeterans.TryGetValue(cls.Value, out var pending))
                {
                    agent.ApplyRecord(pending);
                    _pendingVeterans.Remove(cls.Value);
                }
                else if (SpecialistRoster.TryGet(_rosterSaved, cls.Value, out var saved) && !saved.Corpse)
                {
                    agent.ApplyRecord(saved);
                }
            }

            workshop.MarkRobotFabricated();
            workshop.TryClockIn(agent);
            _everHadRobot = true;
            _agents.Add(agent);
            if (Agent == null)
                Agent = agent;
            agent.BindNavMesh(_campusNav);

            string label = data.displayName ?? cls.Value.ToString();
            if (announce)
            {
                _overseerHud?.Notify(hire > 0
                    ? $"{label} hired at {workshop.DisplayName} — {hire} EU."
                    : $"{label} fabricated at {workshop.DisplayName}.", 3.2f);
                DemoAudio.PlayRobotSpawn(pos);
                DemoVfx.ClaimRing(pos, TintForClass(cls.Value));
            }
            Debug.Log($"[GameLoop] Fabricated {label} from {workshop.DisplayName}.");
            return true;
        }

        private SpecialistData DataForClass(SpecialistClass cls) => cls switch
        {
            SpecialistClass.EngineerBot => engineerData,
            SpecialistClass.DefenseMech => defenseData,
            SpecialistClass.Medic => medicData,
            SpecialistClass.HarvesterBot => harvesterData,
            SpecialistClass.SurveyorBot => surveyorData,
            SpecialistClass.TerraformerBot => terraformerData,
            SpecialistClass.CourierBot => courierData,
            SpecialistClass.GeologistBot => geologistData,
            SpecialistClass.SentinelMech => sentinelData,
            _ => scoutData
        };

        private static Color TintForClass(SpecialistClass cls) => cls switch
        {
            SpecialistClass.EngineerBot => new Color(1f, 0.55f, 0.15f),
            SpecialistClass.DefenseMech => new Color(0.85f, 0.22f, 0.22f),
            SpecialistClass.Medic => new Color(0.92f, 0.96f, 1f),
            SpecialistClass.HarvesterBot => new Color(0.82f, 0.62f, 0.18f),
            SpecialistClass.SurveyorBot => new Color(0.45f, 0.82f, 0.95f),
            SpecialistClass.TerraformerBot => new Color(0.42f, 0.82f, 0.38f),
            SpecialistClass.CourierBot => new Color(0.95f, 0.72f, 0.28f),
            SpecialistClass.GeologistBot => new Color(0.68f, 0.52f, 0.32f),
            SpecialistClass.SentinelMech => new Color(0.78f, 0.38f, 0.22f),
            _ => new Color(0.35f, 0.85f, 1f)
        };

        private void ProcessCompletedConstruction()
        {
            if (Placer == null) return;
            _completedBuilds.Clear();
            Placer.TickConstruction(_constructionTick, _completedBuilds);
            for (int i = 0; i < _completedBuilds.Count; i++)
            {
                var order = _completedBuilds[i];
                if (order?.Data == null) continue;
                if (!order.IsRefab) DemoAudio.PlayBuildComplete(order.WorldPosition);
            if (order.IsRefab)
                    {
                        var shop = Village?.FindNear(order.WorldPosition, 6f);
                        if (shop != null && shop.IsWorkshop)
                        {
                            shop.ClearRobotFabricated();
                            TryFabricateRobot(shop, restoreVeteran: false);
                        }
                        continue;
                    }
                if (!ColonyStructure.IsWorkshopCategory(order.Data.category)) continue;
                var st = Village?.FindNear(order.WorldPosition, 4f);
                if (st != null && st.Category == order.Data.category)
                    TryFabricateRobot(st, chargeHire: true);
            }
            if (_completedBuilds.Count > 0)
                SyncNarrativeCivic();
        }

        public bool IsSelected(SpecialistAgent agent) =>
            agent != null && _selected.Contains(agent);

        public void ClearSelection()
        {
            for (int i = 0; i < _selected.Count; i++)
                _selected[i]?.SetSelected(false);
            _selected.Clear();
        }

        public void SelectOnly(SpecialistAgent agent)
        {
            ClearStructureSelection();
            ClearSelection();
            if (agent == null) return;
            _selected.Add(agent);
            agent.SetSelected(true);
            CharacterVoice.Cue(agent, VoiceCue.Select);
        }

        public void ToggleSelect(SpecialistAgent agent)
        {
            if (agent == null) return;
            ClearStructureSelection();
            int idx = _selected.IndexOf(agent);
            if (idx >= 0)
            {
                agent.SetSelected(false);
                _selected.RemoveAt(idx);
                return;
            }

            if (_selected.Count >= MaxPartySize)
            {
                var oldest = _selected[0];
                oldest?.SetSelected(false);
                _selected.RemoveAt(0);
            }

            _selected.Add(agent);
            agent.SetSelected(true);
            CharacterVoice.Cue(agent, VoiceCue.Select);
        }

        private void HandleSelection()
        {
            WorldClickUsedBySelection = false;
            if (!IsPlaying) return;
            if (!Input.GetMouseButtonUp(0)) return;
            if (_isoCam != null && _isoCam.SuppressWorldClick) return;
            if (_overseerHud != null && _overseerHud.PointerBlocksWorld) return;
            if (mainCamera == null) return;

            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            SpecialistAgent best = PickAgentUnderCursor();
            if (best != null)
            {
                if (additive) ToggleSelect(best);
                else SelectOnly(best);
                WorldClickUsedBySelection = true;
                return;
            }

            ColonyStructure building = PickStructureUnderCursor();
            if (building != null)
            {
                SelectStructure(building);
                WorldClickUsedBySelection = true;
                return;
            }

            if (!additive)
            {
                ClearSelection();
                ClearStructureSelection();
            }
        }

        private ColonyStructure PickStructureUnderCursor()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore);
            ColonyStructure best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var st = hits[i].collider != null
                    ? hits[i].collider.GetComponentInParent<ColonyStructure>()
                    : null;
                if (st == null || !st.IsAlive) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = st;
                }
            }

            if (best != null) return best;
            if (_isoCam == null || !_isoCam.TryGetMouseGroundPoint(out Vector3 ground))
                return null;
            if (Village == null) return null;

            const float pickRadius = 2.6f;
            float bestSq = pickRadius * pickRadius;
            var list = Village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive) continue;
                Vector3 p = s.WorldPosition;
                float dx = p.x - ground.x;
                float dz = p.z - ground.z;
                float dSq = dx * dx + dz * dz;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = s;
                }
            }
            return best;
        }

        private SpecialistAgent PickAgentUnderCursor()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore);
            SpecialistAgent best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var agent = hits[i].collider != null
                    ? hits[i].collider.GetComponentInParent<SpecialistAgent>()
                    : null;
                if (agent == null || !_agents.Contains(agent)) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = agent;
                }
            }

            if (best != null) return best;

            // Proximity fallback — works even if colliders were stripped mid-frame.
            if (_isoCam == null || !_isoCam.TryGetMouseGroundPoint(out Vector3 ground))
                return null;

            const float pickRadius = 1.75f;
            float bestSq = pickRadius * pickRadius;
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null) continue;
                Vector3 p = a.transform.position;
                float dx = p.x - ground.x;
                float dz = p.z - ground.z;
                float dSq = dx * dx + dz * dz;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = a;
                }
            }

            return best;
        }

        private SpecialistAgent SpawnOne(SpecialistData data, Vector3 pos, Color tint)
        {
            // Upright locomotion root — NavMeshAgent must not share the FBX -90° X import rotation.
            var root = new GameObject(data != null ? $"Specialist_{data.displayName}" : "Specialist");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.identity);

            GameObject mesh = data != null ? UnitMeshCatalog.LoadForClass(data.specialistClass) : null;
            GameObject prefab = data != null ? data.prefab : null;
            if (prefab == null && data != null)
                prefab = DemoContentCatalog.LoadUnitPrefab(data.specialistClass);

            if (mesh != null)
            {
                ColonyVisualUtility.AttachImportVisual(mesh, root.transform);
            }
            else if (prefab != null)
            {
                ColonyVisualUtility.AttachImportVisual(prefab, root.transform);
            }
            else
            {
                GameObject visual = data != null
                    ? UnitPlaceholderFactory.BuildForClass(data.specialistClass)
                    : UnitPlaceholderFactory.BuildScout();
                Quaternion importRot = visual.transform.rotation;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = importRot;
                visual.name = "Visual";
            }

            ColonyVisualUtility.EnsureUrpMaterials(root);
            ColonyVisualUtility.SnapToGround(root);

            var agent = root.GetComponent<SpecialistAgent>();
            if (agent == null) agent = root.AddComponent<SpecialistAgent>();
            agent.Initialize(data, Flags, Brain, Economy, tint, Placer, _campusNav, _world);
            return agent;
        }

        private void EnsureNavMesh()
        {
            _campusNav = GetComponent<CampusNavMesh>();
            if (_campusNav == null) _campusNav = gameObject.AddComponent<CampusNavMesh>();
            _campusNav.Build(grid);

            for (int i = 0; i < _agents.Count; i++)
                _agents[i]?.BindNavMesh(_campusNav);
        }

        /// <summary>
        /// CaptureStill / Phase 4: Commons + neighbouring HAB plus CanFit pad / PWR-1 /
        /// water + regolith yard. Aaron 2026-09-07: no leftover packing, no
        /// interconnect tube webs. Spaced campus — empty dirt stays.
        /// Does not stamp Phase 4 exit.
        /// </summary>
        public bool StampPhase4StillCampus() => StampPhase4DenseCampus();

        /// <summary>
        /// Sibling of <see cref="StampPhase4StillCampus"/> — same hold + chain, then landmark yards.
        /// </summary>
        public bool StampPhase4DenseCampus()
        {
            StillCaptureHold.Arm();
            PlaceDropCommons();
            bool chain = Village != null && Village.StampStillCampusChain();
            StampStillHubNeighbors();
            StampStillDensityPack();
            StampStillLeftoverPack();
            LastStillStamp = StillCampusDensity.StampLog.FromPieces(Placer, _stillLeftoverNote);
            PrepareStillCaptureWorld();
            float aspect = StillCampusDensity.GameTabAspect;
            if (_isoCam != null)
            {
                var cam = _isoCam.GetComponent<Camera>();
                if (cam != null && cam.aspect > 1.05f)
                    aspect = cam.aspect;
            }

            if (chain)
                SnapStillCampusCamera(aspect);
            else
                Debug.LogWarning("[GameLoop] StampPhase4StillCampus failed — Commons face may be blocked.");
            Debug.Log(
                $"[GameLoop] StampPhase4DenseCampus {LastStillStamp} " +
                $"ortho={LastStillOrtho:0.##} aspect={aspect:0.##} hold={StillCaptureHold.Active}");
            return chain;
        }

        /// <summary>
        /// Aaron 2026-09-07: extra HAB + leftover workshop are leftover density
        /// pressure. Still campus keeps Commons + one neighbouring HAB only.
        /// </summary>
        private void StampStillHubNeighbors()
        {
            // Intentionally empty — do not pack extra HAB / hangar onto the still.
        }

        /// <summary>
        /// Pad + Starship, PWR-1/solar, water farm, regolith camp. Island yards
        /// (MinYardGapCells 4) — ExtraPlacementRule would park forward yards on
        /// Campus B.
        /// </summary>
        private void StampStillDensityPack()
        {
            if (Placer == null || grid == null) return;
            if (!StillCampusDensity.TryGetCommons(Placer, out var commons))
            {
                Debug.LogWarning("[GameLoop] Stamp density skipped — no Commons piece.");
                return;
            }

            var habFace = StillCampusDensity.InferHabFace(Placer, commons);
            var bounds = StillBounds();

            // Concept: HAB chain, pad past the HAB, solar behind Commons, industrial opposite.
            if (StillCampusDensity.TryConceptPad(
                    Placer, commons, habFace, bounds, out Vector2Int padCell))
                InstantStampStillBuilding(BuildingCategory.LandingPad, padCell);
            TryStampStillYardOnFace(
                BuildingCategory.Power, StillCampusDensity.YardSize,
                commons, BuildingPlacer.Cardinal.North, bounds);
            TryStampStillYardOnFace(
                BuildingCategory.RegolithCamp, StillCampusDensity.YardSize,
                commons, StillCampusDensity.Opposite(habFace), bounds);
            NotifyCampusExpanded();
            StampStillSuitCrossings();
        }

        /// <summary>
        /// Aaron 2026-09-07 concept: spacesuited figures crossing the open dirt between yards.
        /// Dressing only — Human Basic Motions walk/idle, no agents, no flags,
        /// nothing bypasses SpecialistBrain.
        /// </summary>
        private void StampStillSuitCrossings()
        {
            int salt = RefreshSuitCrossings();
            if (Village == null) return;
            Vector3 campus = ColonyLayout.CampusOrigin;
            var pad = Village.NearestByCategory(campus, 80f, BuildingCategory.LandingPad);
            var farm = Village.NearestByCategory(campus, 80f, BuildingCategory.Farm);
            if (pad != null && farm != null)
            {
                Transform host = buildingRoot != null ? buildingRoot : transform;
                Vector3 toCam = new Vector3(-1f, 0f, -1f).normalized;
                Vector3 crate = pad.transform.position + toCam * 6.4f + new Vector3(1.6f, 0f, -1.6f);
                crate.y = 0f;
                HeroBuildingKits.BuildCargoCrate(host, crate, 28f, 0);
            }
            if (salt > 0)
                Debug.Log($"[GameLoop] Stamp still suit crossings={salt}");
        }

        /// <summary>
        /// Rebuild dressing walkers on Commons↔HAB / pad / farm dirt. Safe to call after
        /// campus expand. Destroys the previous Dress_SuitCrossings folder first.
        /// </summary>
        private int RefreshSuitCrossings()
        {
            Transform host = buildingRoot != null ? buildingRoot : transform;
            Transform old = host.Find("Dress_SuitCrossings");
            if (old != null)
            {
                if (Application.isPlaying) Object.Destroy(old.gameObject);
                else Object.DestroyImmediate(old.gameObject);
            }

            if (Village == null) return 0;
            Vector3 campus = ColonyLayout.CampusOrigin;
            var commons = Village.NearestByCategory(campus, 80f, BuildingCategory.Commons);
            if (commons == null) return 0;

            var folder = new GameObject("Dress_SuitCrossings").transform;
            folder.SetParent(host, false);

            int salt = 0;
            Vector3 c = commons.transform.position;
            var pad = Village.NearestByCategory(campus, 80f, BuildingCategory.LandingPad);
            if (pad != null)
                salt = StampCrossingPair(folder, c, pad.transform.position, 0.40f, 0.56f, 1.4f, salt);
            var hab = Village.NearestByCategory(campus, 80f, BuildingCategory.Habitat);
            if (hab != null)
                salt = StampCrossingPair(folder, c, hab.transform.position, 0.44f, 0.58f, 2.6f, salt);
            var farm = Village.NearestByCategory(campus, 80f, BuildingCategory.Farm);
            if (farm != null)
                salt = StampCrossingPair(folder, c, farm.transform.position, 0.48f, 0.60f, 1.6f, salt);
            if (pad != null && farm != null)
            {
                Vector3 toCam = new Vector3(-1f, 0f, -1f).normalized;
                Vector3 mid = Vector3.Lerp(farm.transform.position, pad.transform.position, 0.72f) + toCam * 2.2f;
                mid.y = 0f;
                Vector3 along = pad.transform.position - farm.transform.position;
                along.y = 0f;
                if (along.sqrMagnitude > 1f)
                {
                    along.Normalize();
                    float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
                    var fig = HeroBuildingKits.BuildSpacesuitFigure(folder, mid, yaw, salt++);
                    fig?.GetComponent<SuitCrossingWalker>()?.SetPath(mid - along * 2.2f, mid + along * 2.2f);
                }
            }
            return salt;
        }

        /// <summary>
        /// Two figures walking the line a→b, offset toward the camera (-x,-z) so they clear
        /// dock arms and stand on open dirt the ortho view can see.
        /// </summary>
        private static int StampCrossingPair(
            Transform root, Vector3 a, Vector3 b, float t0, float t1, float side, int salt)
        {
            Vector3 dir = b - a;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1f) return salt;
            dir.Normalize();
            Vector3 perp = new Vector3(-1f, 0f, -1f).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            Vector3 p0 = Vector3.Lerp(a, b, t0) + perp * side;
            p0.y = 0f;
            Vector3 p0b = Vector3.Lerp(a, b, t1) + perp * side;
            p0b.y = 0f;
            var fig0 = HeroBuildingKits.BuildSpacesuitFigure(root, p0, yaw, salt++);
            fig0?.GetComponent<SuitCrossingWalker>()?.SetPath(p0, p0b);

            Vector3 p1 = Vector3.Lerp(a, b, t1) + perp * (side + 0.9f);
            p1.y = 0f;
            Vector3 p1b = Vector3.Lerp(a, b, t0) + perp * (side + 0.9f);
            p1b.y = 0f;
            var fig1 = HeroBuildingKits.BuildSpacesuitFigure(root, p1, yaw + 180f, salt++);
            fig1?.GetComponent<SuitCrossingWalker>()?.SetPath(p1, p1b);
            return salt;
        }

        /// <summary>
        /// Aaron 2026-09-07: leftover Inn / wonder / hangar are not a still
        /// density gate. Empty dirt between landmark yards stays.
        /// </summary>
        private void StampStillLeftoverPack()
        {
            _stillLeftoverNote = "spaced";
            Debug.Log("[GameLoop] Stamp leftover skipped leftover=spaced (Aaron 2026-09-07)");
        }

        private StillCampusDensity.BoundsOk StillBounds()
        {
            return (origin, width, height) =>
                grid.InBounds(origin) &&
                grid.InBounds(new Vector2Int(origin.x + width - 1, origin.y + height - 1));
        }

        private void TryStampStillYardOnFace(
            BuildingCategory cat,
            int side,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal face,
            StillCampusDensity.BoundsOk bounds)
        {
            Vector2Int origin = StillCampusDensity.FlushOrigin(
                commons, face, side, side, StillCampusDensity.LandmarkGapCells);
            if (!Placer.CanFitRect(origin, side, side) ||
                (bounds != null && !bounds(origin, side, side)))
            {
                TryStampStillYard(cat, side, commons, face, bounds);
                return;
            }

            bool ok = InstantStampStillBuilding(cat, origin);
            Debug.Log($"[GameLoop] Stamp density {DensityLabel(cat)}={ok} origin={origin} face={face}");
        }

        private void TryStampStillYard(
            BuildingCategory cat,
            int side,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            StillCampusDensity.BoundsOk bounds)
        {
            if (DataForCategory(cat) == null)
            {
                Debug.Log($"[GameLoop] Stamp density {DensityLabel(cat)}=False (no catalog)");
                return;
            }

            Vector2Int origin;
            bool found = StillCampusDensity.TryNext(
                Placer, commons, habFace, side, side, bounds, out origin,
                StillCampusDensity.LandmarkGapCells);
            if (!found)
            {
                Debug.Log($"[GameLoop] Stamp density {DensityLabel(cat)}=False (CanFit)");
                return;
            }

            bool ok = InstantStampStillBuilding(cat, origin);
            Debug.Log($"[GameLoop] Stamp density {DensityLabel(cat)}={ok} origin={origin}");
        }

        private static string DensityLabel(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.LandingPad: return "pad";
                case BuildingCategory.Power: return "pwr";
                case BuildingCategory.Farm: return "water";
                case BuildingCategory.RegolithCamp: return "regolith";
                case BuildingCategory.EngineerWorkshop: return "workshop";
                case BuildingCategory.Inn: return "inn";
                case BuildingCategory.AegisSpire: return "wonder";
                case BuildingCategory.Habitat: return "hab";
                case BuildingCategory.Defense: return "defense";
                default: return cat.ToString();
            }
        }

        private bool InstantStampStillBuilding(BuildingCategory cat, Vector2Int origin)
        {
            var data = DataForCategory(cat);
            if (data == null || Placer == null || grid == null) return false;

            int fw = Mathf.Max(1, data.footprintWidth);
            int fh = Mathf.Max(1, data.footprintHeight);
            Vector3 world = FootprintWorldCenter(origin, fw, fh);
            if (!Placer.TryRestore(data, origin, world, 1f, out _))
                return false;

            Transform root = buildingRoot != null ? buildingRoot : transform;
            GameObject go = ModularBuildingFactory.Spawn(
                data.category, world, root, fw, fh, grid.CellSize);
            go.name = $"Bld_{data.displayName}_still";
            CampusNavMesh.AddObstacle(go);
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
            CampusDressing.DressPlaced(data, go, _body);
            if (data.category == BuildingCategory.LandingPad)
                SyncLaunchGate();
            return true;
        }

        /// <summary>
        /// CaptureStill only: freeze life-support / colony-extinct so OUTPOST LOST cannot cover the shutter,
        /// and drop queued W2 cut / ArrivalLog / travel-toast overlays so they cannot cover the campus.
        /// Stamp HAB latches EverHadHab without a census — that is the still15 fail overlay.
        /// </summary>
        public void PrepareStillCaptureWorld()
        {
            StillCaptureHold.Arm();


            EmptyRosterFailed = false;
            _emptyRosterTimer = 0f;
            _mission?.ClearLossForStill();
            EnsureNarrative();
            _narrative.ClearPendingCuts();
            _overseerHud?.ClearToast();
        }

        /// <summary>Rebuild walkable mesh after village HABs / connectors expand the campus.</summary>
        public void NotifyCampusExpanded()

        {
            if (_campusNav != null && grid != null)
            {
                _campusNav.Build(grid);
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i]?.BindNavMesh(_campusNav);
            }

            RefreshSuitCrossings();

            if (InFaunaGrace)
                return;
            if (_ecologyCooldown > OverseerRules.FaunaExpandDelay)
                _ecologyCooldown = OverseerRules.FaunaExpandDelay;
        }

        /// <summary>Ships only land where there is a pad. There is no power grid.</summary>
        private void RefreshPowerBudget()
        {
            if (Economy == null) return;
            Economy.HasDock = Settlement != null && Settlement.HasPad;
        }

        private void TickCampusEcology(float dt)
        {
            if (Threat == null) return;
            int pieces = Placer != null ? Placer.Pieces.Count : 0;
            float ambient = _body != null ? _body.AmbientThreat : 0.12f;
            float expand = _body != null ? _body.ExpansionThreat : 0.018f;
            ambient += expand * Mathf.Min(14, pieces);
            ambient *= Mathf.Clamp(_tech.AmbientThreatScale, 0.35f, 1.2f);
            ambient *= Mathf.Clamp(ReplayRules.AmbientThreatMul, 0.7f, 1.8f);
            int uncleared = _world != null ? _world.UnclearedLairCount : 1;
            bool densQuiet = _world != null && _world.Lairs.Count > 0 && uncleared <= 0;
            if (densQuiet)
                ambient *= 0.55f;
            Threat.Ambient = ambient;

            _ecologyCooldown -= dt;
            if (_ecologyCooldown > 0f) return;

            if (densQuiet)
            {
                RetreatCampusFauna();
                _ecologyCooldown = 18f * ReplayRules.FaunaSpawnIntervalScale;
                return;
            }

            TrySpawnCampusFauna();
        }

        private void RetreatCampusFauna()
        {
            bool any = false;
            var names = new List<string>();
            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s == null || !s.IsAlive) continue;
                if (!DustStalkerAgent.IsCampusPest(s.Kind)) continue;
                string label = (s.RoleLabel ?? s.Kind.ToString()).ToLowerInvariant();
                if (!names.Contains(label)) names.Add(label);
                s.BeginRetreat();
                any = true;
            }

            if (any && !_faunaRetreated)
            {
                _faunaRetreated = true;
                LogOverseer("Dens quiet — campus pests scatter: " + JoinEnglish(names) + ".");
            }
        }

        private void TrySpawnCampusFauna()
        {
            if (Settlement == null || Threat == null) return;
            if (InFaunaGrace)
            {
                float left = _mission != null
                    ? OverseerRules.FaunaGraceSeconds - _mission.MissionElapsed
                    : OverseerRules.FaunaGraceSeconds;
                _ecologyCooldown = Mathf.Max(1f, left);
                return;
            }
            if (_world != null && _world.Lairs.Count > 0 && _world.UnclearedLairCount <= 0) return;

            int mites = CountFauna(FaunaKind.Mite);
            int leeches = CountFauna(FaunaKind.Leech);
            int ticks = CountFauna(FaunaKind.Tick);
            int wisps = CountFauna(FaunaKind.Wisp);
            int creepers = CountFauna(FaunaKind.Creeper);
            int hoppers = CountFauna(FaunaKind.Hopper);
            int uncleared = _world != null ? _world.UnclearedLairCount : 0;
            int cap = _body != null ? Mathf.Max(2, _body.CampusFaunaCap) : 4;
            cap = Mathf.Max(2, Mathf.RoundToInt(cap * ReplayRules.FaunaCapScale));
            if (Settlement.HasOutpost) cap += 2;
            cap += uncleared;
            if (mites + leeches + ticks + wisps + creepers + hoppers >= cap)
            {
                _ecologyCooldown = 12f * ReplayRules.FaunaSpawnIntervalScale;
                return;
            }

            float miteW = (_body != null ? _body.MiteSpawnWeight : 1f) * ReplayRules.FaunaWeightMul;
            float leechW = (_body != null ? _body.LeechSpawnWeight : 1f) * ReplayRules.FaunaWeightMul;
            float tickW = (_body != null ? _body.TickSpawnWeight : 0f) * ReplayRules.FaunaWeightMul;
            float wispW = (_body != null ? _body.WispSpawnWeight : 0f) * ReplayRules.FaunaWeightMul;
            float creeperW = (_body != null ? _body.CreeperSpawnWeight : 0f) * ReplayRules.FaunaWeightMul;
            float hopperW = (_body != null ? _body.HopperSpawnWeight : 0f) * ReplayRules.FaunaWeightMul;
            int kindCap = Mathf.Min(3, 1 + uncleared / 3);
            int miteCap = miteW > 0.05f ? kindCap : 0;
            int leechCap = leechW > 0.05f ? kindCap : 0;
            int tickCap = tickW > 0.05f ? kindCap : 0;
            int wispCap = wispW > 0.05f ? kindCap : 0;
            int creeperCap = creeperW > 0.05f ? kindCap : 0;
            int hopperCap = hopperW > 0.05f ? kindCap : 0;
            if (_body != null && _body.RadiationDrainPerSecond > 0f && wispW > 0.05f)
                wispCap = Mathf.Max(wispCap, 1);

            Vector3 campus = CampusFaunaOrigin();

            var farm = Village != null
                ? Village.NearestByCategory(campus, 80f, BuildingCategory.Farm)
                : null;
            if (farm != null && TrySpawnCampusKind(
                    FaunaKind.Creeper, creepers, creeperCap,
                    farm.WorldPosition, campus,
                    CreeperFirstLog()))
                return;

            var hab = Village != null
                ? Village.NearestByCategory(campus, 80f, BuildingCategory.Habitat)
                : null;
            if (hab == null && Village != null)
                hab = Village.NearestVillageHab(campus, 80f);
            if (hab != null && TrySpawnCampusKind(
                    FaunaKind.Hopper, hoppers, hopperCap,
                    hab.WorldPosition, campus,
                    HopperFirstLog()))
                return;

            var mine = Village != null
                ? Village.NearestByCategory(campus, 80f, BuildingCategory.Mine, BuildingCategory.Mining)
                : null;
            string tickLine = _body != null && _body.Id == CelestialBodyId.Belt
                ? "Rock ticks on the ore — post Defend Area."
                : "Dust ticks on the ore — post Defend Area.";
            if (mine != null && TrySpawnCampusKind(
                    FaunaKind.Tick, ticks, tickCap,
                    mine.WorldPosition, campus, tickLine))
                return;

            var pwr = Village != null
                ? Village.NearestPower(campus, 80f)
                : null;
            string wispLine = _body != null && _body.RadiationDrainPerSecond > 0f
                ? "Ice wisps off the crust — post Clear Threat."
                : "Dust wisps on the grid — post Clear Threat.";
            if (pwr != null && TrySpawnCampusKind(
                    FaunaKind.Wisp, wisps, wispCap, pwr.WorldPosition, campus, wispLine))
                return;

            var camp = Village != null
                ? Village.NearestExtractor(campus, 80f)
                : null;
            string miteLine = _body != null && _body.PreferMineMites
                ? "Rock mites on the ore — post Defend Area."
                : "Regolith mites on the farm — post Defend Area.";
            if (camp != null && TrySpawnCampusKind(
                    FaunaKind.Mite, mites, miteCap,
                    camp.WorldPosition, campus, miteLine))
                return;

            string leechLine = _body != null && _body.RadiationDrainPerSecond > 0f
                ? "Fissure leeches on the grid — post Clear Threat."
                : "Watt leeches on the Power Node — post Clear Threat.";
            if (pwr != null && TrySpawnCampusKind(
                    FaunaKind.Leech, leeches, leechCap, pwr.WorldPosition, campus, leechLine))
                return;

            _ecologyCooldown = 8f * ReplayRules.FaunaSpawnIntervalScale;
        }

        private bool TrySpawnCampusKind(
            FaunaKind kind, int have, int cap, Vector3 attractor, Vector3 campus, string firstLog)
        {
            if (have >= cap) return false;
            Vector3 home = FaunaSpawnNear(attractor, campus);
            if (SpawnFaunaAt(kind, home) == null) return false;
            if (have == 0 && !string.IsNullOrEmpty(firstLog))
            {
                LogOverseer(firstLog);
                // Hopper/fauna must not pan the campus close-up off-frame.
                bool campusPlaced = Placer != null && Placer.Pieces != null && Placer.Pieces.Count > 0;
                if (!campusPlaced)
                    GlanceAt(home, force: true);
            }
            _ecologyCooldown = 10f * ReplayRules.FaunaSpawnIntervalScale;
            return true;
        }

        private string CreeperFirstLog()
        {
            if (_body == null) return "Soil creepers on the farm — post Defend Area.";
            switch (_body.Id)
            {
                case CelestialBodyId.Mars: return "Dust creepers on the farm — post Defend Area.";
                case CelestialBodyId.Europa: return "Ice creepers on the greenhouse — post Defend Area.";
                default: return "Soil creepers on the farm — post Defend Area.";
            }
        }

        private string HopperFirstLog()
        {
            if (_body == null) return "Ash hoppers on the HAB — post Clear Threat.";
            switch (_body.Id)
            {
                case CelestialBodyId.Mars: return "Dust hoppers at the Commons — post Clear Threat.";
                case CelestialBodyId.Belt: return "Shard hoppers on the Commons — post Clear Threat.";
                default: return "Ash hoppers on the HAB — post Clear Threat.";
            }
        }

        private static string JoinEnglish(List<string> parts)
        {
            if (parts == null || parts.Count == 0) return "campus pests";
            if (parts.Count == 1) return parts[0];
            if (parts.Count == 2) return parts[0] + " and " + parts[1];
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(i == parts.Count - 1 ? ", and " : ", ");
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        private Vector3 CampusFaunaOrigin()
        {
            if (Settlement != null && Settlement.HasOutpost && Random.value > 0.42f)
                return ColonyLayout.CampusBOrigin;
            return ColonyLayout.CampusOrigin;
        }

        private static Vector3 FaunaSpawnNear(Vector3 target, Vector3 campus)
        {
            Vector3 away = target - campus;
            away.y = 0f;
            if (away.sqrMagnitude < 0.25f) away = Vector3.forward;
            away.Normalize();
            Vector2 jitter = Random.insideUnitCircle * 2.5f;
            return target + away * 9f + new Vector3(jitter.x, 0f, jitter.y);
        }

        private int CountFauna(FaunaKind kind)
        {
            int n = 0;
            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s != null && s.IsAlive && s.Kind == kind) n++;
            }
            return n;
        }

        private static string FaunaObjectName(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.Mite: return "RegolithMite";
                case FaunaKind.Leech: return "WattLeech";
                case FaunaKind.Wisp: return "IceWisp";
                case FaunaKind.Tick: return "RockTick";
                case FaunaKind.Creeper: return "SoilCreeper";
                case FaunaKind.Hopper: return "AshHopper";
                case FaunaKind.JunkBot: return "JunkBot";
                default: return "DustStalker";
            }
        }

        /// <summary>Spawn campus-attracted fauna or a Dust Stalker.</summary>
        public DustStalkerAgent SpawnFaunaAt(FaunaKind kind, Vector3 home, Transform parent = null)
        {
            if (kind == FaunaKind.Stalker)
                return SpawnStalkerAt(home, parent);
            if (Threat == null) return null;

            Transform root = parent != null ? parent : (_threatRoot != null ? _threatRoot : transform);
            GameObject mesh = UnitMeshCatalog.LoadFauna(kind);
            GameObject go = new GameObject(FaunaObjectName(kind));
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(home, Quaternion.identity);
            if (mesh != null)
            {
                ColonyVisualUtility.AttachImportVisual(mesh, go.transform);
            }
            else
            {
                GameObject visual = UnitPlaceholderFactory.BuildFauna(kind);
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = Vector3.zero;
            }
            if (kind == FaunaKind.Hopper)
            {
                Transform visual = go.transform.Find("Visual");
                GameObject scaleRoot = visual != null ? visual.gameObject : go;
                ColonyVisualUtility.ScaleToHeight(scaleRoot, 2.35f);
            }
            else if (kind == FaunaKind.JunkBot)
            {
                Transform visual = go.transform.Find("Visual");
                GameObject scaleRoot = visual != null ? visual.gameObject : go;
                ColonyVisualUtility.ScaleToHeight(scaleRoot, 0.95f);
            }
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            home = go.transform.position;

            var agent = go.GetComponent<DustStalkerAgent>();
            if (agent == null) agent = go.AddComponent<DustStalkerAgent>();
            agent.Initialize(Threat, Flags, home, this);
            agent.SetKind(kind);
            agent.ApplyBodyTune(_body);
            if (_mission != null && _mission.FrenzyActive)
                agent.SetFrenzy(true);
            _stalkers.Add(agent);
            return agent;
        }

        private void SpawnThreats()
        {
            _stalkers.Clear();
            if (!spawnDustStalkers || Threat == null)
                return;

            if (_world != null && _world.Lairs.Count > 0)
            {
                _world.SpawnLairStalkers(_threatRoot != null ? _threatRoot : transform);
                Debug.Log($"[GameLoop] Spawned {_stalkers.Count} Dust Stalker(s) from {_world.Lairs.Count} lair(s).");
                return;
            }

            // Fallback if world gen produced no lairs.
            if (dustStalkerCount > 0)
                SpawnStalkerWave(dustStalkerCount, Mathf.Max(14f, stalkerSpawnRadius), ColonyLayout.CampusOrigin);
            if (spawnSecondBody && campusBStalkerCount > 0)
                SpawnStalkerWave(campusBStalkerCount, 11f, ColonyLayout.CampusBOrigin);
            Debug.Log($"[GameLoop] Spawned {_stalkers.Count} Dust Stalker(s) (fallback ring).");
        }

        private void GenerateWorld()
        {
            _faunaRetreated = false;
            _radWarned = false;
            _world = GetComponent<PlanetaryWorldGen>();
            if (_world == null) _world = gameObject.AddComponent<PlanetaryWorldGen>();
            _world.Generate(this, grid, BodySeed.Current, _body);
        }

        /// <summary>Spawn one stalker at a world point (lair / wave helpers).</summary>
        public DustStalkerAgent SpawnStalkerAt(Vector3 home, Transform parent = null)
        {
            if (Threat == null) return null;

            Transform root = parent != null ? parent : (_threatRoot != null ? _threatRoot : transform);
            GameObject stalkerPrefab = DemoContentCatalog.LoadStalkerPrefab();
            GameObject go;
            if (stalkerPrefab != null)
            {
                go = new GameObject("DustStalker");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(home, Quaternion.identity);
                ColonyVisualUtility.AttachImportVisual(stalkerPrefab, go.transform);
            }
            else
            {
                go = UnitPlaceholderFactory.BuildDustStalker();
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(home, Quaternion.identity);
            }

            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            home = go.transform.position;

            var stalker = go.GetComponent<DustStalkerAgent>();
            if (stalker == null) stalker = go.AddComponent<DustStalkerAgent>();
            stalker.Initialize(Threat, Flags, home, this);
            stalker.ApplyBodyTune(_body);
            _stalkers.Add(stalker);
            return stalker;
        }

        /// <summary>Spawns additional stalkers. Returns count spawned.</summary>
        public int SpawnStalkerWave(int count, float radius) =>
            SpawnStalkerWave(count, radius, ColonyLayout.CampusOrigin);

        public int SpawnStalkerWave(int count, float radius, Vector3 origin)
        {
            if (count <= 0 || Threat == null) return 0;

            float r = Mathf.Max(10f, radius);
            int spawned = 0;
            float phase = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < count; i++)
            {
                float angle = phase + (Mathf.PI * 2f * i) / count;
                Vector3 home = origin + new Vector3(
                    Mathf.Cos(angle) * r,
                    0f,
                    Mathf.Sin(angle) * r);
                if (SpawnStalkerAt(home) != null)
                    spawned++;
            }

            return spawned;
        }

        private void EnsureHud()
        {
            _overseerHud = GetComponent<OverseerHud>();
            if (_overseerHud == null) _overseerHud = gameObject.AddComponent<OverseerHud>();
            _overseerHud.Bind(this);

            _debugHud = GetComponent<DebugHud>();
            if (_debugHud == null) _debugHud = gameObject.AddComponent<DebugHud>();
            _debugHud.Bind(this);
            _debugHud.SetVisible(false);
        }

        private void CountLabs(out int labs, out int workers)
        {
            labs = 0;
            workers = 0;
            if (Village == null) return;
            var list = Village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive) continue;
                if (s.Category != BuildingCategory.Laboratory) continue;
                labs++;
                workers += s.WorkerCount;
            }
        }

        private void EnsureMission()
        {
            _mission = GetComponent<MissionController>();
            if (_mission == null) _mission = gameObject.AddComponent<MissionController>();
            _mission.Bind(this);
            SyncLaunchGate();
        }

        public int SittingLevy => Village != null ? Village.TotalSittingLevy() : 0;

        public bool HasLivingCourier
        {
            get
            {
                for (int i = 0; i < _agents.Count; i++)
                {
                    var a = _agents[i];
                    if (a != null && a.IsAlive && !a.IsIncapacitated &&
                        a.Data != null && a.Data.specialistClass == SpecialistClass.CourierBot)
                        return true;
                }

                return false;
            }
        }

        public void NotifyLevySitting(int amount)
        {
            if (amount <= 0 || HasLivingCourier) return;
            if (Time.time < _purseToastAt) return;
            _purseToastAt = Time.time + 48f;
            LogOverseer(CompactGrok.PurseSitting(amount));
        }

        public void NotifyLevyStolen(int amount, string where)
        {
            if (amount <= 0) return;
            LogOverseer(CompactGrok.LevyStolen(amount, where));
        }

        public bool TryArmWatchtower(ColonyStructure tower)
        {
            if (tower == null || !tower.IsWatchtower || !tower.IsAlive)
            {
                LogOverseer("Pick a Watchtower.");
                return false;
            }

            if (tower.LaserArmed)
            {
                LogOverseer("Lasers already armed.");
                return false;
            }

            if (Resources == null ||
                !Resources.TrySpend(ResourceId.Metals, OverseerRules.WatchtowerLaserCost))
            {
                LogOverseer($"Arming lasers needs {OverseerRules.WatchtowerLaserCost} EU.");
                return false;
            }

            tower.ArmLasers();
            LogOverseer($"Watchtower lasers armed — {OverseerRules.WatchtowerLaserCost} EU.");
            DemoVfx.ClaimRing(tower.WorldPosition, new Color(0.95f, 0.35f, 0.2f));
            return true;
        }

        public void DeliverLevy(int amount)
        {
            if (amount <= 0 || Settlement == null) return;
            Settlement.NoteLevyDelivered(amount);
            NoteTreasuryIncome(amount);
            LogOverseer(CompactGrok.LevyDelivered(amount));
        }

        public void RetryParty() => PayFobotYard();

        /// <summary>
        /// Y / inspect pay. Requires a living Fobot Yard (Inn or dedicated yard). Credits only.
        /// Stands downed robots and resurrects yard wrecks as the same ego.
        /// </summary>
        public void PayFobotYard()
        {
            if (!HasDownedRobot && !HasScrapCorpse)
            {
                LogOverseer("No one is down — the Fobot Yard is for wrecks and incapacitated robots.");
                return;
            }
            if (!HasFobotYard)
            {
                LogOverseer(CompactGrok.YardNeedsBuilding());
                return;
            }

            var yard = FindFobotYard();
            if (yard == null || !yard.IsAlive)
            {
                LogOverseer(OverseerRules.GrokYardMissing, 6.2f);
                return;
            }

            if (Time.time < _reviveReadyAt)
            {
                LogOverseer($"Fobot Yard cooling down — {FieldReviveReadyIn:F0}s.");
                return;
            }

            ComputeReviveBill(out int met, out _);
            if (met <= 0)
            {
                if (!TryGrok(GrokBeat.YardUnaffordable, true))
                    LogOverseer(CompactGrok.YardBill(met));
                return;
            }

            if (Economy == null || !Economy.CanAffordRevive(met))
            {
                if (!TryGrok(GrokBeat.YardUnaffordable, true))
                    LogOverseer(OverseerRules.GrokYardUnaffordable, 6.2f);
                return;
            }

            if (!Economy.TrySpendRevive(met))
            {
                if (!TryGrok(GrokBeat.YardUnaffordable, true))
                    LogOverseer(OverseerRules.GrokYardUnaffordable, 6.2f);
                return;
            }

            DemoAudio.PlayRetry();
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || !a.IsIncapacitated) continue;
                a.FieldRevive();
                a.NoteRevivePaid();
            }

            ResurrectWrecksAtYard(yard);
            _reviveReadyAt = Time.time + OverseerRules.ReviveCooldown;
            if (!_revivePenaltyApplied)
                _revivePenaltyApplied = true;
            _mission?.OnPartyRevived();
            if (!_yardBillLogged)
            {
                _yardBillLogged = true;
                if (!TryGrok(GrokBeat.YardBill))
                    LogOverseer(OverseerRules.GrokYardFirstBill, 6.4f);
            }
            else if (!TryGrok(GrokBeat.YardBill))
                LogOverseer(CompactGrok.YardBill(met));
            Debug.Log("[GameLoop] Fobot Yard paid.");
        }

        private void ClearCurrentWorldForRetry()
        {
            SaveSystem.DeleteWorld(celestialBody);
            SaveSystem.Delete(SaveSystem.AutosaveSlot);
            DemoSettings.WriteCampus(celestialBody, "");
            DemoSettings.WriteRoster(celestialBody, "");
            // A retry starts a funded new colony, not the previous losing stockpile.
            DemoSettings.SaveExists = false;
            PlayerPrefs.SetInt(DemoSettings.SaveFlagKey, 0);
            PlayerPrefs.Save();
        }

        public void RestartMission()
        {
            if (ReplayRules.BlocksManualSavesAndReloads)
            {
                LogOverseer("Ironman — no second draft. This body cannot be restarted.");
                return;
            }
            ClearCurrentWorldForRetry();
            if (advanceSeedOnRestart)
                BodySeed.AdvanceForNextConquest();
            DemoAudio.PlayRetry();
            DemoSettings.RequestBootIntoPlay();
            ReloadActiveScene();
        }

        /// <summary>Body gate met — increment conquest count and evaluate achievements (Ironman, Skinflint, Clean Sheet).</summary>
        public void NoteBodyConquered()
        {
            Stats.BodiesConquered++;
            Achievements.Evaluate(Stats);
        }

        /// <summary>Same-body reseed (sandbox rematch).</summary>
        public void BeginNextConquest()
        {
            ClearCurrentWorldForRetry();
            BodySeed.AdvanceForNextConquest();
            DemoSettings.RequestBootIntoPlay();
            DemoAudio.PlayRetry();
            ReloadActiveScene();
        }

        /// <summary>Campaign advance: unlock next body and travel there with a fresh seed.</summary>
        public void AdvanceCampaign()
        {
            Vector3 pad = ColonyLayout.CampusOrigin + new Vector3(16f, 0f, 0f);
            LaunchSite.PlayDeparture(pad);
            string freight = PayInterBodyFreight();
            PersistSession();
            CampaignProgress.UnlockNextFrom(celestialBody);
            DemoSettings.RequestBootIntoPlay();
            var next = CampaignProgress.NextAfter(celestialBody);
            if (!next.HasValue)
            {
                BeginNextConquest();
                return;
            }

            string from = _body != null ? _body.DisplayName : celestialBody.ToString();
            string to = CelestialBodyCatalog.Get(next.Value).DisplayName;
            string hop = CampaignCutsceneCatalog.TryGetVictory(celestialBody, out var cut)
                ? cut.LogParagraph
                : $"Departure from {from}. Trajectory locked: {to}.";
            if (!string.IsNullOrEmpty(freight))
                hop += " " + freight;
            CampaignProgress.QueueTravelLog(hop);
            BodySeed.SetBody(next.Value);
            BodySeed.Ensure(next.Value, 0);
            BodySeed.AdvanceForNextConquest();
            DemoAudio.PlayRetry();
            ReloadActiveScene();
        }

        private string PayInterBodyFreight()
        {
            if (Resources == null || _body == null) return "";
            int met = Mathf.Max(0, Mathf.RoundToInt(_body.FreightMetals * Mathf.Max(0.25f, _tech.FreightScale)));
            int ice = Mathf.Max(0, Mathf.RoundToInt(_body.FreightIce * Mathf.Max(0.25f, _tech.FreightScale)));
            if (met <= 0 && ice <= 0) return "";

            _ = ice;
            bool paid = Resources.Get(ResourceId.Metals) >= met;
            if (paid)
            {
                Resources.TrySpend(ResourceId.Metals, met);
                return $"Freight paid: −{met} EU.";
            }

            Resources.ApplyLoss(0.12f);
            return "Freight short — 12% of the stockpile jettisoned to make mass.";
        }

        /// <summary>Switch world without advancing that body's seed. Respects campaign unlocks unless cheating.</summary>
        public void SelectBody(CelestialBodyId body, bool allowLocked = false)
        {
            if (body == celestialBody) return;
            if (!allowLocked && !CampaignProgress.IsUnlocked(body))
            {
                Debug.Log($"[GameLoop] {body} is locked — conquer the prior world first.");
                return;
            }
            PersistSession();
            BodySeed.SetBody(body);
            DemoSettings.RequestBootIntoPlay();
            DemoAudio.PlayRetry();
            ReloadActiveScene();
        }

        /// <summary>
        /// Debug hop from Playing or Paused (tutorial does not block). Shift+F10 unlocks
        /// the campaign spine then cycles; Shift+click a locked chip hops to that body.
        /// Queued so OnGUI and Update cannot double-load the scene in one press.
        /// </summary>
        public void RequestDebugHop(CelestialBodyId body, bool unlockAll)
        {
            if (DemoSettings.FirstHourDemo)
            {
                _overseerHud?.Notify(
                    "This demo stays on Earth. Settings → Full campaign opens the other worlds.",
                    3.5f);
                return;
            }
            if (_bodyHopQueued) return;
            _bodyHopQueued = true;
            _bodyHopTarget = body;
            _bodyHopUnlock = unlockAll;
        }

        public void RequestDebugBodyCycle(bool unlockAll) =>
            RequestDebugHop(CelestialBodyCatalog.Next(celestialBody), unlockAll);

        private void HandleBodyHopHotkeys()
        {
            if (InputBindings.TextEntryActive) return; // typing flag orders
            if (Screen != DemoScreen.Playing && Screen != DemoScreen.Paused) return;
            if (!Input.GetKeyDown(KeyCode.F10)) return;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            RequestDebugBodyCycle(shift);
        }

        private void FlushDebugBodyHop()
        {
            if (!_bodyHopQueued) return;
            _bodyHopQueued = false;
            if (_bodyHopUnlock)
                CampaignProgress.DebugUnlockAll();
            var profile = CelestialBodyCatalog.Get(_bodyHopTarget);
            string hop = _bodyHopUnlock
                ? $"Debug hop — {profile.DisplayName} (all worlds unlocked)."
                : $"Debug hop — {profile.DisplayName}.";
            CampaignProgress.QueueTravelLog(hop);
            SelectBody(_bodyHopTarget, allowLocked: true);
        }

        private static void ReloadActiveScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }

        private void HandleToolHotkeys()
        {
            if (InputBindings.TextEntryActive) return; // typing flag orders
            if (!IsPlaying) return;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (activeTool == OverseerTool.Flag) ApplyTool(OverseerTool.Build);
                else if (activeTool == OverseerTool.Build) ApplyTool(OverseerTool.None);
                else ApplyTool(OverseerTool.Flag);
            }

            if (Input.GetKeyDown(KeyCode.B)) ToggleTool(OverseerTool.Build);
            if (Input.GetKeyDown(KeyCode.G)) ToggleTool(OverseerTool.Flag);
            if (Input.GetKeyDown(KeyCode.T) && _overseerHud != null)
                _overseerHud.ToggleTechPanel();

            if (Input.GetKeyDown(KeyCode.F8) && _debugHud != null)
                _debugHud.ToggleVisible();

            if (Input.GetKeyDown(KeyCode.F6))
                FocusCampus(0);
            if (Input.GetKeyDown(KeyCode.F7))
                FocusCampus(1);
            if (Input.GetKeyDown(KeyCode.F9))
                SeedCampusBAttract();

            if (Input.GetKeyDown(KeyCode.P))
                FormParty();
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                DisbandSelectedParty();

            if (Input.GetKeyDown(KeyCode.R))
                DebugFatigueAll(0.92f);
        }

        /// <summary>
        /// Phase 5E: high Explore bounty at Campus B plaza — Scout may pursue via brain scoring.
        /// No click-to-move; F7 focuses camera on the outpost.
        /// </summary>
        public void SeedCampusBAttract()
        {
            if (Flags == null || exploreFlagData == null) return;

            Vector3 world = ColonyLayout.PartySpawnB;
            if (grid != null)
                world = grid.SnapToCellCenter(world);

            const float bounty = 1600f; // Majesty gold scale
            if (_flagInput != null)
            {
                _flagInput.PostFlagAt(exploreFlagData, world, bounty);
            }
            else
            {
                var handle = Flags.Post(exploreFlagData, world, bounty);
                DemoAudio.PlayFlagPost(world);
                NotifyFlagPosted(handle);
            }

            FocusCampus(1);
            Debug.Log($"[GameLoop] Seeded Explore attractor @ Campus B bounty={bounty:F0}");
        }

        /// <summary>0 = Campus A (primary), 1 = Campus B (second body).</summary>
        public void FocusCampus(int bodyIndex)
        {
            _focusedCampus = bodyIndex <= 0 ? 0 : 1;
            if (_isoCam == null) return;
            Vector3 focus = _focusedCampus == 0 ? ColonyLayout.CameraFocus : ColonyLayout.CameraFocusB;
            bool campusPlaced = Placer != null && Placer.Pieces != null && Placer.Pieces.Count > 0;
            _isoCam.FocusOn(focus, ColonyLayout.PlayOrtho(campusPlaced));
            DemoAudio.SetCampusAmbient(_focusedCampus);
            Debug.Log(_focusedCampus == 0
                ? "[GameLoop] Camera → Campus A (primary)"
                : "[GameLoop] Camera → Campus B (second body)");
        }

        private void ApplyTool(OverseerTool tool)
        {
            activeTool = tool;
            if (_flagInput != null) _flagInput.EnabledPlacement = tool == OverseerTool.Flag;
            if (_buildInput != null) _buildInput.EnabledPlacement = tool == OverseerTool.Build;
            if (tool == OverseerTool.None)
                _overseerHud?.ClearMinimizedMenus();
            else
                _overseerHud?.ExpandMenu(tool);
            Debug.Log($"[GameLoop] Overseer tool → {tool}");
        }

        public void DebugFatigueAll(float value)
        {
            for (int i = 0; i < _agents.Count; i++)
                _agents[i]?.DebugSetFatigue(value);
        }

        // ---- Personality factories (Phase 1.5 values) ----

        /// <summary>Scout: cheap Explore, ignores fights.</summary>
        public static SpecialistData CreateScout()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.ScoutDrone;
            SpecialistPersonality.Apply(s);
            return s;
        }

        /// <summary>Engineer: greedy builder. Ignores cheap flags.</summary>
        public static SpecialistData CreateEngineer()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.EngineerBot;
            SpecialistPersonality.Apply(s);
            return s;
        }

        /// <summary>Defense: cheap combat is fine; no tinkering.</summary>
        public static SpecialistData CreateDefense()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.DefenseMech;
            SpecialistPersonality.Apply(s);
            return s;
        }

        /// <summary>Medic: defends the wounded; will not hunt dens.</summary>
        public static SpecialistData CreateMedic()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.Medic;
            SpecialistPersonality.Apply(s);
            return s;
        }

        public static SpecialistData CreateHarvester()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.HarvesterBot;
            SpecialistPersonality.Apply(s);
            return s;
        }

        public static SpecialistData CreateSurveyor()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.SurveyorBot;
            SpecialistPersonality.Apply(s);
            return s;
        }

        public static SpecialistData CreateTerraformer()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.TerraformerBot;
            SpecialistPersonality.Apply(s);
            return s;
        }

        public static SpecialistData CreateCourier()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.CourierBot;
            SpecialistPersonality.Apply(s);
            return s;
        }

        public static SpecialistData CreateGeologist()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.GeologistBot;
            SpecialistPersonality.Apply(s);
            return s;
        }

        public static SpecialistData CreateSentinel()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.SentinelMech;
            SpecialistPersonality.Apply(s);
            return s;
        }

        private static FlagData CreateFlag(FlagType type, string name, int bounty, float risk, float work, Color color)
        {
            var f = ScriptableObject.CreateInstance<FlagData>();
            f.flagType = type;
            f.displayName = name;
            f.defaultBounty = bounty;
            f.minBounty = 5;
            f.maxBounty = 500;
            f.baseRisk = risk;
            f.workRequired = work;
            f.bannerColor = color;
            SpecialistPersonality.ApplyFlagAffinity(f);
            return f;
        }

        private static BuildingData CreateBuilding(
            string name,
            BuildingCategory cat,
            int metals,
            int power,
            float time,
            int footprintW = 1,
            int footprintH = 1)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.displayName = name;
            b.category = cat;
            b.footprintWidth = Mathf.Max(1, footprintW);
            b.footprintHeight = Mathf.Max(1, footprintH);
            b.buildTimeSeconds = time;
            b.housingSlots = cat == BuildingCategory.Habitat ? 3 : 0;
            b.powerDraw = cat == BuildingCategory.Power
                ? 0
                : cat == BuildingCategory.Defense ? 4 : (power > 0 ? 2 : 0);
            b.powerGen = PowerGenFor(cat, name);
            b.description = cat switch
            {
                BuildingCategory.Inn => "wrecks wait — credits only",
                BuildingCategory.Defense => "auto-fires 18 m",
                BuildingCategory.Mining => "does not grow EU",
                BuildingCategory.ClimateLoom => "unlock from ★ tech — bonus while standing",
                BuildingCategory.AegisSpire => "unlock from ★ tech — bonus while standing",
                BuildingCategory.DeepArchive => "unlock from ★ tech — bonus while standing",
                BuildingCategory.GuildHall => "Guild Hall — assign a class",
                BuildingCategory.Market => "Potions and a regen necklace. Heroes buy with EU.",
                BuildingCategory.Blacksmith => "Guild arms and armor. Heroes buy with EU.",
                BuildingCategory.FobotYard => "Pay EU here to stand wrecks up.",
                BuildingCategory.Watchtower => "Guard post and levy chest. Arm lasers for EU.",
                BuildingCategory.AidStation => "Hurt robots pay EU for a patch. Triage clocks in.",
                _ => b.description
            };
            b.preferredOccupants = DefaultOccupants(cat);
            b.attractionWeight = ColonyStructure.IsWorkshopCategory(cat) ? 1.4f : 1f;
            b.buildCost = Wallet.Credits(metals);
            b.prefab = BuildingVisualCatalog.LoadPrefab(cat);
            return b;
        }

        private static BuildingData CreateGuildHall(RobotGuildId id)
        {
            var g = RobotGuildCatalog.Get(id);
            var b = CreateBuilding(g.HallName, BuildingCategory.GuildHall, 56, 6, 14f, 4, 4);
            b.description = g.CatalogLine;
            b.preferredOccupants = g.Occupants;
            return b;
        }

        private static int PowerGenFor(BuildingCategory cat, string name)
        {
            if (cat != BuildingCategory.Power) return 0;
            if (!string.IsNullOrEmpty(name) && name.IndexOf("Solar", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return 8;
            return 6;
        }

        private static BuildingData[] AppendEconomyBuildings(BuildingData[] current)
        {
            var extra = new[]
            {
                CreateBuilding("Greenhouse Farm", BuildingCategory.Farm, 28, 4, 10f, 4, 4),
                CreateBuilding("Ore Mine", BuildingCategory.Mine, 32, 4, 12f, 4, 4),
                CreateBuilding("Regolith Camp", BuildingCategory.RegolithCamp, 22, 0, 9f, 4, 4),
                CreateBuilding("Scout Workshop", BuildingCategory.ScoutWorkshop, 36, 4, 12f, 4, 4),
                CreateBuilding("Engineer Workshop", BuildingCategory.EngineerWorkshop, 36, 4, 12f, 4, 4),
                CreateBuilding("Fobot Yard", BuildingCategory.Inn, 30, 3, 10f, 4, 4),
                CreateBuilding("Market Stall", BuildingCategory.Market, 34, 2, 10f, 4, 4),
                CreateBuilding("Blacksmith", BuildingCategory.Blacksmith, 48, 4, 12f, 4, 4),
                CreateBuilding("Fobot Yard Bay", BuildingCategory.FobotYard, 52, 4, 12f, 4, 4),
                CreateBuilding("Watchtower", BuildingCategory.Watchtower, 36, 2, 10f, 4, 4),
                CreateBuilding("Aid Station", BuildingCategory.AidStation, 38, 2, 10f, 4, 4),
                CreateBuilding("Defense Workshop", BuildingCategory.DefenseWorkshop, 38, 5, 12f, 4, 4),
                CreateBuilding("Medic Workshop", BuildingCategory.MedicWorkshop, 34, 4, 12f, 4, 4),
                CreateGuildHall(RobotGuildId.Horizon),
                CreateGuildHall(RobotGuildId.Anvil),
                CreateGuildHall(RobotGuildId.Aegis),
                CreateGuildHall(RobotGuildId.Triage),
                CreateBuilding("Harvester Workshop", BuildingCategory.HarvesterWorkshop, 40, 5, 12f, 4, 4),
                CreateBuilding("Surveyor Workshop", BuildingCategory.SurveyorWorkshop, 38, 4, 12f, 4, 4),
                CreateBuilding("Terraformer Workshop", BuildingCategory.TerraformerWorkshop, 42, 5, 12f, 4, 4),
                CreateBuilding("Courier Workshop", BuildingCategory.CourierWorkshop, 36, 4, 12f, 4, 4),
                CreateBuilding("Geologist Workshop", BuildingCategory.GeologistWorkshop, 38, 4, 12f, 4, 4),
                CreateBuilding("Sentinel Workshop", BuildingCategory.SentinelWorkshop, 40, 5, 12f, 4, 4),
                CreateBuilding("Climate Loom", BuildingCategory.ClimateLoom, 92, 12, 18f, 6, 6),
                CreateBuilding("Aegis Spire", BuildingCategory.AegisSpire, 100, 14, 18f, 6, 6),
                CreateBuilding("Deep Archive", BuildingCategory.DeepArchive, 88, 10, 16f, 6, 6)
            };
            if (current == null || current.Length == 0) return extra;
            var merged = new BuildingData[current.Length + extra.Length];
            current.CopyTo(merged, 0);
            extra.CopyTo(merged, current.Length);
            return merged;
        }

        private static void ForceCardinalFootprints(BuildingData[] buildings)
        {
            if (buildings == null) return;
            for (int i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                if (b == null) continue;
                int side = Mathf.Max(1, b.footprintWidth, b.footprintHeight);
                switch (b.category)
                {
                    case BuildingCategory.Commons:
                    case BuildingCategory.LandingPad:
                    case BuildingCategory.ClimateLoom:
                    case BuildingCategory.AegisSpire:
                    case BuildingCategory.DeepArchive:
                        side = 6;
                        break;
                    case BuildingCategory.Habitat:
                    case BuildingCategory.Defense:
                    case BuildingCategory.Inn:
                    case BuildingCategory.Farm:
                    case BuildingCategory.Mine:
                    case BuildingCategory.RegolithCamp:
                    case BuildingCategory.ScoutWorkshop:
                    case BuildingCategory.EngineerWorkshop:
                    case BuildingCategory.DefenseWorkshop:
                    case BuildingCategory.MedicWorkshop:
                    case BuildingCategory.HarvesterWorkshop:
                    case BuildingCategory.SurveyorWorkshop:
                    case BuildingCategory.TerraformerWorkshop:
                    case BuildingCategory.CourierWorkshop:
                    case BuildingCategory.GeologistWorkshop:
                    case BuildingCategory.SentinelWorkshop:
                    case BuildingCategory.GuildHall:
                    case BuildingCategory.Power:
                    case BuildingCategory.Mining:
                    case BuildingCategory.Laboratory:
                    case BuildingCategory.Watchtower:
                    case BuildingCategory.AidStation:
                        side = 4;
                        break;
                    default:
                        side = 4;
                        break;
                }

                b.footprintWidth = side;
                b.footprintHeight = side;
                if (b.category == BuildingCategory.Power && b.powerGen <= 0)
                    b.powerGen = PowerGenFor(b.category, b.displayName);
            }
        }

        private static void StripShopCostsToCredits(BuildingData[] buildings)
        {
            if (buildings == null) return;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null) continue;
                buildings[i].buildCost = Wallet.MetalsOnly(buildings[i].buildCost);
            }
        }

        public void NotifyBuildingPlaced(BuildingData data, GameObject go, Vector3 world)
        {
            if (data == null) return;
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
            CampusDressing.DressPlaced(data, go, _body);
            if (ShouldSnapCampusCamera(data.category))
                SnapCampusCamera();
            DemoVfx.BuildComplete(world);
            ShakeCamera(0.30f, world);
            // No completion sound here: this runs at placement. It plays in ProcessCompletedConstruction.
            if (data.category == BuildingCategory.LandingPad)
                SyncLaunchGate();
            if (data.category == BuildingCategory.FobotYard)
                RefreshWreckVisuals();
            TryClaimOutpost(world, data.category);
            HideDropClaimIfSettled();
            PersistSession();
        }

        /// <summary>Empty-drop orange claim disc is a guide only — hide once Commons exists.</summary>
        private void HideDropClaimIfSettled()
        {
            if (Settlement == null || !Settlement.HasCommons) return;
            var claim = GameObject.Find("DropZone_Claim");
            if (claim != null && claim.activeSelf)
                claim.SetActive(false);
        }

        /// <summary>
        /// Play snap: CampusOrthoSize 10 so the player still has room to place yards.
        /// CaptureStill uses <see cref="SnapStillCampusCamera"/> — still18 dirt was this
        /// 10-ortho on a short-wide Game tab, not a missing pad.
        /// Fauna GlanceAt must not undo this (null zoom once pieces exist).
        /// </summary>
        private void SnapCampusCamera()
        {
            if (_isoCam == null || Placer == null || grid == null) return;
            var pieces = Placer.Pieces;
            if (pieces == null || pieces.Count == 0) return;

            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                sum += FootprintWorldCenter(p.Origin, p.Width, p.Height);
                n++;
            }
            if (n <= 0) return;
            _isoCam.FocusOn(sum / n, ColonyLayout.CampusOrthoSize);
            _isoCam.SnapToTarget();
            _glanceCooldown = 8f;
        }

        /// <summary>
        /// CaptureStill shutter: play campus ortho so the still is a readable
        /// spaced campus. Does not zoom-to-pack the AABB. Does not change play
        /// <see cref="ColonyLayout.CampusOrthoSize"/>.
        /// </summary>
        public void SnapStillCampusCamera(float aspect = 0f)
        {
            if (Placer == null) return;
            if (aspect < 1.05f)
                aspect = StillCampusDensity.GameTabAspect;

            float cell = grid != null && grid.CellSize > 0.01f
                ? grid.CellSize
                : ColonyLayout.DefaultCellSize;
            float ortho = StillCampusDensity.FitStillOrtho(Placer, cell, aspect);
            LastStillOrtho = ortho;

            if (!StillCampusDensity.TryStillFrameAabb(Placer, out Vector2Int min, out Vector2Int max))
                return;

            Vector3 a = FootprintWorldCenter(min, 1, 1);
            Vector3 b = FootprintWorldCenter(
                new Vector2Int(Mathf.Max(min.x, max.x - 1), Mathf.Max(min.y, max.y - 1)), 1, 1);
            Vector3 focus = (a + b) * 0.5f;
            if (grid == null)
            {
                Vector2 center = StillCampusDensity.AabbCenterCells(min, max);
                focus = new Vector3((center.x - 0.5f) * cell, 0f, (center.y - 0.5f) * cell);
            }

            if (_isoCam != null)
            {
                _isoCam.FocusOn(focus, ortho);
                _isoCam.SnapToTarget();
                var cam = _isoCam.GetComponent<Camera>();
                DemoAtmosphere.SyncFog(cam);
            }

            RefreshSuitCrossings();

            _glanceCooldown = 8f;
            Debug.Log($"[GameLoop] SnapStillCampusCamera ortho={ortho:0.##} aspect={aspect:0.##} aabb={min}->{max}");
        }

        private static bool ShouldSnapCampusCamera(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Commons:
                case BuildingCategory.Habitat:
                    return true;
                default:
                    return ColonyStructure.IsWorkshopCategory(cat);
            }
        }

        /// <summary>
        /// Extract flag complete: haul through the nearest drop-off. Matching Mine/Farm/Camp/Power
        /// nearby pays ~full; long haul or no site leaks yield. Same-node double-taps saturate.
        /// </summary>
        public void ApplyExtractYield(Vector3 at, ResourceNode node, SpecialistAgent agent = null)
        {
            if (Economy == null) return;

            int campus = ColonyLayout.NearestCampusIndex(at);
            float saturate = 0f;
            if (node != null)
            {
                EntityId id = node.GetEntityId();
                if (_extractStamp.TryGetValue(id, out float last))
                {
                    float gap = Time.time - last;
                    if (gap < ExtractLogistics.SaturateWindow)
                        saturate = Mathf.Clamp01(1f - gap / ExtractLogistics.SaturateWindow);
                }
                _extractStamp[id] = Time.time;
            }

            ResourceNodeType kind = node != null ? node.NodeType : ResourceNodeType.Regolith;
            bool matching = false;
            bool hasSite = false;
            float dist = ExtractLogistics.MaxHaul;
            string via = null;
            ColonyStructure site = null;
            if (Village != null &&
                Village.TryFindDropOff(at, kind, out site, out dist, out matching))
            {
                hasSite = site != null;
                via = site != null ? site.DisplayName : null;
                if (hasSite && !matching)
                    via = (via ?? "drop-off") + " (mismatch)";
                if (saturate > 0.45f)
                    via = string.IsNullOrEmpty(via) ? "sat" : via + " (sat)";
            }

            bool outpostLocal = hasSite &&
                                Settlement != null &&
                                Settlement.HasOutpost &&
                                site != null &&
                                ColonyLayout.NearestCampusIndex(site.WorldPosition) == 1;

            float eff = ExtractLogistics.HaulEfficiency(dist, matching, hasSite, outpostLocal, saturate);
            eff = Mathf.Clamp(eff + _tech.ExtractHaulBonus, 0.28f, 1.35f);
            if (InSurveyDisc(at))
                eff *= OverseerRules.SurveyExtractMul;
            if (agent != null && agent.Data != null &&
                agent.Data.specialistClass == SpecialistClass.HarvesterBot)
                eff *= OverseerRules.HarvesterExtractMul;
            Economy.GrantExtractYield(campus, node, eff, via);
            if (agent != null && agent.Data != null &&
                agent.Data.specialistClass == SpecialistClass.GeologistBot &&
                site != null &&
                (site.Category == BuildingCategory.Mine || site.Category == BuildingCategory.Mining))
            {
                Resources?.Add(ResourceId.Metals, OverseerRules.GeologistExtractExtraMet);
                LogOverseer($"Geologist bonus +{OverseerRules.GeologistExtractExtraMet} EU at the drop-off.");
            }
        }

        public void NotifySpecialFlag(FlagType type, Vector3 at, SpecialistAgent agent = null)
        {
            switch (type)
            {
                case FlagType.Explore:
                    AddSurveyDisc(at);
                    int charted = ScoutDensInDisc(at);
                    LogOverseer(charted > 0
                        ? $"Survey disc 22 m — charted {charted} den(s). Extract and Research Site pay extra inside."
                        : "Survey disc 22 m / 90 s — Extract and Research Site pay extra inside.");
                    break;
                case FlagType.DefendArea:
                    float extra = 0f;
                    if (agent != null && agent.Data != null &&
                        agent.Data.specialistClass == SpecialistClass.SentinelMech)
                        extra = OverseerRules.SentinelWatchExtra;
                    AddDefendWatch(at, extra);
                    DemoVfx.ClaimRing(at, new Color(0.96f, 0.48f, 0.18f));
                    LogOverseer(extra > 0f
                        ? "Sentinel watch — 70 s of 4 dps cover."
                        : "Defend watch — 50 s of 4 dps cover.");
                    break;
                case FlagType.ResearchSite:
                    if (Research == null)
                        return;
                    float science = 12f;
                    if (InSurveyDisc(at))
                        science += OverseerRules.SurveyScienceExtra;
                    if (agent != null && agent.Data != null &&
                        agent.Data.specialistClass == SpecialistClass.SurveyorBot)
                        science += OverseerRules.SurveyorScienceExtra;
                    if (Research.ActiveTech == TechId.None)
                    {
                        Research.AddScience(science);
                        LogOverseer($"Research Site logged — +{science:F0} science banked. Pick a tech (T).");
                        return;
                    }
                    Research.AddScience(science);
                    var active = TechCatalog.Get(Research.ActiveTech);
                    string techName = active != null ? active.DisplayName : "tech";
                    LogOverseer($"Research Site: +{science:F0} science into {techName}.");
                    break;
                case FlagType.EstablishOutpost:
                    if (Settlement != null && Settlement.HasOutpost)
                    {
                        Resources?.Add(ResourceId.Metals, 80);
                        LogOverseer("Outpost already claimed — the survey sells for +80 EU.");
                        return;
                    }
                    float dx = at.x - ColonyLayout.CampusBOrigin.x;
                    float dz = at.z - ColonyLayout.CampusBOrigin.z;
                    if (dx * dx + dz * dz > 18f * 18f)
                    {
                        LogOverseer("Outpost flag too far from the cyan disc.");
                        return;
                    }
                    Settlement?.ClaimOutpost();
                    LightOutpostBeacon();
                    LogOverseer("Forward outpost claimed from the flag. Extra PWR draw — drop a Mine on site.");
                    break;
                case FlagType.Terraform:
                    Settlement?.AddTerraformPulse();
                    LogOverseer("Terraform pulse — farms tick greener on this crust.");
                    break;
            }
        }

        private void TryClaimOutpost(Vector3 world, BuildingCategory cat)
        {
            if (Settlement == null || Settlement.HasOutpost) return;
            if (!BuildingPlacer.IsForwardOutpost(cat)) return;
            if (ColonyLayout.NearestCampusIndex(world) != 1) return;

            float dx = world.x - ColonyLayout.CampusBOrigin.x;
            float dz = world.z - ColonyLayout.CampusBOrigin.z;
            if (dx * dx + dz * dz > 16f * 16f) return;

            Settlement.ClaimOutpost();
            LightOutpostBeacon();
            LogOverseer("Forward outpost claimed. Extra PWR draw; a matching drop-off on site pays better.");
        }

        private void LightOutpostBeacon()
        {
            if (_outpostBeacon == null) return;
            var rend = _outpostBeacon.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = rend.material;
            var c = new Color(0.35f, 0.88f, 0.95f, 0.62f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
        }

        private void PersistSession()
        {
            DemoSettings.WriteStockpile(Resources);
            DemoSettings.WriteRoster(celestialBody, SpecialistRoster.Encode(CaptureRoster()));
            if (Placer != null)
            {
                var slots = CaptureCampusSlots();
                DemoSettings.WriteCampus(celestialBody, CampusSnapshot.Encode(0, slots));
            }

            var snapshot = CaptureSave("autosave");
            SaveSystem.WriteWorld(snapshot);
            SaveSystem.Write(SaveSystem.AutosaveSlot, snapshot);
        }

        /// <summary>Player-triggered save into one of the numbered slots.</summary>
        public bool SaveToSlot(int slot)
        {
            if (ReplayRules.BlocksManualSavesAndReloads)
            {
                LogOverseer("Ironman — manual saves are closed. Autosave still covers a crash.");
                return false;
            }
            bool ok = SaveSystem.Write(slot, CaptureSave($"slot {slot}"));
            LogOverseer(ok ? $"Colony recorded to slot {slot}." : $"Slot {slot} write failed.");
            PlaytestTelemetry.Record("save", "slot", slot);
            return ok;
        }

        /// <summary>
        /// Continue autosave / numbered-slot capture: campus, stockpile, research, posted flags
        /// (escrow + remaining work), specialist combat state, and living fauna.
        /// Includes world depletion/chart state, roster records and building damage.
        /// </summary>
        public SaveGame CaptureSave(string label)
        {
            var save = new SaveGame
            {
                label = label ?? "",
                playSeconds = _playSeconds,
                simSteps = _sim.TotalSteps,
                body = (int)celestialBody,
                seed = BodySeed.Current,
                highestUnlocked = (int)CampaignProgress.HighestUnlocked,
                rosterBlob = SpecialistRoster.Encode(CaptureRoster()),
                firstHourDemo = DemoSettings.FirstHourDemo,
                tutorialDone = DemoSettings.TutorialDone
            };

            if (Resources != null)
            {
                save.stockpile.regolith = Resources.Get(ResourceId.Regolith);
                save.stockpile.waterIce = Resources.Get(ResourceId.WaterIce);
                save.stockpile.metals = Resources.Get(ResourceId.Metals);
                save.stockpile.power = Resources.Get(ResourceId.Power);
            }

            if (Settlement != null)
            {
                save.settlement.villageHabs = Settlement.VillageHabs;
                save.settlement.hasOutpost = Settlement.HasOutpost;
                save.settlement.everHadHab = Settlement.EverHadHab;
            }

            if (Research != null)
            {
                foreach (var id in Research.Unlocked)
                    save.research.unlocked.Add((int)id);
                save.research.activeTech = (int)Research.ActiveTech;
                save.research.activeProgress = Research.ActiveProgress;
                save.research.bankedScience = Research.BankedScience;
                save.research.progress = Research.CaptureProgress();
            }

                save.replay.mode = (int)ReplayRules.Mode;
                save.replay.challenge = (int)ReplayRules.Challenge;
                save.replay.stance = (int)ReplayRules.Stance;
                save.replay.ironman = ReplayRules.IronmanRun;

            var slots = CaptureCampusSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                Vector3 world = FootprintWorldCenter(new Vector2Int(s.X, s.Y), s.W, s.H);
                var st = Village != null ? Village.FindNear(world, 3f) : null;
                save.buildings.Add(new SaveBuilding
                {
                    category = (int)s.Category,
                    x = s.X,
                    y = s.Y,
                    w = s.W,
                    h = s.H,
                    progressMilli = s.ProgressMilli,
                    villageHab = s.VillageHab,
                    health = 1f,
                    levyPurse = st != null && st.IsResidential ? st.LevyPurse : 0
                });
            }

            OverlayBuildingBoard(save);
            save.villageGrowth = Village?.CaptureGrowth();
            save.collectors = Village?.Collectors.Capture();

            if (Flags != null)
            {
                var open = Flags.Flags;
                for (int i = 0; i < open.Count; i++)
                {
                    var f = open[i];
                    if (f?.Data == null) continue;
                    save.flags.Add(new SaveFlag
                    {
                        flagType = (int)f.Data.flagType,
                        px = f.WorldPosition.x,
                        py = f.WorldPosition.y,
                        pz = f.WorldPosition.z,
                        bounty = f.CurrentBounty,
                        escrowMetals = f.EscrowMetals,
                        postedWork = f.PostedWork,
                        workDone = Mathf.Max(0f, f.PostedWork - Flags.GetWorkRemaining(f)),
                        claimCount = Mathf.Max(0, f.ClaimCount),
                        orders = f.Orders
                    });
                }
            }

            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || a.Data == null) continue;
                Vector3 p = a.transform.position;
                save.agents.Add(new SaveAgent
                {
                    specialistClass = (int)a.Data.specialistClass,
                    hasVeteranRecord = true,
                    veteran = a.ToRecord(corpse: false),
                    px = p.x,
                    py = p.y,
                    pz = p.z,
                    health = a.HealthNormalized,
                    fatigue = a.Fatigue,
                    credits = Mathf.RoundToInt(a.Credits),
                    downed = a.IsIncapacitated,
                    downedTimer = a.RecoverSecondsLeft,
                    claimedFlagIndex = IndexOfSavedFlag(a.ActiveFlag, Flags),
                    levyCarry = a.LevyCarry,
                    level = a.Level,
                    xp = a.Xp,
                    suit = (int)a.EquippedSuit,
                    reviveCount = a.ReviveCount,
                    downCount = a.ReviveCount,
                    partyId = a.Party != null ? a.Party.Id : -1
                });
            }

            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s == null) continue;
                Vector3 p = s.transform.position;
                save.fauna.Add(new SaveFauna
                {
                    kind = (int)s.Kind,
                    px = p.x,
                    py = p.y,
                    pz = p.z,
                    health = s.Health01,
                    lairIndex = SavedLairIndex(s)
                });
            }

            if (_world != null)
            {
                var nodes = _world.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n == null) continue;
                    Vector3 p = n.WorldPosition;
                    save.nodes.Add(new SaveNode
                    {
                        nodeType = (int)n.NodeType,
                        px = p.x,
                        py = p.y,
                        pz = p.z,
                        remaining = n.Remaining
                    });
                }

                var lairs = _world.Lairs;
                for (int i = 0; i < lairs.Count; i++)
                {
                    var l = lairs[i];
                    if (l == null) continue;
                    Vector3 p = l.WorldPosition;
                    save.lairs.Add(new SaveLair
                    {
                        px = p.x,
                        py = p.y,
                        pz = p.z,
                        cleared = l.IsCleared,
                        scouted = l.IsScouted,
                        expansionSpawned = l.ExpansionSpawned
                    });
                }
            }

            if (_mission != null)
            {
                save.mission.state = (int)_mission.State;
                save.mission.elapsed = _mission.MissionElapsed;
                save.mission.sustainHold = _mission.SustainElapsed;
                save.mission.densCleared = _mission.DensCleared;
                save.mission.sustainMet = _mission.SustainComplete;
                save.mission.launchReady = _mission.LaunchReady;
            }

            var roster = CaptureRoster();
            for (int i = 0; i < roster.Count; i++)
            {
                var r = roster[i];
                save.roster.Add(new SaveRosterEntry
                {
                    specialistClass = (int)r.Class,
                    level = r.Level,
                    xp = r.Xp,
                    credits = r.Credits,
                    reviveCount = r.ReviveCount,
                    suit = (int)r.Suit,
                    corpse = r.Corpse
                });
            }

            for (int p = 0; p < _parties.Count; p++)
            {
                var party = _parties[p];
                if (party == null || party.Count < 2) continue;
                var row = new SaveParty { id = party.Id };
                for (int m = 0; m < party.Members.Count; m++)
                {
                    int idx = IndexOfSavedAgent(party.Members[m], save.agents, _agents);
                    if (idx < 0) continue;
                    row.memberIndices.Add(idx);
                    if (party.IsLeader(party.Members[m]))
                        row.leaderIndex = idx;
                }
                if (row.memberIndices.Count >= 2)
                    save.parties.Add(row);
            }

            return save;
        }

        /// <summary>
        /// Restore campus, stockpile, research, posted flags (including remaining work),
        /// specialist combat state and world poses, living fauna poses, den scouted/cleared,
        /// node remaining, mission hold, and formed parties. Soft claims rebind by flag index;
        /// specialists still re-evaluate through SpecialistBrain after load.
        /// </summary>
        public bool ApplySave(SaveGame save)
        {
            // Never pour one planet's entities onto another planet (or a regenerated seed).
            if (save == null || !save.MatchesWorld(celestialBody, BodySeed.Current)) return false;

            if (Resources != null)
            {
                Resources.Set(ResourceId.Regolith, save.stockpile.regolith);
                Resources.Set(ResourceId.WaterIce, save.stockpile.waterIce);
                Resources.Set(ResourceId.Metals, save.stockpile.metals);
                Resources.Set(ResourceId.Power, save.stockpile.power);
            }

            Research?.RestoreFrom(
                save.research.unlocked,
                (TechId)save.research.activeTech,
                save.research.activeProgress,
                save.research.bankedScience, save.research.progress);

            if (Settlement != null)
            {
                if (save.settlement.hasOutpost)
                    Settlement.ClaimOutpost();
            }

            ReplayRules.Restore(save.replay);
            ReplayRules.ApplyIronmanFromSave(save.replay);
            Achievements.IronmanActive = ReplayRules.IronmanRun;
            if (save.rosterBlob != null)
                LoadRosterMemory(save.rosterBlob);
            else
                LoadRosterFromSave(save.roster);
            var savedCampus = new List<CampusSlot>();
            foreach (var building in save.buildings)
                savedCampus.Add(new CampusSlot
                {
                    Category = (BuildingCategory)building.category, X = building.x, Y = building.y,
                    W = building.w, H = building.h, ProgressMilli = building.progressMilli,
                    VillageHab = building.villageHab
                });
            RestoreCampus(savedCampus, save.settlement.population);
            RetryUnpaidCorpses();

            var restoredFlags = RestoreFlags(save.flags);
            var restoredAgents = RestoreAgents(save.agents, restoredFlags, save.rosterBlob != null);
            RestoreParties(save.parties, restoredAgents);
            var restoredLairs = RestoreWorldBoard(save);
            RestoreFauna(save.fauna, restoredLairs);
            RestoreBuildingBoard(save);
            RestoreLevyPurses(save.buildings);
            Village?.RestoreGrowth(save.villageGrowth);
            Village?.Collectors.Restore(save.collectors);
            RefreshWreckVisuals();

            if (_mission != null)
            {
                _mission.RestoreFrom(save.mission);
                if (save.mission != null && save.mission.launchReady)
                    _launchCraftStaged = true;
            }

            _playSeconds = save.playSeconds;
            _sim.Restore(save.simSteps);
            RefreshTechEffects();
            SyncLaunchGate();

            int modules = save.buildings != null ? save.buildings.Count : 0;
            int bounties = save.flags != null ? save.flags.Count : 0;
            int robots = save.agents != null ? save.agents.Count : 0;
            int fauna = save.fauna != null ? save.fauna.Count : 0;
            int dens = save.lairs != null ? save.lairs.Count : 0;
            int parties = save.parties != null ? save.parties.Count : 0;
            LogOverseer(
                $"Colony restored — {modules} modules, {bounties} bounties, {robots} robots, " +
                $"{fauna} fauna, {dens} dens, {parties} parties.");
            PlaytestTelemetry.Record("load", "modules", modules);
            return true;
        }

        private static int IndexOfSavedAgent(
            SpecialistAgent agent, List<SaveAgent> saved, List<SpecialistAgent> live)
        {
            if (agent == null || saved == null || live == null) return -1;
            int ord = 0;
            for (int i = 0; i < live.Count; i++)
            {
                var a = live[i];
                if (a == null || a.Data == null) continue;
                if (ReferenceEquals(a, agent))
                    return ord < saved.Count ? ord : -1;
                ord++;
            }
            return -1;
        }

        private static int IndexOfSavedFlag(FlagHandle flag, FlagManager flags)
        {
            if (flag == null || flags == null) return -1;
            var open = flags.Flags;
            int ord = 0;
            for (int i = 0; i < open.Count; i++)
            {
                if (open[i]?.Data == null) continue;
                if (ReferenceEquals(open[i], flag))
                    return ord;
                ord++;
            }
            return -1;
        }

        private List<FlagHandle> RestoreFlags(List<SaveFlag> saved)
        {
            ClearPostedFlagMarkers();
            Flags?.ClearAll();
            var restored = new List<FlagHandle>(saved != null ? saved.Count : 0);
            if (_flagInput == null || saved == null || Flags == null)
                return restored;

            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                var data = _flagInput.FlagFor((FlagType)s.flagType);
                if (data == null)
                {
                    restored.Add(null);
                    continue;
                }

                float posted = s.postedWork > 0.01f ? s.postedWork : data.workRequired;
                float remaining = posted - Mathf.Max(0f, s.workDone);
                if (remaining <= 0.01f)
                {
                    restored.Add(null);
                    continue;
                }

                var handle = _flagInput.RestoreFlag(
                    data, new Vector3(s.px, s.py, s.pz), s.bounty, s.escrowMetals, s.orders);
                if (handle == null)
                {
                    restored.Add(null);
                    continue;
                }

                Flags.RestoreProgress(handle, posted, remaining);
                restored.Add(handle);
            }

            return restored;
        }

        private void ClearPostedFlagMarkers()
        {
            var markers = Object.FindObjectsByType<FlagMarker>(FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null)
                    Destroy(markers[i].gameObject);
            }
        }

        private List<SpecialistAgent> RestoreAgents(List<SaveAgent> saved, List<FlagHandle> restoredFlags, bool hasEncodedRoster)
        {
            var restored = new List<SpecialistAgent>(saved != null ? saved.Count : 0);
            if (saved == null) return restored;

            var used = new HashSet<SpecialistAgent>();
            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                var cls = (SpecialistClass)s.specialistClass;
                SpecialistAgent agent = null;
                for (int a = 0; a < _agents.Count; a++)
                {
                    var cand = _agents[a];
                    if (cand == null || cand.Data == null) continue;
                    if (used.Contains(cand)) continue;
                    if (cand.Data.specialistClass != cls) continue;
                    agent = cand;
                    break;
                }

                Vector3 pose = new Vector3(s.px, s.py, s.pz);
                if (agent == null)
                {
                    var data = DataForClass(cls);
                    if (data == null)
                    {
                        restored.Add(null);
                        continue;
                    }

                    agent = SpawnOne(data, pose, TintForClass(cls));
                    if (agent == null)
                    {
                        restored.Add(null);
                        continue;
                    }

                    _agents.Add(agent);
                    _everHadRobot = true;
                    if (Agent == null) Agent = agent;
                    FindWorkshopFor(cls)?.MarkRobotFabricated();
                }

                used.Add(agent);
                restored.Add(agent);
                agent.RestoreWorldPose(pose);
                if (s.hasVeteranRecord)
                    agent.ApplyRecord(s.veteran);
                else if (!hasEncodedRoster)
                {
                    int revive = s.reviveCount > 0 ? s.reviveCount : s.downCount;
                    agent.ApplyRecord(new SpecialistRecord
                    {
                        Class = cls,
                        Level = s.level > 0 ? s.level : 1,
                        Xp = Mathf.Max(0, s.xp),
                        Credits = Mathf.Max(0, s.credits),
                        ReviveCount = Mathf.Max(0, revive),
                        Suit = (ShopItemId)s.suit,
                        Corpse = false
                    });
                }
                agent.RestoreCombatState(s.health, s.fatigue, s.credits, s.downed, s.downedTimer);
                agent.RestoreLevyCarry(s.levyCarry);
                agent.BindNavMesh(_campusNav);
                RemoveCorpse(cls);
                _pendingVeterans.Remove(cls);
                if (s.downed)
                    continue;
                int idx = s.claimedFlagIndex;
                if (idx >= 0 && restoredFlags != null && idx < restoredFlags.Count)
                    agent.RestoreActiveFlag(restoredFlags[idx]);
            }

            return restored;
        }

        private void RestoreParties(List<SaveParty> saved, List<SpecialistAgent> restoredAgents)
        {
            for (int i = _parties.Count - 1; i >= 0; i--)
                _parties[i]?.Disband();
            _parties.Clear();
            if (saved == null || restoredAgents == null) return;

            int maxId = 0;
            for (int i = 0; i < saved.Count; i++)
            {
                var row = saved[i];
                if (row == null || row.memberIndices == null) continue;
                // Earlier stabilization saves identified members by class. Convert once,
                // then keep exact agent indices in all new snapshots.
                if (row.memberIndices.Count == 0 && row.memberClasses != null)
                {
                    foreach (int cls in row.memberClasses)
                    {
                        for (int a = 0; a < restoredAgents.Count; a++)
                        {
                            var candidate = restoredAgents[a];
                            if (candidate?.Data == null || !candidate.IsAlive || candidate.Party != null ||
                                (int)candidate.Data.specialistClass != cls || row.memberIndices.Contains(a)) continue;
                            row.memberIndices.Add(a);
                            if (cls == row.leaderClass && row.leaderIndex < 0) row.leaderIndex = a;
                            break;
                        }
                    }
                }
                var members = new List<SpecialistAgent>(HeroParty.MaxSize);
                SpecialistAgent leader = null;
                for (int m = 0; m < row.memberIndices.Count && members.Count < HeroParty.MaxSize; m++)
                {
                    int idx = row.memberIndices[m];
                    if (idx < 0 || idx >= restoredAgents.Count) continue;
                    var agent = restoredAgents[idx];
                    if (agent == null || !agent.IsAlive || agent.Party != null) continue;
                    members.Add(agent);
                    if (idx == row.leaderIndex)
                        leader = agent;
                }
                if (members.Count < 2) continue;
                if (leader == null || !members.Contains(leader))
                    leader = members[0];

                int id = row.id > 0 ? row.id : _nextPartyId;
                var party = new HeroParty(id, leader);
                for (int m = 0; m < members.Count; m++)
                {
                    party.Members.Add(members[m]);
                    members[m].SetParty(party);
                }
                _parties.Add(party);
                if (id > maxId) maxId = id;
            }

            if (maxId >= _nextPartyId)
                _nextPartyId = maxId + 1;
        }

        private int SavedLairIndex(DustStalkerAgent fauna)
        {
            if (_world == null) return -1;
            int savedIndex = 0;
            foreach (var lair in _world.Lairs)
            {
                if (lair == null) continue;
                foreach (var resident in lair.Spawned)
                    if (resident == fauna) return savedIndex;
                savedIndex++;
            }
            return -1;
        }

        private void RestoreFauna(List<SaveFauna> saved, Dictionary<int, StalkerLair> lairs)
        {
            for (int i = _stalkers.Count - 1; i >= 0; i--)
            {
                if (_stalkers[i] != null)
                    _stalkers[i].DespawnQuiet();
            }
            _stalkers.Clear();

            if (saved == null) return;
            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                if (s.health <= 0.001f) continue;
                var agent = SpawnFaunaAt((FaunaKind)s.kind, new Vector3(s.px, s.py, s.pz));
                agent?.RestoreHealth01(s.health);
                if (lairs != null && lairs.TryGetValue(s.lairIndex, out var owner))
                    owner.TrackRestoredFauna(agent);
            }
        }

        private void OverlayBuildingBoard(SaveGame save)
        {
            if (save?.buildings == null || Village == null || grid == null) return;
            for (int i = 0; i < save.buildings.Count; i++)
            {
                var b = save.buildings[i];
                Vector3 world = FootprintWorldCenter(new Vector2Int(b.x, b.y), b.w, b.h);
                var st = Village.FindNear(world, 3f);
                if (st == null || (int)st.Category != b.category) continue;
                b.health = st.Health01;
                b.levyPurse = st.LevyPurse;
                b.laserArmed = st.LaserArmed;
            }
        }

        private Dictionary<int, StalkerLair> RestoreWorldBoard(SaveGame save)
        {
            if (save == null || _world == null) return null;
            RestoreNodes(save.nodes);
            return RestoreLairs(save.lairs);
        }

        private void RestoreNodes(List<SaveNode> saved)
        {
            if (saved == null || saved.Count == 0 || _world == null) return;
            var live = _world.Nodes;
            var pts = new Vector3[live.Count];
            for (int i = 0; i < live.Count; i++)
                pts[i] = live[i] != null ? live[i].WorldPosition : new Vector3(9999f, 0f, 9999f);

            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                int idx = WorldSaveMatch.Nearest(new Vector3(s.px, s.py, s.pz), pts, WorldSaveMatch.MaxDist);
                if (idx < 0) continue;
                var node = live[idx];
                if (node == null || (int)node.NodeType != s.nodeType) continue;
                node.RestoreRemaining(s.remaining);
                pts[idx] = new Vector3(9999f, 0f, 9999f);
            }
        }

        private Dictionary<int, StalkerLair> RestoreLairs(List<SaveLair> saved)
        {
            var restored = new Dictionary<int, StalkerLair>();
            if (saved == null || saved.Count == 0 || _world == null) return restored;
            var live = _world.Lairs;
            var pts = new Vector3[live.Count];
            for (int i = 0; i < live.Count; i++)
                pts[i] = live[i] != null ? live[i].WorldPosition : new Vector3(9999f, 0f, 9999f);

            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                int idx = WorldSaveMatch.Nearest(new Vector3(s.px, s.py, s.pz), pts, WorldSaveMatch.MaxDist);
                if (idx < 0) continue;
                live[idx]?.RestoreChart(s.cleared, s.scouted, s.expansionSpawned);
                if (live[idx] != null) restored[i] = live[idx];
                pts[idx] = new Vector3(9999f, 0f, 9999f);
            }
            return restored;
        }

        private void RestoreBuildingBoard(SaveGame save)
        {
            if (save?.buildings == null || Village == null || grid == null) return;
            for (int i = 0; i < save.buildings.Count; i++)
            {
                var b = save.buildings[i];
                Vector3 world = FootprintWorldCenter(new Vector2Int(b.x, b.y), b.w, b.h);
                var st = Village.FindNear(world, 3f);
                if (st == null || (int)st.Category != b.category) continue;
                st.RestoreHealth01(b.health);
                st.RestoreLevy(b.levyPurse);
                if (b.laserArmed)
                    st.ArmLasers();
            }
        }

        private void RestoreLevyPurses(List<SaveBuilding> saved)
        {
            if (saved == null || Village == null || grid == null) return;
            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                if (s.levyPurse <= 0) continue;
                Vector3 world = FootprintWorldCenter(new Vector2Int(s.x, s.y), Mathf.Max(1, s.w), Mathf.Max(1, s.h));
                var st = Village.FindNear(world, 3f);
                if (st != null && st.IsResidential)
                    st.SetLevyPurse(s.levyPurse);
            }
        }

        private List<CampusSlot> CaptureCampusSlots()
        {
            var slots = new List<CampusSlot>(16);
            if (Placer == null) return slots;
            var pieces = Placer.Pieces;
            var orders = Placer.Orders;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                int milli = 1000;
                for (int o = 0; o < orders.Count; o++)
                {
                    var ord = orders[o];
                    if (ord?.Data == null) continue;
                    if (ord.GridCell != p.Origin || ord.Data.category != p.Category) continue;
                    float req = Mathf.Max(0.1f, ord.RequiredSeconds);
                    milli = Mathf.Clamp(Mathf.RoundToInt(1000f * ord.ProgressSeconds / req), 0, 999);
                    break;
                }

                bool village = false;
                if (p.Category == BuildingCategory.Habitat && Village != null)
                {
                    Vector3 world = FootprintWorldCenter(p.Origin, p.Width, p.Height);
                    var st = Village.FindNear(world, 3f);
                    village = st != null && st.IsVillageHab;
                }

                slots.Add(new CampusSlot
                {
                    Category = p.Category,
                    X = p.Origin.x,
                    Y = p.Origin.y,
                    W = p.Width,
                    H = p.Height,
                    ProgressMilli = milli,
                    VillageHab = village
                });
            }

            return slots;
        }

        private void RestoreCampus()
        {
            if (Placer == null || grid == null) return;
            if (Placer.Pieces.Count > 0) return;

            string raw = DemoSettings.LoadCampus(celestialBody);
            var slots = new List<CampusSlot>(16);
            if (!CampusSnapshot.TryDecode(raw, out int pop, slots) || slots.Count == 0)
                return;

            RestoreCampus(slots, pop);
        }

        private void RestoreCampus(List<CampusSlot> slots, int pop)
        {
            if (Placer == null || grid == null || Placer.Pieces.Count > 0) return;
            slots.Sort((a, b) => CampusSnapshot.Rank(a.Category).CompareTo(CampusSnapshot.Rank(b.Category)));

            int restored = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (RestoreCampusSlot(slots[i]))
                    restored++;
            }

            if (restored <= 0) return;

            NotifyCampusExpanded();
            SyncLaunchGate();
            SnapCampusCamera();
            HideDropClaimIfSettled();
            LogOverseer($"Campus restored — {restored} modules on {(_body != null ? _body.DisplayName : celestialBody.ToString())}.");
            Debug.Log($"[GameLoop] Restored {restored} campus pieces.");
        }

        private bool RestoreCampusSlot(CampusSlot slot)
        {
            // Saves from before airlocks were retired still list their junctions; drop them.
            if (BuildingPlacer.IsRetired(slot.Category)) return false;
            var data = DataForCategory(slot.Category);
            if (data == null)
            {
                Debug.LogWarning($"[GameLoop] Continue skipped {slot.Category} — no catalog data.");
                return false;
            }

            var cell = new Vector2Int(slot.X, slot.Y);
            Vector3 world = FootprintWorldCenter(cell, slot.W, slot.H);
            float progress01 = slot.ProgressMilli / 1000f;
            if (!Placer.TryRestore(data, cell, world, progress01, out ConstructionOrder order))
                return false;

            float cellSize = grid.CellSize;
            Transform root = buildingRoot != null ? buildingRoot : transform;
            GameObject go = ModularBuildingFactory.Spawn(
                data.category,
                world,
                root,
                data.footprintWidth,
                data.footprintHeight,
                cellSize);
            go.name = $"Bld_{data.displayName}_save";
            CampusNavMesh.AddObstacle(go);

            if (slot.VillageHab && data.category == BuildingCategory.Habitat)
                Village?.RegisterRestoredVillageHab(go);
            else
                Village?.RegisterPlacedBuilding(data, data.category, go, world);

            CampusDressing.DressPlaced(data, go, _body);

            if (order != null && !order.IsComplete)
            {
                var site = new GameObject($"Site_save_{order.Id}");
                site.transform.SetParent(root, true);
                site.transform.position = world + Vector3.up * 0.05f;
                site.AddComponent<ConstructionSiteVisual>().Bind(order);
            }
            else if (Village != null && ColonyStructure.IsWorkshopCategory(data.category))
            {
                var st = Village.FindNear(world, 4f);
                if (st != null && st.Category == data.category)
                    TryFabricateRobot(st, announce: false);
            }

            TryClaimOutpost(world, data.category);
            return true;
        }

        private BuildingData DataForCategory(BuildingCategory cat)
        {
            if (starterBuildings == null) return null;
            for (int i = 0; i < starterBuildings.Length; i++)
            {
                if (starterBuildings[i] != null && starterBuildings[i].category == cat)
                    return starterBuildings[i];
            }
            return null;
        }

        private Vector3 FootprintWorldCenter(Vector2Int origin, int w, int h)
        {
            if (grid == null) return Vector3.zero;
            Vector3 corner = grid.CellToWorld(origin);
            float cs = grid.CellSize;
            return corner + new Vector3((Mathf.Max(1, w) - 1) * 0.5f * cs, 0f, (Mathf.Max(1, h) - 1) * 0.5f * cs);
        }

        private static SpecialistClass[] DefaultOccupants(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.ScoutWorkshop:
                case BuildingCategory.Laboratory:
                case BuildingCategory.LandingPad:
                    return new[] { SpecialistClass.ScoutDrone };
                case BuildingCategory.MedicWorkshop:
                case BuildingCategory.AidStation:
                    return new[] { SpecialistClass.Medic };
                case BuildingCategory.Habitat:
                    return null;
                case BuildingCategory.DefenseWorkshop:
                case BuildingCategory.Defense:
                case BuildingCategory.Watchtower:
                    return new[] { SpecialistClass.DefenseMech };
                case BuildingCategory.EngineerWorkshop:
                case BuildingCategory.Farm:
                case BuildingCategory.Mine:
                case BuildingCategory.RegolithCamp:
                case BuildingCategory.Mining:
                    return new[] { SpecialistClass.EngineerBot };
                case BuildingCategory.HarvesterWorkshop:
                    return new[] { SpecialistClass.HarvesterBot };
                case BuildingCategory.SurveyorWorkshop:
                    return new[] { SpecialistClass.SurveyorBot };
                case BuildingCategory.TerraformerWorkshop:
                    return new[] { SpecialistClass.TerraformerBot };
                case BuildingCategory.CourierWorkshop:
                    return new[] { SpecialistClass.CourierBot };
                case BuildingCategory.GeologistWorkshop:
                    return new[] { SpecialistClass.GeologistBot };
                case BuildingCategory.SentinelWorkshop:
                    return new[] { SpecialistClass.SentinelMech };
                default:
                    return null;
            }
        }

        public ColonyStructure SelectedStructure { get; private set; }

        public void ClearStructureSelection()
        {
            if (SelectedStructure != null)
            {
                SelectedStructure.SetSelected(false);
                SelectedStructure = null;
            }
        }

        public void SelectStructure(ColonyStructure st)
        {
            ClearSelection();
            if (SelectedStructure != null && SelectedStructure != st)
                SelectedStructure.SetSelected(false);
            SelectedStructure = st;
            st?.SetSelected(true);
        }

        public void NotifyStructureDestroyed(ColonyStructure st)
        {
            if (st == null) return;
            if (SelectedStructure == st)
                SelectedStructure = null;

            Vector3 world = st.WorldPosition;
            BuildingCategory cat = st.Category;
            if (Placer != null && grid != null)
                Placer.TryReleaseContaining(grid.WorldToCell(world), cat);

            NotifyCampusExpanded();
            PersistSession();
            LogOverseer($"{st.DisplayName} destroyed — rebuild before the next raid.");
        }

        public void SetSelectedWorkplaceClass(SpecialistClass cls)
        {
            SelectedStructure?.SetPreferredClass(cls);
        }

        public void PostAttractFlagOnSelected()
        {
            var st = SelectedStructure;
            if (st == null || _flagInput == null) return;
            SpecialistClass cls = st.HasPreferredClass ? st.PreferredClass : SpecialistClass.ScoutDrone;
            FlagType type = ColonyStructure.AttractFlagFor(cls);
            FlagData data = type switch
            {
                FlagType.Build => buildFlagData,
                FlagType.DefendArea => defendFlagData,
                FlagType.Extract => extractFlagData,
                FlagType.ClearThreat => clearThreatFlagData,
                FlagType.ResearchSite => researchSiteFlagData,
                _ => exploreFlagData
            };
            if (st.Category == BuildingCategory.Farm || st.Category == BuildingCategory.Mine ||
                st.Category == BuildingCategory.RegolithCamp)
                data = extractFlagData;
            if (data == null) return;
            float bounty = _flagInput.Bounty >= MajestyEconomy.FlagMinBounty ? _flagInput.Bounty : data.defaultBounty;
            _flagInput.PostFlagAt(data, st.WorldPosition, bounty);
            DemoVfx.ClaimRing(st.WorldPosition, new Color(1f, 0.85f, 0.2f));
        }

        /// <summary>Form a party from the current selection, else from heroes at the rest beacon.</summary>
        public void FormParty()
        {
            if (TryFormPartyFrom(_selected)) return;
            FormPartyAtInn();
        }

        private bool TryFormPartyFrom(IReadOnlyList<SpecialistAgent> pool)
        {
            var members = new List<SpecialistAgent>(4);
            if (pool == null) return false;
            for (int i = 0; i < pool.Count; i++)
            {
                var a = pool[i];
                if (a == null || !a.IsAlive || a.Party != null) continue;
                members.Add(a);
                if (members.Count >= HeroParty.MaxSize) break;
            }
            if (members.Count < 2) return false;

            SpecialistAgent leader = members[0];
            for (int i = 1; i < members.Count; i++)
            {
                if ((members[i].Data?.courage ?? 0f) > (leader.Data?.courage ?? 0f))
                    leader = members[i];
            }

            var party = new HeroParty(_nextPartyId++, leader);
            for (int i = 0; i < members.Count; i++)
            {
                party.Members.Add(members[i]);
                members[i].SetParty(party);
            }
            _parties.Add(party);
            DemoAudio.PlayPartyForm(leader.transform.position);
            DemoVfx.ClaimRing(leader.transform.position, new Color(0.96f, 0.42f, 0.08f));
            LogOverseer($"Party of {party.Count} — {ColonyStructure.ClassLabel(leader.Data != null ? leader.Data.specialistClass : SpecialistClass.ScoutDrone)} leads. Followers rest and hunt together.");
            Debug.Log($"[Party] Formed #{party.Id} from selection leader={leader.Data?.displayName} size={party.Count}");
            return true;
        }

        /// <summary>Majesty inn party: specialists at the waystation form a group (max 4).</summary>
        public void FormPartyAtInn()
        {
            var atInn = new List<SpecialistAgent>(4);
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || !a.IsAlive) continue;
                if (!KingdomLife.AtInnParty(a.transform.position)) continue;
                if (a.Party != null) continue;
                atInn.Add(a);
                if (atInn.Count >= HeroParty.MaxSize) break;
            }

            if (atInn.Count < 2)
            {
                Debug.Log("[Party] Need 2+ unpartied specialists at the waystation inn.");
                return;
            }

            SpecialistAgent leader = atInn[0];
            for (int i = 1; i < atInn.Count; i++)
            {
                if ((atInn[i].Data?.courage ?? 0f) > (leader.Data?.courage ?? 0f))
                    leader = atInn[i];
            }

            var party = new HeroParty(_nextPartyId++, leader);
            for (int i = 0; i < atInn.Count; i++)
            {
                party.Members.Add(atInn[i]);
                atInn[i].SetParty(party);
            }
            _parties.Add(party);
            DemoAudio.PlayPartyForm(ColonyLayout.InnOutpost);
            DemoVfx.ClaimRing(ColonyLayout.InnOutpost, new Color(0.96f, 0.42f, 0.08f));
            LogOverseer($"Party of {party.Count} formed at the rest beacon.");
            Debug.Log($"[Party] Formed #{party.Id} leader={leader.Data?.displayName} size={party.Count}");
        }

        public void DisbandSelectedParty()
        {
            HeroParty party = null;
            if (_selected.Count > 0 && _selected[0] != null)
                party = _selected[0].Party;
            if (party == null && _parties.Count > 0)
                party = _parties[_parties.Count - 1];
            if (party == null) return;
            _parties.Remove(party);
            party.Disband();
            Debug.Log("[Party] Disbanded.");
        }

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Coherent campus (see ColonyLayout): dome core, habitat spine, power yard, pad/ship.
        /// Uses Majesty-readable visual scale so modules and specialists share one silhouette language.
        /// </summary>
        private void SpawnShowcaseColony()
        {
            if (!spawnShowcaseColony || buildingRoot == null)
                return;

            SpawnShowcaseSet(ColonyLayout.Showcase, ColonyLayout.CampusOrigin, "A");
            if (spawnSecondBody)
                SpawnShowcaseSet(ColonyLayout.ShowcaseB, ColonyLayout.CampusBOrigin, "B");
        }

        private void SpawnShowcaseSet(ColonyLayout.ShowcasePiece[] pieces, Vector3 campusOrigin, string tag)
        {
            if (pieces == null) return;
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                Vector3 world = piece.WorldPositionAt(campusOrigin);
                GameObject go = SpawnMesh(
                    piece.ResourcesPath,
                    world,
                    $"Showcase{tag}_{i}_{System.IO.Path.GetFileName(piece.ResourcesPath)}",
                    piece.ResolveScale(),
                    0f);

                if (piece.ReservesCells && Placer != null && grid != null)
                {
                    Vector2Int origin = ReserveShowcaseFootprint(world, piece.FootprintW, piece.FootprintH);
                    BuildingCategory pieceCat = BuildingCategory.Defense;
                    if (!string.IsNullOrEmpty(piece.ResourcesPath))
                    {
                        if (piece.ResourcesPath.Contains("CommandDome"))
                            pieceCat = BuildingCategory.Commons;
                        else if (piece.ResourcesPath.Contains("HAB"))
                            pieceCat = BuildingCategory.Habitat;
                        else if (piece.ResourcesPath.Contains("LAB"))
                            pieceCat = BuildingCategory.Laboratory;
                        else if (piece.ResourcesPath.Contains("CMD"))
                            pieceCat = BuildingCategory.Defense;
                        else if (piece.ResourcesPath.Contains("OPS"))
                            pieceCat = BuildingCategory.Mining;
                        else if (piece.ResourcesPath.Contains("PWR") || piece.ResourcesPath.Contains("Solar"))
                            pieceCat = BuildingCategory.Power;
                        else if (piece.ResourcesPath.Contains("LandingPad"))
                            pieceCat = BuildingCategory.LandingPad;
                    }
                    Placer.RegisterPiece(origin, piece.FootprintW, piece.FootprintH, pieceCat);
                }

                if (go != null)
                    RegisterShowcaseStructure(go, piece.ResourcesPath);
            }
        }

        private void RegisterShowcaseStructure(GameObject go, string resourcesPath)
        {
            if (go == null || Village == null) return;
            if (string.IsNullOrEmpty(resourcesPath)) return;
            if (resourcesPath.Contains("Starship"))
                return;

            BuildingCategory cat;
            if (resourcesPath.Contains("HAB")) cat = BuildingCategory.Habitat;
            else if (resourcesPath.Contains("LAB")) cat = BuildingCategory.Laboratory;
            else if (resourcesPath.Contains("CMD") || resourcesPath.Contains("CommandDome"))
                cat = resourcesPath.Contains("CommandDome")
                    ? BuildingCategory.Commons
                    : BuildingCategory.Defense;
            else if (resourcesPath.Contains("OPS")) cat = BuildingCategory.Mining;
            else if (resourcesPath.Contains("PWR") || resourcesPath.Contains("Solar"))
                cat = BuildingCategory.Power;
            else if (resourcesPath.Contains("LandingPad")) cat = BuildingCategory.LandingPad;
            else return;

            StructureRole role = StructureRole.Core;
            var st = go.GetComponent<ColonyStructure>();
            if (st == null)
                st = go.AddComponent<ColonyStructure>();
            st.Configure(role, Village, 64f, cat);
            Village.RegisterShowcase(st);
        }

        private Vector2Int ReserveShowcaseFootprint(Vector3 world, int footprintW, int footprintH)
        {
            float cell = grid.CellSize;
            float halfW = (footprintW * cell) * 0.5f;
            float halfH = (footprintH * cell) * 0.5f;
            Vector3 corner = world - new Vector3(halfW, 0f, halfH) + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            Vector2Int origin = grid.WorldToCell(corner);
            Placer.MarkCampusRect(origin, footprintW, footprintH);
            return origin;
        }

        private GameObject SpawnMesh(string resourcesPath, Vector3 position, string name, float scale, float yawDegrees = 0f)
        {
            GameObject prefab = BuildingVisualCatalog.LoadByPath(resourcesPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameLoop] Missing mesh resource: {resourcesPath}");
                return null;
            }

            var go = ColonyVisualUtility.InstantiateOriented(prefab, position, buildingRoot, yawDegrees);
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            CampusNavMesh.AddObstacle(go);
            return go;
        }

        public float FlagStackShare(FlagHandle flag, SpecialistAgent agent)
        {
            if (flag == null || agent == null) return 1f;
            int better = 0;
            float mine = agent.EffectiveWorkRate;
            EntityId id = agent.GetEntityId();
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || a == agent || !a.IsClaiming) continue;
                if (!ReferenceEquals(a.ActiveFlag, flag)) continue;
                float other = a.EffectiveWorkRate;
                if (other > mine + 0.0001f || (Mathf.Abs(other - mine) <= 0.0001f && a.GetEntityId().CompareTo(id) < 0))
                    better++;
            }
            return OverseerRules.StackShare(better);
        }

        public void OnRobotScrapped(SpecialistAgent agent, int salvage)
        {
            if (agent == null || agent.Data == null) return;
            var rec = agent.ToRecord(corpse: true);
            var cls = rec.Class;
            string label = ColonyStructure.ClassLabel(cls);
            _agents.Remove(agent);
            if (Agent == agent) Agent = _agents.Count > 0 ? _agents[0] : null;
            RegisterCorpse(rec);
            NoteMechDeath(agent.transform.position);
            var shop = FindWorkshopFor(cls);
            if (shop != null && shop.IsAlive)
                shop.ClearRobotFabricated();
            string salvageTxt = salvage > 0 ? $" Salvage {salvage} EU." : "";
            int bill = OverseerRules.YardBill(rec.Level, rec.Class);
            LogOverseer($"{label} scrapped — wreck in the Fobot Yard. Stand-up {bill} EU (L{rec.Level}).{salvageTxt}");
        }

        public void NoteMechDeath(Vector3 world)
        {
            _lastMechDeathAt = Time.time;
            TrySpawnJunkBot(world);
        }

        public void OnFaunaKilled(FaunaKind kind, Vector3 world)
        {
            var killer = NearestLivingAgent(world, 18f);
            if (killer == null) return;
            int purse = OverseerRules.KillPurse(kind);
            if (purse > 0)
                killer.EarnCredits(purse, kind.ToString());
            killer.GrantXp(OverseerRules.XpForFauna(kind), kind.ToString());
        }

        private ColonyStructure FindWorkshopFor(SpecialistClass cls)
        {
            if (Village == null) return null;
            var list = Village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive || !s.IsWorkshop) continue;
                var want = ColonyStructure.RobotClassForWorkshop(s.Category);
                if (want.HasValue && want.Value == cls)
                    return s;
            }
            return null;
        }

        private static string ClassWorkshopName(SpecialistClass cls) => cls switch
        {
            SpecialistClass.EngineerBot => "Engineer Workshop",
            SpecialistClass.DefenseMech => "Defense Workshop",
            SpecialistClass.Medic => "Medic Workshop",
            SpecialistClass.HarvesterBot => "Harvester Workshop",
            SpecialistClass.SurveyorBot => "Surveyor Workshop",
            SpecialistClass.TerraformerBot => "Terraformer Workshop",
            SpecialistClass.CourierBot => "Courier Workshop",
            SpecialistClass.GeologistBot => "Geologist Workshop",
            SpecialistClass.SentinelMech => "Sentinel Workshop",
            _ => "Scout Workshop"
        };

        public void ApplyFaunaFrenzy(bool on)
        {
            for (int i = 0; i < _stalkers.Count; i++)
                _stalkers[i]?.SetFrenzy(on);
        }

        public bool HasAliveCategory(BuildingCategory cat)
        {
            if (Village == null) return false;
            var list = Village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s != null && s.IsAlive && s.Category == cat)
                    return true;
            }
            return false;
        }

        public ColonyStructure FindGuildHall(RobotGuildId id)
        {
            if (Village == null) return null;
            var list = Village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive || !s.IsGuild) continue;
                if (RobotGuildCatalog.TryMatch(s.SourceData, out var matched) && matched.Id == id)
                    return s;
                if (s.HasPreferredClass)
                {
                    var byClass = RobotGuildCatalog.ForClass(s.PreferredClass);
                    if (byClass != null && byClass.Id == id)
                        return s;
                }
            }

            return null;
        }

        public bool TryActivateGuildBenefit(RobotGuildId id)
        {
            if (GuildBenefits == null) return false;
            bool hall = FindGuildHall(id) != null;
            if (!GuildBenefits.TryActivate(id, Research, Resources, hall, out string line))
            {
                if (!string.IsNullOrEmpty(line))
                    LogOverseer(line);
                return false;
            }

            LogOverseer(line);
            if (id == RobotGuildId.Horizon)
                PulseHorizonScout();
            return true;
        }

        private void PulseHorizonScout()
        {
            if (_world == null) return;
            var hall = FindGuildHall(RobotGuildId.Horizon);
            Vector3 at = hall != null ? hall.WorldPosition : ColonyLayout.CampusOrigin;
            var lairs = _world.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                var l = lairs[i];
                if (l == null || l.IsCleared || l.IsScouted) continue;
                if (FlatDist(at, l.WorldPosition) <= 48f)
                    l.MarkScouted();
            }
        }

        public ConstructionOrder RefabAt(ColonyStructure st)
        {
            if (st == null || Placer == null) return null;
            return Placer.FindRefabAt(st.WorldPosition, 6f);
        }

        /// <summary>
        /// Upkeep tick. Majesty 2 has no payroll tithe: heroes already hand half of every earning
        /// to their guild till (see SpecialistAgent.EarnCredits), and collectors walk it home.
        /// </summary>
        private void OnUpkeepTithe()
        {
            _lastTithe = 0;
        }

        private void RefreshSustainRates()
        {
            if (Settlement == null) return;
            // Majesty: income is what the tax collectors actually carry into the treasury.
            Settlement.SetIncomeRate(TreasuryIncomePerMin);
        }

        private void TickCourierPad()
        {
            if (Economy == null) return;
            float courier = 1f;
            if (Village != null)
            {
                var pad = Village.NearestByCategory(ColonyLayout.CampusOrigin, 80f, BuildingCategory.LandingPad);
                if (pad != null)
                {
                    for (int i = 0; i < _agents.Count; i++)
                    {
                        var a = _agents[i];
                        if (a == null || a.IsIncapacitated || a.Data == null) continue;
                        if (a.Data.specialistClass != SpecialistClass.CourierBot) continue;
                        if (a.CurrentAction == SpecialistAction.Flee) continue;
                        if (FlatDist(a.transform.position, pad.WorldPosition) < OverseerRules.CourierPadRange)
                        {
                            courier = OverseerRules.CourierResupplyScale;
                            break;
                        }
                    }
                }
            }

            float resupply = 90f * (_body != null ? Mathf.Max(0.4f, _body.ResupplyIntervalScale) : 1f);
            resupply *= Mathf.Max(0.4f, _tech.ResupplyIntervalScale);
            resupply *= Mathf.Max(0.4f, ReplayRules.ResupplyIntervalScale);
            resupply *= courier;
            int fee = _body != null ? Mathf.Max(0, _body.ResupplyDockFee) : 0;
            fee = Mathf.Max(0, fee - _tech.ResupplyFeeDiscount);
            fee += Mathf.Max(0, ReplayRules.ExtraDockFee);
            Economy.SetResupplyRules(resupply, fee);
        }

        private void TickEmptyRosterFail(float dt)
        {
            EmptyRosterFailed = false;
            if (StillCaptureHold.Active)
            {
                _emptyRosterTimer = 0f;
                return;
            }
            if (!_everHadRobot)
            {
                _emptyRosterTimer = 0f;
                return;
            }
            if (RobotCount > 0 || HasRefabInProgress)
            {
                _emptyRosterTimer = 0f;
                return;
            }
            _emptyRosterTimer += dt;
            if (_emptyRosterTimer >= OverseerRules.EmptyRosterFailSeconds)
                EmptyRosterFailed = true;
        }

        private void ComputeReviveBill(out int met, out int ice)
        {
            met = 0;
            ice = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || !a.IsIncapacitated) continue;
                met += a.Data != null
                    ? OverseerRules.YardBill(a.Level, a.Data.specialistClass)
                    : OverseerRules.YardBill(a.Level);
            }
            for (int i = 0; i < _corpses.Count; i++)
                met += OverseerRules.YardBill(_corpses[i].Level, _corpses[i].Class);
            if (met <= 0)
                met = OverseerRules.ReviveMet;
        }

        private List<SpecialistRecord> CaptureRoster()
        {
            var list = new List<SpecialistRecord>(8);
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || a.Data == null) continue;
                list.Add(a.ToRecord(corpse: false));
            }
            for (int i = 0; i < _corpses.Count; i++)
            {
                var rec = _corpses[i];
                rec.Corpse = true;
                bool dup = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j].Class == rec.Class)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup) list.Add(rec);
            }
            foreach (var kv in _pendingVeterans)
            {
                bool dup = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j].Class == kv.Key)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup) list.Add(kv.Value);
            }
            return list;
        }

        private void LoadRosterMemory() => LoadRosterMemory(DemoSettings.LoadRoster(celestialBody));

        private void LoadRosterMemory(string snapshot)
        {
            _rosterSaved.Clear();
            _corpses.Clear();
            _pendingVeterans.Clear();
            SpecialistRoster.TryDecode(snapshot, _rosterSaved);
            ApplyRosterRecords(_rosterSaved);
        }

        private void LoadRosterFromSave(List<SaveRosterEntry> saved)
        {
            _rosterSaved.Clear();
            _corpses.Clear();
            _pendingVeterans.Clear();
            if (saved == null) return;
            for (int i = 0; i < saved.Count; i++)
            {
                var s = saved[i];
                _rosterSaved.Add(new SpecialistRecord
                {
                    Class = (SpecialistClass)s.specialistClass,
                    Level = Mathf.Clamp(s.level > 0 ? s.level : 1, 1, OverseerRules.LevelCap),
                    Xp = Mathf.Max(0, s.xp),
                    Credits = Mathf.Max(0, s.credits),
                    ReviveCount = Mathf.Max(0, s.reviveCount),
                    Suit = (ShopItemId)s.suit,
                    Corpse = s.corpse
                });
            }
            ApplyRosterRecords(_rosterSaved);
        }

        private void ApplyRosterRecords(List<SpecialistRecord> records)
        {
            if (records == null) return;
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (rec.Corpse)
                    RegisterCorpse(rec);
                else
                    _pendingVeterans[rec.Class] = rec;
            }
        }

        private void RetryUnpaidCorpses()
        {
            // Wrecks wait in the Fobot Yard. Continue does not auto-pay or auto-refab.
        }

        private bool HasCorpse(SpecialistClass cls)
        {
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (_corpses[i].Class == cls) return true;
            }
            return false;
        }

        private void RegisterCorpse(SpecialistRecord rec)
        {
            rec.Corpse = true;
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (_corpses[i].Class != rec.Class) continue;
                _corpses[i] = rec;
                RefreshWreckVisuals();
                return;
            }
            _corpses.Add(rec);
            RefreshWreckVisuals();
        }

        private void RemoveCorpse(SpecialistClass cls)
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                if (_corpses[i].Class == cls)
                    _corpses.RemoveAt(i);
            }
            RefreshWreckVisuals();
        }

        private void RefreshWreckVisuals()
        {
            for (int i = 0; i < _wreckVisuals.Count; i++)
            {
                if (_wreckVisuals[i] != null)
                    Destroy(_wreckVisuals[i]);
            }
            _wreckVisuals.Clear();
            Vector3 yard = ScrapyardPosition();
            Transform parent = buildingRoot != null ? buildingRoot : transform;
            for (int i = 0; i < _corpses.Count; i++)
            {
                float ang = i * 0.9f;
                Vector3 at = yard + new Vector3(Mathf.Cos(ang) * 1.6f, 0f, Mathf.Sin(ang) * 1.6f);
                _wreckVisuals.Add(FobotWreck.Spawn(_corpses[i], at, parent));
            }
        }

        /// <summary>
        /// True scrap: new chassis at level 1, 70% workshop MET, 40 s. Consumes the wreck.
        /// Distinct from Fobot Yard, which resurrects this ego.
        /// </summary>
        public bool TryRefabRookie(ColonyStructure shop)
        {
            if (shop == null || !shop.IsAlive || !shop.IsWorkshop) return false;
            var cls = ColonyStructure.RobotClassForWorkshop(shop.Category);
            if (!cls.HasValue) return false;
            if (!TryGetCorpse(cls.Value, out var rec))
            {
                LogOverseer("No wreck for this shop. Pay the Fobot Yard to stand this ego up.");
                return false;
            }

            if (!TryEnqueueScrapRefab(rec, alreadyPaid: false, out int met, out _, out string shopName))
            {
                LogOverseer($"Re-fab needs {OverseerRules.RefabMetals(shop.SourceData)} EU at {shopName}.");
                return false;
            }

            LogOverseer($"Re-fab queued — new {ColonyStructure.ClassLabel(cls.Value)} at L1, {met} EU / 40 s. The wreck is gone.");
            return true;
        }

        public bool HasWreckFor(SpecialistClass cls) => HasCorpse(cls);

        public string WreckSummary()
        {
            if (_corpses.Count <= 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                var rec = _corpses[i];
                sb.Append(ColonyStructure.ClassLabel(rec.Class));
                sb.Append(" L");
                sb.Append(Mathf.Max(1, rec.Level));
                sb.Append(" ");
                sb.Append(OverseerRules.YardBill(rec.Level, rec.Class));
                sb.Append(" EU");
            }
            return sb.ToString();
        }

        private bool TryGetCorpse(SpecialistClass cls, out SpecialistRecord rec)
        {
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (_corpses[i].Class != cls) continue;
                rec = _corpses[i];
                return true;
            }
            rec = default;
            return false;
        }

        private bool TryEnqueueScrapRefab(
            SpecialistRecord rec, bool alreadyPaid, out int met, out int ice, out string shopName)
        {
            ice = 0;
            shopName = ClassWorkshopName(rec.Class);
            var shop = FindWorkshopFor(rec.Class);
            met = OverseerRules.RefabMetals(shop != null ? shop.SourceData : null);
            if (shop == null || !shop.IsAlive)
                return false;
            shopName = shop.DisplayName;
            if (!alreadyPaid)
            {
                if (Economy == null || !Economy.CanAffordRevive(met))
                    return false;
            }

            var data = shop.SourceData;
            Vector2Int cell = Vector2Int.zero;
            if (grid != null)
                cell = grid.WorldToCell(shop.WorldPosition);
            if (Placer == null) return false;
            if (!alreadyPaid && !Economy.TrySpendRevive(met))
                return false;
            if (!Placer.TryEnqueueRefab(data, cell, shop.WorldPosition, 0, OverseerRules.RefabSeconds, rec.Class, out _))
            {
                Resources?.Add(ResourceId.Metals, met);
                return false;
            }

            RemoveCorpse(rec.Class);
            _pendingVeterans.Remove(rec.Class);
            return true;
        }

        private void ResurrectWrecksAtYard(ColonyStructure yard)
        {
            if (yard == null) return;
            var waiting = new List<SpecialistRecord>(_corpses);
            for (int i = 0; i < waiting.Count; i++)
                ResurrectWreck(waiting[i], yard);
        }

        private void ResurrectWreck(SpecialistRecord rec, ColonyStructure yard)
        {
            var data = DataForClass(rec.Class);
            if (data == null) return;
            Vector3 pos = yard.WorldPosition + new Vector3(1.6f, 0f, 0.8f);
            if (grid != null)
                pos = grid.SnapToCellCenter(pos);
            var agent = SpawnOne(data, pos, TintForClass(rec.Class));
            if (agent == null) return;
            rec.Corpse = false;
            rec.ReviveCount = rec.ReviveCount + 1;
            agent.ApplyRecord(rec);
            agent.FieldRevive();
            agent.NoteRevivePaid();
            var shop = FindWorkshopFor(rec.Class);
            if (shop != null && shop.IsAlive)
            {
                shop.MarkRobotFabricated();
                shop.TryClockIn(agent);
            }
            _everHadRobot = true;
            _agents.Add(agent);
            if (Agent == null)
                Agent = agent;
            agent.BindNavMesh(_campusNav);
            RemoveCorpse(rec.Class);
        }

        private ColonyStructure FindFobotYard()
        {
            if (Village == null) return null;
            var dedicated = Village.NearestByCategory(ColonyLayout.CampusOrigin, 240f, BuildingCategory.FobotYard);
            if (dedicated != null && dedicated.IsAlive) return dedicated;
            return Village.NearestByCategory(ColonyLayout.CampusOrigin, 240f, BuildingCategory.Inn);
        }

        private void TickJunkYard(float dt)
        {
            if (DemoSettings.FirstHourDemo) return;
            _junkTimer -= dt;
            if (_junkTimer > 0f) return;
            _junkTimer = OverseerRules.JunkBotSpawnInterval;
            if (InFaunaGrace) return;
            bool corpse = HasScrapCorpse;
            bool recentDeath = _lastMechDeathAt > 0f &&
                               Time.time - _lastMechDeathAt <= OverseerRules.JunkDeathMemory;
            if (!corpse && !recentDeath) return;
            if (CountFauna(FaunaKind.JunkBot) >= OverseerRules.JunkBotCap) return;
            TrySpawnJunkBot(ScrapyardPosition());
        }

        private void TrySpawnJunkBot(Vector3 near)
        {
            if (InFaunaGrace) return;
            if (CountFauna(FaunaKind.JunkBot) >= OverseerRules.JunkBotCap) return;
            Vector3 yard = ScrapyardPosition();
            Vector3 home = near.sqrMagnitude > 0.01f ? near : yard;
            Vector2 jitter = Random.insideUnitCircle * 2.2f;
            Vector3 spawn = home + new Vector3(jitter.x, 0f, jitter.y);
            if (SpawnFaunaAt(FaunaKind.JunkBot, spawn) == null) return;
            if (CountFauna(FaunaKind.JunkBot) == 1)
                LogOverseer("Junk bots rising from the scrap pile — post Clear Threat.");
        }

        private Vector3 ScrapyardPosition()
        {
            var yard = FindFobotYard();
            if (yard != null && yard.IsAlive)
                return yard.WorldPosition;
            for (int i = 0; i < _corpses.Count; i++)
            {
                var shop = FindWorkshopFor(_corpses[i].Class);
                if (shop != null && shop.IsAlive)
                    return shop.WorldPosition;
            }
            if (Village != null)
            {
                var list = Village.Structures;
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    if (s != null && s.IsAlive && s.IsWorkshop)
                        return s.WorldPosition;
                }
            }
            return ColonyLayout.CampusOrigin;
        }

        private SpecialistAgent NearestLivingAgent(Vector3 world, float range)
        {
            SpecialistAgent best = null;
            float bestD = range;
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || !a.IsAlive || a.IsIncapacitated) continue;
                float d = FlatDist(a.transform.position, world);
                if (d < bestD)
                {
                    bestD = d;
                    best = a;
                }
            }
            return best;
        }

        private void TickCampusBoard(float dt)
        {
            _techRefreshCooldown -= dt;
            if (_techRefreshCooldown <= 0f)
            {
                _techRefreshCooldown += 1f / 3f;
                RefreshTechEffects();
            }
            ExpireDiscs(_surveys);
            ExpireDiscs(_watches);
            TickWatches(dt);
            TickBatteries(dt);
            TickDenChart();
        }

        private static void ExpireDiscs(List<TimedDisc> list)
        {
            float now = Time.time;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Until <= now)
                    list.RemoveAt(i);
            }
        }

        private void AddSurveyDisc(Vector3 at)
        {
            _surveys.Add(new TimedDisc { Pos = at, Until = Time.time + OverseerRules.SurveySeconds });
        }

        private void AddDefendWatch(Vector3 at, float extra)
        {
            _watches.Add(new TimedDisc
            {
                Pos = at,
                Until = Time.time + OverseerRules.DefendWatchSeconds + extra
            });
        }

        public bool InSurveyDisc(Vector3 world)
        {
            float rSq = OverseerRules.SurveyRadius * OverseerRules.SurveyRadius;
            for (int i = 0; i < _surveys.Count; i++)
            {
                float dx = _surveys[i].Pos.x - world.x;
                float dz = _surveys[i].Pos.z - world.z;
                if (dx * dx + dz * dz <= rSq) return true;
            }
            return false;
        }

        private bool InDefendWatch(Vector3 world)
        {
            float rSq = OverseerRules.DefendWatchRadius * OverseerRules.DefendWatchRadius;
            for (int i = 0; i < _watches.Count; i++)
            {
                float dx = _watches[i].Pos.x - world.x;
                float dz = _watches[i].Pos.z - world.z;
                if (dx * dx + dz * dz <= rSq) return true;
            }
            return false;
        }

        private bool HasCommonsShade(Vector3 world)
        {
            if (Village == null) return false;
            var commons = Village.NearestByCategory(world, OverseerRules.CommonsShadeRadius, BuildingCategory.Commons);
            if (commons == null || !commons.IsAlive) return false;
            return FlatDist(world, commons.WorldPosition) <= OverseerRules.CommonsShadeRadius;
        }

        private int ScoutDensInDisc(Vector3 at)
        {
            if (_world == null) return 0;
            int n = 0;
            var lairs = _world.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                var l = lairs[i];
                if (l == null || l.IsCleared || l.IsScouted) continue;
                if (!DenChart.InDisc(at, l.WorldPosition, OverseerRules.SurveyRadius)) continue;
                l.MarkScouted();
                n++;
            }

            return n;
        }

        private void TickDenChart()
        {
            if (_world == null) return;
            var lairs = _world.Lairs;
            for (int a = 0; a < _agents.Count; a++)
            {
                var agent = _agents[a];
                if (agent == null || !agent.IsAlive || agent.IsIncapacitated || agent.Data == null)
                    continue;
                if (!DenChart.IsChartClass(agent.Data.specialistClass)) continue;
                Vector3 at = agent.transform.position;
                for (int i = 0; i < lairs.Count; i++)
                {
                    var l = lairs[i];
                    if (l == null || l.IsCleared || l.IsScouted) continue;
                    if (!DenChart.InDisc(at, l.WorldPosition, DenChart.PassiveChartRadius)) continue;
                    l.MarkScouted();
                    LogOverseer(CompactGrok.DenCharted(
                        ColonyStructure.ClassLabel(agent.Data.specialistClass)));
                }
            }
        }

        private void TickWatches(float dt)
        {
            float rSq = OverseerRules.DefendWatchRadius * OverseerRules.DefendWatchRadius;
            for (int w = 0; w < _watches.Count; w++)
            {
                Vector3 pos = _watches[w].Pos;
                for (int i = 0; i < _stalkers.Count; i++)
                {
                    var s = _stalkers[i];
                    if (s == null || !s.IsAlive) continue;
                    float dx = s.transform.position.x - pos.x;
                    float dz = s.transform.position.z - pos.z;
                    if (dx * dx + dz * dz > rSq) continue;
                    s.ApplyCombatDamage(OverseerRules.DefendWatchDps * dt);
                }
            }
        }

        private void TickBatteries(float dt)
        {
            if (Village == null) return;
            var list = Village.Structures;
            if (Time.time >= _batteryLockUntil)
                _batteryLock = null;
            for (int i = 0; i < list.Count; i++)
            {
                var st = list[i];
                if (st == null || !st.IsAlive) continue;
                bool battery = st.Category == BuildingCategory.Defense ||
                               (st.IsWatchtower && st.LaserArmed);
                if (!battery) continue;
                DustStalkerAgent target = _batteryLock;
                if (target == null || !target.IsAlive ||
                    FlatDist(st.WorldPosition, target.transform.position) > OverseerRules.BatteryRange)
                {
                    target = NearestFauna(st.WorldPosition, OverseerRules.BatteryRange);
                    _batteryLock = target;
                    _batteryLockUntil = Time.time + OverseerRules.BatteryRetarget;
                }
                if (target == null) continue;
                float dps = OverseerRules.BatteryDps;
                if (GuildBenefits != null && GuildBenefits.IsActive(RobotGuildId.Aegis))
                    dps *= 1.25f;
                target.ApplyCombatDamage(dps * dt);
            }
        }

        private DustStalkerAgent NearestFauna(Vector3 from, float range)
        {
            DustStalkerAgent best = null;
            float bestD = range;
            for (int i = 0; i < _stalkers.Count; i++)
            {
                var s = _stalkers[i];
                if (s == null || !s.IsAlive) continue;
                float d = FlatDist(from, s.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }
    }
}
