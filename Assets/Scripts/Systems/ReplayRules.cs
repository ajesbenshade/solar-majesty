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
        public const string IronmanKey = "SM_Replay_Ironman";

        public static ColonyRunMode Mode = ColonyRunMode.Campaign;
        public static ChallengeId Challenge = ChallengeId.None;
        public static DoctrineStance Stance = DoctrineStance.Balanced;

        public static void Restore(SaveReplayState saved)
        {
            if (saved == null) return;
            Mode = System.Enum.IsDefined(typeof(ColonyRunMode), saved.mode) ? (ColonyRunMode)saved.mode : ColonyRunMode.Campaign;
            Challenge = System.Enum.IsDefined(typeof(ChallengeId), saved.challenge) ? (ChallengeId)saved.challenge : ChallengeId.None;
            Stance = System.Enum.IsDefined(typeof(DoctrineStance), saved.stance) ? (DoctrineStance)saved.stance : DoctrineStance.Balanced;
        }
        /// <summary>PlayerPrefs preference. Latches into <see cref="IronmanRun"/> at New Game / Continue.</summary>
        public static bool Ironman;

        /// <summary>
        /// In-progress run latch. Manual saves and body restarts consult this so a mid-session
        /// settings flip cannot cheese No Second Draft or un-ironman a live attempt.
        /// </summary>
        public static bool IronmanRun { get; private set; }

        // What the settings do to a run is defined once, on RunRules. These read the process-wide
        // settings; a colony reads its own through GameLoop.Rules.
        public static bool IsEndless => RunRules.Current().IsEndless;
        public static bool BlocksManualSavesAndReloads => RunRules.Current().BlocksManualSavesAndReloads;
        public static float StartStockpileScale => RunRules.Current().StartStockpileScale;
        public static float ResupplyIntervalScale => RunRules.Current().ResupplyIntervalScale;
        public static int ExtraDockFee => RunRules.Current().ExtraDockFee;
        public static float FaunaCapScale => RunRules.Current().FaunaCapScale;
        public static float AmbientThreatMul => RunRules.Current().AmbientThreatMul;
        public static float FaunaWeightMul => RunRules.Current().FaunaWeightMul;
        public static float FaunaSpawnIntervalScale => RunRules.Current().FaunaSpawnIntervalScale;
        public static float GreedHungerBias => RunRules.Current().GreedHungerBias;
        public static float CourageScale => RunRules.Current().CourageScale;
        public static float WorkshopBonusExtra => RunRules.Current().WorkshopBonusExtra;
        public static float ConsiderRangeScale => RunRules.Current().ConsiderRangeScale;

        public static string ModeLabel => Mode == ColonyRunMode.Endless ? Loc.S("ENDLESS") : Loc.S("CAMPAIGN");

        public static string ChallengeLabel => Challenge switch
        {
            ChallengeId.Austere => Loc.S("AUSTERE"),
            ChallengeId.Swarm => Loc.S("SWARM"),
            ChallengeId.TightPurse => Loc.S("TIGHT PURSE"),
            _ => Loc.S("STANDARD")
        };

        public static string StanceLabel => Stance switch
        {
            DoctrineStance.OpenHands => Loc.S("OPEN HANDS"),
            DoctrineStance.AegisWatch => Loc.S("AEGIS WATCH"),
            DoctrineStance.SurveyFirst => Loc.S("SURVEY FIRST"),
            _ => Loc.S("BALANCED")
        };

        public static string IronmanLabel => Ironman ? Loc.S("IRONMAN") : Loc.S("OPEN");

        public static string HudTag
        {
            get
            {
                string tag = ModeLabel;
                if (Challenge != ChallengeId.None) tag += "  ·  " + ChallengeLabel;
                if (Stance != DoctrineStance.Balanced) tag += "  ·  " + StanceLabel;
                if (IronmanRun || Ironman) tag += "  ·  " + Loc.S("IRONMAN");
                return tag;
            }
        }

        public static string StanceHint => Stance switch
        {
            DoctrineStance.OpenHands => Loc.S("Open Hands: cheaper flags (hunger +0.26, past the 0.75 greed bypass). Less courage."),
            DoctrineStance.AegisWatch => Loc.S("Aegis Watch: braver hunts (courage ×1.22) and stronger workshop pull (+0.18)."),
            DoctrineStance.SurveyFirst => Loc.S("Survey First: longer consider range (×1.50) and modest shop pull (+0.10)."),
            _ => Loc.S("Balanced: no hunger, courage, or workshop nudge.")
        };

        public static string ChallengeHint => Challenge switch
        {
            ChallengeId.Austere => Loc.S("Austere: 55% starting stockpile (New Game / reload). Commons + HAB + workshop still fit."),
            ChallengeId.Swarm => Loc.S("Swarm: more fauna (cap ×1.50) at a slower spawn cadence (reload). Post {key:FlagDefend}/{key:FlagClearThreat}."),
            ChallengeId.TightPurse => Loc.S("Tight Purse: Earth ship ×1.55 slower and +80 EU dock fee. Drop stockpile is unchanged."),
            _ => Loc.S("Standard: no challenge modifiers.")
        };

        public static string IronmanHint =>
            Ironman
                ? Loc.S("Ironman: no manual saves, no body restart or abandon. Autosave still covers a crash. Latches at New Game / Continue.")
                : Loc.S("Open run: manual slot saves and body restarts stay available.");

        public static void Load()
        {
            Mode = (ColonyRunMode)Mathf.Clamp(GamePrefs.GetInt(ModeKey, 0), 0, 1);
            Challenge = (ChallengeId)Mathf.Clamp(GamePrefs.GetInt(ChallengeKey, 0), 0, 3);
            Stance = (DoctrineStance)Mathf.Clamp(GamePrefs.GetInt(StanceKey, 0), 0, 3);
            Ironman = GamePrefs.GetInt(IronmanKey, 0) == 1;
        }

        public static void Save()
        {
            GamePrefs.SetInt(ModeKey, (int)Mode);
            GamePrefs.SetInt(ChallengeKey, (int)Challenge);
            GamePrefs.SetInt(StanceKey, (int)Stance);
            GamePrefs.SetInt(IronmanKey, Ironman ? 1 : 0);
            GamePrefs.Save();
        }

        /// <summary>Copy the title-screen preference into the live run. Call from EnterPlaying when not restoring a save.</summary>
        public static void LatchRun()
        {
            IronmanRun = Ironman;
        }

        /// <summary>Continue restores the saved latch so toggling the chip on title cannot un-ironman a slot.</summary>
        public static void ApplyIronmanFromSave(SaveReplayState replay)
        {
            bool on = replay != null && replay.ironman;
            Ironman = on;
            IronmanRun = on;
        }

        public static void CycleMode() =>
            Mode = Mode == ColonyRunMode.Campaign ? ColonyRunMode.Endless : ColonyRunMode.Campaign;

        public static void CycleChallenge() =>
            Challenge = (ChallengeId)(((int)Challenge + 1) % 4);

        public static void CycleStance() =>
            Stance = (DoctrineStance)(((int)Stance + 1) % 4);

        public static void CycleIronman() => Ironman = !Ironman;

        /// <summary>EditMode hygiene — does not wipe a player's live prefs unless tests call Save afterwards.</summary>
        public static void ResetForTests()
        {
            Mode = ColonyRunMode.Campaign;
            Challenge = ChallengeId.None;
            Stance = DoctrineStance.Balanced;
            Ironman = false;
            IronmanRun = false;
            GamePrefs.DeleteKey(ModeKey);
            GamePrefs.DeleteKey(ChallengeKey);
            GamePrefs.DeleteKey(StanceKey);
            GamePrefs.DeleteKey(IronmanKey);
            GamePrefs.Save();
        }
    }
}
