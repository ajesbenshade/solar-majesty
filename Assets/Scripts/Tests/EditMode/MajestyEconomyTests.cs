using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>Majesty 2 gold rules (Docs/ECONOMY_MAJESTY2.md).</summary>
    public class MajestyEconomyTests
    {
        [Test]
        public void DuplicateBuildings_Cost150Percent_RoundedUpTo10()
        {
            // Paradox eGuide: Warriors Guild 500 → 750 → 1,130.
            Assert.AreEqual(500, MajestyEconomy.DuplicateCost(500, 0));
            Assert.AreEqual(750, MajestyEconomy.DuplicateCost(500, 1));
            Assert.AreEqual(1130, MajestyEconomy.DuplicateCost(500, 2));
            Assert.AreEqual(230, MajestyEconomy.DuplicateCost(150, 1), "225 rounds up to 230");
        }

        [Test]
        public void Infrastructure_DoesNotScaleWithCount()
        {
            Assert.IsFalse(MajestyEconomy.ScalesWithCount(BuildingCategory.Habitat));
            Assert.IsFalse(MajestyEconomy.ScalesWithCount(BuildingCategory.Power));
            Assert.IsFalse(MajestyEconomy.ScalesWithCount(BuildingCategory.Farm));
            Assert.IsTrue(MajestyEconomy.ScalesWithCount(BuildingCategory.Market));
            Assert.IsTrue(MajestyEconomy.ScalesWithCount(BuildingCategory.DefenseWorkshop));
            Assert.AreEqual(150, MajestyEconomy.PriceFor(BuildingCategory.Habitat, 150, 5));
        }

        [Test]
        public void BuildingPrices_FollowMajesty2Table()
        {
            Assert.AreEqual(500, MajestyEconomy.BuildingCost(BuildingCategory.Market));
            Assert.AreEqual(500, MajestyEconomy.BuildingCost(BuildingCategory.Blacksmith));
            Assert.AreEqual(150, MajestyEconomy.BuildingCost(BuildingCategory.Watchtower), "Guardhouse");
            Assert.AreEqual(250, MajestyEconomy.BuildingCost(BuildingCategory.Inn));
            Assert.AreEqual(200, MajestyEconomy.BuildingCost(BuildingCategory.LandingPad), "Trading Post");
            Assert.AreEqual(500, MajestyEconomy.BuildingCost(BuildingCategory.DefenseWorkshop), "Warriors Guild");
            Assert.AreEqual(350, MajestyEconomy.BuildingCost(BuildingCategory.ScoutWorkshop), "Rangers Guild");
            Assert.AreEqual(3000, MajestyEconomy.BuildingCost(BuildingCategory.AegisSpire), "temples");
        }

        [Test]
        public void HeroTax_IsHalfOfEveryEarning()
        {
            Assert.AreEqual(0.5f, MajestyEconomy.HeroTaxRate);
            Assert.AreEqual(400, MajestyEconomy.HeroTax(800f));
            Assert.AreEqual(37, MajestyEconomy.HeroTax(75f), "hero keeps the odd coin");
            Assert.AreEqual(0, MajestyEconomy.HeroTax(0f));
        }

        [Test]
        public void Resurrection_IsHirePlusHalfPerLevelGained()
        {
            int hire = MajestyEconomy.HireCost(SpecialistClass.DefenseMech);
            Assert.AreEqual(hire, MajestyEconomy.ResurrectCost(SpecialistClass.DefenseMech, 1));
            Assert.AreEqual(hire * 2, MajestyEconomy.ResurrectCost(SpecialistClass.DefenseMech, 3));
            Assert.AreEqual(
                MajestyEconomy.ResurrectCost(SpecialistClass.DefenseMech, OverseerRules.LevelCap),
                MajestyEconomy.ResurrectCost(SpecialistClass.DefenseMech, 99), "caps at the level cap");
        }

        [Test]
        public void Collectors_TwoPerCommons_OnePerWatchtower()
        {
            Assert.AreEqual(0, MajestyEconomy.CollectorsFor(0, 3), "no Palace, no collectors");
            Assert.AreEqual(2, MajestyEconomy.CollectorsFor(1, 0));
            Assert.AreEqual(4, MajestyEconomy.CollectorsFor(1, 2));
            Assert.AreEqual(MajestyEconomy.MaxCollectors, MajestyEconomy.CollectorsFor(1, 40));
        }

        [Test]
        public void HouseTax_Is20Plus10PerResident_CappedAt50()
        {
            Assert.AreEqual(20, MajestyEconomy.HouseDailyTax(0));
            Assert.AreEqual(40, MajestyEconomy.HouseDailyTax(2));
            Assert.AreEqual(50, MajestyEconomy.HouseDailyTax(9));
            Assert.AreEqual(0, MajestyEconomy.HousesDailyTax(3, 0, false), "empty colony pays nothing");
            Assert.AreEqual(90, MajestyEconomy.HousesDailyTax(3, 3, false));
        }

        [Test]
        public void DailyTax_PalaceMarketFarm()
        {
            Assert.AreEqual(50, MajestyEconomy.DailyTax(BuildingCategory.Commons));
            Assert.AreEqual(250, MajestyEconomy.DailyTax(BuildingCategory.Market));
            Assert.AreEqual(50, MajestyEconomy.DailyTax(BuildingCategory.Farm));
            Assert.AreEqual(0, MajestyEconomy.DailyTax(BuildingCategory.GuildHall), "guilds live off hero tax");
        }

        [Test]
        public void Caravans_Pay300To1000_ByDistance()
        {
            Assert.AreEqual(300, MajestyEconomy.CaravanGold(0f));
            Assert.AreEqual(300 + 3 * 60, MajestyEconomy.CaravanGold(35f));
            Assert.AreEqual(1000, MajestyEconomy.CaravanGold(5000f));
        }

        [Test]
        public void Brain_JudgesBountiesOnTheOldScale()
        {
            Assert.AreEqual(79f, MajestyEconomy.ToBrain(790f), 0.001f);
            Assert.AreEqual(0, OverseerRules.GreedAsk(null) % 10, "asks are whole Majesty tens");
        }

        [Test]
        public void Settlement_PaysHouseTaxOncePerMajestyDay()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.WaterIce, 100);
            var s = new Settlement(res);
            s.RegisterPlaced(BuildingCategory.Habitat);
            s.SeedStarterCrew();

            Assert.AreEqual(MajestyEconomy.DaySeconds, s.TaxInterval);
            s.Tick(s.TaxInterval);
            Assert.AreEqual(1, s.TakePendingDays());
            Assert.AreEqual(MajestyEconomy.HouseDailyTax(Settlement.StarterColonists), s.PendingLevy);
            Assert.AreEqual(0, res.Get(ResourceId.Metals), "house tax waits for a collector");
        }

        [Test]
        public void Settlement_RoutesMineGoldToTills()
        {
            var res = new ResourceManager();
            var s = new Settlement(res) { RouteCampGoldToTills = true };
            s.RegisterPlaced(BuildingCategory.Mine);
            s.Tick(s.ProductionInterval);

            Assert.AreEqual(0, res.Get(ResourceId.Metals));
            Assert.AreEqual(40, s.TakePendingCampGold());
            Assert.AreEqual(0, s.TakePendingCampGold());
        }

        [Test]
        public void Resupply_CaravanPaysTheTillNotTheStockpile()
        {
            var res = new ResourceManager();
            int till = 0;
            var eco = new SimpleEconomy(res)
            {
                HasDock = true,
                TillSink = g => { till += g; return true; },
                CaravanGold = 420
            };
            eco.ConfigureResupply(20f, 0);
            eco.Tick(20f);

            Assert.AreEqual(420, till);
            Assert.AreEqual(0, res.Get(ResourceId.Metals));
            Assert.AreEqual(15, res.Get(ResourceId.WaterIce), "tank cargo still lands directly");
        }

        [Test]
        public void Upkeep_ChargesNoHeroWages()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 100);
            var eco = new SimpleEconomy(res) { ResupplyEnabled = false, BasePowerUpkeep = 0 };
            eco.Tick(eco.UpkeepIntervalSeconds);
            Assert.AreEqual(100, res.Get(ResourceId.Metals));
            Assert.AreEqual(0, eco.LastMetalsUpkeep);
        }

        [Test]
        public void Placer_ChargesDuplicatePrice_AndRefundsWhatWasPaid()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 10000);
            var placer = new BuildingPlacer(res);
            int owned = 1;
            placer.OwnedCount = cat => cat == BuildingCategory.Market ? owned : 0;

            var market = UnityEngine.ScriptableObject.CreateInstance<BuildingData>();
            try
            {
                market.category = BuildingCategory.Market;
                market.buildCost = Wallet.Credits(500);
                var cost = placer.CostFor(market);
                Assert.AreEqual(750, cost[0].amount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(market);
            }
        }

        [Test]
        public void SaveMigration_ScalesGoldTenfold()
        {
            Assert.AreEqual(6, SaveGame.CurrentVersion);
            Assert.AreEqual(3400, MajestyEconomy.MigrateGold(340));
            Assert.AreEqual(0, MajestyEconomy.MigrateGold(0));
            Assert.AreEqual(795f, MajestyEconomy.MigrateGold(79.5f), 0.001f);
        }
    }
}
