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
        MarsShip = 11,
        BeltHauler = 12,
        Icebreaker = 13,
        GuildCharter = 14,
        HarvestDoctrine = 15,
        AegisDoctrine = 16,
        SurveyDoctrine = 17,
        PlanetaryAnvil = 18,
        OrbitalSkyhook = 19,
        GeneVault = 20,
        TerraformCharter = 21,
        FreightDoctrine = 22,
        CoreSampling = 23,
        PerimeterDoctrine = 24,
        ClimateLoom = 25,
        AegisSpire = 26,
        DeepArchive = 27
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
        public bool SecretProject;

        public TechDef(
            TechId id,
            string name,
            string description,
            float scienceCost,
            TechId[] prerequisites = null,
            ResourceAmount[] completeCost = null,
            bool unlocksLaunch = false,
            float researchRateBonus = 0f,
            bool secretProject = false)
        {
            Id = id;
            DisplayName = name;
            Description = description;
            ScienceCost = scienceCost > 1f ? scienceCost : 1f;
            Prerequisites = prerequisites ?? Array.Empty<TechId>();
            CompleteCost = completeCost;
            UnlocksLaunch = unlocksLaunch;
            ResearchRateBonus = researchRateBonus;
            SecretProject = secretProject;
        }
    }
}
