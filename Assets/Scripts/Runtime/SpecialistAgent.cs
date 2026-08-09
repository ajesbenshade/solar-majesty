using UnityEngine;

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

        [Header("Needs (0-1)")]
        [SerializeField] [Range(0f, 1f)] private float fatigue;
        [SerializeField] [Range(0f, 1f)] private float healthNormalized = 1f;
        [SerializeField] [Range(0f, 1f)] private float greedHunger = 0.5f;

        [Header("Debug force (playtesting only — not player commands)")]
        [SerializeField] private bool debugForceFatigue;
        [SerializeField] [Range(0f, 1f)] private float debugFatigueValue = 0.9f;

        [Header("Presentation")]
        [SerializeField] private Color bodyTint = Color.white;

        // Injected by GameLoop (pure systems)
        private FlagManager _flags;
        private SpecialistBrain _brain;
        private SimpleEconomy _economy;

        private float _thinkTimer;
        private BrainDecision _lastDecision;
        private FlagHandle _activeFlag;
        private bool _claimedActive;
        private Vector3 _idleTarget;
        private bool _hasIdleTarget;
        private float _restTimer;
        private string _status = "boot";

        // Work pulse placeholder animation
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

        /// <summary>
        /// Pushed by GameLoop from ThreatPressure.Current each frame.
        /// Feeds SpecialistBrain.Evaluate as bodyDanger (risk term).
        /// </summary>
        public void SetBodyDanger(float danger01)
        {
            bodyDanger = Mathf.Clamp01(danger01);
        }

        /// <summary>Called by GameLoop after spawn. Does not accept player move orders.</summary>
        public void Initialize(
            SpecialistData specialistData,
            FlagManager flagManager,
            SpecialistBrain brain,
            SimpleEconomy economy = null,
            Color? tint = null)
        {
            data = specialistData;
            _flags = flagManager;
            _brain = brain;
            _economy = economy;
            fatigue = 0.1f;
            healthNormalized = 1f;
            greedHunger = 0.55f;
            _thinkTimer = Random.Range(0f, thinkIntervalMax);
            _lastDecision = BrainDecision.Idle(0f, "spawn");
            _status = "idle";
            gameObject.name = $"Specialist_{data.displayName}";

            _baseScale = transform.localScale;
            _bodyRend = GetComponentInChildren<Renderer>();
            if (tint.HasValue) bodyTint = tint.Value;
            ApplyBodyTint(bodyTint);

            _statusDisplay = GetComponent<SpecialistStatusDisplay>();
            if (_statusDisplay == null)
                _statusDisplay = gameObject.AddComponent<SpecialistStatusDisplay>();
            _statusDisplay.Bind(this);
        }

        private void Update()
        {
            if (data == null || _brain == null || _flags == null)
                return;

            if (debugForceFatigue)
                fatigue = debugFatigueValue;

            float dt = Time.deltaTime;
            TickNeeds(dt);
            TickThink(dt);
            TickBehaviour(dt);
            TickWorkPulse(dt);
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

            SpecialistContext ctx = BuildContext();
            BrainDecision decision = _brain.Evaluate(ctx, _flags.Flags, bodyDanger);
            ApplyDecision(decision);
        }

        private SpecialistContext BuildContext()
        {
            return new SpecialistContext
            {
                Data = data,
                Position = transform.position,
                Fatigue = fatigue,
                GreedHunger = greedHunger,
                CurrentFlag = _activeFlag,
                HealthNormalized = healthNormalized
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
                }
                _status = $"pursue_{decision.TargetFlag.Data.flagType}";
            }
            else
            {
                ReleaseClaim();
                _activeFlag = null;
                _status = decision.Action == SpecialistAction.Rest ? "rest" : "idle";
                if (decision.Action == SpecialistAction.Idle)
                    _hasIdleTarget = false;
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
            target.y = transform.position.y;
            float dist = Vector3.Distance(transform.position, target);

            if (dist > arriveDistance)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, target, data.moveSpeed * dt);
                _status = $"moving_to_{_activeFlag.Data.flagType}";
                return;
            }

            // In range: apply work autonomously + pulse feedback.
            _status = $"working_{_activeFlag.Data.flagType}";
            _workPulse = 1f;
            float work = data.workRate * dt;
            bool done = _flags.ApplyWork(_activeFlag, work);
            if (done)
            {
                float bounty = _activeFlag.CurrentBounty;
                _economy?.GrantBountyReward(bounty);
                greedHunger = Mathf.Clamp01(greedHunger - 0.25f);
                Debug.Log($"[Specialist] {data.displayName} completed flag bounty={bounty}");
                ReleaseClaim();
                _activeFlag = null;
                _status = "completed_flag";
                _workPulse = 1.5f;
            }
        }

        private void TickRest(float dt)
        {
            _restTimer += dt;
            _status = "resting";
            if (_restTimer > 3f && fatigue < 0.35f)
                _restTimer = 0f;
        }

        private void TickIdle(float dt)
        {
            _status = "idle";
            if (!_hasIdleTarget || Vector3.Distance(transform.position, _idleTarget) < 0.3f)
            {
                if (Random.value < 0.02f)
                {
                    Vector2 r = Random.insideUnitCircle * idleWanderRadius;
                    _idleTarget = transform.position + new Vector3(r.x, 0f, r.y);
                    _hasIdleTarget = true;
                }
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                _idleTarget,
                data.moveSpeed * 0.35f * dt);
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

        private void OnDestroy()
        {
            ReleaseClaim();
        }

        public void DebugSetFatigue(float value) => fatigue = Mathf.Clamp01(value);

        private void ApplyBodyTint(Color c)
        {
            if (_bodyRend == null) return;
            if (_bodyRend.material.HasProperty("_Color"))
                _bodyRend.material.color = c;
            else if (_bodyRend.material.HasProperty("_BaseColor"))
                _bodyRend.material.SetColor("_BaseColor", c);
        }

        public string DebugLine()
        {
            string flagInfo = _activeFlag != null
                ? $"{_activeFlag.Data.flagType} b={_activeFlag.CurrentBounty:F0}"
                : "-";
            return $"{data?.displayName ?? "?"} | {_lastDecision.Action} | " +
                   $"score={_lastDecision.Score:F2} | {_lastDecision.Reason} | " +
                   $"fat={fatigue:F2} danger={bodyDanger:F2} | flag={flagInfo} | {_status}";
        }
    }
}
