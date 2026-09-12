using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Committed overhaul numbers from Docs/GROK_GAMEPLAY_OVERHAUL.md.
    /// Does not change SpecialistBrain.ScoreFlag.
    /// </summary>
    public static class OverseerRules
    {
        public const float RecoverSeconds = 12f;
        public const float ScrapWindow = 90f;
        public const float ScrapChance = 0.40f;
        public const float RefabCostScale = 0.70f;
        public const float RefabSeconds = 40f;
        public const float SalvageCreditFrac = 0.40f;

        public const int ReviveMet = 40;
        public const int ReviveIce = 0;
        /// <summary>Each successful revive of that mech multiplies the scrapyard bill (Majesty temple tax).</summary>
        public const float ReviveCostGrowth = 1.5f;
        public const int ReviveCostMaxSteps = 8;
        public const float ReviveCooldown = 120f;
        public const float ReviveHp = 0.50f;
        public const float ReviveFatigue = 0.40f;
        public const int ReviveRatingPenalty = 8;
        public const float RecoverHp = 0.28f;
        public const float RecoverFatigue = 0.60f;
        public const float EmptyRosterFailSeconds = 20f;

        public const float TitheRate = 0.12f;
        public const int TitheCap = 18;
        public const float TitheFloor = 25f;
        public const int ThinMetals = 20;
        public const float ThinMetalsHunger = 0.15f;

        /// <summary>Personal MET the specialist keeps from an extract haul (stockpile still gets GrantExtractYield).</summary>
        public const int ExtractPurseMet = 4;
        public const int ExtractPurseMetBonus = 2;
        public const int PestKillMet = 3;
        public const int StalkerKillMet = 8;
        public const int JunkKillMet = 2;
        public const int InnStayMet = 2;
        public const float InnStaySeconds = 8f;
        public const int WorkshopRepairMet = 4;
        public const float WorkshopRepairHp = 0.22f;
        public const float WorkshopRepairFatigue = 0.18f;

        public const int LevelCap = 10;
        public const int LevelStart = 1;
        public const float LevelHpPerStep = 0.08f;
        public const float LevelDpsPerStep = 0.07f;
        public const int XpFlag = 12;
        public const int XpClearThreat = 18;
        public const int XpExtract = 10;
        public const int XpDen = 25;
        public const int XpPest = 8;
        public const int XpStalker = 14;
        public const int XpJunk = 5;

        public const int JunkBotCap = 3;
        public const float JunkBotSpawnInterval = 12f;
        public const float JunkDeathMemory = 36f;
        public const float JunkBotBiteDps = 0.035f;
        public const int JunkBotStealMet = 1;
        public const float JunkBotStealSeconds = 2.4f;

        public const float PowerShortWork = 0.70f;
        public const int IceDeathThreshold = 4;
        public const float SustainMetPerMin = 1.5f;
        public const float SustainIcePerMin = 1.0f;

        public const float BuildLabourRadius = 28f;
        public const float PartyFollowerWork = 0.55f;
        public const float PartyFollowerRange = 8f;

        public const float SurveyRadius = 22f;
        public const float SurveySeconds = 90f;
        public const float SurveyExtractMul = 1.25f;
        public const float SurveyScienceExtra = 8f;
        public const float ScoutedDenWorkMul = 0.70f;
        public const float ScoutedDenPostRange = 12f;

        public const float DefendWatchSeconds = 50f;
        public const float DefendWatchDps = 4f;
        public const float DefendWatchRadius = 16f;
        public const float SentinelWatchExtra = 20f;

        public const float BatteryRange = 18f;
        public const float BatteryDps = 4f;
        public const float BatteryRetarget = 0.5f;
        public const int BatteryExtraPwr = 2;

        public const float CommonsShadeRadius = 20f;
        public const float CommonsShadeDanger = 0.85f;

        public const float DefenseStalkerDpsMul = 1.35f;
        public const float SentinelStalkerDpsMul = 0.85f;
        public const float MedicDownedRecoverMul = 2f;
        public const float MedicRange = 3.6f;
        public const float HarvesterExtractMul = 1.25f;
        public const float SurveyorScienceExtra = 8f;
        public const int GeologistExtractExtraMet = 2;
        public const float TerraformerPulse = 0.02f;
        public const float TerraformerPulseInterval = 30f;
        public const float TerraformerFarmRange = 10f;
        public const float CourierPadRange = 8f;
        public const float CourierResupplyScale = 0.85f;
        public const float CourierOutpostWork = 1.20f;

        public const float PressureInterval = 75f;
        public const float FrenzyPressure = 50f;
        public const float FrenzySpeed = 1.25f;
        public const float FrenzyBite = 1.20f;
        public const float RaidAbortDamageWindow = 1.5f;
        public const float RaidAbortHealth = 0.50f;

        /// <summary>
        /// Empty-start drop window. Dens still generate and wander at the rim; campus
        /// pests and structure raids wait so Commons (18s) or a Battery (16s) can finish.
        /// </summary>
        public const float FaunaGraceSeconds = 90f;
        /// <summary>After a module completes, wait before attracting a campus pest onto it.</summary>
        public const float FaunaExpandDelay = 18f;
        /// <summary>
        /// Continue/load with a standing Commons: dens stay on the rim, but leeches/wisps
        /// wait this long so the player can read the board and post Clear Threat.
        /// </summary>
        public const float ContinueReentrySeconds = 30f;

        /// <summary>
        /// One leech/wisp latch steals this fraction of that Power Node's gen.
        /// Stacked latches add; <see cref="PowerSiphonStackCap"/> keeps the node SICK, not 0.
        /// </summary>
        public const float PowerSiphonPerLatch = 0.35f;
        /// <summary>Max fraction of a node's gen stolen no matter how many leeches/wisps stack.</summary>
        public const float PowerSiphonStackCap = 0.65f;
        /// <summary>
        /// Stockpile snk while latched, per Power Node (not per pest). 1 PWR / 1.2s.
        /// Three stacked leeches used to dump 120 PWR in ~32s at 1/0.8s each.
        /// </summary>
        public const float PowerSiphonStockpileInterval = 1.2f;
        public const int PowerSiphonStockpileAmount = 1;

        /// <summary>
        /// First colonist death after ICE drops below <see cref="IceDeathThreshold"/>.
        /// Was hitchhiked on the 24s tax tick (could fire the same frame ICE went critical).
        /// 28s covers post-F2 + ~20s walk/clear at 3.5 m/s.
        /// </summary>
        public const float LifeSupportFailSeconds = 28f;

        public const float RefusalChipSeconds = 2.4f;
        public const float RefusalRetrigger = 4f;

        /// <summary>
        /// Smallest integer bounty that matches the greed-gate display (Engineer ~79).
        /// Must ceil, not round: rounding down produces an ask the hero then refuses.
        /// </summary>
        public static int GreedAsk(SpecialistData data)
        {
            if (data == null) return 18;
            return Mathf.Max(1, Mathf.CeilToInt((18f + data.baseGreed * 95f) * 0.78f));
        }

        public static float StackShare(int rank)
        {
            if (rank <= 0) return 1f;
            if (rank == 1) return 0.55f;
            if (rank == 2) return 0.35f;
            return 0.20f;
        }

        public static int RefabMetals(BuildingData data)
        {
            if (data?.buildCost == null) return 25;
            int met = 0;
            for (int i = 0; i < data.buildCost.Length; i++)
            {
                if (data.buildCost[i].resource == ResourceId.Metals)
                    met += data.buildCost[i].amount;
            }
            return Mathf.Max(1, Mathf.RoundToInt(met * RefabCostScale));
        }

        /// <summary>Scrapyard CRED for this mech. n=0 → 40, then ×1.5 each step.</summary>
        public static int ReviveMetals(int reviveCount)
        {
            int n = Mathf.Clamp(reviveCount, 0, ReviveCostMaxSteps);
            return Mathf.Max(ReviveMet, Mathf.RoundToInt(ReviveMet * Mathf.Pow(ReviveCostGrowth, n)));
        }

        /// <summary>Yard bill scales with hero level (L1 = base).</summary>
        public static int ReviveMetalsForLevel(int level) =>
            ReviveMetals(Mathf.Max(0, level - 1));

        public static int ReviveIceCost(int reviveCount) => 0;

        /// <summary>Cumulative XP required to stand at this level (L1 = 0).</summary>
        public static int XpToReach(int level)
        {
            if (level <= 1) return 0;
            int cap = Mathf.Min(level, LevelCap);
            int xp = 0;
            for (int l = 1; l < cap; l++)
                xp += XpForNext(l);
            return xp;
        }

        /// <summary>XP to go from <paramref name="fromLevel"/> to fromLevel+1. L1→2 = 40.</summary>
        public static int XpForNext(int fromLevel)
        {
            int l = Mathf.Clamp(fromLevel, 1, LevelCap - 1);
            return 25 + l * 15;
        }

        public static float LevelHpMul(int level)
        {
            int steps = Mathf.Max(0, Mathf.Min(level, LevelCap) - 1);
            return 1f + steps * LevelHpPerStep;
        }

        public static float LevelDpsMul(int level)
        {
            int steps = Mathf.Max(0, Mathf.Min(level, LevelCap) - 1);
            return 1f + steps * LevelDpsPerStep;
        }

        public static int XpForFlag(FlagType type)
        {
            switch (type)
            {
                case FlagType.ClearThreat: return XpClearThreat;
                case FlagType.Extract: return XpExtract;
                default: return XpFlag;
            }
        }

        public static int XpForFauna(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.JunkBot: return XpJunk;
                case FaunaKind.Stalker: return XpStalker;
                default: return XpPest;
            }
        }

        public static int KillPurse(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.JunkBot: return JunkKillMet;
                case FaunaKind.Stalker: return StalkerKillMet;
                default: return PestKillMet;
            }
        }
    }

    public enum FlagRefusalKind
    {
        WouldTake = 0,
        Greed = 1,
        TooFar = 2,
        Hurt = 3,
        NotMyJob = 4,
        Hunting = 5,
        Ignored = 6
    }
}
