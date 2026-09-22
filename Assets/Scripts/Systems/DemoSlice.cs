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
            if (!DemoSettings.FirstHourDemo) return true;
            return DemoRank(cat) < 100;
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

        /// <summary>
        /// First-hour build list, hotkey order. 100 means hidden.
        /// Commons is already on the claim, so it is not in the list.
        /// </summary>
        public static int DemoRank(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.EngineerWorkshop: return 0;
                case BuildingCategory.DefenseWorkshop: return 1;
                case BuildingCategory.Utility: return 2;
                case BuildingCategory.Habitat: return 3;
                case BuildingCategory.Farm: return 4;
                case BuildingCategory.Mine: return 5;
                case BuildingCategory.Power: return 6;
                case BuildingCategory.LandingPad: return 7;
                case BuildingCategory.Laboratory: return 8;
                case BuildingCategory.Defense: return 9;
                case BuildingCategory.ScoutWorkshop: return 10;
                case BuildingCategory.MedicWorkshop: return 11;
                default: return 100;
            }
        }

        /// <summary>
        /// Catalog indices the build menu should show, hotkey order.
        /// One entry per category. Full campaign keeps catalog order.
        /// </summary>
        public static void CollectVisible(IList<BuildingData> catalog, List<int> into)
        {
            into.Clear();
            if (catalog == null) return;
            if (!DemoSettings.FirstHourDemo)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    if (catalog[i] != null)
                        into.Add(i);
                }
                return;
            }

            var best = new int[12];
            for (int i = 0; i < best.Length; i++)
                best[i] = -1;
            for (int i = 0; i < catalog.Count; i++)
            {
                var data = catalog[i];
                if (data == null) continue;
                int rank = DemoRank(data.category);
                if (rank < 0 || rank >= best.Length) continue;
                if (best[rank] < 0)
                    best[rank] = i;
            }
            for (int rank = 0; rank < best.Length; rank++)
            {
                if (best[rank] >= 0)
                    into.Add(best[rank]);
            }
        }
    }
}
