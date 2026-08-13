using System;

namespace SolarMajesty
{
    public enum TechId
    {
        None = 0,
        FieldSurvey = 1,
        HabOps = 2,
        ExtractBasics = 3,
        LabScience = 4,
        LifeSupport = 5,
        OreRefining = 6,
        PowerSystems = 7,
        MedProtocols = 8,
        DeepSurvey = 9,
        LunarRocket = 10,
        MarsShip = 11
    }

    /// <summary>Static tech definition used by <see cref="ResearchManager"/>.</summary>
    public sealed class TechDef
    {
        public TechId Id;
        public string DisplayName;
        public string Description;
        public float ScienceCost;
        public TechId[] Prerequisites;
        public ResourceAmount[] CompleteCost;
        public bool UnlocksLaunch;
        public float ResearchRateBonus;

        public TechDef(
            TechId id,
            string name,
            string description,
            float scienceCost,
            TechId[] prerequisites = null,
            ResourceAmount[] completeCost = null,
            bool unlocksLaunch = false,
            float researchRateBonus = 0f)
        {
            Id = id;
            DisplayName = name;
            Description = description;
            ScienceCost = scienceCost > 1f ? scienceCost : 1f;
            Prerequisites = prerequisites ?? Array.Empty<TechId>();
            CompleteCost = completeCost;
            UnlocksLaunch = unlocksLaunch;
            ResearchRateBonus = researchRateBonus;
        }
    }
}
