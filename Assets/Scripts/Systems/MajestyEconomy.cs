using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty 2 economy, mapped onto the Compact colony. One source of truth for every gold (CRED)
    /// number, and the pure math behind the gold loop:
    ///
    ///   treasury → flags / buildings / hires → heroes earn → half is taxed to their guild's till,
    ///   the rest is spent at the Market / Blacksmith / Guild / Inn / Aid tills → buildings also
    ///   accrue a daily tax → tax collectors walk the tills home → treasury.
    ///
    /// Numbers follow Majesty 2 (Prima guide / Paradox mission eGuide): guild 250–1,000,
    /// Market 500, Blacksmith 500, Guardhouse 150, Inn 250, Trading Post 200, temples 3,000,
    /// every extra building of a type costs 150% of the previous (rounded up to 10), heroes
    /// hand half their earnings to the guild, Palace 2/4/6 tax collectors, Palace 50/day,
    /// Marketplace 250/day, houses 20–50/day, caravans 300–1,000, resurrection = hire cost
    /// plus half of it per level gained. See Docs/ECONOMY_MAJESTY2.md.
    ///
    /// ICE and PWR stay life-support / grid constraints — never shop currency.
    /// </summary>
    public static class MajestyEconomy
    {
        // ------------------------------------------------------------------ scale & time

        /// <summary>
        /// CRED values are Majesty 2 gold. The flag brain was tuned on a 1/10 scale (a bounty of
        /// 79 meant "fair pay"), so it divides by this to keep hero judgement identical.
        /// </summary>
        public const float GoldScale = 10f;

        /// <summary>One Majesty "day" of building tax, in sim seconds.</summary>
        public const float DaySeconds = 60f;

        /// <summary>Brain units for a CRED amount (bounty, purse) — identity at the old scale.</summary>
        public static float ToBrain(float gold) => gold / GoldScale;

        // ------------------------------------------------------------------ hero taxes

        /// <summary>Majesty 2: "Half the gold earned by heroes is taken as taxes."</summary>
        public const float HeroTaxRate = 0.5f;

        /// <summary>Guild share of a hero's earning (rounded down — the hero keeps the odd coin).</summary>
        public static int HeroTax(float earned) =>
            earned <= 0f ? 0 : Mathf.FloorToInt(earned * HeroTaxRate);

        // ------------------------------------------------------------------ tax collectors

        /// <summary>Palace L1 houses two collectors (L2 four, L3 six). Commons is the Palace.</summary>
        public const int CollectorsPerCommons = 2;

        /// <summary>Guardhouses also house a collector — the Watchtower is the Guardhouse.</summary>
        public const int CollectorsPerWatchtower = 1;

        public const int MaxCollectors = 8;

        /// <summary>Collector setting: skip tills below this (Majesty "minimum to collect").</summary>
        public const int CollectMinimum = 40;

        /// <summary>
        /// Collector health on the heroes' normalized scale (a hero is 1). Mob bites run 0.03–0.18
        /// a second, so a collector caught in the open lasts a few seconds to half a minute.
        /// </summary>
        public const float CollectorHp = 0.6f;
        /// <summary>A mob this close to a travelling collector goes after it.</summary>
        public const float CollectorAmbushRange = 14f;
        /// <summary>Seconds before the Commons sends out a replacement for a killed collector.</summary>
        public const float CollectorRespawnSeconds = 30f;

        /// <summary>Collector setting: walk home once carrying this much ("minimum to return").</summary>
        public const int ReturnAt = 300;

        public const float CollectorSpeed = 3.2f;
        public const float CollectorArrive = 3.6f;

        /// <summary>A pest that catches a collector snatches this share of the bag.</summary>
        public const float CollectorRobShare = 0.5f;

        public static int CollectorsFor(int commons, int watchtowers)
        {
            if (commons <= 0) return 0;
            int n = commons * CollectorsPerCommons + Mathf.Max(0, watchtowers) * CollectorsPerWatchtower;
            return Mathf.Clamp(n, 0, MaxCollectors);
        }

        // ------------------------------------------------------------------ daily building tax

        /// <summary>Palace L1 tax per day.</summary>
        public const int CommonsDailyTax = 50;

        /// <summary>Marketplace L1 tax per day.</summary>
        public const int MarketDailyTax = 250;

        /// <summary>Windmill-style producer tax per day (Greenhouse Farm).</summary>
        public const int FarmDailyTax = 50;

        /// <summary>Peasant house: 20 empty, +10 per resident, capped at 50.</summary>
        public const int HouseDailyBase = 20;
        public const int HouseDailyPerResident = 10;
        public const int HouseDailyMax = 50;

        /// <summary>Solar farm energy sold into its till each day (Majesty windmill-style producer).</summary>
        public const int SolarFarmDailyTax = 60;

        /// <summary>
        /// A mine is the Majesty trading post: each day it refines a load of nuclear fuel worth
        /// more energy the farther it sits from the Commons (300–1,000), and a tax collector has
        /// to walk that load home past whatever lives in between.
        /// </summary>
        public static int MineDailyEnergy(float metersFromCommons) => CaravanGold(metersFromCommons);

        /// <summary>A HAB with no census pays a settled house's tax: 20 + two residents' 10.</summary>
        public const int HouseDailyFlat = HouseDailyBase + 2 * HouseDailyPerResident;

        public static int HouseDailyTax(int residents) =>
            Mathf.Min(HouseDailyMax, HouseDailyBase + Mathf.Max(0, residents) * HouseDailyPerResident);

        /// <summary>Total house tax for the census (Settlement does not see per-HAB residents).</summary>
        public static int HousesDailyTax(int habs, int population, bool overcrowded)
        {
            if (habs <= 0 || population <= 0) return 0;
            int raw = Mathf.Min(habs * HouseDailyMax, habs * HouseDailyBase + population * HouseDailyPerResident);
            return overcrowded ? Mathf.RoundToInt(raw * 0.65f) : raw;
        }

        /// <summary>Daily tax that lands in a building's till, houses included.</summary>
        public static int DailyTax(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Commons: return CommonsDailyTax;
                case BuildingCategory.Market: return MarketDailyTax;
                case BuildingCategory.Farm: return FarmDailyTax;
                case BuildingCategory.Habitat: return HouseDailyFlat;
                case BuildingCategory.Power: return SolarFarmDailyTax;
                default: return 0;
            }
        }

        // ------------------------------------------------------------------ trading post / caravans

        /// <summary>Gold a landed ship's cargo is worth at the Market (Majesty caravan: 300–1,000).</summary>
        public const int CaravanGoldBase = 300;

        /// <summary>Bonus per 10 m between pad and market (longer route, richer caravan).</summary>
        public const int CaravanGoldPer10m = 60;

        public const int CaravanGoldMax = 1000;

        public static int CaravanGold(float padToMarketMeters)
        {
            int bonus = Mathf.FloorToInt(Mathf.Max(0f, padToMarketMeters) / 10f) * CaravanGoldPer10m;
            return Mathf.Clamp(CaravanGoldBase + bonus, CaravanGoldBase, CaravanGoldMax);
        }

        // ------------------------------------------------------------------ building prices

        /// <summary>Majesty 2 price of the first building of a category.</summary>
        public static int BuildingCost(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Commons: return 500;            // Palace (first build here)
                case BuildingCategory.Habitat: return 150;            // housing, Majesty houses are free
                case BuildingCategory.Power: return 150;
                case BuildingCategory.Mining: return 300;             // ops drop-off
                case BuildingCategory.Laboratory: return 1000;        // Wizards Guild
                case BuildingCategory.LandingPad: return 200;         // Trading Post
                case BuildingCategory.Defense: return 300;            // Wizard Tower
                case BuildingCategory.Utility: return 50;
                case BuildingCategory.Farm: return 200;               // windmill
                case BuildingCategory.Mine: return 300;
                case BuildingCategory.RegolithCamp: return 200;
                case BuildingCategory.Inn: return 250;                // Inn
                case BuildingCategory.ScoutWorkshop: return 350;      // Rangers Guild
                case BuildingCategory.EngineerWorkshop: return 400;
                case BuildingCategory.DefenseWorkshop: return 500;    // Warriors Guild
                case BuildingCategory.MedicWorkshop: return 750;      // Clerics Guild
                case BuildingCategory.HarvesterWorkshop: return 250;  // Rogues Guild
                case BuildingCategory.SurveyorWorkshop: return 600;
                case BuildingCategory.TerraformerWorkshop: return 700;
                case BuildingCategory.CourierWorkshop: return 250;
                case BuildingCategory.GeologistWorkshop: return 500;
                case BuildingCategory.SentinelWorkshop: return 1000;  // Dwarven Tower
                case BuildingCategory.GuildHall: return 1000;         // Hall of Lords
                case BuildingCategory.ClimateLoom:
                case BuildingCategory.AegisSpire:
                case BuildingCategory.DeepArchive: return 3000;       // temples
                case BuildingCategory.Market: return 500;             // Marketplace
                case BuildingCategory.Blacksmith: return 500;         // Blacksmith
                case BuildingCategory.FobotYard: return 400;
                case BuildingCategory.Watchtower: return 150;         // Guardhouse
                case BuildingCategory.AidStation: return 300;
                default: return 250;
            }
        }

        /// <summary>
        /// Majesty 2: "Every additional building of this type is 150 percent of the cost of the
        /// first building, rounded up to the nearest 10" — compounding (500 → 750 → 1,130).
        /// Housing, power, farms, camps, and junctions are infrastructure and stay flat.
        /// </summary>
        public const float DuplicateMultiplier = 1.5f;

        public static bool ScalesWithCount(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Habitat:
                case BuildingCategory.Power:
                case BuildingCategory.Utility:
                case BuildingCategory.Farm:
                case BuildingCategory.Mine:
                case BuildingCategory.RegolithCamp:
                    return false;
                default:
                    return true;
            }
        }

        public static int DuplicateCost(int firstCost, int alreadyOwned)
        {
            if (firstCost <= 0) return 0;
            double cost = firstCost;
            for (int i = 0; i < alreadyOwned; i++)
                cost = System.Math.Ceiling(cost * DuplicateMultiplier / 10.0) * 10.0;
            return (int)System.Math.Min(int.MaxValue, cost);
        }

        public static int PriceFor(BuildingCategory cat, int firstCost, int alreadyOwned) =>
            ScalesWithCount(cat) ? DuplicateCost(firstCost, alreadyOwned) : firstCost;

        /// <summary>Overwrite every catalog price with the Majesty 2 table (assets may hold old values).</summary>
        public static void ApplyBuildingPrices(BuildingData[] buildings)
        {
            if (buildings == null) return;
            for (int i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                if (b == null) continue;
                b.buildCost = Wallet.Credits(BuildingCost(b.category));
            }
        }

        // ------------------------------------------------------------------ heroes

        /// <summary>Recruit fee paid from the treasury when a workshop fabricates its robot.</summary>
        public static int HireCost(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return 150;    // Ranger
                case SpecialistClass.EngineerBot: return 200;
                case SpecialistClass.DefenseMech: return 500;   // Warrior
                case SpecialistClass.Medic: return 400;         // Cleric
                case SpecialistClass.HarvesterBot: return 100;  // Rogue
                case SpecialistClass.SurveyorBot: return 300;
                case SpecialistClass.TerraformerBot: return 350;
                case SpecialistClass.CourierBot: return 100;
                case SpecialistClass.GeologistBot: return 250;
                case SpecialistClass.SentinelMech: return 600;  // Dwarf
                default: return 250;
            }
        }

        /// <summary>
        /// Majesty 2: "The cost of resurrecting a hero is the base cost for that hero, plus half
        /// that cost for every level they have gained."
        /// </summary>
        public static int ResurrectCost(SpecialistClass cls, int level) => ResurrectCost(HireCost(cls), level);

        public static int ResurrectCost(int hireCost, int level)
        {
            int gained = Mathf.Max(0, Mathf.Min(level, OverseerRules.LevelCap) - 1);
            return Mathf.Max(1, hireCost + hireCost * gained / 2);
        }

        public static SpecialistClass? ClassForWorkshop(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.ScoutWorkshop: return SpecialistClass.ScoutDrone;
                case BuildingCategory.EngineerWorkshop: return SpecialistClass.EngineerBot;
                case BuildingCategory.DefenseWorkshop: return SpecialistClass.DefenseMech;
                case BuildingCategory.MedicWorkshop: return SpecialistClass.Medic;
                case BuildingCategory.HarvesterWorkshop: return SpecialistClass.HarvesterBot;
                case BuildingCategory.SurveyorWorkshop: return SpecialistClass.SurveyorBot;
                case BuildingCategory.TerraformerWorkshop: return SpecialistClass.TerraformerBot;
                case BuildingCategory.CourierWorkshop: return SpecialistClass.CourierBot;
                case BuildingCategory.GeologistWorkshop: return SpecialistClass.GeologistBot;
                case BuildingCategory.SentinelWorkshop: return SpecialistClass.SentinelMech;
                default: return null;
            }
        }

        /// <summary>Heroes arrive with a little pocket money so the first potion is possible.</summary>
        public const int HeroStartingPurse = 50;

        // ------------------------------------------------------------------ flags

        /// <summary>Default / floor / ceiling reward for each flag kind (Majesty flags: 100s–1,000s).</summary>
        public static int FlagDefaultBounty(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return 400;
                case FlagType.ClearThreat: return 800;
                case FlagType.Build: return 700;
                case FlagType.Extract: return 550;
                case FlagType.DefendArea: return 650;
                case FlagType.ResearchSite: return 500;
                case FlagType.EstablishOutpost: return 750;
                case FlagType.Terraform: return 700;
                default: return 500;
            }
        }

        public const int FlagMinBounty = 50;

        /// <summary>One +/− press on a posted flag's reward.</summary>
        public const float FlagBountyStep = 50f;
        public const int FlagMaxBounty = 5000;

        public static void ApplyFlagBounties(FlagData flag)
        {
            if (flag == null) return;
            flag.defaultBounty = FlagDefaultBounty(flag.flagType);
            flag.minBounty = FlagMinBounty;
            flag.maxBounty = FlagMaxBounty;
        }

        // ------------------------------------------------------------------ save migration

        /// <summary>Saves before v6 stored gold on the old 1/10 scale.</summary>
        public const int GoldScaleSaveVersion = 6;

        public static int MigrateGold(int oldGold) =>
            oldGold <= 0 ? oldGold : (int)System.Math.Min(int.MaxValue, (long)oldGold * (long)GoldScale);

        public static float MigrateGold(float oldGold) => oldGold <= 0f ? oldGold : oldGold * GoldScale;
    }
}
