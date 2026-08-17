using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SolarMajesty
{
    /// <summary>
    /// Thin runtime driver for one autonomous specialist.
    /// Decisions come only from SpecialistBrain — the player never path-commands this unit.
    /// </summary>
    public class SpecialistAgent : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SpecialistData data;

        [Header("Think")]
        [SerializeField] private float thinkIntervalMin = 0.4f;
        [SerializeField] private float thinkIntervalMax = 0.6f;
        [SerializeField] private float bodyDanger = 0.3f;
        [SerializeField] private bool logDecisions = true;

        [Header("Movement / work")]
        [SerializeField] private float arriveDistance = 1.1f;
        [SerializeField] private float idleWanderRadius = 2.5f;
        [SerializeField] private bool useNavMesh = true;

        [Header("Needs (0-1)")]
        [SerializeField] [Range(0f, 1f)] private float fatigue;
        [SerializeField] [Range(0f, 1f)] private float healthNormalized = 1f;
        [SerializeField] [Range(0f, 1f)] private float greedHunger = 0.5f;

        [Header("Personal wallet / gear")]
        [SerializeField] private float credits;
        [SerializeField] private ShopItemId equippedSuit = ShopItemId.None;

        [Header("Phase 2A combat")]
        [SerializeField] private float incapacitateThreshold = 0.02f;
        [SerializeField] private float recoverySeconds = 12f;
        [SerializeField] private float restHealPerSecond = 0.08f;

        [Header("Debug force (playtesting only — not player commands)")]
        [SerializeField] private bool debugForceFatigue;
        [SerializeField] [Range(0f, 1f)] private float debugFatigueValue = 0.9f;

        [Header("Presentation")]
        [SerializeField] private Color bodyTint = Color.white;

        private FlagManager _flags;
        private SpecialistBrain _brain;
        private SimpleEconomy _economy;
        private BuildingPlacer _placer;
        private CampusNavMesh _navMesh;
        private PlanetaryWorldGen _world;
        private NavMeshAgent _agent;
        private GameLoop _loop;

        private float _thinkTimer;
        private BrainDecision _lastDecision;
        private FlagHandle _activeFlag;
        private bool _claimedActive;
        private Vector3 _idleTarget;
        private bool _hasIdleTarget;
        private float _restTimer;
        private string _status = "boot";
        private bool _incapacitated;
        private float _recoverTimer;

        private Vector3 _baseScale = Vector3.one;
        private float _workPulse;
        private SpecialistStatusDisplay _statusDisplay;
        private GameObject _selectRing;
        private bool _selected;
        private float _geneTimer;
        private float _geneCourage;
        private float _geneSpeed;
        private float _geneWork;
        private float _shopCooldown;
        private ColonyStructure _repairTarget;
        private bool _radExposed;
        private float _radFlash;
        private bool _radToast;
        private float _stoodUpAt = -999f;
        private bool _scrapRiskLogged;
        private bool _buildLaborHit;
        private float _refusalUntil;
        private float _refusalGate;
        private string _refusalChip;
        private float _terraformerPulseTimer;
        private bool _scrapped;

        public SpecialistData Data => data;
        public BrainDecision LastDecision => _lastDecision;
        public string LastReason => _lastDecision.Reason ?? "none";
        public float LastScore => _lastDecision.Score;
        public float Fatigue => fatigue;
        public float HealthNormalized => healthNormalized;
        public float GreedHunger => greedHunger;
        public float Credits => credits;
        public ShopItemId EquippedSuit => equippedSuit;
        public float GeneSecondsLeft => _geneTimer;
        public string SuitLabel =>
            equippedSuit == ShopItemId.None
                ? "no suit"
                : (ShopCatalog.Get(equippedSuit)?.DisplayName ?? "suit");
        public string Status => _status;
        public string FlavorLine { get; private set; } = "Booting.";
        public SpecialistAction CurrentAction => _lastDecision.Action;
        public FlagHandle ActiveFlag => _activeFlag;
        public float BodyDanger => bodyDanger;
        public bool IsIncapacitated => _incapacitated;
        public bool IsAlive => !_scrapped && (!_incapacitated || healthNormalized > incapacitateThreshold);
        public bool IsClaiming => _claimedActive;
        public int HireMin => OverseerRules.GreedAsk(data);
        public float RecoverSecondsLeft => _incapacitated ? Mathf.Max(0f, _recoverTimer) : 0f;
        public bool ScrapRisk =>
            !_incapacitated && _stoodUpAt > 0f &&
            Time.time - _stoodUpAt < OverseerRules.ScrapWindow &&
            healthNormalized < 0.55f;
        public string RefusalChip =>
            Time.time < _refusalUntil ? _refusalChip : null;
        public bool IsSelected => _selected;
        public HeroParty Party { get; private set; }
        public bool IsPartyLeader => Party != null && Party.IsLeader(this);
        public ColonyStructure Workplace { get; private set; }

        public float EffectiveMoveSpeed =>
            (data != null ? data.moveSpeed : 3.5f) *
            (1f + _geneSpeed + SuitSpeedBonus()) *
            BodyMoveScale();

        public float EffectiveWorkRate
        {
            get
            {
                float r = (data != null ? data.workRate : 1f) * (1f + _geneWork);
                if (_loop != null && _loop.Economy != null && _loop.Economy.PowerShort)
                    r *= OverseerRules.PowerShortWork;
                return r;
            }
        }

        public float EffectiveCourage =>
            Mathf.Clamp01(((data != null ? data.courage : 0.5f) + _geneCourage) * ReplayRules.CourageScale);

        public float ArmorMitigation
        {
            get
            {
                var suit = ShopCatalog.Get(equippedSuit);
                return suit != null ? Mathf.Clamp01(suit.ArmorMitigation) : 0f;
            }
        }

        public void SetParty(HeroParty party) => Party = party;

        public void SetWorkplace(ColonyStructure workplace)
        {
            if (Workplace == workplace) return;
            Workplace?.ClockOut(this);
            Workplace = workplace;
            Workplace?.TryClockIn(this);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_selectRing != null)
                _selectRing.SetActive(selected);
        }

        public void SetBodyDanger(float danger01) => bodyDanger = Mathf.Clamp01(danger01);

        public void ApplyDamage(float amount01, bool feedback = true)
        {
            if (amount01 <= 0f || _incapacitated) return;
            float mitigated = amount01 * (1f - ArmorMitigation);
            healthNormalized = Mathf.Clamp01(healthNormalized - mitigated);
            if (feedback)
            {
                DemoAudio.PlayBite();
                DemoVfx.HitFlash(transform, new Color(1f, 0.25f, 0.2f));
            }
            if (healthNormalized <= incapacitateThreshold)
                EnterIncapacitated();
        }

        public void EarnCredits(float amount, string reason = null)
        {
            if (amount <= 0f) return;
            credits += amount;
            greedHunger = Mathf.Clamp01(greedHunger - Mathf.Clamp01(amount / 120f) * 0.35f);
            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[Credits] {data?.displayName} +${amount:F0} ({reason}) → ${credits:F0}");
        }

        private float SuitSpeedBonus()
        {
            var suit = ShopCatalog.Get(equippedSuit);
            return suit != null ? suit.SpeedBonus : 0f;
        }

        public void ReceiveHeal(float amount01)
        {
            if (amount01 <= 0f) return;
            healthNormalized = Mathf.Clamp01(healthNormalized + amount01);
        }

        public void ReviveFull()
        {
            if (_scrapped) return;
            _incapacitated = false;
            _recoverTimer = 0f;
            healthNormalized = 1f;
            fatigue = 0.1f;
            _status = "revived";
            _stoodUpAt = Time.time;
            IndustrialArtDressing.ClearTintOverlay(gameObject);
            SetAgentStopped(false);
        }

        public void FieldRevive()
        {
            if (_scrapped || !_incapacitated) return;
            _incapacitated = false;
            _recoverTimer = 0f;
            healthNormalized = OverseerRules.ReviveHp;
            fatigue = OverseerRules.ReviveFatigue;
            _status = "field_revive";
            _stoodUpAt = Time.time;
            IndustrialArtDressing.ClearTintOverlay(gameObject);
            SetAgentStopped(false);
        }

        public void ShowRefusal(string chip)
        {
            if (string.IsNullOrEmpty(chip)) return;
            if (Time.time < _refusalGate) return;
            _refusalChip = chip;
            _refusalUntil = Time.time + OverseerRules.RefusalChipSeconds;
            _refusalGate = Time.time + OverseerRules.RefusalRetrigger;
        }

        public int CollectTithe()
        {
            if (_scrapped || _incapacitated || credits <= OverseerRules.TitheFloor) return 0;
            int tithe = Mathf.Min(OverseerRules.TitheCap, Mathf.FloorToInt(credits * OverseerRules.TitheRate));
            if (tithe <= 0) return 0;
            credits -= tithe;
            return tithe;
        }

        public void AccelerateRecover(float dt)
        {
            if (!_incapacitated || dt <= 0f) return;
            _recoverTimer -= dt;
        }

        public void Initialize(
            SpecialistData specialistData,
            FlagManager flagManager,
            SpecialistBrain brain,
            SimpleEconomy economy = null,
            Color? tint = null,
            BuildingPlacer placer = null,
            CampusNavMesh navMesh = null,
            PlanetaryWorldGen world = null)
        {
            data = specialistData;
            _flags = flagManager;
            _brain = brain;
            _economy = economy;
            _placer = placer;
            _navMesh = navMesh;
            _world = world;
            _loop = FindAnyObjectByType<GameLoop>();
            fatigue = 0.1f;
            healthNormalized = 1f;
            greedHunger = 0.55f;
            credits = 20f;
            equippedSuit = ShopItemId.None;
            _geneTimer = 0f;
            _geneCourage = _geneSpeed = _geneWork = 0f;
            _incapacitated = false;
            _recoverTimer = 0f;
            _thinkTimer = Random.Range(0f, thinkIntervalMax);
            _lastDecision = BrainDecision.Idle(0f, "spawn");
            _status = "idle";
            FlavorLine = SpecialistFlavor.CardLine(data.specialistClass, SpecialistAction.Idle, "spawn", null);
            gameObject.name = $"Specialist_{data.displayName}";

            _baseScale = transform.localScale;
            if (tint.HasValue) bodyTint = tint.Value;
            IndustrialArtDressing.ClearTintOverlay(gameObject);
            EnsureNavAgent();

            _statusDisplay = GetComponent<SpecialistStatusDisplay>();
            if (_statusDisplay == null)
                _statusDisplay = gameObject.AddComponent<SpecialistStatusDisplay>();
            _statusDisplay.Bind(this);

            EnsureSelectProxy();
            EnsureSelectRing();
            SetSelected(false);
        }

        /// <summary>
        /// Dedicated pick volume — FBX imports strip colliders, and child meshes are unreliable to click.
        /// </summary>
        private void EnsureSelectProxy()
        {
            Transform existing = transform.Find("SelectProxy");
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return;
            }

            var proxy = new GameObject("SelectProxy");
            proxy.transform.SetParent(transform, false);
            proxy.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            proxy.layer = gameObject.layer;

            var cap = proxy.AddComponent<CapsuleCollider>();
            cap.center = Vector3.zero;
            cap.height = 2.4f;
            cap.radius = 0.85f;
            cap.isTrigger = false;
            cap.direction = 1; // Y-axis
        }

        private void EnsureSelectRing()
        {
            if (_selectRing != null) return;
            _selectRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _selectRing.name = "SelectRing";
            _selectRing.transform.SetParent(transform, false);
            _selectRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            _selectRing.transform.localScale = new Vector3(1.35f, 0.02f, 1.35f);
            Object.Destroy(_selectRing.GetComponent<Collider>());
            var rend = _selectRing.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                                       ?? Shader.Find("Sprites/Default"));
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.96f, 0.42f, 0.08f, 0.85f));
                else if (mat.HasProperty("_Color"))
                    mat.color = new Color(0.96f, 0.42f, 0.08f, 0.85f);
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _selectRing.SetActive(false);
        }

        /// <summary>Called after CampusNavMesh.Build so agents warp onto the mesh.</summary>
        public void BindNavMesh(CampusNavMesh navMesh)
        {
            _navMesh = navMesh;
            EnsureNavAgent();
            if (_agent != null && _navMesh != null && _navMesh.IsReady &&
                _navMesh.SamplePosition(transform.position, out Vector3 onMesh))
            {
                _agent.Warp(onMesh);
            }
        }

        private void EnsureNavAgent()
        {
            if (!useNavMesh) return;
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = data != null ? data.moveSpeed : 3.5f;
            _agent.angularSpeed = 720f;
            _agent.acceleration = 18f;
            _agent.stoppingDistance = arriveDistance * 0.85f;
            _agent.radius = 0.4f;
            _agent.height = 2.2f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            _agent.updateRotation = false;
        }

        private void Update()
        {
            if (data == null || _brain == null || _flags == null)
                return;

            if (debugForceFatigue)
                fatigue = debugFatigueValue;

            float dt = Time.deltaTime;
            if (_incapacitated)
            {
                SetAgentStopped(true);
                TickIncapacitated(dt);
                TickWorkPulse(dt);
                return;
            }

            TickNeeds(dt);
            TickRadiation(dt);
            TickGene(dt);
            TickMedic(dt);
            TickThink(dt);
            TickBehaviour(dt);
            TickWorkPulse(dt);
            if (_agent != null && _agent.enabled)
                _agent.speed = EffectiveMoveSpeed;
        }

        private void TickGene(float dt)
        {
            _shopCooldown = Mathf.Max(0f, _shopCooldown - dt);
            if (_geneTimer <= 0f)
            {
                _geneCourage = 0f;
                _geneSpeed = 0f;
                _geneWork = 0f;
                return;
            }

            _geneTimer = Mathf.Max(0f, _geneTimer - dt);
            if (_geneTimer <= 0f)
            {
                _geneCourage = 0f;
                _geneSpeed = 0f;
                _geneWork = 0f;
                Debug.Log($"[Shop] {data.displayName} gene therapy wore off.");
            }
        }

        private void EnterIncapacitated()
        {
            if (_scrapped) return;
            bool secondDown = _stoodUpAt > 0f && Time.time - _stoodUpAt < OverseerRules.ScrapWindow;
            if (secondDown && Random.value < OverseerRules.ScrapChance)
            {
                ScrapSelf();
                return;
            }

            _incapacitated = true;
            _recoverTimer = OverseerRules.RecoverSeconds;
            ReleaseClaim();
            _activeFlag = null;
            _lastDecision = BrainDecision.Idle(0f, "incapacitated");
            _status = "incapacitated";
            SetAgentStopped(true);
            IndustrialArtDressing.SetTintOverlay(gameObject, new Color(0.38f, 0.38f, 0.4f));
            DemoVfx.DeathBurst(transform.position, bodyTint);
            Debug.Log($"[Specialist] {data.displayName} incapacitated — recovering in {OverseerRules.RecoverSeconds:F0}s");
        }

        private void ScrapSelf()
        {
            if (_scrapped) return;
            _scrapped = true;
            _incapacitated = true;
            ReleaseClaim();
            _activeFlag = null;
            SetWorkplace(null);
            int salvage = Mathf.FloorToInt(credits * OverseerRules.SalvageCreditFrac);
            credits = 0f;
            if (salvage > 0)
                _loop?.Resources?.Add(ResourceId.Metals, salvage);
            string label = ColonyStructure.ClassLabel(data != null ? data.specialistClass : SpecialistClass.ScoutDrone);
            _loop?.OnRobotScrapped(this, salvage);
            DemoVfx.DeathBurst(transform.position, bodyTint);
            Debug.Log($"[Specialist] {data?.displayName} SCRAPPED — salvage {salvage} MET");
            Destroy(gameObject);
        }

        private void TickIncapacitated(float dt)
        {
            _recoverTimer -= dt;
            _status = $"down {_recoverTimer:F1}s";
            healthNormalized = Mathf.Clamp01(healthNormalized + dt * 0.04f);
            if (_recoverTimer <= 0f && healthNormalized > 0.25f)
            {
                _incapacitated = false;
                healthNormalized = OverseerRules.RecoverHp;
                fatigue = Mathf.Max(fatigue, OverseerRules.RecoverFatigue);
                _stoodUpAt = Time.time;
                _scrapRiskLogged = false;
                _status = "recovered";
                IndustrialArtDressing.ClearTintOverlay(gameObject);
                SetAgentStopped(false);
                if (!_scrapRiskLogged)
                {
                    _scrapRiskLogged = true;
                    _loop?.LogOverseer($"SCRAP RISK — {data.displayName} just stood up. Another down in 90 s can scrap them.");
                }
                Debug.Log($"[Specialist] {data.displayName} recovered");
            }
        }

        private void TickNeeds(float dt)
        {
            switch (_lastDecision.Action)
            {
                case SpecialistAction.PursueFlag:
                case SpecialistAction.Hunt:
                case SpecialistAction.Repair:
                    fatigue = Mathf.Clamp01(fatigue + dt * 0.035f);
                    greedHunger = Mathf.Clamp01(greedHunger + dt * 0.01f);
                    break;
                case SpecialistAction.Flee:
                    fatigue = Mathf.Clamp01(fatigue + dt * 0.02f);
                    break;
                case SpecialistAction.Rest:
                    float innBoost = KingdomLife.AtRest(transform.position, OutpostClaimed) ? 1.45f : 0.55f;
                    fatigue = Mathf.Clamp01(fatigue - dt * 0.12f * innBoost);
                    healthNormalized = Mathf.Clamp01(healthNormalized + dt * restHealPerSecond * innBoost);
                    break;
                case SpecialistAction.Wander:
                    greedHunger = Mathf.Clamp01(greedHunger + dt * 0.018f);
                    fatigue = Mathf.Clamp01(fatigue - dt * 0.01f);
                    break;
                default:
                    greedHunger = Mathf.Clamp01(greedHunger + dt * 0.012f);
                    fatigue = Mathf.Clamp01(fatigue - dt * 0.015f);
                    break;
            }
        }

        private void TickThink(float dt)
        {
            _thinkTimer -= dt;
            if (_thinkTimer > 0f) return;
            _thinkTimer = Random.Range(thinkIntervalMin, thinkIntervalMax);

            BrainDecision decision = _brain.Evaluate(BuildContext(), _flags.Flags, bodyDanger);
            if (decision.Action != SpecialistAction.Flee &&
                _lastDecision.Action == SpecialistAction.Rest &&
                KingdomLife.AtRest(transform.position, OutpostClaimed) &&
                (fatigue > 0.22f || healthNormalized < 0.82f))
            {
                decision = BrainDecision.Rest(
                    Mathf.Max(decision.Score, 0.6f),
                    "recovering_at_inn",
                    RestPosition);
            }

            Party?.PromoteIfNeeded();
            if (Party != null && !Party.IsLeader(this) &&
                decision.Action != SpecialistAction.Flee)
            {
                var lead = Party.Leader;
                if (lead != null && lead.IsAlive)
                    decision = FollowLeader(lead);
            }
            ApplyDecision(decision);
            SyncWorkplace(decision);
        }

        private void SyncWorkplace(BrainDecision decision)
        {
            var duty = FindDutyBuilding();
            bool fieldJob = decision.Action == SpecialistAction.PursueFlag ||
                            decision.Action == SpecialistAction.Flee ||
                            decision.Action == SpecialistAction.Hunt ||
                            decision.Action == SpecialistAction.Repair;
            if (fieldJob)
            {
                if (decision.Action == SpecialistAction.PursueFlag &&
                    decision.TargetFlag != null && duty != null &&
                    FlatDistance(decision.TargetFlag.WorldPosition, duty.WorldPosition) < 12f)
                {
                    SetWorkplace(duty);
                    return;
                }
                SetWorkplace(null);
                return;
            }

            if (duty != null && FlatDistance(transform.position, duty.WorldPosition) < 5f)
                SetWorkplace(duty);
            else if (Workplace != null &&
                     FlatDistance(transform.position, Workplace.WorldPosition) > 14f)
                SetWorkplace(null);
        }

        private ColonyStructure FindDutyBuilding()
        {
            if (_loop?.Village == null || data == null) return null;
            if (Workplace != null && Workplace.IsAlive &&
                (!Workplace.HasPreferredClass || Workplace.PreferredClass == data.specialistClass))
                return Workplace;
            return _loop.Village.NearestDutyFor(data.specialistClass, transform.position, 42f);
        }

        public SpecialistContext PeekContext() => BuildContext();

        private BrainDecision FollowLeader(SpecialistAgent lead)
        {
            Vector3 inn = RestPosition;
            switch (lead.CurrentAction)
            {
                case SpecialistAction.Rest:
                    return BrainDecision.Rest(0.55f, "party_rest", inn);
                case SpecialistAction.Flee:
                    return BrainDecision.Flee(inn, 0.9f, "party_flee");
                case SpecialistAction.Hunt:
                    return BrainDecision.Wander(lead.transform.position, 0.45f, "party_hunt");
                case SpecialistAction.PursueFlag:
                    if (lead.ActiveFlag != null)
                        return BrainDecision.Wander(lead.ActiveFlag.WorldPosition, 0.45f, "party_follow_flag");
                    break;
            }
            return BrainDecision.Wander(lead.transform.position, 0.4f, "party_follow");
        }

        private SpecialistContext BuildContext()
        {
            Vector3 hunt = Vector3.zero;
            float huntDist = 99f;
            bool hasHunt = TryNearestStalker(out hunt, out huntDist);

            Vector3? site = NearestConstruction();
            Vector3? node = null;
            if (_world != null)
            {
                var n = _world.FindNearestNodeAny(transform.position, 40f);
                if (n != null && !n.IsDepleted)
                    node = n.WorldPosition;
            }

            int salt = GetEntityId().GetHashCode();
            var duty = FindDutyBuilding();
            Vector3 vocation;
            Vector3 workshopPos = Vector3.zero;
            bool hasWorkshop = false;
            if (duty != null)
            {
                vocation = duty.WorldPosition;
                workshopPos = duty.WorldPosition;
                hasWorkshop = duty.IsWorkshop || duty.IsGuild;
            }
            else
            {
                vocation = KingdomLife.VocationAnchor(
                    data.specialistClass, transform.position, site, node, salt);
            }

            if (data.specialistClass == SpecialistClass.ScoutDrone && _world != null)
            {
                var lair = NearestUnscoutedLair();
                if (lair != null)
                    vocation = lair.WorldPosition;
            }

            Vector3 patient = Vector3.zero;
            bool hasPatient = data.specialistClass == SpecialistClass.Medic &&
                              TryNearestWounded(out patient, out _);

            ColonyStructure damaged = null;
            if (_loop != null && _loop.Village != null &&
                data.specialistClass == SpecialistClass.EngineerBot)
            {
                damaged = _loop.Village.NearestDamaged(transform.position, 42f);
            }

            bool hasRepair = damaged != null;
            Vector3 repairPos = hasRepair ? damaged.WorldPosition : Vector3.zero;
            float repairDist = hasRepair ? FlatDistance(transform.position, repairPos) : 99f;
            float repairNeed = hasRepair ? (1f - damaged.Health01) : 0f;

            float hunger = Mathf.Clamp01(greedHunger + ReplayRules.GreedHungerBias);
            if (_loop != null && _loop.Resources != null &&
                _loop.Resources.Get(ResourceId.Metals) < OverseerRules.ThinMetals)
                hunger = Mathf.Clamp01(hunger + OverseerRules.ThinMetalsHunger);

            return new SpecialistContext
            {
                Data = data,
                Position = transform.position,
                Fatigue = fatigue,
                GreedHunger = hunger,
                CurrentFlag = _activeFlag,
                HealthNormalized = healthNormalized,
                SafetyPosition = KingdomLife.RestNear(transform.position, OutpostClaimed),
                VocationPosition = vocation,
                HuntPosition = hunt,
                HuntDistance = huntDist,
                HasHunt = hasHunt,
                CurrentAction = _lastDecision.Action,
                WorkshopPosition = workshopPos,
                HasWorkshop = hasWorkshop,
                FlagWorkshopBonus = hasWorkshop
                    ? 0.22f + (_loop != null && _loop.Settlement != null && _loop.Settlement.HasGuild ? 0.16f : 0f)
                      + ReplayRules.WorkshopBonusExtra
                    : 0f,
                HasPatient = hasPatient,
                PatientPosition = patient,
                HasRepair = hasRepair,
                RepairPosition = repairPos,
                RepairDistance = repairDist,
                RepairNeed = repairNeed,
                CourageEffective = EffectiveCourage
            };
        }

        private void ApplyDecision(BrainDecision decision)
        {
            bool changed = decision.Action != _lastDecision.Action ||
                           decision.Reason != _lastDecision.Reason ||
                           !ReferenceEquals(decision.TargetFlag, _lastDecision.TargetFlag);

            _lastDecision = decision;

            if (decision.Action == SpecialistAction.PursueFlag && decision.TargetFlag != null)
            {
                if (!ReferenceEquals(_activeFlag, decision.TargetFlag))
                {
                    ReleaseClaim();
                    _activeFlag = decision.TargetFlag;
                    _flags.AddClaim(_activeFlag);
                    _claimedActive = true;
                    _buildLaborHit = false;
                    DemoAudio.PlayClaim();
                    DemoVfx.ClaimRing(_activeFlag.WorldPosition, new Color(1f, 0.85f, 0.2f));
                    if (decision.TargetFlag.Data != null)
                        _loop?.LogOverseer(SpecialistFlavor.ClaimLine(
                            data.displayName, data.specialistClass, decision.TargetFlag.Data.flagType));
                }
                _idleTarget = _activeFlag.WorldPosition;
                _hasIdleTarget = true;
                _status = $"pursue_{decision.TargetFlag.Data.flagType}";
                FlavorLine = SpecialistFlavor.CardLine(
                    data.specialistClass, SpecialistAction.PursueFlag, decision.Reason,
                    decision.TargetFlag.Data != null ? decision.TargetFlag.Data.flagType : (FlagType?)null);
                SetDestination(_activeFlag.WorldPosition);
            }
            else
            {
                ReleaseClaim();
                _activeFlag = null;
                _idleTarget = decision.TargetPosition;
                _hasIdleTarget = _idleTarget.sqrMagnitude > 0.01f;
                _status = decision.Reason ?? decision.Action.ToString().ToLowerInvariant();
                FlavorLine = SpecialistFlavor.CardLine(
                    data != null ? data.specialistClass : SpecialistClass.ScoutDrone,
                    decision.Action, decision.Reason, null);
                if (decision.Action == SpecialistAction.Repair && _loop?.Village != null)
                    _repairTarget = _loop.Village.NearestDamaged(transform.position, 48f);
                else if (decision.Action != SpecialistAction.Repair)
                    _repairTarget = null;
                if (decision.Action != SpecialistAction.Rest || !KingdomLife.AtRest(transform.position, OutpostClaimed))
                    SetAgentStopped(false);
                if (_hasIdleTarget)
                    SetDestination(_idleTarget);
            }

            if (changed && logDecisions)
            {
                Debug.Log(
                    $"[Specialist] {data.displayName} → {decision.Action} " +
                    $"score={decision.Score:F2} reason={decision.Reason} " +
                    $"fatigue={fatigue:F2} hp={healthNormalized:F2}");
            }
        }

        private void TickBehaviour(float dt)
        {
            switch (_lastDecision.Action)
            {
                case SpecialistAction.PursueFlag:
                    TickPursue(dt);
                    break;
                case SpecialistAction.Rest:
                    TickRest(dt);
                    break;
                case SpecialistAction.Flee:
                    TickFlee(dt);
                    break;
                case SpecialistAction.Hunt:
                    TickHunt(dt);
                    break;
                case SpecialistAction.Repair:
                    TickRepair(dt);
                    break;
                case SpecialistAction.Wander:
                    TickWanderTown(dt);
                    break;
                default:
                    TickIdle(dt);
                    break;
            }
        }

        private void TickPursue(float dt)
        {
            if (_activeFlag == null)
            {
                _status = "pursue_lost_flag";
                return;
            }

            if (!_flags.TryGet(_activeFlag.RuntimeId, out _))
            {
                ReleaseClaim();
                _activeFlag = null;
                _status = "flag_gone";
                return;
            }

            Vector3 target = _activeFlag.WorldPosition;
            float dist = FlatDistance(transform.position, target);

            if (dist > arriveDistance)
            {
                SetDestination(target);
                MoveFallback(target, EffectiveMoveSpeed * dt);
                _status = $"moving_to_{_activeFlag.Data.flagType}";
                return;
            }

            SetAgentStopped(true);
            _status = $"working_{_activeFlag.Data.flagType}";
            _workPulse = 1f;
            float share = _loop != null ? _loop.FlagStackShare(_activeFlag, this) : 1f;
            float work = EffectiveWorkRate * share * dt;
            if (data != null && data.specialistClass == SpecialistClass.CourierBot &&
                _activeFlag.Data.flagType == FlagType.EstablishOutpost)
                work *= OverseerRules.CourierOutpostWork;
            bool done = _flags.ApplyWork(_activeFlag, work);

            if (_activeFlag.Data.flagType == FlagType.Build && _placer != null)
                ContributeBuildLabor(work);

            if (done)
            {
                float bounty = _activeFlag.CurrentBounty;
                var completedType = _activeFlag.Data.flagType;
                EarnCredits(bounty, $"flag_{completedType}");
                _economy?.ReleaseBountyEscrow(_activeFlag.EscrowMetals);
                if (completedType == FlagType.Extract)
                {
                    ResourceNode node = _world != null
                        ? _world.FindNearestNode(transform.position, 10f)
                        : null;
                    if (_loop != null)
                        _loop.ApplyExtractYield(transform.position, node, this);
                    else
                    {
                        int campus = ColonyLayout.NearestCampusIndex(transform.position);
                        _economy?.GrantExtractYield(campus, node);
                    }
                }
                else if (completedType == FlagType.ClearThreat && _world != null)
                {
                    var lair = _world.FindNearestLair(transform.position, 12f);
                    lair?.ForceClear();
                }
                else if (completedType == FlagType.Explore)
                {
                    _loop?.NotifySpecialFlag(completedType, transform.position, this);
                }
                else if (completedType == FlagType.DefendArea)
                {
                    _loop?.NotifySpecialFlag(completedType, transform.position, this);
                }
                else if (completedType == FlagType.Build)
                {
                    if (!_buildLaborHit)
                        _loop?.LogOverseer("No site in 28 m — paid for showing up.");
                }
                else if (completedType == FlagType.ResearchSite ||
                         completedType == FlagType.EstablishOutpost ||
                         completedType == FlagType.Terraform)
                {
                    _loop?.NotifySpecialFlag(completedType, transform.position, this);
                }
                greedHunger = Mathf.Clamp01(greedHunger - 0.25f);
                if (completedType == FlagType.Extract)
                {
                    DemoAudio.PlayExtract();
                    DemoVfx.ExtractPing(transform.position);
                }
                else
                {
                    DemoAudio.PlayClaim();
                    DemoVfx.ClaimRing(transform.position, new Color(0.3f, 1f, 0.5f));
                }
                Debug.Log($"[Specialist] {data.displayName} completed {completedType} bounty=${bounty:F0}");
                ReleaseClaim();
                _activeFlag = null;
                _status = "completed_flag";
                _workPulse = 1.5f;
            }
        }

        private void TickRepair(float dt)
        {
            if (_loop?.Village == null)
            {
                _status = "repair_no_village";
                return;
            }

            if (_repairTarget == null || !_repairTarget.NeedsRepair)
                _repairTarget = _loop.Village.NearestDamaged(transform.position, 48f);

            if (_repairTarget == null)
            {
                _status = "repair_done";
                return;
            }

            Vector3 target = _repairTarget.WorldPosition;
            if (FlatDistance(transform.position, target) > arriveDistance + 0.6f)
            {
                SetDestination(target);
                MoveFallback(target, EffectiveMoveSpeed * dt);
                _status = "moving_to_repair";
                return;
            }

            SetAgentStopped(true);
            _status = "repairing";
            _workPulse = 1f;
            float healed = _repairTarget.Repair(EffectiveWorkRate * 6.5f * dt);
            if (healed > 0f)
                EarnCredits(Mathf.Clamp(healed * 0.55f, 0.15f, 2.5f), "repair");

            if (!_repairTarget.NeedsRepair)
            {
                DemoAudio.PlayClaim();
                DemoVfx.ClaimRing(target, new Color(0.45f, 0.85f, 1f));
                _status = "repaired";
                _repairTarget = null;
            }
        }

        private void ContributeBuildLabor(float workSeconds)
        {
            if (_placer == null || _activeFlag == null) return;
            var orders = _placer.Orders;
            ConstructionOrder best = null;
            float bestDist = OverseerRules.BuildLabourRadius;
            Vector3 me = transform.position;
            for (int i = 0; i < orders.Count; i++)
            {
                var o = orders[i];
                if (o == null || o.IsComplete) continue;
                float d = Vector3.Distance(me, o.WorldPosition);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = o;
                }
            }
            if (best != null)
            {
                _placer.ApplyLabor(best, workSeconds);
                _buildLaborHit = true;
            }
        }

        private void TickRest(float dt)
        {
            Vector3 inn = RestPosition;
            if (FlatDistance(transform.position, inn) > KingdomLife.InnArrive)
            {
                SetDestination(inn);
                MoveFallback(inn, EffectiveMoveSpeed * dt);
                _status = "seeking_inn";
                return;
            }

            SetAgentStopped(true);
            _restTimer += dt;
            _status = "resting_at_inn";
            ConsiderShopPurchase();
            if (_restTimer > 3f && fatigue < 0.35f)
                _restTimer = 0f;
        }

        private void TickFlee(float dt)
        {
            Vector3 inn = RestPosition;
            if (FlatDistance(transform.position, inn) > KingdomLife.InnArrive)
            {
                SetDestination(inn);
                MoveFallback(inn, EffectiveMoveSpeed * 1.35f * dt);
                _status = "fleeing";
                return;
            }

            SetAgentStopped(true);
            _status = "refuge_inn";
            fatigue = Mathf.Clamp01(fatigue - dt * 0.08f);
            healthNormalized = Mathf.Clamp01(healthNormalized + dt * restHealPerSecond);
            ConsiderShopPurchase();
        }

        private void ConsiderShopPurchase()
        {
            if (_shopCooldown > 0f || data == null) return;
            int purse = Mathf.FloorToInt(credits);

            // Prefer permanent armor when unprotected and funded.
            var suit = ShopCatalog.BestAffordableSuit(purse, equippedSuit);
            if (suit != null && (equippedSuit == ShopItemId.None || healthNormalized < 0.85f || greedHunger < 0.55f))
            {
                if (TryBuy(suit))
                    return;
            }

            // Consumable gene when none active and wallet allows.
            if (_geneTimer <= 1f)
            {
                var gene = ShopCatalog.PreferredGene(data.specialistClass, purse);
                if (gene != null && (greedHunger < 0.7f || bodyDanger > 0.35f || fatigue > 0.4f))
                    TryBuy(gene);
            }
        }

        private bool TryBuy(ShopItemDef item)
        {
            if (item == null || credits < item.Cost) return false;
            credits -= item.Cost;
            _shopCooldown = 8f;

            if (item.Kind == ShopItemKind.PermanentSuit)
            {
                equippedSuit = item.Id;
                DemoVfx.ClaimRing(transform.position, new Color(0.7f, 0.85f, 1f));
                DemoAudio.PlayClaim();
                Debug.Log($"[Shop] {data.displayName} bought {item.DisplayName} for ${item.Cost} (armor {item.ArmorMitigation:P0})");
                return true;
            }

            _geneTimer = Mathf.Max(_geneTimer, item.DurationSeconds);
            _geneCourage = Mathf.Max(_geneCourage, item.CourageBonus);
            _geneSpeed = Mathf.Max(_geneSpeed, item.SpeedBonus);
            _geneWork = Mathf.Max(_geneWork, item.WorkBonus);
            DemoVfx.ClaimRing(transform.position, new Color(0.55f, 1f, 0.45f));
            DemoAudio.PlayClaim();
            Debug.Log($"[Shop] {data.displayName} bought {item.DisplayName} for ${item.Cost}");
            return true;
        }

        private void TickHunt(float dt)
        {
            if (!TryNearestStalker(out Vector3 prey, out float dist))
            {
                _status = "hunt_lost";
                return;
            }

            if (dist > KingdomLife.HuntRange)
            {
                SetDestination(prey);
                MoveFallback(prey, EffectiveMoveSpeed * dt);
                _status = "hunting";
                return;
            }

            SetAgentStopped(true);
            _status = "engaging";
            _workPulse = 1f;
            var stalker = NearestStalkerAgent();
            if (stalker != null)
            {
                float mul = HuntDpsMul(stalker.Kind);
                stalker.ApplyCombatDamage(EffectiveWorkRate * 8f * dt * mul);
            }
        }

        private void TickWanderTown(float dt)
        {
            Vector3 dest = _lastDecision.TargetPosition;
            if (dest.sqrMagnitude < 0.01f)
                dest = KingdomLife.Plaza(ColonyLayout.NearestCampusIndex(transform.position));

            float arrive = Mathf.Max(0.8f, idleWanderRadius * 0.55f);
            if (FlatDistance(transform.position, dest) < arrive)
            {
                _status = _lastDecision.Reason ?? "wandering";
                SetAgentStopped(true);
                TryPartyFollowWork(dt);
                TickTerraformerVocation(dt);
                return;
            }

            SetDestination(dest);
            MoveFallback(dest, EffectiveMoveSpeed * 0.72f * dt);
            _status = _lastDecision.Reason ?? "wandering";
            TryPartyFollowWork(dt);
            TickTerraformerVocation(dt);
        }

        private void TickIdle(float dt)
        {
            TickWanderTown(dt);
        }

        private bool TryNearestStalker(out Vector3 pos, out float dist)
        {
            pos = Vector3.zero;
            dist = 99f;
            var stalker = NearestStalkerAgent();
            if (stalker == null || !stalker.IsAlive) return false;
            pos = stalker.transform.position;
            dist = FlatDistance(transform.position, pos);
            return dist < 28f;
        }

        private void TickMedic(float dt)
        {
            if (data == null || data.specialistClass != SpecialistClass.Medic) return;
            if (!IsAlive) return;
            var agents = _loop != null ? _loop.Agents : null;
            if (agents == null) return;

            const float range = 3.6f;
            for (int i = 0; i < agents.Count; i++)
            {
                var ally = agents[i];
                if (ally == null || ally == this) continue;
                if (FlatDistance(transform.position, ally.transform.position) > range) continue;
                if (ally.IsIncapacitated)
                    ally.AccelerateRecover(dt);
                else if (ally.HealthNormalized < 0.98f)
                    ally.ReceiveHeal(dt * 0.14f);
            }
        }

        private bool TryNearestWounded(out Vector3 pos, out float dist)
        {
            pos = Vector3.zero;
            dist = 99f;
            var agents = _loop != null ? _loop.Agents : null;
            if (agents == null) return false;

            Vector3 me = transform.position;
            bool found = false;
            for (int i = 0; i < agents.Count; i++)
            {
                var ally = agents[i];
                if (ally == null || ally == this) continue;
                if (!ally.IsIncapacitated && ally.HealthNormalized >= 0.82f) continue;
                float d = FlatDistance(me, ally.transform.position);
                if (d >= 28f || d >= dist) continue;
                dist = d;
                pos = ally.transform.position;
                found = true;
            }

            return found;
        }

        private DustStalkerAgent NearestStalkerAgent()
        {
            IReadOnlyList<DustStalkerAgent> list = _loop != null ? _loop.Stalkers : null;
            if (list == null || list.Count == 0) return null;
            DustStalkerAgent best = null;
            float bestD = 28f;
            Vector3 me = transform.position;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive) continue;
                float d = FlatDistance(me, s.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        private Vector3? NearestConstruction()
        {
            if (_placer == null || _placer.Orders == null) return null;
            ConstructionOrder best = null;
            float bestD = 22f;
            Vector3 me = transform.position;
            for (int i = 0; i < _placer.Orders.Count; i++)
            {
                var o = _placer.Orders[i];
                if (o == null || o.IsComplete) continue;
                float d = Vector3.Distance(me, o.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = o;
                }
            }
            return best != null ? best.WorldPosition : (Vector3?)null;
        }

        private void TryPartyFollowWork(float dt)
        {
            if (Party == null || Party.IsLeader(this)) return;
            var lead = Party.Leader;
            if (lead == null || lead.ActiveFlag == null || _flags == null) return;
            if (FlatDistance(transform.position, lead.ActiveFlag.WorldPosition) > OverseerRules.PartyFollowerRange)
                return;

            float work = EffectiveWorkRate * OverseerRules.PartyFollowerWork * dt;
            if (data != null && data.specialistClass == SpecialistClass.CourierBot &&
                lead.ActiveFlag.Data != null &&
                lead.ActiveFlag.Data.flagType == FlagType.EstablishOutpost)
                work *= OverseerRules.CourierOutpostWork;
            if (_flags.ApplyWork(lead.ActiveFlag, work))
                _status = "party_work";
        }

        private void TickTerraformerVocation(float dt)
        {
            if (data == null || data.specialistClass != SpecialistClass.TerraformerBot) return;
            if (_loop?.Village == null || _loop.Settlement == null) return;
            var farm = _loop.Village.NearestByCategory(transform.position, OverseerRules.TerraformerFarmRange, BuildingCategory.Farm);
            if (farm == null) return;
            _terraformerPulseTimer += dt;
            if (_terraformerPulseTimer < OverseerRules.TerraformerPulseInterval) return;
            _terraformerPulseTimer = 0f;
            _loop.Settlement.AddTerraformPulse(OverseerRules.TerraformerPulse);
        }

        private float HuntDpsMul(FaunaKind kind)
        {
            if (data == null) return 1f;
            if (data.specialistClass == SpecialistClass.DefenseMech &&
                (kind == FaunaKind.Stalker || kind == FaunaKind.Hopper))
                return OverseerRules.DefenseStalkerDpsMul;
            if (data.specialistClass == SpecialistClass.SentinelMech && kind == FaunaKind.Stalker)
                return OverseerRules.SentinelStalkerDpsMul;
            return 1f;
        }

        private StalkerLair NearestUnscoutedLair()
        {
            if (_world == null) return null;
            var lairs = _world.Lairs;
            StalkerLair best = null;
            float bestD = 80f;
            for (int i = 0; i < lairs.Count; i++)
            {
                var l = lairs[i];
                if (l == null || l.IsCleared || l.IsScouted) continue;
                float d = FlatDistance(transform.position, l.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = l;
                }
            }
            return best;
        }

        private void SetDestination(Vector3 world)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            SetAgentStopped(false);
            if (_navMesh != null && _navMesh.SamplePosition(world, out Vector3 onMesh))
                world = onMesh;
            if (!_agent.pathPending && (_agent.destination - world).sqrMagnitude > 0.25f)
                _agent.SetDestination(world);
        }

        private void MoveFallback(Vector3 target, float step)
        {
            if (_agent != null && _agent.isOnNavMesh) return;
            target.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, target, step);
        }

        private void SetAgentStopped(bool stopped)
        {
            if (_agent == null) return;
            if (_agent.isOnNavMesh)
                _agent.isStopped = stopped;
            if (stopped && _agent.isOnNavMesh)
                _agent.ResetPath();
        }

        private void TickWorkPulse(float dt)
        {
            if (_workPulse > 0f)
            {
                _workPulse = Mathf.Max(0f, _workPulse - dt * 3f);
                float s = 1f + Mathf.Sin((1f - _workPulse) * Mathf.PI) * 0.18f * _workPulse;
                transform.localScale = _baseScale * s;
            }
            else if (_radExposed)
            {
                float s = 1f + Mathf.Sin(Time.time * 6.5f) * 0.035f;
                transform.localScale = _baseScale * s;
            }
            else if (transform.localScale != _baseScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, dt * 8f);
            }
        }

        private void ReleaseClaim()
        {
            if (_claimedActive && _activeFlag != null && _flags != null)
                _flags.RemoveClaim(_activeFlag);
            _claimedActive = false;
        }

        private void OnDestroy()
        {
            SetWorkplace(null);
            ReleaseClaim();
        }

        public void DebugSetFatigue(float value) => fatigue = Mathf.Clamp01(value);

        private void TickRadiation(float dt)
        {
            var body = _loop != null ? _loop.BodyProfile : null;
            if (body == null || body.RadiationDrainPerSecond <= 0f)
            {
                _radExposed = false;
                return;
            }

            float nearest = FlatDistance(transform.position, ColonyLayout.CampusOrigin);
            if (OutpostClaimed)
                nearest = Mathf.Min(nearest, FlatDistance(transform.position, ColonyLayout.CampusBOrigin));

            _radExposed = nearest > body.RadiationSafeRadius;
            if (!_radExposed) return;

            ApplyDamage(body.RadiationDrainPerSecond * dt, false);
            _radFlash -= dt;
            if (_radFlash <= 0f)
            {
                _radFlash = 2.4f;
                DemoVfx.HitFlash(transform, new Color(0.45f, 0.85f, 1f));
                if (!_radToast)
                {
                    _radToast = true;
                    _loop.NoteRadiationExposure();
                }
            }
        }

        private bool OutpostClaimed =>
            _loop != null && _loop.Settlement != null && _loop.Settlement.HasOutpost;

        private Vector3 RestPosition =>
            KingdomLife.RestNear(transform.position, OutpostClaimed);

        private float BodyMoveScale()
        {
            var body = _loop != null ? _loop.BodyProfile : null;
            return body != null ? Mathf.Clamp(body.MoveSpeedScale, 0.5f, 1.4f) : 1f;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public string DebugLine()
        {
            string flagInfo = _activeFlag != null
                ? $"{_activeFlag.Data.flagType} b={_activeFlag.CurrentBounty:F0}"
                : "-";
            string nav = _agent != null && _agent.isOnNavMesh ? "nav" : "direct";
            return $"{data?.displayName ?? "?"} | {_lastDecision.Action} | " +
                   $"score={_lastDecision.Score:F2} | {_lastDecision.Reason} | " +
                   $"hp={healthNormalized:F2} fat={fatigue:F2} danger={bodyDanger:F2} | " +
                   $"flag={flagInfo} | {_status} | {nav}" +
                   (_incapacitated ? " [DOWN]" : "");
        }
    }
}
