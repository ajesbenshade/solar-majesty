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
        [SerializeField] private int treasuryGoal = 5000;

        [Header("Launch gate (tech tree)")]
        [Tooltip("When true, Lunar Rocket (or other launch unlock) must be researched.")]
        [SerializeField] private bool requireLaunchTech = true;

        [Header("Optional pressure")]
        [SerializeField] private bool spawnPressureFromLairs = true;
        [SerializeField] private int pressureStalkerCount = 2;
        [SerializeField] private float pressureCooldown = 75f;

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

        private bool _loggedDens;
        private bool _loggedSustain;
        private bool _loggedFrenzy;
        private bool _frenzy;

        public MissionState State => _state;
        /// <summary>Living fauna that hold the dens gate (town spawns never do, see <see cref="DensGate"/>).</summary>
        public int StalkersRemaining { get; private set; }
        public bool IsWon => _state == MissionState.Won;
        public bool IsLost => _state == MissionState.Lost;

        public bool DensCleared { get; private set; }
        public bool SustainComplete => _sustainElapsed >= sustainHoldSeconds;
        public bool LaunchReady => _launchReady;
        public bool AllGatesMet => DensCleared && SustainComplete && LaunchReady;
        public bool FrenzyActive => _frenzy;
        public int GatesComplete
        {
            get
            {
                int n = 0;
                if (DensCleared) n++;
                if (SustainComplete) n++;
                if (LaunchReady) n++;
                return n;
            }
        }

        public float SustainElapsed => _sustainElapsed;
        public float SustainRequired => sustainHoldSeconds;
        public int TreasuryGoal => treasuryGoal;
        public int TreasuryCurrent =>
            _loop != null && _loop.Settlement != null ? _loop.Settlement.Treasury : 0;
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
                    if (ReplayRules.IsEndless)
                        return Loc.S("Keep colonizing — dens quiet, colony holding");
                    if (_loop != null && !CampaignProgress.NextAfter(_loop.ActiveBody).HasValue)
                        return Loc.F("{0} holds — campaign spine complete", _loop.BodyProfile?.DisplayNameText ?? Loc.S("Outpost"));
                    return Loc.S("Outpost secured — advance the campaign");
                }
                if (!DensCleared)
                {
                    if (LairCount > 0 && UnclearedLairs > 0)
                        return Loc.F("Clear dens — {0} left ({1} Clear Threat)", UnclearedLairs, InputBindings.Hint(GameAction.FlagClearThreat));
                    // Every den is quiet but the gate still waits on fauna that came out of them
                    // (pressure waves do not scatter). "Clear dens — 0 left" sent players looking
                    // for a den that was not there (autoplay, Earth: six sim-minutes of it).
                    if (LairCount > 0 && StalkersRemaining > 0)
                        return Loc.F("Dens quiet — hunt the last {0} fauna ({1} Clear Threat)", StalkersRemaining, InputBindings.Hint(GameAction.FlagClearThreat));
                    return Loc.S("Clear remaining fauna near campus");
                }
                if (!SustainComplete)
                {
                    string hint = _loop?.Settlement != null ? _loop.Settlement.SustainHint : Loc.S("hold population");
                    return Loc.F("Sustain — {0}", hint);
                }
                if (!LaunchReady)
                {
                    if (_loop != null && _loop.Research != null &&
                        !_loop.Research.HasLaunchUnlockFor(_loop.ActiveBody))
                    {
                        string craft = _loop.Research.LaunchTechLabel(_loop.ActiveBody);
                        return Loc.F("Research {0} (TECH · {1}) — then place a Landing Pad", craft, InputBindings.Hint(GameAction.ToolTech));
                    }
                    return Loc.S("Place a Landing Pad — departure craft stages there");
                }
                return Loc.S("Securing…");
            }
        }

        public string WinHeadline
        {
            get
            {
                if (ReplayRules.IsEndless)
                    return Loc.S("KEEP COLONIZING");
                if (_loop != null && !CampaignProgress.NextAfter(_loop.ActiveBody).HasValue)
                    return Loc.S("SOLAR CONQUEST COMPLETE");
                return Loc.S("OUTPOST SECURED");
            }
        }

        public string WinDetail
        {
            get
            {
                if (ReplayRules.IsEndless)
                {
                    var endless = _loop != null ? _loop.BodyProfile : null;
                    if (endless != null && !string.IsNullOrEmpty(endless.EndlessLog))
                        return endless.EndlessLogText;
                    return Loc.S("Gates met. Stay on this world — rating stands while you keep expanding.");
                }
                var body = _loop != null ? _loop.BodyProfile : null;
                if (body != null && CampaignCutsceneCatalog.TryGetVictory(body.Id, out var cut))
                    return cut.LogParagraphText;
                if (body != null && !string.IsNullOrEmpty(body.VictoryLog))
                    return body.VictoryLogText;
                return Loc.S("Dens cleared · colony sustained · launch ready.");
            }
        }

        public string WinSubline
        {
            get
            {
                if (ReplayRules.IsEndless)
                    return Loc.S("Endless mode: no launch to the next world. Keep the colony standing.");
                if (DemoSettings.FirstHourDemo)
                {
                    string craft = _loop != null && _loop.Research != null
                        ? _loop.Research.LaunchTechLabel(CelestialBodyId.Earth)
                        : Loc.S("Lunar Rocket");
                    return LaunchPath.ObjectiveText(false, false, craft);
                }
                if (_loop == null || !CampaignProgress.NextAfter(_loop.ActiveBody).HasValue)
                    return Loc.S("The campaign is complete — every world from Earth to Europa is yours. Revisit any of them from the title.");
                var next = CampaignProgress.NextAfter(_loop.ActiveBody);
                string name = next.HasValue
                    ? CelestialBodyCatalog.Get(next.Value).DisplayNameText
                    : Loc.S("the next body");
                return Loc.F("Stage departure, or keep overseeing here. Next: {0}.", name);
            }
        }

        public string FailHeadline => WasDeadlineFail ? Loc.S("MISSION TIME EXPIRED") : Loc.S("OUTPOST LOST");

        public string FailDetail
        {
            get
            {
                var body = _loop != null ? _loop.BodyProfile : null;
                if (WasDeadlineFail)
                    return Loc.S("The window to secure the outpost closed. Restart for a tighter run.");
                if (_loop != null && _loop.ColonyExtinct)
                    return Loc.S("The Colony Commons fell and the treasury with it. Retry this body, or New Game.");
                if (_loop != null && _loop.RobotCount == 0)
                    return Loc.S("No robots left and no re-fab in the shop. Rebuild a workshop, or retry.");
                if (body != null && !string.IsNullOrEmpty(body.FailLog))
                    return body.FailLogText;
                return Loc.S("Every specialist is incapacitated. Revive the party to retake the plaza.");
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
            _loggedDens = false;
            _loggedSustain = false;
            _loggedFrenzy = false;
            _frenzy = false;
            DensCleared = false;
            UnclearedLairs = 0;
            LairCount = 0;

            var body = _loop.BodyProfile;
            if (body != null)
            {
                treasuryGoal = TreasuryGoalFor(body.StartMetals);
                sustainHoldSeconds = Mathf.Max(5f, body.SustainHoldSeconds);
            }

            _launchReady = !requireLaunchTech;
            if (_loop.Settlement != null)
                _loop.Settlement.SetTreasuryGoal(treasuryGoal, OverseerRules.SustainMetPerMin);
        }

        /// <summary>Hold half again the starting treasury, rounded up to 500 CRED.</summary>
        public static int TreasuryGoalFor(int startTreasury) =>
            Mathf.Max(1000, Mathf.CeilToInt(startTreasury * 1.5f / 500f) * 500);

        /// <summary>Week 2+: call when rocket tech is researched and craft is built.</summary>
        public void SetLaunchReady(bool ready) => _launchReady = ready;

        public void Restore(SaveMissionState s) => RestoreFrom(s);

        /// <summary>
        /// Continue restore of mission elapsed / sustain hold / win-loss latch.
        /// DensCleared is refreshed from restored lairs on the next Tick.
        /// </summary>
        public void RestoreFrom(SaveMissionState saved)
        {
            if (saved == null) return;
            _armed = true;
            _missionElapsed = Mathf.Max(0f, saved.elapsed);
            _sustainElapsed = Mathf.Max(0f, saved.sustainHold);
            _launchReady = saved.launchReady;
            DensCleared = saved.densCleared;
            if (saved.densCleared)
                _loggedDens = true;
            if (saved.sustainMet || _sustainElapsed >= sustainHoldSeconds)
                _loggedSustain = true;

            var state = (MissionState)saved.state;
            if (state == MissionState.Won)
            {
                _state = MissionState.Won;
                _winLatched = true;
            }
            else if (state == MissionState.Lost)
            {
                _state = MissionState.Lost;
                _loseLatched = true;
            }
            else
            {
                _state = MissionState.Active;
            }
        }

        public void Tick()
        {
            if (_loop == null) return;

            StalkersRemaining = CountDenFauna(_loop.Stalkers);
            RefreshGates();

            if (StillCaptureHold.Active)
            {
                ClearLossForStill();
                return;
            }

            if (!_armed)
            {
                if (Time.timeSinceLevelLoad > 1f)
                    _armed = true;
                return;
            }

            if (_state == MissionState.Won) return;

            if (_loop.ColonyExtinct || _loop.EmptyRosterFailed)
            {
                if (_state != MissionState.Lost)
                    EnterLost();
                return;
            }

            if (_state == MissionState.Lost)
                return;

            _missionElapsed += Time.deltaTime;
            TickSustain(Time.deltaTime);
            TickFrenzy();
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

        /// <summary>EditMode: set the three gates without a live colony or a world refresh.</summary>
        public void ArmForTests(bool densCleared, bool sustainComplete, bool launchReady)
        {
            _loop = null;
            _armed = true;
            _state = MissionState.Active;
            _winLatched = false;
            _loseLatched = false;
            if (sustainHoldSeconds < 1f)
                sustainHoldSeconds = 40f;
            DensCleared = densCleared;
            _sustainElapsed = sustainComplete ? sustainHoldSeconds : 0f;
            _launchReady = launchReady;
        }

        /// <summary>EditMode: the den board as <see cref="RefreshGates"/> would read it.</summary>
        internal void SetDenCountsForTests(int lairs, int uncleared, int stalkers)
        {
            LairCount = lairs;
            UnclearedLairs = uncleared;
            StalkersRemaining = stalkers;
            DensCleared = DensGate.Met(lairs, uncleared, stalkers);
        }

        /// <summary>EditMode: the den board with the live fauna list, counted as <see cref="Tick"/> counts it.</summary>
        internal void SetDenBoardForTests(int lairs, int uncleared, System.Collections.Generic.IReadOnlyList<DustStalkerAgent> fauna) =>
            SetDenCountsForTests(lairs, uncleared, CountDenFauna(fauna));

        /// <summary>
        /// EditMode: the same gate check <see cref="Tick"/> uses, without audio, VFX, or a scene load.
        /// </summary>
        public void EvaluateVictoryForTests()
        {
            if (_state == MissionState.Won || _state == MissionState.Lost) return;
            if (AllGatesMet)
                EnterWon();
        }

        /// <summary>
        /// CaptureStill shutter: drop a latched OUTPOST LOST so the fail modal cannot cover the campus.
        /// No-op unless <see cref="StillCaptureHold"/> is armed.
        /// </summary>
        public void ClearLossForStill()
        {
            if (!StillCaptureHold.Active) return;
            if (_state != MissionState.Lost) return;
            _state = MissionState.Active;
            _loseLatched = false;
            _deadlineFail = false;
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

            DensCleared = DensGate.Met(LairCount, UnclearedLairs, StalkersRemaining);

            if (DensCleared && !_loggedDens)
            {
                _loggedDens = true;
                _loop.LogOverseer(Loc.S("Dens quiet. Fauna no longer holds the plaza."));
            }
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

            if (SustainComplete && !_loggedSustain)
            {
                _loggedSustain = true;
                _loop.LogOverseer(Loc.F("Treasury holding — {0:N0}/{1:N0} EU for {2}s.", set.Treasury, treasuryGoal, Mathf.RoundToInt(sustainHoldSeconds)));
            }
        }

        private void TickFrenzy()
        {
            if (_frenzy || DensCleared) return;
            if (GatesComplete < 2) return;
            _frenzy = true;
            _loop?.ApplyFaunaFrenzy(true);
            if (!_loggedFrenzy)
            {
                _loggedFrenzy = true;
                _loop?.LogOverseer(Loc.S("Dens frenzy — they know you're leaving."));
            }
        }

        private void TickPressure(float dt)
        {
            if (!spawnPressureFromLairs || DensCleared) return;
            if (_loop.World == null || UnclearedLairs <= 0) return;
            if (_loop.InFaunaGrace)
            {
                _pressureTimer = Mathf.Max(_pressureTimer, pressureCooldown);
                return;
            }

            _pressureTimer -= dt;
            if (_pressureTimer > 0f) return;
            _pressureTimer = _frenzy ? OverseerRules.FrenzyPressure : OverseerRules.PressureInterval;

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

            int size = OverseerRules.PressureWaveSize(Mathf.Max(1, pressureStalkerCount), LooseFaunaAlive());
            if (size <= 0) return;
            int spawned = _loop.SpawnStalkerWave(
                size,
                10f,
                origin);
            if (spawned > 0)
                Debug.Log($"[Mission] Pressure wave — {spawned} from uncleared dens.");
        }

        private void EnterWon()
        {
            if (!VictoryOffer.ShouldLatch(
                    _state == MissionState.Won,
                    _state == MissionState.Lost,
                    AllGatesMet,
                    _winLatched))
                return;

            _state = MissionState.Won;
            _winLatched = true;

            // EditMode tests latch the gates without a colony, a stinger, or a scene load.
            if (_loop == null || !Application.isPlaying)
                return;

            _loop.NoteBodyConquered();
            bool nextBuilt = VictoryOffer.NextIsBuilt(_loop.ActiveBody);
            var offer = VictoryOffer.For(_loop.ActiveBody, nextBuilt, ReplayRules.IsEndless);
            if (offer.HoldSim)
                _loop.HoldForVictory();

            // The score's stinger is the fanfare; the SFX one is the fallback, never both.
            bool sting = AdaptiveMusic.PlayStinger(true);
            if (!sting) DemoAudio.PlayVictory();
            OverseerVoice.SpeakCue(VoiceCue.Victory, AlertSeverity.Critical, sting ? 4.2f : 0.8f);
            DemoVfx.ClaimRing(
                ColonyLayout.CampusOriginFor(_loop.FocusedCampus),
                new Color(0.35f, 0.95f, 0.55f));
            _loop.LogOverseer(offer.Body);
            // Slow orbit over the colony before the verdict draws (the HUD waits for it).
            bool spineDone = !ReplayRules.IsEndless && !CampaignProgress.NextAfter(_loop.ActiveBody).HasValue;
            CameraBeatDirector.PlayVictory(_loop, ColonyLayout.CampusOriginFor(_loop.FocusedCampus), spineDone
                ? Loc.S("THE SOLAR SYSTEM IS YOURS")
                : Loc.F("{0} CONQUERED", CelestialBodyCatalog.Get(_loop.ActiveBody).DisplayNameText.ToUpperInvariant()));
            if (ReplayRules.IsEndless)
                Debug.Log("[Mission] Victory — keep colonizing. Rating stands.");
            else
                Debug.Log("[Mission] Victory — dens cleared, colony sustained, launch ready.");
            if (!ReplayRules.IsEndless && !CampaignProgress.NextAfter(_loop.ActiveBody).HasValue)
                Debug.Log("[Mission] Campaign spine complete.");
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
                _loop.LogOverseer(FailDetail);
            }
        }

        /// <summary>Living fauna that are not a den's residents (pressure waves, yard pests).</summary>
        private int LooseFaunaAlive()
        {
            int residents = 0;
            var lairs = _loop.World != null ? _loop.World.Lairs : null;
            if (lairs != null)
            {
                for (int i = 0; i < lairs.Count; i++)
                {
                    var spawned = lairs[i] != null ? lairs[i].Spawned : null;
                    if (spawned == null) continue;
                    for (int j = 0; j < spawned.Count; j++)
                        if (spawned[j] != null && spawned[j].IsAlive) residents++;
                }
            }
            return Mathf.Max(0, CountLivingStalkers() - residents);
        }

        /// <summary>
        /// Fauna holding the dens gate: alive, not a town spawn, and not a yard pest or junk (<see cref="HoldsDens"/>). Burrow pests, edge raiders and
        /// wreck junk keep coming after the last den; counting them flipped the gate open and shut
        /// (autoplay: repeated "gates 1/3 / 2/3" after every den was cleared).
        /// </summary>
        internal static int CountDenFauna(System.Collections.Generic.IReadOnlyList<DustStalkerAgent> fauna)
        {
            if (fauna == null) return 0;
            int n = 0;
            for (int i = 0; i < fauna.Count; i++)
            {
                var s = fauna[i];
                if (s != null && DensGate.Holds(s.IsAlive, s.IsTownSpawn) && HoldsDens(s.Kind, s.IsAlive, false)) n++;
            }
            return n;
        }

        /// <summary>
        /// Does this fauna keep the dens gate open? Den stalkers do. The kingdom spawn table (alpha
        /// raiders at the colony edge, town burrows, scrapyard junk) keeps running after the dens are
        /// cleared, and yard pests are campus ecology, so neither can hold back the victory.
        /// </summary>
        internal static bool HoldsDens(FaunaKind kind, bool alive, bool kingdomSpawn) =>
            alive && !kingdomSpawn && !DustStalkerAgent.IsCampusPest(kind) && !DustStalkerAgent.IsJunk(kind);

        private int CountLivingStalkers()
        {
            if (_loop == null || _loop.Stalkers == null) return 0;
            int n = 0;
            for (int i = 0; i < _loop.Stalkers.Count; i++)
            {
                var s = _loop.Stalkers[i];
                if (s != null && HoldsDens(s.Kind, s.IsAlive, _loop.IsKingdomSpawn(s))) n++;
            }
            return n;
        }
    }
}
