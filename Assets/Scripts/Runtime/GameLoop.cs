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
        [SerializeField] private FlagData exploreFlagData;
        [SerializeField] private FlagData clearThreatFlagData;
        [SerializeField] private FlagData buildFlagData;
        [SerializeField] private FlagData extractFlagData;
        [SerializeField] private FlagData defendFlagData;
        [SerializeField] private BuildingData[] starterBuildings;

        [Header("Slice settings")]
        [SerializeField] private OverseerTool activeTool = OverseerTool.None;
        [SerializeField] private Vector3 specialistSpawnOffset = new Vector3(24f, 0f, 12f);
        [SerializeField] private bool seedStartingResources = true;
        [SerializeField] private bool spawnFullParty = true;
        [Tooltip("Campus A party size (1–4). Slot 4 is the Medic.")]
        [SerializeField] [Range(1, 4)] private int partySize = 4;

        public const int MaxPartySize = 4;

        [Header("Phase 1.6 Threat")]
        [SerializeField] private bool spawnDustStalkers = true;
        [SerializeField] private int dustStalkerCount = 2;
        [SerializeField] private float stalkerSpawnRadius = 14f;
        [SerializeField] private bool spawnSecondBody = true;
        [SerializeField] private int campusBStalkerCount = 2;
        [Tooltip("Phase 5E: spawn a Scout detachment at Campus B (shared brain, local threat).")]
        [SerializeField] private bool spawnCampusBDetachment = true;

        [Header("Demo greybox visuals")]
        [SerializeField] private bool spawnGroundPlane = true;
        [SerializeField] private bool spawnShowcaseColony = true;

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
        private bool _launchCraftStaged;

        public MissionController Mission => _mission;

        /// <summary>HUD helper for hold timer display.</summary>
        public string FormatHold(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private void Awake()
        {
            CampaignProgress.Ensure();
            celestialBody = BodySeed.LoadSavedBody();
            if (!CampaignProgress.IsUnlocked(celestialBody))
                celestialBody = CelestialBodyId.Earth;
            _body = CelestialBodyCatalog.Get(celestialBody);
            BodySeed.Ensure(celestialBody, worldSeedOverride);

            EnsureSceneRefs();
            BuildPureSystems();
            EnsureContent();
            WireInputDrivers();
            ConfigureCamera();
            Village = GetComponent<VillageExpansion>();
            if (Village == null) Village = gameObject.AddComponent<VillageExpansion>();
            Village.Bind(this);
            SpawnShowcaseColony();
            SpawnEarthStarterCamps();
            GenerateWorld();
            SpawnParty();
            SpawnThreats();
            EnsureNavMesh();
            DemoAtmosphere.Apply(mainCamera, transform, _body);
            PlanetaryMapDressing.Apply(transform, grid, _body);
            KingdomLife.Dress(transform);
            EnsureHud();
            EnsureMission();
            LaunchSite.ClearSession();
            _launchCraftStaged = false;
            BootstrapResearch();
            SyncLaunchGate();
            DemoAudio.Ensure();
            DemoAudio.SetCampusAmbient(0);

            Debug.Log($"[GameLoop] Demo ready — {_body.DisplayName} seed={BodySeed.Current}.");
        }

        /// <summary>
        /// Earth tutorial: starter Farm + Mine so sustain is learnable without hunting the build menu first.
        /// </summary>
        private void SpawnEarthStarterCamps()
        {
            if (celestialBody != CelestialBodyId.Earth || buildingRoot == null) return;

            SpawnStarterCamp(
                BuildingCategory.Farm,
                ColonyLayout.CampusOrigin + new Vector3(-10f, 0f, -18f),
                "EarthFarm");
            SpawnStarterCamp(
                BuildingCategory.Mine,
                ColonyLayout.CampusOrigin + new Vector3(10f, 0f, -18f),
                "EarthMine");

            if (Resources != null)
            {
                Resources.Add(ResourceId.WaterIce, 10);
                Resources.Add(ResourceId.Metals, 8);
                Resources.Add(ResourceId.Regolith, 12);
            }
        }

        private void SpawnStarterCamp(BuildingCategory cat, Vector3 world, string name)
        {
            GameObject prefab = BuildingVisualCatalog.LoadPrefab(cat);
            GameObject go;
            float scale = ColonyLayout.ScaleForCategory(cat);
            if (prefab != null)
            {
                go = ColonyVisualUtility.InstantiateOriented(prefab, world, buildingRoot);
                go.transform.localScale = Vector3.one * scale;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(buildingRoot, true);
                go.transform.position = world + Vector3.up * (0.75f * scale);
                go.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f) * scale;
            }

            go.name = name;
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            CampusNavMesh.AddObstacle(go);

            var data = FindBuildingData(cat);
            if (data != null)
                NotifyBuildingPlaced(data, go, world);
            else
                Village?.RegisterPlacedBuilding(null, cat, go, world);
        }

        private BuildingData FindBuildingData(BuildingCategory cat)
        {
            if (starterBuildings == null) return null;
            for (int i = 0; i < starterBuildings.Length; i++)
            {
                if (starterBuildings[i] != null && starterBuildings[i].category == cat)
                    return starterBuildings[i];
            }
            return null;
        }

        private void BootstrapResearch()
        {
            if (Research == null) return;
            if (Research.ActiveTech == TechId.None && Research.CanSelect(TechId.FieldSurvey))
                Research.TrySelect(TechId.FieldSurvey);
        }

        private void Update()
        {
            HandleToolHotkeys();
            HandleSelection();
            PushThreatToSpecialists();
            _world?.TickLairs();
            Village?.Tick(Time.deltaTime);
            Settlement?.Tick(Time.deltaTime);
            TickResearch(Time.deltaTime);
            _mission?.Tick();

            _constructionTick += Time.deltaTime;
            if (_constructionTick >= 0.25f)
            {
                Placer?.TickConstruction(_constructionTick);

                var living = new List<SpecialistData>(_agents.Count);
                for (int i = 0; i < _agents.Count; i++)
                {
                    if (_agents[i] != null && _agents[i].Data != null)
                        living.Add(_agents[i].Data);
                }
                Economy?.Tick(_constructionTick, living);
                _constructionTick = 0f;
            }

            // Prune destroyed stalkers from list
            for (int i = _stalkers.Count - 1; i >= 0; i--)
            {
                if (_stalkers[i] == null)
                    _stalkers.RemoveAt(i);
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
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                var col = new Color(0.48f, 0.44f, 0.38f);
                if (rend.material.HasProperty("_BaseColor"))
                    rend.material.SetColor("_BaseColor", col);
                else if (rend.material.HasProperty("_Color"))
                    rend.material.color = col;
            }
        }

        private void BuildPureSystems()
        {
            Resources = new ResourceManager();
            if (seedStartingResources)
            {
                Resources.Set(ResourceId.Regolith, 80);
                Resources.Set(ResourceId.WaterIce, 40);
                Resources.Set(ResourceId.Metals, 200);
                Resources.Set(ResourceId.Power, 80);
            }

            Flags = new FlagManager();
            Placer = new BuildingPlacer(Resources);
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

                    if (BuildingPlacer.RequiresCampusLink(data.category) &&
                        !Placer.TouchesCampus(cell, data.footprintWidth, data.footprintHeight))
                        return false;

                    return true;
                };
            }

            Brain = new SpecialistBrain();
            Economy = new SimpleEconomy(Resources);
            Settlement = new Settlement(Resources);
            Research = new ResearchManager(Resources);
            Research.TechUnlocked += OnTechUnlocked;
            Threat = new ThreatPressure { Ambient = 0.18f };
        }

        private void OnTechUnlocked(TechId id)
        {
            SyncLaunchGate();
            Debug.Log($"[GameLoop] Tech unlocked: {id}");
        }

        private void SyncLaunchGate()
        {
            if (_mission == null || Research == null) return;
            if (!Research.HasLaunchUnlockFor(celestialBody)) return;

            bool wasReady = _mission.LaunchReady;
            _mission.SetLaunchReady(true);
            if (!_launchCraftStaged)
            {
                _launchCraftStaged = true;
                bool heavy = celestialBody != CelestialBodyId.Earth;
                LaunchSite.EnsureReady(buildingRoot != null ? buildingRoot : transform, heavy);
            }

            if (!wasReady)
                Debug.Log("[GameLoop] Launch gate ready — departure craft staged.");
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

            BindUnitPrefab(scoutData, SpecialistClass.ScoutDrone);
            BindUnitPrefab(engineerData, SpecialistClass.EngineerBot);
            BindUnitPrefab(defenseData, SpecialistClass.DefenseMech);
            BindUnitPrefab(medicData, SpecialistClass.Medic);

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
                    ?? CreateFlag(FlagType.DefendArea, "Defend Area", 65, 0.25f, 9f, new Color(0.85f, 0.35f, 1f));

            if (starterBuildings == null || starterBuildings.Length == 0)
            {
                starterBuildings = DemoContentCatalog.LoadStarterBuildings();
                if (starterBuildings == null || starterBuildings.Length == 0)
                {
                    starterBuildings = new[]
                    {
                        CreateBuilding("Landing Pad", BuildingCategory.LandingPad, 40, 5, 10f, 6, 6),
                        CreateBuilding("Hab Module (HAB-1)", BuildingCategory.Habitat, 50, 8, 12f, 4, 4),
                        CreateBuilding("Power Node (PWR-1)", BuildingCategory.Power, 35, 0, 8f, 3, 3),
                        CreateBuilding("Ops Unit (OPS-1)", BuildingCategory.Mining, 45, 6, 14f, 3, 3),
                        CreateBuilding("Lab Module (LAB-1)", BuildingCategory.Laboratory, 55, 10, 14f, 3, 3),
                        CreateBuilding("Command (CMD-1)", BuildingCategory.Defense, 60, 8, 16f, 4, 4)
                    };
                }
            }

            // Bind Blender blockout meshes from Resources (no Inspector wiring required).
            for (int i = 0; i < starterBuildings.Length; i++)
            {
                if (starterBuildings[i] != null && starterBuildings[i].prefab == null)
                    starterBuildings[i].prefab = BuildingVisualCatalog.LoadPrefab(starterBuildings[i].category);
            }

            starterBuildings = AppendEconomyBuildings(starterBuildings);
            ForceCardinalFootprints(starterBuildings);
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
                flagRoot);
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

            // Plaza south of the dome — same campus as buildings (not a random corner).
            Vector3 origin = ColonyLayout.PartySpawn;
            if (specialistSpawn != null)
                specialistSpawn.position = origin;

            int size = spawnFullParty ? Mathf.Clamp(partySize, 1, MaxPartySize) : 1;

            // Scout — cyan, curious, moderate greed
            Agent = SpawnOne(scoutData, origin + new Vector3(0f, 0f, 0f), new Color(0.35f, 0.85f, 1f));
            _agents.Add(Agent);

            if (size >= 2)
            {
                // Engineer — orange, greedy builder, cautious
                _agents.Add(SpawnOne(engineerData, origin + new Vector3(1.8f, 0f, 0.4f), new Color(1f, 0.55f, 0.15f)));
            }

            if (size >= 3)
            {
                // Defense — red, brave combat, less greedy
                _agents.Add(SpawnOne(defenseData, origin + new Vector3(-1.8f, 0f, 0.4f), new Color(0.85f, 0.22f, 0.22f)));
            }

            if (size >= 4)
            {
                _agents.Add(SpawnOne(medicData, origin + new Vector3(0f, 0f, 1.6f), new Color(0.92f, 0.96f, 1f)));
            }

            // Phase 5E: Campus B Scout only if party still has a free slot (cap = 4).
            if (spawnCampusBDetachment && spawnSecondBody && _agents.Count < MaxPartySize)
            {
                var bScout = SpawnOne(
                    scoutData,
                    ColonyLayout.PartySpawnB,
                    new Color(0.45f, 0.95f, 1f));
                if (bScout != null)
                {
                    bScout.gameObject.name = "Specialist_ScoutDrone_CampusB";
                    _agents.Add(bScout);
                    Debug.Log("[GameLoop] Campus B Scout detachment deployed.");
                }
            }

            Debug.Log($"[GameLoop] Party size {_agents.Count}/{MaxPartySize}.");
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

            GameObject prefab = data != null ? data.prefab : null;
            if (prefab == null && data != null)
                prefab = DemoContentCatalog.LoadUnitPrefab(data.specialistClass);

            if (prefab != null)
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
            if (_campusNav == null || grid == null) return;
            _campusNav.Build(grid);
            for (int i = 0; i < _agents.Count; i++)
                _agents[i]?.BindNavMesh(_campusNav);
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
            ReloadActiveScene();
        }

        /// <summary>Same-body reseed (sandbox rematch).</summary>
        public void BeginNextConquest()
        {
            BodySeed.AdvanceForNextConquest();
            DemoAudio.PlayRetry();
            ReloadActiveScene();
        }

        /// <summary>Campaign advance: unlock next body and travel there with a fresh seed.</summary>
        public void AdvanceCampaign()
        {
            Vector3 pad = ColonyLayout.CampusOrigin + new Vector3(16f, 0f, 0f);
            LaunchSite.PlayDeparture(pad);
            CampaignProgress.UnlockNextFrom(celestialBody);
            var next = CampaignProgress.NextAfter(celestialBody);
            if (!next.HasValue)
            {
                BeginNextConquest();
                return;
            }

            BodySeed.SetBody(next.Value);
            BodySeed.Ensure(next.Value, 0);
            BodySeed.AdvanceForNextConquest();
            DemoAudio.PlayRetry();
            ReloadActiveScene();
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
            BodySeed.SetBody(body);
            DemoAudio.PlayRetry();
            ReloadActiveScene();
        }

        private static void ReloadActiveScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }

        private void HandleToolHotkeys()
        {
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

            if (Input.GetKeyDown(KeyCode.F10))
            {
                // Debug cheat: cycle any body. Hold Shift to also unlock all.
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    CampaignProgress.DebugUnlockAll();
                SelectBody(CelestialBodyCatalog.Next(celestialBody), allowLocked: true);
            }

            if (Input.GetKeyDown(KeyCode.P))
                FormPartyAtInn();
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
                Flags.Post(exploreFlagData, world, bounty);
                DemoAudio.PlayFlagPost();
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

        /// <summary>Scout: high explore, moderate greed, mid courage.</summary>
        public static SpecialistData CreateScout()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.ScoutDrone;
            s.displayName = "Scout Drone";
            s.baseGreed = 0.40f;
            s.courage = 0.55f;
            s.workaholicBias = 0.30f;
            s.explorePreference = 0.95f;
            s.buildPreference = 0.20f;
            s.combatPreference = 0.25f;
            s.extractPreference = 0.45f;
            s.moveSpeed = 4.4f;
            s.workRate = 1.0f;
            return s;
        }

        /// <summary>Engineer: high build + high greed, low courage.</summary>
        public static SpecialistData CreateEngineer()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.EngineerBot;
            s.displayName = "Engineer Bot";
            s.baseGreed = 0.85f;       // picky about pay
            s.courage = 0.25f;         // avoids risky ClearThreat
            s.workaholicBias = 0.70f;  // resists resting
            s.explorePreference = 0.20f;
            s.buildPreference = 0.95f;
            s.combatPreference = 0.15f;
            s.extractPreference = 0.70f;
            s.moveSpeed = 3.1f;
            s.workRate = 1.35f;
            return s;
        }

        /// <summary>Defense: high combat + high courage, lower greed.</summary>
        public static SpecialistData CreateDefense()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.DefenseMech;
            s.displayName = "Defense Mech";
            s.baseGreed = 0.35f;       // will take cheaper combat jobs
            s.courage = 0.90f;
            s.workaholicBias = 0.50f;
            s.explorePreference = 0.25f;
            s.buildPreference = 0.15f;
            s.combatPreference = 0.95f;
            s.extractPreference = 0.20f;
            s.moveSpeed = 3.0f;
            s.workRate = 1.15f;
            return s;
        }

        /// <summary>Medic: low combat, inn triage, heals wounded specialists in the field.</summary>
        public static SpecialistData CreateMedic()
        {
            var s = ScriptableObject.CreateInstance<SpecialistData>();
            s.specialistClass = SpecialistClass.Medic;
            s.displayName = "Medic";
            s.baseGreed = 0.30f;
            s.courage = 0.45f;
            s.workaholicBias = 0.55f;
            s.explorePreference = 0.35f;
            s.buildPreference = 0.10f;
            s.combatPreference = 0.08f;
            s.extractPreference = 0.15f;
            s.moveSpeed = 3.6f;
            s.workRate = 1.1f;
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
            b.powerDraw = power > 0 ? 2 : 0;
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

        private static BuildingData[] AppendEconomyBuildings(BuildingData[] current)
        {
            var extra = new[]
            {
                CreateBuilding("Plus Connector", BuildingCategory.Utility, 8, 0, 4f, 2, 2),
                CreateBuilding("Greenhouse Farm", BuildingCategory.Farm, 28, 4, 10f, 3, 3),
                CreateBuilding("Ore Mine", BuildingCategory.Mine, 32, 4, 12f, 3, 3),
                CreateBuilding("Regolith Camp", BuildingCategory.RegolithCamp, 22, 0, 9f, 3, 3),
                CreateBuilding("Scout Workshop", BuildingCategory.ScoutWorkshop, 36, 4, 12f, 3, 3),
                CreateBuilding("Engineer Workshop", BuildingCategory.EngineerWorkshop, 36, 4, 12f, 3, 3),
                CreateBuilding("Defense Workshop", BuildingCategory.DefenseWorkshop, 38, 5, 12f, 3, 3)
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
                    case BuildingCategory.Habitat:
                    case BuildingCategory.Defense:
                    case BuildingCategory.Inn:
                        side = 4;
                        break;
                    case BuildingCategory.Utility:
                        side = 2;
                        break;
                    case BuildingCategory.LandingPad:
                        side = 6;
                        break;
                    default:
                        side = 3;
                        break;
                }

                b.footprintWidth = side;
                b.footprintHeight = side;
            }
        }

        public void NotifyBuildingPlaced(BuildingData data, GameObject go, Vector3 world)
        {
            if (data == null) return;
            Village?.RegisterPlacedBuilding(data, data.category, go, world);
        }

        private static SpecialistClass[] DefaultOccupants(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.ScoutWorkshop:
                case BuildingCategory.Laboratory:
                case BuildingCategory.LandingPad:
                    return new[] { SpecialistClass.ScoutDrone };
                case BuildingCategory.Habitat:
                    return new[] { SpecialistClass.Medic };
                case BuildingCategory.DefenseWorkshop:
                case BuildingCategory.Defense:
                    return new[] { SpecialistClass.DefenseMech };
                case BuildingCategory.EngineerWorkshop:
                case BuildingCategory.Farm:
                case BuildingCategory.Mine:
                case BuildingCategory.RegolithCamp:
                case BuildingCategory.Mining:
                    return new[] { SpecialistClass.EngineerBot };
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

        /// <summary>Majesty inn party: specialists at the waystation form a group (max 4).</summary>
        public void FormPartyAtInn()
        {
            var atInn = new List<SpecialistAgent>(4);
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null || !a.IsAlive) continue;
                if (FlatDist(a.transform.position, ColonyLayout.InnOutpost) > 6.5f) continue;
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
                GameObject go;
                if (!string.IsNullOrEmpty(piece.ResourcesPath) &&
                    piece.ResourcesPath.Contains("ModularTube"))
                {
                    go = ColonyVisualUtility.SpawnPlusConnector(world, buildingRoot, piece.ResolveScale());
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
                    ReserveShowcaseFootprint(world, piece.FootprintW, piece.FootprintH);

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
                cat = BuildingCategory.Defense;
            else if (resourcesPath.Contains("OPS")) cat = BuildingCategory.Mining;
            else if (resourcesPath.Contains("PWR") || resourcesPath.Contains("Solar"))
                cat = BuildingCategory.Power;
            else if (resourcesPath.Contains("LandingPad")) cat = BuildingCategory.LandingPad;
            else return;

            StructureRole role = StructureRole.Core;
            var st = go.GetComponent<ColonyStructure>() ?? go.AddComponent<ColonyStructure>();
            st.Configure(role, Village, 64f, cat);
            Village.RegisterShowcase(st);
        }

        private void ReserveShowcaseFootprint(Vector3 world, int footprintW, int footprintH)
        {
            float cell = grid.CellSize;
            float halfW = (footprintW * cell) * 0.5f;
            float halfH = (footprintH * cell) * 0.5f;
            Vector3 corner = world - new Vector3(halfW, 0f, halfH) + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            Vector2Int origin = grid.WorldToCell(corner);
            Placer.MarkCampusRect(origin, footprintW, footprintH);
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
