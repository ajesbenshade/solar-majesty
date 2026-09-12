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

        [TearDown]
        public void TearDown()
        {
            ResearchManager.WipeUnlocks();
        }
    }
}
