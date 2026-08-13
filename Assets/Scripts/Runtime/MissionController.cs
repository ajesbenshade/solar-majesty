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
    /// Phase 4A multi-stake mission: clear stalker waves + hold timer + finish a construction.
    /// Does not touch SpecialistBrain.
    /// </summary>
    public class MissionController : MonoBehaviour
    {
        [Header("Combat waves")]
        [SerializeField] private int reinforcementCount = 2;
        [SerializeField] private float reinforcementDelay = 2.5f;

        [Header("Hold / colony stakes")]
        [SerializeField] private float holdSecondsRequired = 60f;
        [SerializeField] private int metalsGoal = 0;
        [SerializeField] private int buildingsRequired = 1;

        [Header("Phase 5A deadline")]
        [SerializeField] private bool enforceDeadline = true;
        [SerializeField] private float missionDeadlineSeconds = 180f;

        private GameLoop _loop;
        private MissionState _state = MissionState.Active;
        private int _wave = 1;
        private int _waveTarget;
        private bool _armed;
        private bool _winLatched;
        private bool _loseLatched;
        private bool _awaitingReinforcements;
        private float _reinforceTimer;
        private float _holdElapsed;
        private float _missionElapsed;
        private bool _combatCleared;
        private int _completedBuildings;
        private bool _deadlineFail;

        public MissionState State => _state;
        public int StalkersRemaining { get; private set; }
        public int WaveTarget => Mathf.Max(1, _waveTarget);
        public int Wave => _wave;
        public bool IsWon => _state == MissionState.Won;
        public bool IsLost => _state == MissionState.Lost;

        public bool CombatCleared => _combatCleared;
        public bool HoldComplete => _holdElapsed >= holdSecondsRequired;
        public bool ColonyComplete =>
            _completedBuildings >= buildingsRequired ||
            (metalsGoal > 0 && _loop != null && _loop.Resources != null &&
             _loop.Resources.Get(ResourceId.Metals) >= metalsGoal);

        public float HoldElapsed => _holdElapsed;
        public float HoldRequired => holdSecondsRequired;
        public float MissionElapsed => _missionElapsed;
        public float MissionDeadline => missionDeadlineSeconds;
        public bool DeadlineEnabled => enforceDeadline;
        public bool WasDeadlineFail => _deadlineFail;
        public int CompletedBuildings => _completedBuildings;
        public int BuildingsRequired => buildingsRequired;
        public int MetalsGoal => metalsGoal;
        public int MetalsCurrent =>
            _loop != null && _loop.Resources != null ? _loop.Resources.Get(ResourceId.Metals) : 0;

        public string ObjectiveLabel
        {
            get
            {
                if (_state == MissionState.Won) return "Outpost secured — all stakes met";
                if (_awaitingReinforcements) return "Reinforcements inbound…";
                if (!_combatCleared)
                {
                    return _wave <= 1
                        ? "Wave 1 — clear Dust Stalkers"
                        : "Wave 2 — clear reinforcements";
                }
                if (!HoldComplete) return "Hold the outpost";
                if (!ColonyComplete) return "Finish a construction order";
                return "Securing…";
            }
        }

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            _armed = false;
            _state = MissionState.Active;
            _wave = 1;
            _waveTarget = 0;
            _winLatched = false;
            _loseLatched = false;
            _awaitingReinforcements = false;
            _reinforceTimer = 0f;
            _holdElapsed = 0f;
            _missionElapsed = 0f;
            _combatCleared = false;
            _completedBuildings = 0;
            _deadlineFail = false;
        }

        public void Tick()
        {
            if (_loop == null) return;

            StalkersRemaining = CountLivingStalkers();
            RefreshColonyProgress();

            if (!_armed)
            {
                _waveTarget = StalkersRemaining;
                if (_waveTarget > 0 || Time.timeSinceLevelLoad > 1f)
                {
                    _armed = true;
                    if (_waveTarget <= 0)
                        _combatCleared = true;
                }
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

            // Hold clock only while the outpost is standing.
            _holdElapsed = Mathf.Min(holdSecondsRequired, _holdElapsed + Time.deltaTime);

            if (enforceDeadline && _missionElapsed >= missionDeadlineSeconds &&
                !(_combatCleared && HoldComplete && ColonyComplete))
            {
                _deadlineFail = true;
                EnterLost();
                return;
            }

            if (_awaitingReinforcements)
            {
                _reinforceTimer -= Time.deltaTime;
                if (_reinforceTimer <= 0f)
                    SpawnReinforcementWave();
                return;
            }

            if (!_combatCleared && _waveTarget > 0 && StalkersRemaining <= 0)
            {
                if (_wave <= 1)
                    BeginReinforcementCountdown();
                else
                {
                    _combatCleared = true;
                    DemoAudio.PlayClaim();
                    Debug.Log("[Mission] Combat stake clear — hold + construction still required.");
                }
            }

            if (_combatCleared && HoldComplete && ColonyComplete)
                EnterWon();
        }

        public void OnPartyRevived()
        {
            if (_state == MissionState.Lost)
            {
                _state = MissionState.Active;
                _loseLatched = false;
            }
        }

        public void DismissWinToSandbox()
        {
            _winLatched = true;
        }

        private void RefreshColonyProgress()
        {
            _completedBuildings = 0;
            if (_loop.Placer == null || _loop.Placer.Orders == null) return;
            for (int i = 0; i < _loop.Placer.Orders.Count; i++)
            {
                var o = _loop.Placer.Orders[i];
                if (o != null && o.IsComplete)
                    _completedBuildings++;
            }
        }

        private void BeginReinforcementCountdown()
        {
            _awaitingReinforcements = true;
            _reinforceTimer = reinforcementDelay;
            DemoAudio.PlayFlagPost();
            Debug.Log("[Mission] Wave 1 cleared — reinforcements inbound.");
        }

        private void SpawnReinforcementWave()
        {
            _awaitingReinforcements = false;
            _wave = 2;

            Vector3 origin = ColonyLayout.CampusOriginFor(_loop.FocusedCampus);
            // Prefer an uncleared lair so wave 2 feels like the moon pushing back.
            if (_loop.World != null)
            {
                var lairs = _loop.World.Lairs;
                for (int i = 0; i < lairs.Count; i++)
                {
                    if (lairs[i] != null && !lairs[i].IsCleared)
                    {
                        origin = lairs[i].WorldPosition;
                        break;
                    }
                }
            }

            int spawned = _loop.SpawnStalkerWave(
                Mathf.Max(1, reinforcementCount),
                10f,
                origin);
            _waveTarget = spawned;
            StalkersRemaining = CountLivingStalkers();
            DemoAudio.PlayBite();
            Debug.Log($"[Mission] Wave 2 — {spawned} Dust Stalker(s) closing from procedural dens.");
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
                Debug.Log("[Mission] Victory — combat, hold, and colony stakes met.");
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
