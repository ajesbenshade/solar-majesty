using UnityEngine;

namespace SolarMajesty
{
    public enum MissionState
    {
        Active = 0,
        Won = 1,
        Lost = 2
    }

    /// <summary>
    /// Campaign gates: clear dens, sustain population goal, launch-ready (tech stub until Week 2).
    /// Does not touch SpecialistBrain.
    /// </summary>
    public class MissionController : MonoBehaviour
    {
        [Header("Sustain gate")]
        [SerializeField] private float sustainHoldSeconds = 40f;
        [SerializeField] private int populationGoal = 12;

        [Header("Launch gate (tech tree)")]
        [Tooltip("When true, Lunar Rocket (or other launch unlock) must be researched.")]
        [SerializeField] private bool requireLaunchTech = true;

        [Header("Optional pressure")]
        [SerializeField] private bool spawnPressureFromLairs = true;
        [SerializeField] private int pressureStalkerCount = 2;
        [SerializeField] private float pressureCooldown = 90f;

        [Header("Deadline (off for campaign)")]
        [SerializeField] private bool enforceDeadline = false;
        [SerializeField] private float missionDeadlineSeconds = 900f;

        private GameLoop _loop;
        private MissionState _state = MissionState.Active;
        private bool _armed;
        private bool _winLatched;
        private bool _loseLatched;
        private float _missionElapsed;
        private float _sustainElapsed;
        private float _pressureTimer;
        private bool _deadlineFail;
        private bool _launchReady;

        public MissionState State => _state;
        public int StalkersRemaining { get; private set; }
        public bool IsWon => _state == MissionState.Won;
        public bool IsLost => _state == MissionState.Lost;

        public bool DensCleared { get; private set; }
        public bool SustainComplete => _sustainElapsed >= sustainHoldSeconds;
        public bool LaunchReady => _launchReady;
        public bool AllGatesMet => DensCleared && SustainComplete && LaunchReady;

        public float SustainElapsed => _sustainElapsed;
        public float SustainRequired => sustainHoldSeconds;
        public int PopulationGoal => populationGoal;
        public int PopulationCurrent =>
            _loop != null && _loop.Settlement != null ? _loop.Settlement.Population : 0;
        public int UnclearedLairs { get; private set; }
        public int LairCount { get; private set; }

        public float MissionElapsed => _missionElapsed;
        public float MissionDeadline => missionDeadlineSeconds;
        public bool DeadlineEnabled => enforceDeadline;
        public bool WasDeadlineFail => _deadlineFail;

        // Compat aliases so older HUD/debug call sites compile during transition.
        public bool CombatCleared => DensCleared;
        public bool HoldComplete => SustainComplete;
        public bool ColonyComplete => LaunchReady;
        public int Wave => 1;
        public int WaveTarget => Mathf.Max(1, LairCount);
        public int CompletedBuildings => LaunchReady ? 1 : 0;
        public int BuildingsRequired => 1;
        public int MetalsGoal => 0;
        public int MetalsCurrent => 0;
        public float HoldElapsed => _sustainElapsed;
        public float HoldRequired => sustainHoldSeconds;

        public string ObjectiveLabel
        {
            get
            {
                if (_state == MissionState.Won)
                {
                    if (_loop != null && _loop.ActiveBody == CelestialBodyId.Mars)
                        return "Solar conquest complete — Mars holds";
                    return "Outpost secured — advance the campaign";
                }
                if (!DensCleared)
                {
                    if (LairCount > 0)
                        return $"Clear dens — {UnclearedLairs} left (F2 Clear Threat)";
                    return "Clear remaining fauna near campus";
                }
                if (!SustainComplete)
                {
                    string hint = _loop?.Settlement != null ? _loop.Settlement.SustainHint : "hold population";
                    return $"Sustain — {hint}";
                }
                if (!LaunchReady)
                {
                    string craft = _loop != null && _loop.Research != null
                        ? _loop.Research.LaunchTechLabel(_loop.ActiveBody)
                        : "departure craft";
                    return $"Research {craft} (TECH · T) — craft stages on the pad";
                }
                return "Securing…";
            }
        }

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            _armed = false;
            _state = MissionState.Active;
            _winLatched = false;
            _loseLatched = false;
            _missionElapsed = 0f;
            _sustainElapsed = 0f;
            _pressureTimer = pressureCooldown * 0.5f;
            _deadlineFail = false;
            DensCleared = false;
            UnclearedLairs = 0;
            LairCount = 0;

