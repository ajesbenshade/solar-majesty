namespace SolarMajesty
{
    /// <summary>
    /// Bonuses from unlocked doctrines and Secret Projects. Applied by GameLoop —
    /// SpecialistBrain scoring is not modified.
    /// </summary>
    public struct TechEffects
    {
        public float ExtractHaulBonus;
        public float FarmYieldBonus;
        public float MineYieldBonus;
        public float PowerDrawScale;
        public float FreightScale;
        public float ResupplyIntervalScale;
        public int ResupplyFeeDiscount;
        public float GrowIntervalScale;
        public int ExtraBeds;
        public float AmbientThreatScale;

        public static TechEffects Neutral => new TechEffects
        {
            PowerDrawScale = 1f,
            FreightScale = 1f,
            ResupplyIntervalScale = 1f,
            GrowIntervalScale = 1f,
            AmbientThreatScale = 1f
        };

        public static TechEffects From(ResearchManager research)
        {
            var e = Neutral;
            if (research == null) return e;

            if (research.IsUnlocked(TechId.HarvestDoctrine))
            {
                e.ExtractHaulBonus += 0.12f;
                e.MineYieldBonus += 0.15f;
            }

            if (research.IsUnlocked(TechId.CoreSampling))
                e.MineYieldBonus += 0.08f;

            if (research.IsUnlocked(TechId.AegisDoctrine))
                e.PowerDrawScale *= 0.85f;

            if (research.IsUnlocked(TechId.PlanetaryAnvil))
            {
                e.MineYieldBonus += 0.25f;
                e.ExtractHaulBonus += 0.08f;
            }

            if (research.IsUnlocked(TechId.OrbitalSkyhook))
            {
                e.FreightScale = 0.5f;
                e.ResupplyFeeDiscount = 99;
                e.ResupplyIntervalScale = 0.75f;
            }

            if (research.IsUnlocked(TechId.GeneVault))
            {
                e.GrowIntervalScale = 0.7f;
                e.ExtraBeds = 3;
                e.FarmYieldBonus += 0.12f;
            }

            if (research.IsUnlocked(TechId.ClimateLoom))
            {
                e.FarmYieldBonus += 0.18f;
                e.GrowIntervalScale *= 0.85f;
            }

            if (research.IsUnlocked(TechId.AegisSpire))
            {
                e.PowerDrawScale *= 0.88f;
                e.AmbientThreatScale *= 0.72f;
            }

            return e;
        }
    }
}
