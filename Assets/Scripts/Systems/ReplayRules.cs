using UnityEngine;

namespace SolarMajesty
{
    public enum ColonyRunMode
    {
        Campaign = 0,
        Endless = 1
    }

    public enum ChallengeId
    {
        None = 0,
        Austere = 1,
        Swarm = 2,
        TightPurse = 3
    }

    /// <summary>
    /// Optional colony philosophy. Nudges existing SpecialistContext fields only —
    /// SpecialistBrain scoring is unchanged.
    /// </summary>
    public enum DoctrineStance
    {
        Balanced = 0,
        OpenHands = 1,
        AegisWatch = 2,
        SurveyFirst = 3
    }

    /// <summary>
    /// PlayerPrefs-backed replay modifiers. Economy / fauna apply on scene load;
    /// doctrine hunger/courage/workshop bonus apply live through SpecialistAgent context.
    /// </summary>
    public static class ReplayRules
    {
        public const string ModeKey = "SM_Replay_Mode";
        public const string ChallengeKey = "SM_Replay_Challenge";
        public const string StanceKey = "SM_Replay_Stance";

        public static ColonyRunMode Mode = ColonyRunMode.Campaign;
        public static ChallengeId Challenge = ChallengeId.None;
        public static DoctrineStance Stance = DoctrineStance.Balanced;

        public static bool IsEndless => Mode == ColonyRunMode.Endless;

        /// <summary>
        /// Austere Earth: 340 MET × 0.55 = 187. Palace 70 + airlock 8 + HAB 50 + workshop 36 = 164.
        /// Leftover ~23 MET — tight, workshop still affordable. Do not drop below 0.50.
        /// </summary>
        public static float StartStockpileScale => Challenge == ChallengeId.Austere ? 0.55f : 1f;
        public static float ResupplyIntervalScale => Challenge == ChallengeId.TightPurse ? 1.55f : 1f;
        public static int ExtraDockFee => Challenge == ChallengeId.TightPurse ? 8 : 0;
        public static float FaunaCapScale => Challenge == ChallengeId.Swarm ? 1.50f : 1f;
        public static float AmbientThreatMul => Challenge == ChallengeId.Swarm ? 1.28f : 1f;
        public static float FaunaWeightMul => Challenge == ChallengeId.Swarm ? 1.22f : 1f;
        /// <summary>Swarm stretches spawn cadence so cap fills over a session, not a two-minute dump.</summary>
        public static float FaunaSpawnIntervalScale => Challenge == ChallengeId.Swarm ? 1.35f : 1f;

        /// <summary>
        /// Open Hands +0.26 on spawn hunger 0.55 → 0.81, which clears the 0.75 cheap-flag greed bypass.
        /// </summary>
        public static float GreedHungerBias =>
            Stance == DoctrineStance.OpenHands ? 0.26f :
            Stance == DoctrineStance.AegisWatch ? -0.08f : 0f;

        public static float CourageScale =>
            Stance == DoctrineStance.AegisWatch ? 1.22f :
            Stance == DoctrineStance.OpenHands ? 0.90f : 1f;

        public static float WorkshopBonusExtra =>
            Stance == DoctrineStance.AegisWatch ? 0.18f :
            Stance == DoctrineStance.SurveyFirst ? 0.10f : 0f;

        /// <summary>Survey First 1.50 → Brain.ConsiderRange 120; Evaluate uses max(explore, range×0.7) ≈ 84 m vs 75.</summary>
        public static float ConsiderRangeScale =>
            Stance == DoctrineStance.SurveyFirst ? 1.50f : 1f;

        public static string ModeLabel => Mode == ColonyRunMode.Endless ? "ENDLESS" : "CAMPAIGN";

        public static string ChallengeLabel => Challenge switch
        {
            ChallengeId.Austere => "AUSTERE",
            ChallengeId.Swarm => "SWARM",
            ChallengeId.TightPurse => "TIGHT PURSE",
            _ => "STANDARD"
        };

        public static string StanceLabel => Stance switch
        {
            DoctrineStance.OpenHands => "OPEN HANDS",
            DoctrineStance.AegisWatch => "AEGIS WATCH",
            DoctrineStance.SurveyFirst => "SURVEY FIRST",
            _ => "BALANCED"
        };

        public static string HudTag
        {
            get
            {
                string tag = ModeLabel;
                if (Challenge != ChallengeId.None) tag += "  ·  " + ChallengeLabel;
                if (Stance != DoctrineStance.Balanced) tag += "  ·  " + StanceLabel;
                return tag;
            }
        }

        public static string StanceHint => Stance switch
        {
            DoctrineStance.OpenHands => "Open Hands: cheaper flags (hunger +0.26, past the 0.75 greed bypass). Less courage.",
            DoctrineStance.AegisWatch => "Aegis Watch: braver hunts (courage ×1.22) and stronger workshop pull (+0.18).",
            DoctrineStance.SurveyFirst => "Survey First: longer consider range (×1.50) and modest shop pull (+0.10).",
            _ => "Balanced: no hunger, courage, or workshop nudge."
        };

        public static string ChallengeHint => Challenge switch
        {
            ChallengeId.Austere => "Austere: 55% starting stockpile (New Game / reload). Palace + airlock + HAB + workshop still fit.",
            ChallengeId.Swarm => "Swarm: more fauna (cap ×1.50) at a slower spawn cadence (reload). Post F5/F2.",
            ChallengeId.TightPurse => "Tight Purse: Earth ship ×1.55 slower and +8 MET dock fee. Drop stockpile is unchanged.",
            _ => "Standard: no challenge modifiers."
        };

        public static void Load()
        {
            Mode = (ColonyRunMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 1);
            Challenge = (ChallengeId)Mathf.Clamp(PlayerPrefs.GetInt(ChallengeKey, 0), 0, 3);
            Stance = (DoctrineStance)Mathf.Clamp(PlayerPrefs.GetInt(StanceKey, 0), 0, 3);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(ModeKey, (int)Mode);
            PlayerPrefs.SetInt(ChallengeKey, (int)Challenge);
            PlayerPrefs.SetInt(StanceKey, (int)Stance);
            PlayerPrefs.Save();
        }

        public static void CycleMode() =>
            Mode = Mode == ColonyRunMode.Campaign ? ColonyRunMode.Endless : ColonyRunMode.Campaign;

        public static void CycleChallenge() =>
            Challenge = (ChallengeId)(((int)Challenge + 1) % 4);

        public static void CycleStance() =>
            Stance = (DoctrineStance)(((int)Stance + 1) % 4);
    }
}
