using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty 2 peasants raise their own houses. Here villagers slowly raise tax houses and solar
    /// farms around the Commons, one project at a time, and only while no enemy is near the
    /// settlement or the site. The player never places these; they defend the ground they grow on.
    /// </summary>
    public static class VillageGrowth
    {
        public const int MaxHouses = 12;
        public const int MaxSolarFarms = 6;
        /// <summary>One solar farm for every this many houses.</summary>
        public const int HousesPerSolarFarm = 2;

        public const float HouseBuildSeconds = 45f;
        public const float SolarBuildSeconds = 60f;
        /// <summary>Pause between finishing one project and staking out the next.</summary>
        public const float CooldownSeconds = 15f;
        /// <summary>No enemy within this many metres of the Commons or the site.</summary>
        public const float SafeRadius = 30f;

        public const float MinRing = 12f;
        public const float MaxRing = 46f;

        /// <summary>What villagers build next, or null when the village is full.</summary>
        public static BuildingCategory? NextProject(int houses, int solarFarms)
        {
            bool wantSolar = solarFarms < MaxSolarFarms && solarFarms * HousesPerSolarFarm < houses;
            if (wantSolar) return BuildingCategory.Power;
            if (houses < MaxHouses) return BuildingCategory.Habitat;
            if (solarFarms < MaxSolarFarms) return BuildingCategory.Power;
            return null;
        }

        public static float BuildSeconds(BuildingCategory cat) =>
            cat == BuildingCategory.Power ? SolarBuildSeconds : HouseBuildSeconds;

        public static bool IsVillageBuilt(BuildingCategory cat) =>
            cat == BuildingCategory.Habitat || cat == BuildingCategory.Power;
    }
}
