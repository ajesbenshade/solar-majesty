using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class WalletGuildMarketTests
    {
        [Test]
        public void TechCompleteCosts_AreCreditsOnly()
        {
            var techs = TechCatalog.All;
            for (int i = 0; i < techs.Count; i++)
            {
                Assert.IsTrue(
                    Wallet.IsCreditsOnly(techs[i].CompleteCost),
                    techs[i].DisplayName);
            }
        }

        [Test]
        public void Revive_IsCreditsOnly()
        {
            Assert.AreEqual(0, OverseerRules.ReviveIce);
            Assert.AreEqual(0, OverseerRules.ReviveIceCost(0));
            Assert.AreEqual(0, OverseerRules.ReviveIceCost(8));
            Assert.Greater(OverseerRules.ReviveMetals(0), 0);
        }

        [Test]
        public void MarketItems_Exist()
        {
            Assert.AreEqual(ShopVendor.Market, ShopCatalog.Get(ShopItemId.HealthPotion).Vendor);
            Assert.AreEqual(ShopVendor.Market, ShopCatalog.Get(ShopItemId.MagicPotion).Vendor);
            Assert.AreEqual(ShopVendor.Market, ShopCatalog.Get(ShopItemId.RegenNecklace).Vendor);
            Assert.AreEqual(ShopVendor.GuildHall, ShopCatalog.Get(ShopItemId.AnvilRig).Vendor);
            Assert.AreEqual(ShopVendor.Blacksmith, ShopCatalog.Get(ShopItemId.AnvilSledge).Vendor);
            Assert.AreEqual(ShopItemKind.Weapon, ShopCatalog.Get(ShopItemId.HorizonNeedle).Kind);
        }

        [Test]
        public void Blacksmith_IsClassLockedAndBeatsGuildKit()
        {
            var sledge = ShopCatalog.BestBlacksmithBuy(
                SpecialistClass.EngineerBot, 200, ShopItemId.AnvilRig, ShopItemId.None);
            Assert.IsNotNull(sledge);
            Assert.AreEqual(ShopVendor.Blacksmith, sledge.Vendor);
            Assert.AreEqual(SpecialistClass.EngineerBot, sledge.ForClass);
        }

        [Test]
        public void SmithAndYard_UnlockFromTech()
        {
            Assert.AreEqual(27, (int)BuildingCategory.Blacksmith);
            Assert.AreEqual(28, (int)BuildingCategory.FobotYard);
            Assert.AreEqual(TechId.OreRefining, GameLoop.TechRequiredFor(BuildingCategory.Blacksmith));
            Assert.AreEqual(TechId.MedProtocols, GameLoop.TechRequiredFor(BuildingCategory.FobotYard));
        }

        [Test]
        public void YardBill_ScalesWithLevel()
        {
            Assert.AreEqual(OverseerRules.ReviveMet, OverseerRules.ReviveMetalsForLevel(1));
            Assert.Greater(OverseerRules.ReviveMetalsForLevel(4), OverseerRules.ReviveMetalsForLevel(1));
        }

        [Test]
        public void GuildUpgrade_IsClassLocked()
        {
            var anvil = ShopCatalog.BestGuildUpgrade(SpecialistClass.EngineerBot, 200, ShopItemId.None);
            Assert.IsNotNull(anvil);
            Assert.AreEqual(SpecialistClass.EngineerBot, anvil.ForClass);

            var scout = ShopCatalog.BestGuildUpgrade(SpecialistClass.ScoutDrone, 200, ShopItemId.None);
            Assert.IsNotNull(scout);
            Assert.AreEqual(SpecialistClass.ScoutDrone, scout.ForClass);
        }

        [Test]
        public void MarketPrefersHealthWhenHurt()
        {
            var item = ShopCatalog.PreferredMarketBuy(
                SpecialistClass.EngineerBot, 40, 0.4f, ShopItemId.None);
            Assert.IsNotNull(item);
            Assert.AreEqual(ShopItemId.HealthPotion, item.Id);
        }

        [Test]
        public void GuildBenefit_RequiresResearchHallAndCredits()
        {
            ResearchManager.WipeUnlocks();
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 100);
            var research = new ResearchManager(res);
            var dir = new GuildBenefitDirector();

            Assert.IsFalse(dir.CanActivate(RobotGuildId.Horizon, research, res, true));

            research.RestoreFrom(
                new[] { (int)TechId.GuildCharter, (int)TechId.HorizonPulse },
                TechId.None, 0f, 0f);

            Assert.IsFalse(dir.CanActivate(RobotGuildId.Horizon, research, res, false),
                "no hall, no pulse");
            Assert.IsTrue(dir.CanActivate(RobotGuildId.Horizon, research, res, true));

            Assert.IsTrue(dir.TryActivate(RobotGuildId.Horizon, research, res, true, out _));
            Assert.IsTrue(dir.IsActive(RobotGuildId.Horizon));
            Assert.AreEqual(64, res.Get(ResourceId.Metals), "36 CRED spent");

            Assert.IsFalse(dir.TryActivate(RobotGuildId.Horizon, research, res, true, out _));
            ResearchManager.WipeUnlocks();
        }

        [Test]
        public void GuildCharters_SitOnTheTechTree()
        {
            Assert.IsNotNull(TechCatalog.Get(TechId.HorizonPulse));
            Assert.AreEqual(TechId.GuildCharter, TechCatalog.Get(TechId.AnvilOvertime).Prerequisites[0]);
            Assert.IsTrue(Wallet.IsCreditsOnly(TechCatalog.Get(TechId.AegisWatchfire).CompleteCost));
        }

        [Test]
        public void MetalsOnly_DropsPowerAndFoldsIceIntoCredits()
        {
            var mixed = new[]
            {
                new ResourceAmount(ResourceId.Metals, 40),
                new ResourceAmount(ResourceId.WaterIce, 15),
                new ResourceAmount(ResourceId.Power, 20)
            };
            var folded = Wallet.MetalsOnly(mixed);
            Assert.IsTrue(Wallet.IsCreditsOnly(folded));
            Assert.AreEqual(1, folded.Length);
            Assert.AreEqual(55, folded[0].amount);
        }

        [Test]
        public void Market_IsCategoryTwentySix()
        {
            Assert.AreEqual(26, (int)BuildingCategory.Market);
            Assert.AreEqual(TechId.ExtractBasics, GameLoop.TechRequiredFor(BuildingCategory.Market));
        }

        [Test]
        public void LevySplit_PutsLeftoverOnTheHeaviestStop()
        {
            int[] split = LevyRun.SplitByWeights(5, new[] { 2, 1 });
            Assert.AreEqual(2, split.Length);
            Assert.AreEqual(5, split[0] + split[1]);
            Assert.GreaterOrEqual(split[0], split[1]);
        }

        [Test]
        public void MarketSiphon_HoldsTheIceReserve()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.WaterIce, 40);
            res.Set(ResourceId.Regolith, 40);
            res.Set(ResourceId.Metals, 0);
            Assert.AreEqual(12, MarketSiphon.IceReserve(2));
            Assert.IsTrue(MarketSiphon.TrySiphon(res, 2, out int credits, out int ice, out int reg));
            Assert.Greater(credits, 0);
            Assert.GreaterOrEqual(res.Get(ResourceId.WaterIce), MarketSiphon.IceReserve(2));
            Assert.AreEqual(credits, res.Get(ResourceId.Metals));
            Assert.Greater(ice + reg, 0);
        }

        [Test]
        public void MarketSiphon_DoesNotTouchADryTank()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.WaterIce, 10);
            res.Set(ResourceId.Regolith, 10);
            Assert.IsFalse(MarketSiphon.TrySiphon(res, 4, out _, out _, out _));
            Assert.AreEqual(10, res.Get(ResourceId.WaterIce));
        }

        [Test]
        public void AidStation_UnlocksFromLifeSupport()
        {
            Assert.AreEqual(30, (int)BuildingCategory.AidStation);
            Assert.AreEqual(TechId.LifeSupport, GameLoop.TechRequiredFor(BuildingCategory.AidStation));
            Assert.Greater(OverseerRules.AidStationHealCost, 0);
        }

        [Test]
        public void AnalogCatalog_CoversTheMajestyLoops()
        {
            Assert.GreaterOrEqual(MajestyAnalog.All.Length, 16);
            Assert.IsNotNull(MajestyAnalog.ForCompactName("Colony Commons"));
            Assert.IsNotNull(MajestyAnalog.ForCompactName("Aid Station"));
            Assert.IsNotNull(MajestyAnalog.ForCompactName("Watchtower"));
        }

        [Test]
        public void Watchtower_IsLevyChestWhenCommonsIsFar()
        {
            Assert.AreEqual(29, (int)BuildingCategory.Watchtower);
            Assert.IsTrue(LevyRun.PreferWatchtower(40f, 12f));
            Assert.IsFalse(LevyRun.PreferWatchtower(10f, 8f), "near Commons, walk home");
            Assert.IsFalse(LevyRun.PreferWatchtower(40f, 38f), "tower not closer enough");
            Assert.AreEqual(TechId.None, GameLoop.TechRequiredFor(BuildingCategory.Watchtower));
        }

        [Test]
        public void LevyAccrue_MatchesOldTaxRate()
        {
            Assert.AreEqual(4, LevyRun.Accrue(2, false));
            Assert.AreEqual(Mathf.RoundToInt(3 * 2 * 0.65f), LevyRun.Accrue(3, true));
        }

        [TearDown]
        public void TearDown()
        {
            ResearchManager.WipeUnlocks();
        }
    }
}
