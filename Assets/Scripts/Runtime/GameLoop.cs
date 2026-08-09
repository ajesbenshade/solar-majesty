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
        [SerializeField] private Vector3 specialistSpawnOffset = new Vector3(8f, 0f, 8f);
        [SerializeField] private bool seedStartingResources = true;
        [SerializeField] private bool spawnFullParty = true;

        // Pure systems
        public ResourceManager Resources { get; private set; }
        public FlagManager Flags { get; private set; }
        public BuildingPlacer Placer { get; private set; }
        public SpecialistBrain Brain { get; private set; }
        public SimpleEconomy Economy { get; private set; }

        // Drivers
        public SpecialistAgent Agent { get; private set; } // first / primary (Scout)
        public IReadOnlyList<SpecialistAgent> Agents => _agents;
        public OverseerTool ActiveTool => activeTool;
        public float FlagBounty => _flagInput != null ? _flagInput.Bounty : 0f;

        private readonly List<SpecialistAgent> _agents = new List<SpecialistAgent>();
        private FlagPlacementInput _flagInput;
        private BuildingPlacementInput _buildInput;
        private IsometricCameraController _isoCam;
        private float _constructionTick;

        private void Awake()
        {
            EnsureSceneRefs();
            BuildPureSystems();
            EnsureContent();
            WireInputDrivers();
            ConfigureCamera();
            SpawnParty();
            EnsureHud();

            Debug.Log("[GameLoop] Phase 1.5 ready — Scout / Engineer / Defense autonomous party.");
        }

        private void Update()
        {
            HandleToolHotkeys();

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
                Placer.ExtraPlacementRule = (cell, data) => grid.InBounds(cell);

            Brain = new SpecialistBrain();
            Economy = new SimpleEconomy(Resources);
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
                starterBuildings = new[]
                {
                    CreateBuilding("Landing Pad", BuildingCategory.LandingPad, 40, 5, 10f),
                    CreateBuilding("Hab Module", BuildingCategory.Habitat, 50, 8, 12f),
                    CreateBuilding("Power Node", BuildingCategory.Power, 35, 0, 8f),
                    CreateBuilding("Mining Outpost", BuildingCategory.Mining, 45, 6, 14f)
                };
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
            mainCamera.orthographicSize = 14f;
            mainCamera.transform.position = new Vector3(-16f, 20f, -16f);
            mainCamera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            if (mainCamera.GetComponent<AudioListener>() == null)
                mainCamera.gameObject.AddComponent<AudioListener>();
        }

        private void SpawnParty()
        {
            _agents.Clear();
            Vector3 origin = specialistSpawn != null ? specialistSpawn.position : specialistSpawnOffset;

            // Scout — cyan, curious, moderate greed
            Agent = SpawnOne(scoutData, origin + new Vector3(0f, 0f, 0f), new Color(0.35f, 0.85f, 1f));
            _agents.Add(Agent);

            if (!spawnFullParty) return;

            // Engineer — orange, greedy builder, cautious
            _agents.Add(SpawnOne(engineerData, origin + new Vector3(2.2f, 0f, 0.5f), new Color(1f, 0.55f, 0.15f)));

            // Defense — red, brave combat, less greedy
            _agents.Add(SpawnOne(defenseData, origin + new Vector3(-2.2f, 0f, 0.5f), new Color(0.85f, 0.22f, 0.22f)));
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
                go.transform.position = pos;
                go.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            }

            var agent = go.GetComponent<SpecialistAgent>();
            if (agent == null) agent = go.AddComponent<SpecialistAgent>();
            agent.Initialize(data, Flags, Brain, Economy, tint);
            return agent;
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

        private static BuildingData CreateBuilding(string name, BuildingCategory cat, int metals, int power, float time)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.displayName = name;
            b.category = cat;
            b.footprintWidth = 1;
            b.footprintHeight = 1;
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
            return b;
        }
    }
}
