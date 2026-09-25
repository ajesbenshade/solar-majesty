using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Playable-demo surface. The full campaign stays in the repo.
    /// When <see cref="DemoSettings.FirstHourDemo"/> is on, a new player only
    /// meets the Earth greed beat: workshop, a refused Build flag, a raised
    /// bounty, then one Defend flag. Guilds, markets, yards, Belt, and Europa
    /// stay hidden until Settings turns the full campaign on.
    /// </summary>
    public static class DemoSlice
    {
        public static bool ShowBuilding(BuildingCategory cat)
        {
            int rank = MenuRank(cat);
            if (rank >= Hidden) return false;
            return !DemoSettings.FirstHourDemo || rank < CampaignOnly;
        }

        public static bool ShowBody(CelestialBodyId id)
        {
            if (!DemoSettings.FirstHourDemo) return true;
            return id != CelestialBodyId.Belt && id != CelestialBodyId.Europa;
        }

        public static bool ShowTech(TechId id)
        {
            if (!DemoSettings.FirstHourDemo) return true;
            switch (id)
            {
                case TechId.FieldSurvey:
                case TechId.HabOps:
                case TechId.ExtractBasics:
                case TechId.LabScience:
                case TechId.LifeSupport:
                case TechId.OreRefining:
                case TechId.PowerSystems:
                case TechId.LunarRocket:
                    return true;
                default:
                    return false;
            }
        }

        private const int CampaignOnly = 100;
        private const int Hidden = 1000;

        /// <summary>
        /// The build menu, hotkey order, for both the Earth demo and the full campaign. Ranks under
        /// 100 are the core Majesty list; 100+ join once the full campaign is on. Commons is placed
        /// for the player, houses and solar farms are raised by villagers (VillageGrowth), and the
        /// retired regolith pieces never show.
        /// </summary>
        public static int MenuRank(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.EngineerWorkshop: return 0;
                case BuildingCategory.DefenseWorkshop: return 1;
                case BuildingCategory.Watchtower: return 3;
                case BuildingCategory.Market: return 4;
                case BuildingCategory.Mine: return 5;
                case BuildingCategory.Farm: return 6;
                case BuildingCategory.LandingPad: return 7;
                case BuildingCategory.Laboratory: return 8;
                case BuildingCategory.Defense: return 9;
                case BuildingCategory.ScoutWorkshop: return 10;
                case BuildingCategory.MedicWorkshop: return 11;
                case BuildingCategory.Blacksmith: return 12;
                case BuildingCategory.GuildHall: return 100;
                case BuildingCategory.AidStation: return 101;
                case BuildingCategory.FobotYard: return 102;
                case BuildingCategory.HarvesterWorkshop: return 103;
                case BuildingCategory.SurveyorWorkshop: return 104;
                case BuildingCategory.TerraformerWorkshop: return 105;
                case BuildingCategory.CourierWorkshop: return 106;
                case BuildingCategory.GeologistWorkshop: return 107;
                case BuildingCategory.SentinelWorkshop: return 108;
                case BuildingCategory.ClimateLoom: return 109;
                case BuildingCategory.AegisSpire: return 110;
                case BuildingCategory.DeepArchive: return 111;
                default: return Hidden;
            }
        }

        /// <summary>Kept for callers that still ask for the demo rank; same list as the menu.</summary>
        public static int DemoRank(BuildingCategory cat)
        {
            int rank = MenuRank(cat);
            return rank < CampaignOnly ? rank : 100;
        }

        /// <summary>
        /// Catalog indices the build menu should show, hotkey order. One entry per category,
        /// except guild halls, where each guild is its own building.
        /// </summary>
        public static void CollectVisible(IList<BuildingData> catalog, List<int> into)
        {
            into.Clear();
            if (catalog == null) return;
            var picked = new List<(int rank, int index)>(catalog.Count);
            var seen = new HashSet<BuildingCategory>();
            var halls = new HashSet<string>();
            for (int i = 0; i < catalog.Count; i++)
            {
                var data = catalog[i];
                if (data == null || !ShowBuilding(data.category)) continue;
                if (data.category == BuildingCategory.GuildHall)
                {
                    if (!halls.Add(data.displayName ?? "")) continue;
                }
                else if (!seen.Add(data.category)) continue;
                picked.Add((MenuRank(data.category), i));
            }
            picked.Sort((x, y) => x.rank != y.rank ? x.rank.CompareTo(y.rank) : x.index.CompareTo(y.index));
            for (int i = 0; i < picked.Count; i++)
                into.Add(picked[i].index);
        }
    }
}
