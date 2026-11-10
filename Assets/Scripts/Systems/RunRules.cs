// A run's rules as plain data — pure C#, no MonoBehaviour.

using System;

namespace SolarMajesty
{
    /// <summary>
    /// The rules a colony runs under (<see cref="ReplayRules"/>), copied out of the process-wide
    /// settings so a sim, a command log or a second overseer can carry its own.
    /// </summary>
    [Serializable]
    public struct RunRules
    {
        public int mode;
        public int challenge;
        public int stance;
        public bool ironman;

        public ColonyRunMode Mode => (ColonyRunMode)mode;
        public ChallengeId Challenge => (ChallengeId)challenge;
        public DoctrineStance Stance => (DoctrineStance)stance;

        public bool IsEndless => Mode == ColonyRunMode.Endless;

        /// <summary>True once the run latched Ironman: blocks manual slot saves and body restarts.</summary>
        public bool BlocksManualSavesAndReloads => ironman;

        /// <summary>
        /// Austere Earth: 3,400 CRED × 0.55 = 1,870. Commons 500 + HAB 150 + a Rogues-tier
        /// workshop 250 + hire 100 = 1,000. Leftover ~870 CRED for the first flags. Do not drop below 0.50.
        /// </summary>
        public float StartStockpileScale => Challenge == ChallengeId.Austere ? 0.55f : 1f;
        public float ResupplyIntervalScale => Challenge == ChallengeId.TightPurse ? 1.55f : 1f;
        public int ExtraDockFee => Challenge == ChallengeId.TightPurse ? 80 : 0;
        public float FaunaCapScale => Challenge == ChallengeId.Swarm ? 1.50f : 1f;
        public float AmbientThreatMul => Challenge == ChallengeId.Swarm ? 1.28f : 1f;
        public float FaunaWeightMul => Challenge == ChallengeId.Swarm ? 1.22f : 1f;
        /// <summary>Swarm stretches spawn cadence so cap fills over a session, not a two-minute dump.</summary>
        public float FaunaSpawnIntervalScale => Challenge == ChallengeId.Swarm ? 1.35f : 1f;

        /// <summary>
        /// Open Hands +0.26 on spawn hunger 0.55 → 0.81, which clears the 0.75 cheap-flag greed bypass.
        /// </summary>
        public float GreedHungerBias =>
            Stance == DoctrineStance.OpenHands ? 0.26f :
            Stance == DoctrineStance.AegisWatch ? -0.08f : 0f;

        public float CourageScale =>
            Stance == DoctrineStance.AegisWatch ? 1.22f :
            Stance == DoctrineStance.OpenHands ? 0.90f : 1f;

        public float WorkshopBonusExtra =>
            Stance == DoctrineStance.AegisWatch ? 0.18f :
            Stance == DoctrineStance.SurveyFirst ? 0.10f : 0f;

        /// <summary>Survey First 1.50 → Brain.ConsiderRange 120; Evaluate uses max(explore, range×0.7) ≈ 84 m vs 75.</summary>
        public float ConsiderRangeScale =>
            Stance == DoctrineStance.SurveyFirst ? 1.50f : 1f;

        public static RunRules Current() => new RunRules
        {
            mode = (int)ReplayRules.Mode,
            challenge = (int)ReplayRules.Challenge,
            stance = (int)ReplayRules.Stance,
            ironman = ReplayRules.IronmanRun
        };
    }
}
