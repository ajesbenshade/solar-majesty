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

        private readonly List<SpecialistAgent> _agents = new List<SpecialistAgent>();
        private readonly List<DustStalkerAgent> _stalkers = new List<DustStalkerAgent>();
        private FlagPlacementInput _flagInput;
        private BuildingPlacementInput _buildInput;
        private IsometricCameraController _isoCam;
        private Transform _threatRoot;
        private float _constructionTick;

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
            EnsureHud();

            Debug.Log("[GameLoop] Demo ready — party, stalkers, mesh colony kit, ThreatPressure → bodyDanger.");
        }

        private void Update()
        {
            HandleToolHotkeys();
            PushThreatToSpecialists();

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
            for (int i = 0; i < _agents.Count; i++)
                _agents[i]?.SetBodyDanger(danger);
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
                ground.transform.localScale = new Vector3(6f, 1f, 6f);
                var rend = ground.GetComponent<Renderer>();
                if (rend != null)
                {
                    var col = new Color(0.42f, 0.34f, 0.28f);
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
            if (scoutData == null) scoutData = CreateScout();
            if (engineerData == null) engineerData = CreateEngineer();
            if (defenseData == null) defenseData = CreateDefense();

            if (exploreFlagData == null)
                exploreFlagData = CreateFlag(FlagType.Explore, "Explore", 40, 0.08f, 4f, new Color(0.3f, 0.85f, 1f));
            if (clearThreatFlagData == null)
                clearThreatFlagData = CreateFlag(FlagType.ClearThreat, "Clear Threat", 80, 0.4f, 6f, new Color(1f, 0.3f, 0.25f));
            if (buildFlagData == null)
                buildFlagData = CreateFlag(FlagType.Build, "Build Here", 70, 0.1f, 8f, new Color(1f, 0.65f, 0.15f));

            if (starterBuildings == null || starterBuildings.Length == 0)
            {
                // Footprints match demo visual scale (mesh meters × ColonyLayout scale / cellSize).
                starterBuildings = new[]
                {
                    CreateBuilding("Landing Pad", BuildingCategory.LandingPad, 40, 5, 10f, 6, 6),
                    CreateBuilding("Hab Module (HAB-1)", BuildingCategory.Habitat, 50, 8, 12f, 4, 3),
                    CreateBuilding("Power Node (PWR-1)", BuildingCategory.Power, 35, 0, 8f, 3, 3),
                    CreateBuilding("Ops Unit (OPS-1)", BuildingCategory.Mining, 45, 6, 14f, 3, 3)
                };
            }

            // Bind Blender blockout meshes from Resources (no Inspector wiring required).
            for (int i = 0; i < starterBuildings.Length; i++)
            {
                if (starterBuildings[i] != null && starterBuildings[i].prefab == null)
                    starterBuildings[i].prefab = BuildingVisualCatalog.LoadPrefab(starterBuildings[i].category);
            }
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
            if (data.prefab != null)
            {
                go = Instantiate(data.prefab, pos, Quaternion.identity, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(transform);
                // Capsule default height 2; scale to ~2.4m tall readable hero next to scaled modules.
                go.transform.position = pos + Vector3.up * 1.2f;
                go.transform.localScale = new Vector3(0.85f, 1.2f, 0.85f);
            }

            var agent = go.GetComponent<SpecialistAgent>();
            if (agent == null) agent = go.AddComponent<SpecialistAgent>();
            agent.Initialize(data, Flags, Brain, Economy, tint);
            return agent;
        }

        private void SpawnThreats()
        {
            _stalkers.Clear();
            if (!spawnDustStalkers || dustStalkerCount <= 0 || Threat == null)
                return;

            // Ring just outside the campus so threat is visible without sitting on the plaza.
            Vector3 origin = ColonyLayout.CampusOrigin;
            float radius = Mathf.Max(14f, stalkerSpawnRadius);

            for (int i = 0; i < dustStalkerCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / dustStalkerCount + 0.65f;
                Vector3 pos = origin + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(_threatRoot != null ? _threatRoot : transform);
                go.transform.localScale = new Vector3(1.4f, 0.6f, 1.6f);
                Object.Destroy(go.GetComponent<Collider>());

                var stalker = go.AddComponent<DustStalkerAgent>();
                Vector3 home = pos + Vector3.up * 0.35f;
                stalker.Initialize(Threat, Flags, home);
                _stalkers.Add(stalker);
            }

            Debug.Log($"[GameLoop] Spawned {_stalkers.Count} Dust Stalker(s). Post ClearThreat (F2) near them to defeat.");
        }

        private void EnsureHud()
        {
            var hud = GetComponent<DebugHud>();
            if (hud == null) hud = gameObject.AddComponent<DebugHud>();
            hud.Bind(this);
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
            }
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
        }
    }
}
