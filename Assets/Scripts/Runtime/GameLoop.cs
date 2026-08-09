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
        [SerializeField] private FlagData exploreFlagData;
        [SerializeField] private FlagData clearThreatFlagData;
        [SerializeField] private FlagData buildFlagData;
        [SerializeField] private FlagData extractFlagData;
        [SerializeField] private FlagData defendFlagData;
        [SerializeField] private BuildingData[] starterBuildings;

        [Header("Slice settings")]
        [SerializeField] private OverseerTool activeTool = OverseerTool.Flag;
        [SerializeField] private Vector3 specialistSpawnOffset = new Vector3(24f, 0f, 12f);
        [SerializeField] private bool seedStartingResources = true;
        [SerializeField] private bool spawnFullParty = true;

        [Header("Phase 1.6 Threat")]
        [SerializeField] private bool spawnDustStalkers = true;
        [SerializeField] private int dustStalkerCount = 2;
        [SerializeField] private float stalkerSpawnRadius = 14f;

        [Header("Demo greybox visuals")]
        [SerializeField] private bool spawnGroundPlane = true;
        [SerializeField] private bool spawnShowcaseColony = true;

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
        public float CurrentThreatPressure => Threat != null ? Threat.Current : 0f;

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
        private readonly List<DustStalkerAgent> _stalkers = new List<DustStalkerAgent>();
        private FlagPlacementInput _flagInput;
        private BuildingPlacementInput _buildInput;
        private IsometricCameraController _isoCam;
        private Transform _threatRoot;
        private float _constructionTick;
        private DebugHud _debugHud;
        private OverseerHud _overseerHud;
        private CampusNavMesh _campusNav;
        private MissionController _mission;

        public MissionController Mission => _mission;

        /// <summary>HUD helper for hold timer display.</summary>
        public string FormatHold(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private void Awake()
        {
            EnsureSceneRefs();
            BuildPureSystems();
            EnsureContent();
            WireInputDrivers();
            ConfigureCamera();
            SpawnParty();
            SpawnThreats();
            SpawnShowcaseColony();
            EnsureNavMesh();
            DemoAtmosphere.Apply(mainCamera, transform);
            EnsureHud();
            EnsureMission();
            DemoAudio.Ensure();

            Debug.Log("[GameLoop] Demo ready — Phase 5A map/deadline/ambient.");
        }

        private void Update()
        {
            HandleToolHotkeys();
            PushThreatToSpecialists();
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

        /// <summary>Each frame: ThreatPressure.Current → SpecialistAgent.bodyDanger for brain risk term.</summary>
        private void PushThreatToSpecialists()
        {
            if (Threat == null) return;
            float danger = Threat.Current;
            // Phase 4B: claimed DefendArea flags calm the outpost while Defense works them.
            if (HasActiveDefendClaim())
                danger = Mathf.Clamp01(danger * 0.55f);

            for (int i = 0; i < _agents.Count; i++)
                _agents[i]?.SetBodyDanger(danger);
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

            if (spawnGroundPlane && GameObject.Find("GroundPlane") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "GroundPlane";
                ground.transform.SetParent(transform);
                // Center under campus; default plane is 10×10 units at scale 1.
                ground.transform.position = ColonyLayout.CampusOrigin;
                // Cover expanded IsoGrid (~56×56 × 1.5) with margin.
                ground.transform.localScale = new Vector3(10f, 1f, 10f);
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
                    return true;
                };
            }

            Brain = new SpecialistBrain();
            Economy = new SimpleEconomy(Resources);
            Threat = new ThreatPressure { Ambient = 0.18f };
        }

        private void EnsureContent()
        {
            // Prefer authored Resources/DemoContent assets; factories remain Play-safe fallback.
            if (scoutData == null) scoutData = DemoContentCatalog.LoadScout() ?? CreateScout();
            if (engineerData == null) engineerData = DemoContentCatalog.LoadEngineer() ?? CreateEngineer();
            if (defenseData == null) defenseData = DemoContentCatalog.LoadDefense() ?? CreateDefense();

            BindUnitPrefab(scoutData, SpecialistClass.ScoutDrone);
            BindUnitPrefab(engineerData, SpecialistClass.EngineerBot);
            BindUnitPrefab(defenseData, SpecialistClass.DefenseMech);

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
                        CreateBuilding("Hab Module (HAB-1)", BuildingCategory.Habitat, 50, 8, 12f, 4, 3),
                        CreateBuilding("Power Node (PWR-1)", BuildingCategory.Power, 35, 0, 8f, 3, 3),
                        CreateBuilding("Ops Unit (OPS-1)", BuildingCategory.Mining, 45, 6, 14f, 3, 3),
                        CreateBuilding("Lab Module (LAB-1)", BuildingCategory.Laboratory, 55, 10, 14f, 3, 2),
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
            ApplyTool(activeTool);
        }

        private void ConfigureCamera()
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = ColonyLayout.CameraOrthoSize;
            mainCamera.nearClipPlane = 0.3f;
            mainCamera.farClipPlane = 500f;
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
                    float maxX = grid.Width * grid.CellSize + 8f;
                    float maxZ = grid.Height * grid.CellSize + 8f;
                    _isoCam.SetPanBounds(new Vector2(-8f, -8f), new Vector2(maxX, maxZ));
                }
                _isoCam.FocusOn(focus, ColonyLayout.CameraOrthoSize);
                _isoCam.SnapToTarget();
            }
        }

        private void SpawnParty()
        {
            _agents.Clear();
            // Plaza south of the dome — same campus as buildings (not a random corner).
            Vector3 origin = ColonyLayout.PartySpawn;
            if (specialistSpawn != null)
                specialistSpawn.position = origin;

            // Scout — cyan, curious, moderate greed
            Agent = SpawnOne(scoutData, origin + new Vector3(0f, 0f, 0f), new Color(0.35f, 0.85f, 1f));
            _agents.Add(Agent);

            if (!spawnFullParty) return;

            // Engineer — orange, greedy builder, cautious
            _agents.Add(SpawnOne(engineerData, origin + new Vector3(1.8f, 0f, 0.4f), new Color(1f, 0.55f, 0.15f)));

            // Defense — red, brave combat, less greedy
            _agents.Add(SpawnOne(defenseData, origin + new Vector3(-1.8f, 0f, 0.4f), new Color(0.85f, 0.22f, 0.22f)));
        }

        private SpecialistAgent SpawnOne(SpecialistData data, Vector3 pos, Color tint)
        {
            GameObject go;
            GameObject prefab = data != null ? data.prefab : null;
            if (prefab == null && data != null)
                prefab = DemoContentCatalog.LoadUnitPrefab(data.specialistClass);

            if (prefab != null)
            {
                go = Instantiate(prefab, pos, Quaternion.identity, transform);
            }
            else
            {
                // Runtime silhouette fallback if prefabs not generated yet.
                go = data != null
                    ? UnitPlaceholderFactory.BuildForClass(data.specialistClass)
                    : UnitPlaceholderFactory.BuildScout();
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
            }

            var agent = go.GetComponent<SpecialistAgent>();
            if (agent == null) agent = go.AddComponent<SpecialistAgent>();
            agent.Initialize(data, Flags, Brain, Economy, tint, Placer, _campusNav);
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

        private void SpawnThreats()
        {
            _stalkers.Clear();
            if (!spawnDustStalkers || dustStalkerCount <= 0 || Threat == null)
                return;

            SpawnStalkerWave(dustStalkerCount, Mathf.Max(14f, stalkerSpawnRadius));
            Debug.Log($"[GameLoop] Spawned {_stalkers.Count} Dust Stalker(s). Post ClearThreat (F2) near them to defeat.");
        }

        /// <summary>Spawns additional stalkers for mission wave 2. Returns count spawned.</summary>
        public int SpawnStalkerWave(int count, float radius)
        {
            if (count <= 0 || Threat == null) return 0;

            Vector3 origin = ColonyLayout.CampusOrigin;
            float r = Mathf.Max(10f, radius);
            GameObject stalkerPrefab = DemoContentCatalog.LoadStalkerPrefab();
            int spawned = 0;
            float phase = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < count; i++)
            {
                float angle = phase + (Mathf.PI * 2f * i) / count;
                Vector3 pos = origin + new Vector3(
                    Mathf.Cos(angle) * r,
                    0f,
                    Mathf.Sin(angle) * r);
                Vector3 home = pos + Vector3.up * 0.2f;

                GameObject go;
                if (stalkerPrefab != null)
                {
                    go = Object.Instantiate(stalkerPrefab, home, Quaternion.identity,
                        _threatRoot != null ? _threatRoot : transform);
                }
                else
                {
                    go = UnitPlaceholderFactory.BuildDustStalker();
                    go.transform.SetParent(_threatRoot != null ? _threatRoot : transform, false);
                    go.transform.position = home;
                }

                var stalker = go.GetComponent<DustStalkerAgent>();
                if (stalker == null) stalker = go.AddComponent<DustStalkerAgent>();
                stalker.Initialize(Threat, Flags, home);
                _stalkers.Add(stalker);
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

        private void EnsureMission()
        {
            _mission = GetComponent<MissionController>();
            if (_mission == null) _mission = gameObject.AddComponent<MissionController>();
            _mission.Bind(this);
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
            DemoAudio.PlayRetry();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }

        private void HandleToolHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                activeTool = activeTool == OverseerTool.Build ? OverseerTool.Flag : OverseerTool.Build;
                ApplyTool(activeTool);
            }

            if (Input.GetKeyDown(KeyCode.B)) ApplyTool(OverseerTool.Build);
            if (Input.GetKeyDown(KeyCode.G)) ApplyTool(OverseerTool.Flag);
            if (Input.GetKeyDown(KeyCode.Q)) ApplyTool(OverseerTool.None);

            if (Input.GetKeyDown(KeyCode.F8) && _debugHud != null)
                _debugHud.ToggleVisible();

            if (Input.GetKeyDown(KeyCode.R))
                DebugFatigueAll(0.92f);
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

        /// <summary>
        /// Coherent campus (see ColonyLayout): dome core, habitat spine, power yard, pad/ship.
        /// Uses Majesty-readable visual scale so modules and specialists share one silhouette language.
        /// </summary>
        private void SpawnShowcaseColony()
        {
            if (!spawnShowcaseColony || buildingRoot == null)
                return;

            for (int i = 0; i < ColonyLayout.Showcase.Length; i++)
            {
                var piece = ColonyLayout.Showcase[i];
                SpawnMesh(
                    piece.ResourcesPath,
                    piece.WorldPosition,
                    $"Showcase_{i}_{System.IO.Path.GetFileName(piece.ResourcesPath)}",
                    piece.ResolveScale(),
                    piece.YawDegrees);

                if (piece.ReservesCells && Placer != null && grid != null)
                    ReserveShowcaseFootprint(piece);
            }
        }

        private void ReserveShowcaseFootprint(ColonyLayout.ShowcasePiece piece)
        {
            // Footprint origin = SW corner of AABB centered on piece world position.
            Vector3 world = piece.WorldPosition;
            float cell = grid.CellSize;
            float halfW = (piece.FootprintW * cell) * 0.5f;
            float halfH = (piece.FootprintH * cell) * 0.5f;
            Vector3 corner = world - new Vector3(halfW, 0f, halfH) + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            Vector2Int origin = grid.WorldToCell(corner);
            Placer.MarkOccupiedRect(origin, piece.FootprintW, piece.FootprintH);
        }

        private void SpawnMesh(string resourcesPath, Vector3 position, string name, float scale, float yawDegrees = 0f)
        {
            GameObject prefab = BuildingVisualCatalog.LoadByPath(resourcesPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameLoop] Missing mesh resource: {resourcesPath}");
                return;
            }

            var go = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yawDegrees, 0f), buildingRoot);
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            ColonyVisualUtility.EnsureUrpMaterials(go);
            CampusNavMesh.AddObstacle(go);
        }
    }
}