            var body = _loop.BodyProfile;
            if (body != null)
            {
                populationGoal = Mathf.Max(1, body.PopulationGoal);
                sustainHoldSeconds = Mathf.Max(5f, body.SustainHoldSeconds);
            }

            _launchReady = !requireLaunchTech;
            if (_loop.Settlement != null)
                _loop.Settlement.SetPopulationGoal(populationGoal);
        }

        /// <summary>Week 2+: call when rocket tech is researched and craft is built.</summary>
        public void SetLaunchReady(bool ready) => _launchReady = ready;

        public void Tick()
        {
            if (_loop == null) return;

            StalkersRemaining = CountLivingStalkers();
            RefreshGates();

            if (!_armed)
            {
                if (Time.timeSinceLevelLoad > 1f)
                    _armed = true;
                return;
            }

            if (_state == MissionState.Won) return;

            if (_loop.IsOutpostOverwhelmed)
            {
                if (_state != MissionState.Lost)
                    EnterLost();
                return;
            }

            if (_state == MissionState.Lost)
                return;

            _missionElapsed += Time.deltaTime;
            TickSustain(Time.deltaTime);
            TickPressure(Time.deltaTime);

            if (enforceDeadline && _missionElapsed >= missionDeadlineSeconds && !AllGatesMet)
            {
                _deadlineFail = true;
                EnterLost();
                return;
            }

            if (AllGatesMet)
                EnterWon();
        }

        public void OnPartyRevived()
        {
            if (_state == MissionState.Lost && !_deadlineFail)
            {
                _state = MissionState.Active;
                _loseLatched = false;
            }
        }

        public void DismissWinToSandbox()
        {
            _winLatched = true;
        }

        private void RefreshGates()
        {
            LairCount = 0;
            UnclearedLairs = 0;
            if (_loop.World != null)
            {
                LairCount = _loop.World.Lairs.Count;
                UnclearedLairs = _loop.World.UnclearedLairCount;
            }

            if (LairCount > 0)
                DensCleared = UnclearedLairs <= 0 && StalkersRemaining <= 0;
            else
                DensCleared = StalkersRemaining <= 0;
        }

        private void TickSustain(float dt)
        {
            var set = _loop.Settlement;
            if (set == null)
            {
                _sustainElapsed = 0f;
                return;
            }

            if (set.IsSustainable)
                _sustainElapsed = Mathf.Min(sustainHoldSeconds, _sustainElapsed + dt);
            else
                _sustainElapsed = Mathf.Max(0f, _sustainElapsed - dt * 0.5f);
        }

        private void TickPressure(float dt)
        {
            if (!spawnPressureFromLairs || DensCleared) return;
            if (_loop.World == null || UnclearedLairs <= 0) return;

            _pressureTimer -= dt;
            if (_pressureTimer > 0f) return;
            _pressureTimer = pressureCooldown;

            Vector3 origin = ColonyLayout.CampusOrigin;
            var lairs = _loop.World.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                if (lairs[i] != null && !lairs[i].IsCleared)
                {
                    origin = lairs[i].WorldPosition;
                    break;
                }
            }

            int spawned = _loop.SpawnStalkerWave(
                Mathf.Max(1, pressureStalkerCount),
                10f,
                origin);
            if (spawned > 0)
                Debug.Log($"[Mission] Pressure wave — {spawned} from uncleared dens.");
        }

        private void EnterWon()
        {
            _state = MissionState.Won;
            if (!_winLatched)
            {
                _winLatched = true;
                DemoAudio.PlayVictory();
                DemoVfx.ClaimRing(
                    ColonyLayout.CampusOriginFor(_loop.FocusedCampus),
                    new Color(0.35f, 0.95f, 0.55f));
                Debug.Log("[Mission] Victory — dens cleared, colony sustained, launch ready.");
                if (_loop.ActiveBody == CelestialBodyId.Mars)
                    Debug.Log("[Mission] Mars finale — solar conquest complete.");
            }
        }

        private void EnterLost()
        {
            _state = MissionState.Lost;
            if (!_loseLatched)
            {
                _loseLatched = true;
                if (_deadlineFail)
                    Debug.Log("[Mission] Defeat — mission deadline elapsed.");
                else
                    Debug.Log("[Mission] Defeat — outpost overwhelmed.");
            }
        }

        private int CountLivingStalkers()
        {
            if (_loop == null || _loop.Stalkers == null) return 0;
            int n = 0;
            for (int i = 0; i < _loop.Stalkers.Count; i++)
            {
                var s = _loop.Stalkers[i];
                if (s != null && s.IsAlive) n++;
            }
            return n;
        }
    }
}
