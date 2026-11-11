using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The perk stats as the game reads them (<see cref="CivRules"/>, the placer and the
    /// economy). One test per wired stat: the neutral civ (and a null civ) gives today's number
    /// exactly, and the civ that owns the perk gives the design doc's number (section 2).
    /// </summary>
    public class CivPerkWiringTests
    {
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        private static CivModifiers None => CivModifiers.For(CivId.None);
        private static CivModifiers Concord => CivModifiers.For(CivId.LumenConcord);
        private static CivModifiers Exchange => CivModifiers.For(CivId.TetherExchange);
        private static CivModifiers Mandate => CivModifiers.For(CivId.BastionMandate);
        private static CivModifiers Pathfinder => CivModifiers.For(CivId.PathfinderAccord);

        [SetUp]
        public void SetUp() => CivCatalog.UseDatabase(null);

        [TearDown]
        public void TearDown()
        {
            CivCatalog.ReloadDatabase();
            foreach (var o in _made)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _made.Clear();
        }

        private BuildingData Building(BuildingCategory cat, int price, float seconds)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            _made.Add(data);
            data.category = cat;
            data.buildCost = Wallet.Credits(price);
            data.buildTimeSeconds = seconds;
            return data;
        }

        private static IEnumerable<CivModifiers> Neutral()
        {
            yield return null;
            yield return CivModifiers.For(CivId.None);
        }

        // ---- 1. Bounty pay ----------------------------------------------------------------

        [Test]
        public void BountyPay_NeutralKeepsTheWholeEscrowSpent_ConcordGetsTenPercentBack()
        {
            foreach (var civ in Neutral())
            {
                Assert.AreEqual(0, CivRules.BountyRebate(civ, 400));
                Assert.AreEqual(400, CivRules.BountyTreasuryCost(civ, 400));
            }
            Assert.AreEqual(360, CivRules.BountyTreasuryCost(Concord, 400), "Explore 400 -> 360");
            Assert.AreEqual(720, CivRules.BountyTreasuryCost(Concord, 800), "Clear Threat 800 -> 720");
            Assert.AreEqual(40, CivRules.BountyRebate(Concord, 400));
            Assert.AreEqual(0, CivRules.BountyRebate(Concord, 0));
        }

        [Test]
        public void BountyPay_EconomyReturnsTheCivShareOnCompletion()
        {
            foreach (var civ in new[] { null, None, Concord })
            {
                var res = new ResourceManager();
                res.Set(ResourceId.Metals, 1000);
                var eco = new SimpleEconomy(res) { ResupplyEnabled = false, Civ = civ };
                Assert.IsTrue(eco.TryEscrowBounty(400f, out int escrow));
                Assert.AreEqual(400, escrow, "the hero is still promised the whole bounty");
                Assert.AreEqual(600, res.Get(ResourceId.Metals));
                eco.ReleaseBountyEscrow(escrow);
                Assert.AreEqual(0, eco.EscrowedMetals);
                bool concord = civ != null && civ.Id == CivId.LumenConcord;
                Assert.AreEqual(concord ? 640 : 600, res.Get(ResourceId.Metals), civ != null ? civ.Id.ToString() : "null");
                Assert.AreEqual(concord ? 40 : 0, eco.LastBountyRebate);
            }
        }

        // ---- 2. Hire cost (and the revival bill built on it) -------------------------------

        [Test]
        public void HireCost_NeutralIsToday_ConcordCheaper_MandateDearer()
        {
            foreach (var civ in Neutral())
            {
                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    Assert.AreEqual(MajestyEconomy.HireCost(cls), CivRules.HireCost(civ, cls), cls.ToString());
                    for (int level = 1; level <= 6; level++)
                        Assert.AreEqual(OverseerRules.YardBill(level, cls), CivRules.YardBill(civ, level, cls), $"{cls} L{level}");
                }
                foreach (BuildingCategory cat in Enum.GetValues(typeof(BuildingCategory)))
                {
                    var data = Building(cat, 300, 10f);
                    Assert.AreEqual(OverseerRules.RefabMetals(data), CivRules.RefabMetals(civ, data), cat.ToString());
                }
            }
            Assert.AreEqual(120, CivRules.HireCost(Concord, SpecialistClass.ScoutDrone));
            Assert.AreEqual(400, CivRules.HireCost(Concord, SpecialistClass.DefenseMech));
            Assert.AreEqual(180, CivRules.HireCost(Mandate, SpecialistClass.ScoutDrone));
            Assert.AreEqual(600, CivRules.HireCost(Mandate, SpecialistClass.DefenseMech));
            Assert.AreEqual(MajestyEconomy.ResurrectCost(400, 3), CivRules.YardBill(Concord, 3, SpecialistClass.DefenseMech),
                "revival is built on the hire fee, so it moves with it");
            Assert.AreEqual(150, CivRules.HireCost(Exchange, SpecialistClass.ScoutDrone));
            Assert.AreEqual(150, CivRules.HireCost(Pathfinder, SpecialistClass.ScoutDrone));
        }

        [Test]
        public void HeroStartingPurse_IsUnchangedForEveryCiv()
        {
            foreach (var civ in new[] { null, None, Concord, Exchange, Mandate, Pathfinder })
                Assert.AreEqual(MajestyEconomy.HeroStartingPurse, CivRules.HeroStartingPurse(civ), "the doubled purse was dropped");
        }

        // ---- 3. Building tax --------------------------------------------------------------

        [Test]
        public void BuildingTax_NeutralIsToday_ConcordTwentyPercentLower()
        {
            foreach (var civ in Neutral())
            {
                foreach (BuildingCategory cat in Enum.GetValues(typeof(BuildingCategory)))
                    Assert.AreEqual(MajestyEconomy.DailyTax(cat), CivRules.DailyTax(civ, cat), cat.ToString());
            }
            Assert.AreEqual(200, CivRules.DailyTax(Concord, BuildingCategory.Market), "Market 250 -> 200");
            Assert.AreEqual(40, CivRules.DailyTax(Concord, BuildingCategory.Commons), "Commons 50 -> 40");
            Assert.AreEqual(50, CivRules.DailyTax(Mandate, BuildingCategory.Commons));
        }

        // ---- 4. Collectors ----------------------------------------------------------------

        [Test]
        public void Collectors_NeutralIsToday_ExchangeFieldsThreePerCommons()
        {
            foreach (var civ in Neutral())
            {
                Assert.AreEqual(MajestyEconomy.CollectorsPerCommons, CivRules.CollectorsPerCommons(civ));
                for (int commons = 0; commons <= 3; commons++)
                for (int towers = 0; towers <= 6; towers++)
                    Assert.AreEqual(MajestyEconomy.CollectorsFor(commons, towers), CivRules.CollectorsFor(civ, commons, towers), $"{commons}/{towers}");
            }
            Assert.AreEqual(3, CivRules.CollectorsPerCommons(Exchange));
            Assert.AreEqual(3, CivRules.CollectorsFor(Exchange, 1, 0));
            Assert.AreEqual(5, CivRules.CollectorsFor(Exchange, 1, 2));
            Assert.AreEqual(MajestyEconomy.MaxCollectors, CivRules.CollectorsFor(Exchange, 2, 9), "the cap of 8 is shared");
            Assert.AreEqual(0, CivRules.CollectorsFor(Exchange, 0, 3), "no Commons, no collectors");
        }

        // ---- 5. Trade pay -----------------------------------------------------------------

        [Test]
        public void TradePay_NeutralIsToday_ExchangeTwentyPercentMore()
        {
            foreach (var civ in Neutral())
            {
                foreach (int gold in new[] { 0, 200, 300, 1234, 2100 })
                    Assert.AreEqual(gold, CivRules.TradeGold(civ, gold));
            }
            Assert.AreEqual(240, CivRules.TradeGold(Exchange, 200), "short load 200 -> 240");
            Assert.AreEqual(2520, CivRules.TradeGold(Exchange, 2100), "long load 2,100 -> 2,520");
            Assert.AreEqual(300, CivRules.DailyTax(Exchange, BuildingCategory.Market), "Market 250 -> 300 a day");
            Assert.AreEqual(50, CivRules.DailyTax(Exchange, BuildingCategory.Commons), "only the Market is trade");
            Assert.AreEqual(200, CivRules.TradeGold(Pathfinder, 200), "Pathfinder: no bonus per load");
        }

        // ---- 6. Starting treasury ---------------------------------------------------------

        [Test]
        public void StartingTreasury_NeutralIsToday_ExchangeFifteenPercentSmaller()
        {
            foreach (var civ in Neutral())
            {
                foreach (int start in new[] { 0, 2600, 3000, 3200, 3400, 3800 })
                    Assert.AreEqual(start, CivRules.StartingTreasury(civ, start));
            }
            Assert.AreEqual(2890, CivRules.StartingTreasury(Exchange, 3400), "Earth");
            Assert.AreEqual(2550, CivRules.StartingTreasury(Exchange, 3000), "Luna");
            Assert.AreEqual(2720, CivRules.StartingTreasury(Exchange, 3200), "Mars");
        }

        // ---- 7. Commons health ------------------------------------------------------------

        [Test]
        public void CommonsHealth_NeutralIs140_Mandate200_Pathfinder100()
        {
            foreach (var civ in Neutral())
                Assert.IsTrue(140f.Equals(CivRules.CommonsHealth(civ)), "bit for bit");
            Assert.AreEqual(140f, OverseerRules.CommonsHealth);
            Assert.AreEqual(200f, CivRules.CommonsHealth(Mandate));
            Assert.AreEqual(100f, CivRules.CommonsHealth(Pathfinder));
            Assert.AreEqual(140f, CivRules.CommonsHealth(Concord));
        }

        // ---- 8. Gun range (and gun damage, which no civ changes now) ------------------------

        [Test]
        public void GunRange_NeutralIs18_Mandate21_GunDamageUnchangedEverywhere()
        {
            foreach (var civ in Neutral())
            {
                Assert.IsTrue(OverseerRules.BatteryRange.Equals(CivRules.GunRange(civ)));
                Assert.IsTrue(OverseerRules.BatteryDps.Equals(CivRules.GunDamage(civ)));
            }
            Assert.AreEqual(21f, CivRules.GunRange(Mandate));
            foreach (var civ in new[] { Concord, Exchange, Mandate, Pathfinder })
                Assert.AreEqual(OverseerRules.BatteryDps, CivRules.GunDamage(civ), $"{civ.Id}: gun damage stays 4 per second");
        }

        // ---- 9. Laser arming --------------------------------------------------------------

        [Test]
        public void LaserArming_NeutralIs400_Mandate250()
        {
            foreach (var civ in Neutral())
                Assert.AreEqual(OverseerRules.WatchtowerLaserCost, CivRules.WatchtowerArmingCost(civ));
            Assert.AreEqual(250, CivRules.WatchtowerArmingCost(Mandate));
            Assert.AreEqual(400, CivRules.WatchtowerArmingCost(Pathfinder));
        }

        // ---- 10. Outpost building cost ----------------------------------------------------

        [Test]
        public void OutpostCost_NeutralIsToday_PathfinderTwentyPercentCheaper()
        {
            Assert.IsTrue(CivRules.IsOutpostBuilding(BuildingCategory.Watchtower));
            Assert.IsTrue(CivRules.IsOutpostBuilding(BuildingCategory.Mine));
            Assert.IsTrue(CivRules.IsOutpostBuilding(BuildingCategory.LandingPad));
            Assert.IsFalse(CivRules.IsOutpostBuilding(BuildingCategory.Commons));
            Assert.IsFalse(CivRules.IsOutpostBuilding(BuildingCategory.Market));

            foreach (var civ in Neutral())
            {
                foreach (BuildingCategory cat in Enum.GetValues(typeof(BuildingCategory)))
                    Assert.AreEqual(155, CivRules.BuildingPrice(civ, cat, 155), cat.ToString());
            }
            Assert.AreEqual(120, CivRules.BuildingPrice(Pathfinder, BuildingCategory.Watchtower, 150));
            Assert.AreEqual(240, CivRules.BuildingPrice(Pathfinder, BuildingCategory.Mine, 300));
            Assert.AreEqual(160, CivRules.BuildingPrice(Pathfinder, BuildingCategory.LandingPad, 200));
            Assert.AreEqual(500, CivRules.BuildingPrice(Pathfinder, BuildingCategory.Market, 500), "not an outpost");
            Assert.AreEqual(150, CivRules.BuildingPrice(Mandate, BuildingCategory.Watchtower, 150));
        }

        [Test]
        public void OutpostCost_ThePlacerCharges_ItWithTheDuplicateSurchargeOnTop()
        {
            var tower = Building(BuildingCategory.Watchtower, 150, 10f);
            var mine = Building(BuildingCategory.Mine, 300, 12f);
            var market = Building(BuildingCategory.Market, 500, 10f);

            foreach (var civ in Neutral())
            {
                var placer = new BuildingPlacer(new ResourceManager()) { Civ = civ };
                Assert.AreSame(tower.buildCost, placer.CostFor(tower), "the neutral civ returns the catalog price untouched");
                Assert.AreSame(mine.buildCost, placer.CostFor(mine));
                placer.OwnedCount = cat => 1;
                Assert.AreEqual(MajestyEconomy.DuplicateCost(150, 1), placer.CostFor(tower)[0].amount);
                Assert.AreEqual(300, placer.CostFor(mine)[0].amount, "mines do not scale with count");
            }

            var pf = new BuildingPlacer(new ResourceManager()) { Civ = Pathfinder };
            Assert.AreEqual(120, pf.CostFor(tower)[0].amount);
            Assert.AreEqual(240, pf.CostFor(mine)[0].amount);
            Assert.AreSame(market.buildCost, pf.CostFor(market), "not an outpost");
            Assert.AreEqual(150, tower.buildCost[0].amount, "the catalog record is never written");
            pf.OwnedCount = cat => 1;
            Assert.AreEqual(MajestyEconomy.DuplicateCost(120, 1), pf.CostFor(tower)[0].amount, "surcharge on the civ's price");
            Assert.AreEqual(240, pf.CostFor(mine)[0].amount);
        }

        // ---- 11. Hero speed ---------------------------------------------------------------

        [Test]
        public void HeroSpeed_NeutralIsToday_PathfinderTenPercentFaster()
        {
            foreach (var civ in Neutral())
            {
                foreach (float speed in new[] { 0.1f, 2.9f, 3f, 3.1f, 3.6f, 4.3f, 4.4f })
                    Assert.IsTrue(speed.Equals(CivRules.HeroMoveSpeed(civ, speed)), $"{speed} bit for bit");
            }
            Assert.AreEqual(4.84f, CivRules.HeroMoveSpeed(Pathfinder, 4.4f), 1e-5f, "Scout Drone");
            Assert.AreEqual(3.3f, CivRules.HeroMoveSpeed(Pathfinder, 3.0f), 1e-5f, "Defense Mech");
            Assert.AreEqual(4.4f, CivRules.HeroMoveSpeed(Concord, 4.4f));
        }

        // ---- 12. Build time ---------------------------------------------------------------

        [Test]
        public void BuildTime_NeutralIsToday_PathfinderTwentyFivePercentFaster()
        {
            foreach (var civ in Neutral())
            {
                foreach (float s in new[] { 0f, 0.05f, 7.5f, 10f, 12f, 18f, 33.3f })
                {
                    Assert.IsTrue(s.Equals(CivRules.BuildSeconds(civ, s)), $"{s} bit for bit");
                    var placer = new BuildingPlacer(new ResourceManager()) { Civ = civ };
                    Assert.IsTrue(Mathf.Max(0.1f, s).Equals(placer.BuildSecondsFor(Building(BuildingCategory.Market, 100, s))),
                        $"placer {s}");
                }
            }
            Assert.AreEqual(7.5f, CivRules.BuildSeconds(Pathfinder, 10f), 1e-5f, "Watchtower");
            Assert.AreEqual(9f, CivRules.BuildSeconds(Pathfinder, 12f), 1e-5f, "Ore Mine");
            Assert.AreEqual(13.5f, CivRules.BuildSeconds(Pathfinder, 18f), 1e-5f, "Commons");
            var pf = new BuildingPlacer(new ResourceManager()) { Civ = Pathfinder };
            Assert.AreEqual(13.5f, pf.BuildSecondsFor(Building(BuildingCategory.Commons, 500, 18f)), 1e-5f);
        }

        // ---- 13. Near-home unit damage (Mandate: Bulwark and Picket) -------------------------

        [Test]
        public void NearHomeDamage_OnlyMandateBulwarkAndPicket_OnlyNearHome()
        {
            foreach (var civ in new[] { null, None, Concord, Exchange, Pathfinder })
            {
                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    Assert.IsFalse(CivRules.HasNearHomeBonus(civ, cls));
                    Assert.IsTrue(1f.Equals(CivRules.StrikeDamageMul(civ, cls, true)), $"{cls} near home");
                    Assert.IsTrue(1f.Equals(CivRules.StrikeDamageMul(civ, cls, false)), $"{cls} away");
                }
            }

            var mandate = Mandate;
            Assert.AreEqual(1.25f, CivRules.StrikeDamageMul(mandate, SpecialistClass.DefenseMech, true), 1e-6f, "Bulwark at home");
            Assert.AreEqual(1.25f, CivRules.StrikeDamageMul(mandate, SpecialistClass.ScoutDrone, true), 1e-6f, "Picket at home");
            Assert.AreEqual(1f, CivRules.StrikeDamageMul(mandate, SpecialistClass.DefenseMech, false), "away from home");
            Assert.AreEqual(1f, CivRules.StrikeDamageMul(mandate, SpecialistClass.EngineerBot, true), "Sapper has no bonus");
            Assert.AreEqual(1f, CivRules.StrikeDamageMul(mandate, SpecialistClass.SentinelMech, true), "not in the Mandate roster");
            Assert.AreEqual(20f, OverseerRules.CommonsShadeRadius, "near home is the Commons' 20 m");
        }

        // ---- Per-player accent colours ----------------------------------------------------

        [Test]
        public void Accent_DuplicatesAndReservedColoursAreBlocked()
        {
            var magenta = new Color(0.85f, 0.15f, 0.65f);
            var blue = new Color(0.20f, 0.30f, 0.90f);
            Assert.IsTrue(CivAccents.CanUse(magenta, null));
            Assert.IsTrue(CivAccents.CanUse(magenta, new List<Color> { blue }));
            Assert.IsFalse(CivAccents.CanUse(magenta, new List<Color> { new Color(0.86f, 0.16f, 0.64f) }), "a near-duplicate is a duplicate");
            Assert.IsFalse(CivAccents.CanUse(CivPalette.CoreCyan, null), "cyan is the Core");
            Assert.IsFalse(CivAccents.CanUse(CivPalette.PodAmber, null), "amber is the chargers");
            Assert.IsFalse(CivAccents.CanUse(CivCatalog.Get(CivId.TetherExchange).teamSecondary, null), "violet is a team colour");

            var a = CivModifiers.For(CivId.PathfinderAccord, magenta);
            var b = CivModifiers.For(CivId.PathfinderAccord, blue);
            Assert.AreEqual(a.Id, b.Id, "two players may hold the same civ");
            Assert.AreNotEqual(a.Accent, b.Accent);
        }
    }
}
