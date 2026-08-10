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

        [Header("Phase 2A combat")]
        [SerializeField] private float incapacitateThreshold = 0.02f;
        [SerializeField] private float recoverySeconds = 8f;
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
        private NavMeshAgent _agent;

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
        private Renderer _bodyRend;
        private SpecialistStatusDisplay _statusDisplay;

        public SpecialistData Data => data;
        public BrainDecision LastDecision => _lastDecision;
        public string LastReason => _lastDecision.Reason ?? "none";
        public float LastScore => _lastDecision.Score;
        public float Fatigue => fatigue;
        public float HealthNormalized => healthNormalized;
        public float GreedHunger => greedHunger;
        public string Status => _status;
        public SpecialistAction CurrentAction => _lastDecision.Action;
        public FlagHandle ActiveFlag => _activeFlag;
        public float BodyDanger => bodyDanger;
        public bool IsIncapacitated => _incapacitated;
        public bool IsAlive => !_incapacitated || healthNormalized > incapacitateThreshold;

        public void SetBodyDanger(float danger01) => bodyDanger = Mathf.Clamp01(danger01);

        public void ApplyDamage(float amount01)
        {
            if (amount01 <= 0f || _incapacitated) return;
            healthNormalized = Mathf.Clamp01(healthNormalized - amount01);
            DemoAudio.PlayBite();
            DemoVfx.HitFlash(transform, new Color(1f, 0.25f, 0.2f));
            if (healthNormalized <= incapacitateThreshold)
                EnterIncapacitated();
        }

        public void ReviveFull()
        {
            _incapacitated = false;
            _recoverTimer = 0f;
            healthNormalized = 1f;
            fatigue = 0.1f;
            _status = "revived";
            ApplyBodyTint(bodyTint);
            SetAgentStopped(false);
        }

        public void Initialize(
            SpecialistData specialistData,
            FlagManager flagManager,
            SpecialistBrain brain,
            SimpleEconomy economy = null,
            Color? tint = null,
            BuildingPlacer placer = null,
            CampusNavMesh navMesh = null)
        {
            data = specialistData;
            _flags = flagManager;
            _brain = brain;
            _economy = economy;
            _placer = placer;
            _navMesh = navMesh;
            fatigue = 0.1f;
            healthNormalized = 1f;
            greedHunger = 0.55f;
            _incapacitated = false;
            _recoverTimer = 0f;
            _thinkTimer = Random.Range(0f, thinkIntervalMax);
            _lastDecision = BrainDecision.Idle(0f, "spawn");
            _status = "idle";
            gameObject.name = $"Specialist_{data.displayName}";

            _baseScale = transform.localScale;
            _bodyRend = GetComponentInChildren<Renderer>();
            if (tint.HasValue) bodyTint = tint.Value;
            ApplyBodyTint(bodyTint);
            EnsureNavAgent();

            _statusDisplay = GetComponent<SpecialistStatusDisplay>();
            if (_statusDisplay == null)
                _statusDisplay = gameObject.AddComponent<SpecialistStatusDisplay>();
            _statusDisplay.Bind(this);
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
            TickThink(dt);
            TickBehaviour(dt);
            TickWorkPulse(dt);
        }

        private void EnterIncapacitated()
        {
            _incapacitated = true;
            _recoverTimer = recoverySeconds;
            ReleaseClaim();
            _activeFlag = null;
            _lastDecision = BrainDecision.Idle(0f, "incapacitated");
            _status = "incapacitated";
            SetAgentStopped(true);
            ApplyBodyTint(Color.Lerp(bodyTint, Color.black, 0.55f));
            DemoVfx.DeathBurst(transform.position, bodyTint);
            Debug.Log($"[Specialist] {data.displayName} incapacitated — recovering in {recoverySeconds:F0}s");
        }

        private void TickIncapacitated(float dt)
        {
            _recoverTimer -= dt;
            _status = $"down {_recoverTimer:F1}s";
            healthNormalized = Mathf.Clamp01(healthNormalized + dt * 0.04f);
            if (_recoverTimer <= 0f && healthNormalized > 0.25f)
            {
                _incapacitated = false;
                fatigue = Mathf.Max(fatigue, 0.55f);
                _status = "recovered";
                ApplyBodyTint(bodyTint);
                SetAgentStopped(false);
                Debug.Log($"[Specialist] {data.displayName} recovered");
            }
        }

        private void TickNeeds(float dt)
        {
            switch (_lastDecision.Action)
            {
                case SpecialistAction.PursueFlag:
                    fatigue = Mathf.Clamp01(fatigue + dt * 0.035f);
                    greedHunger = Mathf.Clamp01(greedHunger + dt * 0.01f);
                    break;
                case SpecialistAction.Rest:
                    fatigue = Mathf.Clamp01(fatigue - dt * 0.12f);
                    healthNormalized = Mathf.Clamp01(healthNormalized + dt * restHealPerSecond);
                    break;
                default:
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
            ApplyDecision(decision);
        }

        private SpecialistContext BuildContext() => new SpecialistContext
        {
            Data = data,
            Position = transform.position,
            Fatigue = fatigue,
            GreedHunger = greedHunger,
            CurrentFlag = _activeFlag,
            HealthNormalized = healthNormalized
        };

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
                    DemoAudio.PlayClaim();
                    DemoVfx.ClaimRing(_activeFlag.WorldPosition, new Color(1f, 0.85f, 0.2f));
                }
                _status = $"pursue_{decision.TargetFlag.Data.flagType}";
                SetDestination(_activeFlag.WorldPosition);
            }
            else
            {
                ReleaseClaim();
                _activeFlag = null;
                _status = decision.Action == SpecialistAction.Rest ? "rest" : "idle";
                if (decision.Action == SpecialistAction.Idle)
                    _hasIdleTarget = false;
                SetAgentStopped(decision.Action == SpecialistAction.Rest);
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
                MoveFallback(target, data.moveSpeed * dt);
                _status = $"moving_to_{_activeFlag.Data.flagType}";
                return;
            }

            SetAgentStopped(true);
            _status = $"working_{_activeFlag.Data.flagType}";
            _workPulse = 1f;
            float work = data.workRate * dt;
            bool done = _flags.ApplyWork(_activeFlag, work);

            if (_activeFlag.Data.flagType == FlagType.Build && _placer != null)
                ContributeBuildLabor(work);

            if (done)
            {
                float bounty = _activeFlag.CurrentBounty;
                var completedType = _activeFlag.Data.flagType;
                _economy?.GrantBountyReward(bounty);
                if (completedType == FlagType.Extract)
                    _economy?.GrantExtractYield(ColonyLayout.NearestCampusIndex(transform.position));
                greedHunger = Mathf.Clamp01(greedHunger - 0.25f);
                DemoAudio.PlayClaim();
                DemoVfx.ClaimRing(transform.position, new Color(0.3f, 1f, 0.5f));
                Debug.Log($"[Specialist] {data.displayName} completed {completedType} bounty={bounty}");
                ReleaseClaim();
                _activeFlag = null;
                _status = "completed_flag";
                _workPulse = 1.5f;
            }
        }

        private void ContributeBuildLabor(float workSeconds)
        {
            if (_placer == null || _activeFlag == null) return;
            var orders = _placer.Orders;
            ConstructionOrder best = null;
            float bestDist = 6f;
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
                _placer.ApplyLabor(best, workSeconds);
        }

        private void TickRest(float dt)
        {
            SetAgentStopped(true);
            _restTimer += dt;
            _status = "resting";
            if (_restTimer > 3f && fatigue < 0.35f)
                _restTimer = 0f;
        }

        private void TickIdle(float dt)
        {
            _status = "idle";
            if (!_hasIdleTarget || FlatDistance(transform.position, _idleTarget) < 0.3f)
            {
                if (Random.value < 0.02f)
                {
                    Vector2 r = Random.insideUnitCircle * idleWanderRadius;
                    _idleTarget = transform.position + new Vector3(r.x, 0f, r.y);
                    _hasIdleTarget = true;
                    SetDestination(_idleTarget);
                }
                return;
            }

            SetDestination(_idleTarget);
            MoveFallback(_idleTarget, data.moveSpeed * 0.35f * dt);
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

        private void OnDestroy() => ReleaseClaim();

        public void DebugSetFatigue(float value) => fatigue = Mathf.Clamp01(value);

        private void ApplyBodyTint(Color c)
        {
            if (_bodyRend == null) return;
            if (_bodyRend.material.HasProperty("_Color"))
                _bodyRend.material.color = c;
            else if (_bodyRend.material.HasProperty("_BaseColor"))
                _bodyRend.material.SetColor("_BaseColor", c);
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
