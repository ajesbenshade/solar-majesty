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
        public const int ReviveIce = 8;
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

        public const float RefusalChipSeconds = 2.4f;
        public const float RefusalRetrigger = 4f;

        /// <summary>Smallest integer bounty that matches the greed-gate display (Engineer ~79).</summary>
        public static int GreedAsk(SpecialistData data)
        {
            if (data == null) return 18;
            return Mathf.Max(1, Mathf.RoundToInt((18f + data.baseGreed * 95f) * 0.78f));
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
