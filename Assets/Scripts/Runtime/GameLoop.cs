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
        public IReadOnlyList<HeroParty> Parties => _parties;
        public CelestialBodyId ActiveBody => celestialBody;
        public bool StartsEmpty => !spawnShowcaseColony;
        public bool SpawnWaystationInn => spawnWaystationInn;
        public DemoScreen Screen { get; private set; } = DemoScreen.Title;
        public bool IsPlaying => Screen == DemoScreen.Playing;
        public bool AllowsCamera => Screen == DemoScreen.Playing || Screen == DemoScreen.Title;
        public const int TutorialCompleteStep = 6;
        public int TutorialStep { get; private set; }
        public bool IsTutorialActive => !DemoSettings.TutorialDone && TutorialStep < TutorialCompleteStep;
        public BuildingData[] StarterBuildings => starterBuildings;
        public CelestialBodyProfile BodyProfile => _body;
        public PlanetaryWorldGen World => _world;
        public int MoonSeedValue => BodySeed.Current;

        public void SetTool(OverseerTool tool) => ApplyTool(tool);

        /// <summary>Bottom-dock toggle: pressing an active tool again closes it (back to inspect).</summary>
        public void ToggleTool(OverseerTool tool)
        {
            if (tool == OverseerTool.None || activeTool == tool)
                ApplyTool(OverseerTool.None);
            else
                ApplyTool(tool);
        }

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
                Ice = Resources != null ? Resources.Get(ResourceId.WaterIce) : 0,
                PowerGen = Economy != null ? Economy.PowerGen : 0,
                PowerDraw = Economy != null ? Economy.PowerDraw : 0,
                RobotCount = robots,
                MeanHealth = meanHp,
                MissionElapsed = m != null ? m.MissionElapsed : 0f,
                GatesMet = dens && sustain && launch
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

        /// <summary>True when every living specialist is incapacitated.</summary>
        public bool IsOutpostOverwhelmed
        {
            get
            {
                if (_agents.Count == 0) return false;
                int living = 0;
                int down = 0;
                for (int i = 0; i < _agents.Count; i++)
                {
                    var a = _agents[i];
                    if (a == null) continue;
                    living++;
                    if (a.IsIncapacitated) down++;
                }
                return living > 0 && down == living;
            }
        }

        private readonly List<SpecialistAgent> _agents = new List<SpecialistAgent>();
        private readonly List<SpecialistAgent> _selected = new List<SpecialistAgent>(MaxPartySize);
        private readonly List<DustStalkerAgent> _stalkers = new List<DustStalkerAgent>();
        private readonly List<HeroParty> _parties = new List<HeroParty>(4);
        private int _nextPartyId = 1;
        private FlagPlacementInput _flagInput;
        private BuildingPlacementInput _buildInput;
        private IsometricCameraController _isoCam;
        private Transform _threatRoot;
        private float _constructionTick;
        private DebugHud _debugHud;
        private OverseerHud _overseerHud;
        private int _focusedCampus;
        private CampusNavMesh _campusNav;
        private MissionController _mission;
        private PlanetaryWorldGen _world;
        private CelestialBodyProfile _body;
        private TechEffects _tech = TechEffects.Neutral;
        private bool _launchCraftStaged;
        private DemoScreen _settingsReturn = DemoScreen.Title;
        private float _autosaveTimer;
        private float _interestTimer;
        private float _ecologyCooldown = 5f;
        private bool _faunaRetreated;
        private bool _radWarned;
        private float _glanceCooldown;
        private bool _bodyHopQueued;
        private CelestialBodyId _bodyHopTarget;
        private bool _bodyHopUnlock;
        private GameObject _outpostBeacon;
        private readonly Dictionary<EntityId, float> _extractStamp = new Dictionary<EntityId, float>(8);
        private readonly List<ConstructionOrder> _completedBuilds = new List<ConstructionOrder>(8);

        public MissionController Mission => _mission;
        public OverseerLog Log { get; } = new OverseerLog();

        /// <summary>Soft camera pan toward a world event. Rate-limited so it never fights the player.</summary>
        public void GlanceAt(Vector3 world, float? orthoSize = null, bool force = false)
        {
            if (!IsPlaying || _isoCam == null) return;
            if (!force && _glanceCooldown > 0f) return;
            _isoCam.GlanceAt(world, orthoSize ?? ColonyLayout.CameraOrthoSize);
            _glanceCooldown = 5.5f;
        }

        public void LogOverseer(string line)
        {
            Log.Push(line);
            _overseerHud?.Notify(line, 4.2f);
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

        private void Awake()
        {
            DemoSettings.Load();
            CampaignProgress.Ensure();
            celestialBody = BodySeed.LoadSavedBody();
            if (!CampaignProgress.IsUnlocked(celestialBody))
                celestialBody = CelestialBodyId.Earth;
            _body = CelestialBodyCatalog.Get(celestialBody);
            ModularBuildingFactory.BindBody(_body);
            BodySeed.Ensure(celestialBody, worldSeedOverride);

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
            KingdomLife.Dress(transform, emptyStart: StartsEmpty);
            CampusDressing.Reset();
            CampusDressing.RefreshTubes(Placer, grid, buildingRoot != null ? buildingRoot : transform);
            EnsureHud();
            EnsureMission();
            LaunchSite.ClearSession();
            _launchCraftStaged = false;
            BootstrapResearch();
            SyncLaunchGate();
            DemoAudio.Ensure();
            DemoAudio.SetBody(_body);
            DemoAudio.ApplyVolumes();
            DemoAudio.SetCampusAmbient(0);

            string travel = CampaignProgress.ConsumeTravelLog();
            if (!string.IsNullOrEmpty(travel))
                Log.Push(travel);
            if (_body != null && !string.IsNullOrEmpty(_body.ArrivalLog))
                Log.Push(_body.ArrivalLog);

            if (DemoSettings.BootStraightIntoPlay)
                EnterPlaying(loadStockpile: DemoSettings.SaveExists);
            else
                EnterTitle();

            Debug.Log($"[GameLoop] Demo ready — {_body.DisplayName} seed={BodySeed.Current}" +
                      (StartsEmpty ? " (empty start)." : "."));
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        public void EnterTitle()
        {
            Screen = DemoScreen.Title;
            Time.timeScale = 0f;
            ApplyTool(OverseerTool.None);
        }

        public void EnterPlaying(bool loadStockpile)
        {
            Screen = DemoScreen.Playing;
            Time.timeScale = 1f;
            if (loadStockpile)
            {
                DemoSettings.TryLoadStockpile(Resources);
                RestoreCampus();
            }
            PersistSession();
            TutorialStep = DemoSettings.TutorialDone ? TutorialCompleteStep : 0;
            _overseerHud?.OnSessionPlaying();
            if (ReplayRules.Mode != ColonyRunMode.Campaign ||
                ReplayRules.Challenge != ChallengeId.None ||
                ReplayRules.Stance != DoctrineStance.Balanced)
            {
                LogOverseer($"Replay: {ReplayRules.HudTag}. Doctrine nudges hunger/courage/workshop pull only.");
            }
        }

        public void StartNewGame()
        {
            ResearchManager.WipeUnlocks();
            CampaignProgress.ResetCampaign();
            DemoSettings.ClearSave();
            DemoSettings.ResetTutorial();
            ReplayRules.Save();
            DemoSettings.RequestBootIntoPlay();
            BodySeed.SetBody(CelestialBodyId.Earth);
            ReloadActiveScene();
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
            Time.timeScale = 1f;
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
            TutorialStep = TutorialCompleteStep;
            DemoSettings.MarkTutorialDone();
        }

        public void RestartTutorial()
        {
            DemoSettings.ResetTutorial();
            TutorialStep = 0;
            _overseerHud?.Notify("Tutorial reset — COMMONS first, then airlock, HAB, workshop, flag, TECH.", 4f);
        }

        public void NotifyTechOpened()
        {
            if (TutorialStep == 5)
                AdvanceTutorial();
        }

        public void CancelFlag(FlagHandle handle)
        {
            if (handle == null || Flags == null) return;
            int refund = handle.EscrowMetals;
            Economy?.RefundBountyEscrow(refund);
            Flags.Cancel(handle);
            _overseerHud?.Notify(refund > 0 ? $"Flag cancelled — {refund} MET returned." : "Flag cancelled.", 2.4f);
            Debug.Log("[Flags] Cancelled — metals refunded.");
        }

        public void NotifyFlagPosted(FlagHandle handle)
        {
            RefreshFlagInterest();
            if (handle == null) return;
            if (_agents.Count == 0)
            {
                LogOverseer("Flag posted — fabricate a workshop robot before anyone can take it.");
                return;
            }
            if (handle.InterestCount <= 0)
                LogOverseer($"Ignored — raise bounty (+) or pick a type they want. {handle.Data?.displayName ?? "Flag"} ${handle.CurrentBounty:F0}.");
            else
                LogOverseer($"{handle.InterestCount} tempted: {handle.InterestLabel}");
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
                for (int a = 0; a < _agents.Count; a++)
                {
                    var agent = _agents[a];
                    if (agent == null || !agent.IsAlive || agent.Data == null) continue;
                    if (!Brain.WouldTakeFlag(agent.PeekContext(), flag, agent.BodyDanger, out _))
                        continue;
                    n++;
                    string label = ColonyStructure.ClassLabel(agent.Data.specialistClass);
                    if (names.Length == 0) names = label;
                    else if (names.IndexOf(label, System.StringComparison.Ordinal) < 0)
                        names += " · " + label;
                }
                flag.InterestCount = n;
                flag.InterestLabel = n <= 0
                    ? (_agents.Count == 0 ? "no robots yet" : "ignored — raise $")
                    : names;
            }
        }

        private void HandleSessionHotkeys()
        {
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
            if (DemoSettings.TutorialDone || TutorialStep >= TutorialCompleteStep) return;
            for (int n = 0; n < TutorialCompleteStep; n++)
            {
                int before = TutorialStep;
                if (TutorialStep == 0 && Settlement != null && Settlement.HasCommons)
                    AdvanceTutorial();
                else if (TutorialStep == 1 && HasAnyAirlock())
                    AdvanceTutorial();
                else if (TutorialStep == 2 && Settlement != null && Settlement.CoreHabs > 0)
                    AdvanceTutorial();
                else if (TutorialStep == 3 && HasAnyWorkshop())
                    AdvanceTutorial();
                else if (TutorialStep == 4 && Flags != null && Flags.Flags.Count > 0)
                    AdvanceTutorial();
                if (TutorialStep == before || TutorialStep >= TutorialCompleteStep)
                    break;
            }
        }

        private bool HasAnyAirlock()
        {
            if (Placer == null) return false;
            var pieces = Placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].IsAirlock) return true;
            }
            return false;
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

        private void AdvanceTutorial()
        {
            TutorialStep++;
            if (TutorialStep >= TutorialCompleteStep)
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
        /// No buildings — player spends the starter stockpile.
        /// </summary>
        private void SeedEmptyStartClaim()
        {
            if (Placer == null || grid == null) return;

            SeedSoftClaim(ColonyLayout.CampusOrigin, campus: true);
            SeedSoftClaim(ColonyLayout.CampusBOrigin, campus: false);
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
            HandleSelection();
            TryCancelFlagUnderCursor();
            PushThreatToSpecialists();
            _world?.TickLairs(Placer != null ? Placer.Pieces.Count : 0);
            RefreshPowerBudget();
            if (Settlement != null)
            {
                Settlement.ProductionScale = Economy != null && Economy.PowerShort ? 0.45f : 1f;
                Settlement.Tick(Time.deltaTime);
            }
            Village?.Tick(Time.deltaTime);
            TickResearch(Time.deltaTime);
            _mission?.Tick();
            TickTutorial();
            TickAutosave();
            TickFlagInterest(Time.deltaTime);
            TickCampusEcology(Time.deltaTime);
            if (_glanceCooldown > 0f)
                _glanceCooldown -= Time.deltaTime;

            _constructionTick += Time.deltaTime;
            if (_constructionTick >= 0.25f)
            {
                ProcessCompletedConstruction();

                var living = new List<SpecialistData>(_agents.Count);
                for (int i = 0; i < _agents.Count; i++)
                {
                    if (_agents[i] != null && _agents[i].Data != null)
                        living.Add(_agents[i].Data);
                }
                Economy?.Tick(_constructionTick, living);
                _constructionTick = 0f;
            }

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
            ground.transform.position = new Vector3(worldW * 0.5f, 0f, worldH * 0.5f);
            ground.transform.localScale = new Vector3(worldW / 10f, 1f, worldH / 10f);
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
            Placer = new BuildingPlacer(Resources);
            Placer.HasCommons = () => Settlement != null && Settlement.HasCommons;
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

                    bool hasCommons = Settlement != null && Settlement.HasCommons;

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

                    // Airlocks: only on module face midlines (symmetry-axis ends).
                    if (BuildingPlacer.IsAirlock(data.category))
                        return Placer.IsValidAirlockDock(cell);

                    if (!IsBuildingUnlocked(data.category))
                        return false;

                    // Campus B: pad / extract / power / defense without tubes.
                    if (BuildingPlacer.IsForwardOutpost(data.category) &&
                        Placer.OverlapsOutpostClaim(cell, data.footprintWidth, data.footprintHeight))
                        return true;

                    // Every other module must Lego-dock onto an airlock end.
                    return Placer.IsValidModuleDock(cell, data.footprintWidth, data.footprintHeight);
                };
            }

            Brain = new SpecialistBrain();
            ApplyReplayToBrain();
            Economy = new SimpleEconomy(Resources);
            float resupply = 90f * (_body != null ? Mathf.Max(0.4f, _body.ResupplyIntervalScale) : 1f);
            int fee = _body != null ? Mathf.Max(0, _body.ResupplyDockFee) : 0;
            Economy.ConfigureResupply(resupply, fee);
            Settlement = new Settlement(Resources);
            if (_body != null)
                Settlement.SetBodyYield(_body.FarmYieldScale, _body.MineYieldScale);
            // Rebind after Settlement exists (constructor order).
            Placer.HasCommons = () => Settlement != null && Settlement.HasCommons;
            Research = new ResearchManager(Resources);
            Research.TechUnlocked += OnTechUnlocked;
            Threat = new ThreatPressure { Ambient = 0.18f };
        }

        private void ApplyStarterStockpile()
        {
            var body = _body ?? CelestialBodyCatalog.Get(celestialBody);
            float scale = Mathf.Clamp(ReplayRules.StartStockpileScale, 0.35f, 1f);
            Resources.Set(ResourceId.Regolith, Mathf.Max(0, Mathf.RoundToInt(body.StartRegolith * scale)));
            Resources.Set(ResourceId.WaterIce, Mathf.Max(0, Mathf.RoundToInt(body.StartWaterIce * scale)));
            Resources.Set(ResourceId.Metals, Mathf.Max(0, Mathf.RoundToInt(body.StartMetals * scale)));
            Resources.Set(ResourceId.Power, Mathf.Max(0, Mathf.RoundToInt(body.StartPower * scale)));
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
            else if (id == TechId.GuildCharter)
                LogOverseer("Guild Charter signed. Dock a hall and assign SCOUT/ENG/DEF/MED — Horizon, Anvil, Aegis, or Triage. Flags near the hall pull that class.");
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
            _tech = TechEffects.From(Research);
            if (Settlement != null)
            {
                Settlement.BonusBeds = _tech.ExtraBeds;
                Settlement.GrowInterval = 18f * Mathf.Max(0.4f, _tech.GrowIntervalScale);
                Settlement.SetTechYieldBonus(_tech.FarmYieldBonus, _tech.MineYieldBonus);
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
            }
        }

        private void TickResearch(float dt)
        {
            if (Research == null) return;
            CountLabs(out int labs, out int workers);
            float mult = _body != null ? _body.ResearchRateMultiplier : 1f;
            Research.Tick(dt, labs, workers, mult);
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
                        CreateBuilding("Ops Unit (OPS-1)", BuildingCategory.Mining, 45, 6, 14f, 4, 4),
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
            NormalizeCatalogNames(starterBuildings);
            starterBuildings = AppendEconomyBuildings(starterBuildings);
            ForceCardinalFootprints(starterBuildings);
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
                        break;
                    case BuildingCategory.Mining:
                        b.displayName = "Ops Unit (OPS-1)";
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
            mainCamera.orthographicSize = ColonyLayout.CameraOrthoSize;
            mainCamera.nearClipPlane = 0.3f;
            mainCamera.farClipPlane = 900f;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
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
                _isoCam.FocusOn(focus, ColonyLayout.CameraOrthoSize);
                _isoCam.SnapToTarget();
            }
        }

        private void SpawnParty()
        {
            _agents.Clear();
            ClearSelection();
            Agent = null;
            // Outdoor robots are fabricated when their workshop finishes construction.
            // Humans live only in HABs (Settlement census) — never as outdoor agents.
            Debug.Log("[GameLoop] No starter robots — build workshops to fabricate outdoor robots.");
        }

        /// <summary>Fabricate one outdoor robot when a workshop finishes building.</summary>
        public bool TryFabricateRobot(ColonyStructure workshop, bool announce = true)
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

            var agent = SpawnOne(data, pos, TintForClass(cls.Value));
            if (agent == null) return false;

            workshop.MarkRobotFabricated();
            workshop.TryClockIn(agent);
            _agents.Add(agent);
            if (Agent == null)
                Agent = agent;
            agent.BindNavMesh(_campusNav);

            string label = data.displayName ?? cls.Value.ToString();
            if (announce)
            {
                _overseerHud?.Notify($"{label} fabricated at {workshop.DisplayName}.", 3.2f);
                DemoAudio.PlayClaim();
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
                if (!ColonyStructure.IsWorkshopCategory(order.Data.category)) continue;
                var st = Village?.FindNear(order.WorldPosition, 4f);
                if (st != null && st.Category == order.Data.category)
                    TryFabricateRobot(st);
            }
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

        /// <summary>Rebuild walkable mesh after village HABs / connectors expand the campus.</summary>
        public void NotifyCampusExpanded()
        {
            if (_campusNav != null && grid != null)
            {
                _campusNav.Build(grid);
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i]?.BindNavMesh(_campusNav);
            }

            CampusDressing.RefreshTubes(Placer, grid, buildingRoot != null ? buildingRoot : transform);

            if (_ecologyCooldown > 4f)
                _ecologyCooldown = 4f;
            TrySpawnCampusFauna();
        }

        private void RefreshPowerBudget()
        {
            if (Economy == null) return;

            int gen = 0;
            int draw = 0;
            var structures = Village != null ? Village.Structures : null;
            if (structures != null)
            {
                for (int i = 0; i < structures.Count; i++)
                {
                    var st = structures[i];
                    if (st == null || !st.IsAlive) continue;
                    var data = st.SourceData;
                    if (st.Category == BuildingCategory.Power)
                    {
                        gen += data != null && data.powerGen > 0 ? data.powerGen : 6;
                        continue;
                    }
                    if (st.Category == BuildingCategory.Utility) continue;
                    draw += data != null && data.powerDraw > 0 ? data.powerDraw : 1;
                }
            }

            int robots = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i] != null) robots++;
            }
            draw += robots;

            if (Settlement != null && Settlement.HasOutpost)
            {
                int extra = _body != null ? Mathf.Max(0, _body.OutpostPowerDraw) : 2;
                draw += extra;
            }

            float pwrScale = _body != null ? Mathf.Max(0.25f, _body.PowerDrawScale) : 1f;
            pwrScale *= Mathf.Max(0.4f, _tech.PowerDrawScale);
            Economy.PowerGen = gen;
            Economy.PowerDraw = Mathf.Max(0, Mathf.RoundToInt(draw * pwrScale));
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
            if (_world != null && _world.Lairs.Count > 0 && _world.UnclearedLairCount <= 0) return;

            int mites = CountFauna(FaunaKind.Mite);
            int leeches = CountFauna(FaunaKind.Leech);
            int ticks = CountFauna(FaunaKind.Tick);
            int wisps = CountFauna(FaunaKind.Wisp);
            int creepers = CountFauna(FaunaKind.Creeper);
            int hoppers = CountFauna(FaunaKind.Hopper);
            int cap = _body != null ? Mathf.Max(2, _body.CampusFaunaCap) : 4;
            cap = Mathf.Max(2, Mathf.RoundToInt(cap * ReplayRules.FaunaCapScale));
            if (Settlement.HasOutpost) cap += 2;
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
            int miteCap = Mathf.Min(4, Mathf.RoundToInt((Settlement.Farms + Settlement.Mines) * miteW));
            int leechCap = Mathf.Min(4, Mathf.RoundToInt(Settlement.PowerPlants * leechW));
            int tickCap = Mathf.Min(4, Mathf.RoundToInt(Settlement.Mines * tickW));
            int wispCap = Mathf.Min(4, Mathf.RoundToInt(Settlement.PowerPlants * wispW));
            int creeperCap = Mathf.Min(4, Mathf.RoundToInt(Settlement.Farms * creeperW));
            int habs = Settlement.CoreHabs + Settlement.VillageHabs;
            int hopperCap = Mathf.Min(4, Mathf.RoundToInt(habs * hopperW));
            if (_body != null && _body.RadiationDrainPerSecond > 0f && wispW > 0.05f)
                wispCap = Mathf.Max(wispCap, 1);
            if (creeperW >= 1f && creeperCap < 1 && Settlement.Farms > 0)
                creeperCap = 1;
            if (hopperW >= 1f && hopperCap < 1 && habs > 0)
                hopperCap = 1;

            Vector3 campus = CampusFaunaOrigin();

            var farm = Village != null
                ? Village.NearestByCategory(campus, 80f, BuildingCategory.Farm)
                : null;
            if (TrySpawnCampusKind(
                    FaunaKind.Creeper, creepers, creeperCap,
                    farm != null ? farm.WorldPosition : campus, campus,
                    CreeperFirstLog()))
                return;

            var hab = Village != null
                ? Village.NearestByCategory(campus, 80f, BuildingCategory.Habitat)
                : null;
            if (hab == null && Village != null)
                hab = Village.NearestVillageHab(campus, 80f);
            if (TrySpawnCampusKind(
                    FaunaKind.Hopper, hoppers, hopperCap,
                    hab != null ? hab.WorldPosition : campus, campus,
                    HopperFirstLog()))
                return;

            var mine = Village != null
                ? Village.NearestByCategory(campus, 80f, BuildingCategory.Mine, BuildingCategory.Mining)
                : null;
            string tickLine = _body != null && _body.Id == CelestialBodyId.Belt
                ? "Rock ticks on the ore — post Defend Area."
                : "Dust ticks on the ore — post Defend Area.";
            if (TrySpawnCampusKind(
                    FaunaKind.Tick, ticks, tickCap,
                    mine != null ? mine.WorldPosition : campus, campus, tickLine))
                return;

            var pwr = Village != null
                ? Village.NearestPower(campus, 80f)
                : null;
            Vector3 pwrPos = pwr != null ? pwr.WorldPosition : campus;
            string wispLine = _body != null && _body.RadiationDrainPerSecond > 0f
                ? "Ice wisps off the crust — post Clear Threat."
                : "Dust wisps on the grid — post Clear Threat.";
            if (TrySpawnCampusKind(FaunaKind.Wisp, wisps, wispCap, pwrPos, campus, wispLine))
                return;

            var camp = Village != null
                ? Village.NearestExtractor(campus, 80f)
                : null;
            string miteLine = _body != null && _body.PreferMineMites
                ? "Rock mites on the ore — post Defend Area."
                : "Regolith mites on the farm — post Defend Area.";
            if (TrySpawnCampusKind(
                    FaunaKind.Mite, mites, miteCap,
                    camp != null ? camp.WorldPosition : campus, campus, miteLine))
                return;

            string leechLine = _body != null && _body.RadiationDrainPerSecond > 0f
                ? "Fissure leeches on the grid — post Clear Threat."
                : "Watt leeches on the Power Node — post Clear Threat.";
            if (TrySpawnCampusKind(FaunaKind.Leech, leeches, leechCap, pwrPos, campus, leechLine))
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
                case CelestialBodyId.Mars: return "Dust hoppers at the airlocks — post Clear Threat.";
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
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            home = go.transform.position;

            var agent = go.GetComponent<DustStalkerAgent>();
            if (agent == null) agent = go.AddComponent<DustStalkerAgent>();
            agent.Initialize(Threat, Flags, home, this);
            agent.SetKind(kind);
            agent.ApplyBodyTune(_body);
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

        public void RetryParty()
        {
            DemoAudio.PlayRetry();
            for (int i = 0; i < _agents.Count; i++)
                _agents[i]?.ReviveFull();
            _mission?.OnPartyRevived();
            Debug.Log("[GameLoop] Party revived — outpost holds.");
        }

        public void RestartMission()
        {
            if (advanceSeedOnRestart)
                BodySeed.AdvanceForNextConquest();
            DemoAudio.PlayRetry();
            DemoSettings.RequestBootIntoPlay();
            ReloadActiveScene();
        }

        /// <summary>Same-body reseed (sandbox rematch).</summary>
        public void BeginNextConquest()
        {
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
            string hop = $"Departure from {from}. Trajectory locked: {to}.";
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

            bool paid = Resources.Get(ResourceId.Metals) >= met &&
                        Resources.Get(ResourceId.WaterIce) >= ice;
            if (paid)
            {
                Resources.TrySpend(ResourceId.Metals, met);
                Resources.TrySpend(ResourceId.WaterIce, ice);
                return $"Freight paid: −{met} MET −{ice} ICE.";
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
            if (_bodyHopQueued) return;
            _bodyHopQueued = true;
            _bodyHopTarget = body;
            _bodyHopUnlock = unlockAll;
        }

        public void RequestDebugBodyCycle(bool unlockAll) =>
            RequestDebugHop(CelestialBodyCatalog.Next(celestialBody), unlockAll);

        private void HandleBodyHopHotkeys()
        {
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
            if (!IsPlaying) return;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (activeTool == OverseerTool.Flag) ApplyTool(OverseerTool.Build);
                else if (activeTool == OverseerTool.Build) ApplyTool(OverseerTool.None);
                else ApplyTool(OverseerTool.Flag);
            }

            if (Input.GetKeyDown(KeyCode.B)) ToggleTool(OverseerTool.Build);
            if (Input.GetKeyDown(KeyCode.G)) ToggleTool(OverseerTool.Flag);
            if (Input.GetKeyDown(KeyCode.Q)) ApplyTool(OverseerTool.None);
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

            const float bounty = 160f;
            if (_flagInput != null)
            {
                _flagInput.PostFlagAt(exploreFlagData, world, bounty);
            }
            else
            {
                var handle = Flags.Post(exploreFlagData, world, bounty);
                DemoAudio.PlayFlagPost();
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
            _isoCam.FocusOn(focus, ColonyLayout.CameraOrthoSize);
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
            b.powerDraw = cat == BuildingCategory.Power ? 0 : (power > 0 ? 2 : 0);
            b.powerGen = PowerGenFor(cat, name);
            b.preferredOccupants = DefaultOccupants(cat);
            b.attractionWeight = ColonyStructure.IsWorkshopCategory(cat) ? 1.4f : 1f;
            b.buildCost = power > 0
                ? new[]
                {
                    new ResourceAmount(ResourceId.Metals, metals),
                    new ResourceAmount(ResourceId.Power, power)
                }
                : new[] { new ResourceAmount(ResourceId.Metals, metals) };
            b.prefab = BuildingVisualCatalog.LoadPrefab(cat);
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
                CreateBuilding("Airlock Junction", BuildingCategory.Utility, 8, 0, 4f, 2, 2),
                CreateBuilding("Greenhouse Farm", BuildingCategory.Farm, 28, 4, 10f, 4, 4),
                CreateBuilding("Ore Mine", BuildingCategory.Mine, 32, 4, 12f, 4, 4),
                CreateBuilding("Regolith Camp", BuildingCategory.RegolithCamp, 22, 0, 9f, 4, 4),
                CreateBuilding("Scout Workshop", BuildingCategory.ScoutWorkshop, 36, 4, 12f, 4, 4),
                CreateBuilding("Engineer Workshop", BuildingCategory.EngineerWorkshop, 36, 4, 12f, 4, 4),
                CreateBuilding("Defense Workshop", BuildingCategory.DefenseWorkshop, 38, 5, 12f, 4, 4),
                CreateBuilding("Medic Workshop", BuildingCategory.MedicWorkshop, 34, 4, 12f, 4, 4),
                CreateBuilding("Guild Hall", BuildingCategory.GuildHall, 56, 6, 14f, 4, 4),
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
                        side = 4;
                        break;
                    case BuildingCategory.Utility:
                        side = 2;
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

        public void NotifyBuildingPlaced(BuildingData data, GameObject go, Vector3 world)
        {
            if (data == null) return;
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
            CampusDressing.DressPlaced(data, go, _body);
            CampusDressing.RefreshTubes(Placer, grid, buildingRoot != null ? buildingRoot : transform);
            DemoVfx.BuildComplete(world);
            DemoAudio.PlayBuildComplete();
            if (data.category == BuildingCategory.Commons ||
                data.category == BuildingCategory.LandingPad)
                GlanceAt(world);
            if (data.category == BuildingCategory.LandingPad)
                SyncLaunchGate();
            TryClaimOutpost(world, data.category);
            PersistSession();
        }

        /// <summary>
        /// Extract flag complete: haul through the nearest drop-off. Matching Mine/Farm/Camp/Power
        /// nearby pays ~full; long haul or no site leaks yield. Same-node double-taps saturate.
        /// </summary>
        public void ApplyExtractYield(Vector3 at, ResourceNode node)
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
            Economy.GrantExtractYield(campus, node, eff, via);
        }

        public void NotifySpecialFlag(FlagType type, Vector3 at)
        {
            switch (type)
            {
                case FlagType.ResearchSite:
                    if (Research == null || Research.ActiveTech == TechId.None)
                    {
                        LogOverseer("Research Site logged — pick a tech first.");
                        return;
                    }
                    Research.AddScience(12f);
                    var active = TechCatalog.Get(Research.ActiveTech);
                    string techName = active != null ? active.DisplayName : "tech";
                    LogOverseer($"Research Site: +12 science into {techName}.");
                    break;
                case FlagType.EstablishOutpost:
                    if (Settlement != null && Settlement.HasOutpost)
                    {
                        Resources?.Add(ResourceId.Regolith, 8);
                        LogOverseer("Outpost already claimed — survey dumps +8 REG.");
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
            if (Placer == null) return;
            var slots = CaptureCampusSlots();
            int pop = Settlement != null ? Settlement.Population : 0;
            DemoSettings.WriteCampus(celestialBody, CampusSnapshot.Encode(pop, slots));
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

            slots.Sort((a, b) => CampusSnapshot.Rank(a.Category).CompareTo(CampusSnapshot.Rank(b.Category)));

            Vector3 glance = Vector3.zero;
            bool glanceSet = false;
            int restored = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (RestoreCampusSlot(slots[i]))
                {
                    restored++;
                    if (!glanceSet && slots[i].Category == BuildingCategory.Commons)
                    {
                        glance = FootprintWorldCenter(new Vector2Int(slots[i].X, slots[i].Y), slots[i].W, slots[i].H);
                        glanceSet = true;
                    }
                }
            }

            if (pop > 0 && Settlement != null)
                Settlement.RestorePopulation(pop);

            if (restored <= 0) return;

            NotifyCampusExpanded();
            SyncLaunchGate();
            CampusDressing.RefreshTubes(Placer, grid, buildingRoot != null ? buildingRoot : transform);
            if (glanceSet)
                GlanceAt(glance, force: true);
            LogOverseer($"Campus restored — {restored} modules on {(_body != null ? _body.DisplayName : celestialBody.ToString())}.");
            Debug.Log($"[GameLoop] Restored {restored} campus pieces.");
        }

        private bool RestoreCampusSlot(CampusSlot slot)
        {
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
                    return new[] { SpecialistClass.Medic };
                case BuildingCategory.Habitat:
                    return null;
                case BuildingCategory.DefenseWorkshop:
                case BuildingCategory.Defense:
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
            if (SelectedStructure == st)
                SelectedStructure = null;
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
            float bounty = _flagInput.Bounty > 10f ? _flagInput.Bounty : 80f;
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
            DemoAudio.PlayClaim();
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
            DemoAudio.PlayClaim();
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
            CampusDressing.RefreshTubes(Placer, grid, buildingRoot);
        }

        private void SpawnShowcaseSet(ColonyLayout.ShowcasePiece[] pieces, Vector3 campusOrigin, string tag)
        {
            if (pieces == null) return;
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                Vector3 world = piece.WorldPositionAt(campusOrigin);
                GameObject go;
                if (!string.IsNullOrEmpty(piece.ResourcesPath) &&
                    piece.ResourcesPath.Contains("ModularTube"))
                {
                    float span = Mathf.Max(piece.FootprintW, piece.FootprintH) *
                                 (grid != null ? grid.CellSize : ColonyLayout.DefaultCellSize);
                    go = ColonyVisualUtility.SpawnPlusConnector(world, buildingRoot, span);
                    go.name = $"Showcase{tag}_{i}_Plus";
                    CampusNavMesh.AddObstacle(go);
                }
                else
                {
                    go = SpawnMesh(
                        piece.ResourcesPath,
                        world,
                        $"Showcase{tag}_{i}_{System.IO.Path.GetFileName(piece.ResourcesPath)}",
                        piece.ResolveScale(),
                        0f);
                }

                if (piece.ReservesCells && Placer != null && grid != null)
                {
                    Vector2Int origin = ReserveShowcaseFootprint(world, piece.FootprintW, piece.FootprintH);
                    BuildingCategory pieceCat = BuildingCategory.Utility;
                    if (!string.IsNullOrEmpty(piece.ResourcesPath))
                    {
                        if (piece.ResourcesPath.Contains("ModularTube"))
                            pieceCat = BuildingCategory.Utility;
                        else if (piece.ResourcesPath.Contains("CommandDome"))
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
                        else
                            pieceCat = BuildingCategory.Utility;
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
            if (resourcesPath.Contains("ModularTube") || resourcesPath.Contains("Starship"))
                return;

            BuildingCategory cat = BuildingCategory.Utility;
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
    }
}
