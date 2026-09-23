using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class ResourceManagerTests
    {
        [Test]
        public void NewStockpile_StartsEmpty()
        {
            var res = new ResourceManager();

            Assert.AreEqual(0, res.Get(ResourceId.Metals));
            Assert.AreEqual(0, res.Get(ResourceId.WaterIce));
            Assert.AreEqual(0, res.Get(ResourceId.Regolith));
            Assert.AreEqual(0, res.Get(ResourceId.Power));
        }

        [Test]
        public void TrySpend_FailsAndLeavesStockUntouched_WhenShort()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 10);

            Assert.IsFalse(res.TrySpend(ResourceId.Metals, 11));
            Assert.AreEqual(10, res.Get(ResourceId.Metals), "a failed spend must not partially debit");
        }

        [Test]
        public void TrySpend_MultiCost_IsAllOrNothing()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 10);
            res.Set(ResourceId.WaterIce, 1);
            var cost = new[]
            {
                new ResourceAmount(ResourceId.Metals, 5),
                new ResourceAmount(ResourceId.WaterIce, 5)
            };

            Assert.IsFalse(res.TrySpend(cost));
            Assert.AreEqual(10, res.Get(ResourceId.Metals), "metals must not be debited when ice is short");
            Assert.AreEqual(1, res.Get(ResourceId.WaterIce));
        }

        [Test]
        public void SpendUpTo_ClampsToAvailable()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 7);

            Assert.AreEqual(7, res.SpendUpTo(ResourceId.Metals, 100));
            Assert.AreEqual(0, res.Get(ResourceId.Metals));
        }

        [Test]
        public void StockChanged_FiresOnAdd()
        {
            var res = new ResourceManager();
            int fired = 0;
            int last = 0;
            res.StockChanged += (id, value) =>
            {
                if (id != ResourceId.Metals) return;
                fired++;
                last = value;
            };

            res.Add(ResourceId.Metals, 5);
            res.Add(ResourceId.Metals, 0);

            Assert.AreEqual(1, fired, "a zero delta should not raise the event");
            Assert.AreEqual(5, last);
        }

        [Test]
        public void ApplyLoss_JettisonsAFraction()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 100);
            res.Set(ResourceId.Regolith, 10);

            res.ApplyLoss(0.5f);

            Assert.AreEqual(50, res.Get(ResourceId.Metals));
            Assert.AreEqual(5, res.Get(ResourceId.Regolith));
        }

        [Test]
        public void CanAfford_NullOrEmptyCost_IsFree()
        {
            var res = new ResourceManager();

            Assert.IsTrue(res.CanAfford(null));
            Assert.IsTrue(res.CanAfford(new ResourceAmount[0]));
        }
    }

    public class SettlementTests
    {
        private static Settlement Make(out ResourceManager res, int coreHabs = 0)
        {
            res = new ResourceManager();
            var s = new Settlement(res);
            for (int i = 0; i < coreHabs; i++)
                s.RegisterPlaced(BuildingCategory.Habitat);
            return s;
        }

        [Test]
        public void Buildings_PayIntoTills_NeverStraightIntoTheTreasury()
        {
            var s = Make(out ResourceManager res);
            s.RegisterPlaced(BuildingCategory.Farm);
            s.RegisterPlaced(BuildingCategory.Mine);
            s.RegisterPlaced(BuildingCategory.Power);

            s.Tick(s.TaxInterval);

            Assert.AreEqual(0, res.Get(ResourceId.WaterIce), "farms pay daily tax; they grow no ICE");
            Assert.AreEqual(0, res.Get(ResourceId.Metals), "energy waits in tills for a tax collector");
            Assert.AreEqual(1, s.SolarFarms);
            Assert.AreEqual(1, s.TakePendingDays());
        }

        [Test]
        public void DailyTaxDays_CountInTheEarthDemoToo()
        {
            bool prior = DemoSettings.FirstHourDemo;
            DemoSettings.FirstHourDemo = true;
            try
            {
                var s = Make(out _, coreHabs: 2);
                s.Tick(s.TaxInterval);
                Assert.AreEqual(1, s.TakePendingDays(), "tax collectors need tills to empty in the demo");
                Assert.AreEqual(2 * MajestyEconomy.HouseDailyFlat, s.LastTax);
            }
            finally
            {
                DemoSettings.FirstHourDemo = prior;
            }
        }

        [Test]
        public void LevyDeposit_AddsCredToTheTreasury()
        {
            var s = Make(out ResourceManager res);
            s.NoteLevyDeposited(120);
            Assert.AreEqual(120, res.Get(ResourceId.Metals));
            Assert.AreEqual(120, s.LastLevyDeposited);
        }

        [Test]
        public void IsSustainable_NeedsCommonsTreasuryAndIncome()
        {
            var s = Make(out ResourceManager res);
            s.SetTreasuryGoal(5000, OverseerRules.SustainMetPerMin);
            res.Set(ResourceId.Metals, 6000);
            s.SetIncomeRate(OverseerRules.SustainMetPerMin);
            Assert.IsFalse(s.IsSustainable, "no Commons yet");

            s.RegisterPlaced(BuildingCategory.Commons);
            Assert.IsTrue(s.IsSustainable);

            res.Set(ResourceId.Metals, 4999);
            Assert.IsFalse(s.IsSustainable, "treasury under goal");
            StringAssert.Contains("5,000", s.SustainHint);

            res.Set(ResourceId.Metals, 6000);
            s.SetIncomeRate(0f);
            Assert.IsFalse(s.IsSustainable, "no income");
        }

        [Test]
        public void TerraformPulse_RaisesFarmTax_Capped()
        {
            var s = Make(out _);
            for (int i = 0; i < 20; i++) s.AddTerraformPulse();
            Assert.AreEqual(1.6f, s.FarmTaxScale, 1e-4f);
        }

        [Test]
        public void EverHadHab_LatchesAfterTheLastHouseFalls()
        {
            var s = Make(out _);

            Assert.IsFalse(s.EverHadHab);
            s.RegisterPlaced(BuildingCategory.Habitat);
            Assert.IsTrue(s.EverHadHab);
            s.Unregister(BuildingCategory.Habitat);
            Assert.IsTrue(s.EverHadHab);
        }
    }

    public class StillCaptureHoldTests
    {
        [TearDown]
        public void TearDown() => StillCaptureHold.Disarm();

        [Test]
        public void Arm_StaysActiveUntilDisarm()
        {
            Assert.IsFalse(StillCaptureHold.Active);
            StillCaptureHold.Arm();
            Assert.IsTrue(StillCaptureHold.Active);
            StillCaptureHold.Disarm();
            Assert.IsFalse(StillCaptureHold.Active);
        }

        [Test]
        public void EditorSessionKey_MatchesCaptureStillHold()
        {
            Assert.AreEqual("SM_CaptureStill_Hold", StillCaptureHold.EditorSessionKey);
        }
    }

    public class OverseerRulesTests
    {
        [Test]
        public void GreedAsk_RisesWithGreed()
        {
            var cheap = ScriptableObject.CreateInstance<SpecialistData>();
            var dear = ScriptableObject.CreateInstance<SpecialistData>();
            try
            {
                cheap.baseGreed = 0.2f;
                dear.baseGreed = 0.9f;

                Assert.Less(OverseerRules.GreedAsk(cheap), OverseerRules.GreedAsk(dear));
            }
            finally
            {
                Object.DestroyImmediate(cheap);
                Object.DestroyImmediate(dear);
            }
        }

        [Test]
        public void GreedAsk_NullData_HasASafeFloor()
        {
            Assert.AreEqual(180, OverseerRules.GreedAsk(null));
        }

        [Test]
        public void StackShare_DegradesByRank()
        {
            Assert.AreEqual(1f, OverseerRules.StackShare(0));
            Assert.AreEqual(0.55f, OverseerRules.StackShare(1));
            Assert.AreEqual(0.35f, OverseerRules.StackShare(2));
            Assert.AreEqual(0.20f, OverseerRules.StackShare(3));
            Assert.AreEqual(0.20f, OverseerRules.StackShare(99), "the tail stays flat");
        }

        [Test]
        public void RefabMetals_IsSeventyPercentOfTheMetalCost()
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            try
            {
                data.buildCost = new[]
                {
                    new ResourceAmount(ResourceId.Metals, 50),
                    new ResourceAmount(ResourceId.Regolith, 30)
                };

                Assert.AreEqual(35, OverseerRules.RefabMetals(data), "regolith is not part of the refab price");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void RefabMetals_NullData_HasASafeFloor()
        {
            Assert.AreEqual(250, OverseerRules.RefabMetals(null));
        }

        [Test]
        public void YardBill_IsHirePlusHalfPerLevel_Majesty2()
        {
            Assert.AreEqual(400, OverseerRules.YardBill(1));
            Assert.AreEqual(400, OverseerRules.YardBill(0), "level 0 clamps to L1");
            Assert.AreEqual(600, OverseerRules.YardBill(2));
            Assert.AreEqual(800, OverseerRules.YardBill(3));
            Assert.AreEqual(500 + 250 * 3, OverseerRules.YardBill(4, SpecialistClass.DefenseMech), "warrior-tier hire 500");
        }

        [Test]
        public void YardBill_CapsAtReviveCostMaxSteps()
        {
            int capped = OverseerRules.YardBill(OverseerRules.LevelCap);
            Assert.AreEqual(capped, OverseerRules.YardBill(99));
            Assert.Greater(capped, OverseerRules.YardBill(1));
        }

        [Test]
        public void YardBill_UsesLevelNotReviveCountAlias()
        {
            Assert.AreEqual(OverseerRules.YardBill(4), OverseerRules.ReviveMetals(4));
            Assert.AreNotEqual(OverseerRules.YardBill(1), OverseerRules.YardBill(4));
        }

        [Test]
        public void ReviveIce_IsDroppedFromTheYardBill()
        {
            Assert.AreEqual(0, OverseerRules.ReviveIce);
            Assert.AreEqual(0, OverseerRules.ReviveIceCost(0));
            Assert.AreEqual(0, OverseerRules.ReviveIceCost(8));
            Assert.AreEqual(0, OverseerRules.ReviveIceCost(99));
        }
    }

    public class FobotYardEconomyTests
    {
        [Test]
        public void CanAffordRevive_IsMetalsOnly_EvenWithEmptyIce()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 40);
            res.Set(ResourceId.WaterIce, 0);
            var eco = new SimpleEconomy(res);

            Assert.IsTrue(eco.CanAffordRevive(40));
            Assert.IsTrue(eco.CanAffordRevive(40, 8), "ICE on the call is ignored");
            Assert.IsFalse(eco.CanAffordRevive(41));
        }

        [Test]
        public void TrySpendRevive_DoesNotDebitIce()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 60);
            res.Set(ResourceId.WaterIce, 12);
            var eco = new SimpleEconomy(res);

            Assert.IsTrue(eco.TrySpendRevive(60, 8));
            Assert.AreEqual(0, res.Get(ResourceId.Metals));
            Assert.AreEqual(12, res.Get(ResourceId.WaterIce), "yard/re-fab must not spend the tank");
        }
    }

    public class LevyMathTests
    {
        [Test]
        public void Accrue_SplitsByResidentCount()
        {
            var residents = new[] { 2, 1 };
            var purses = new[] { 0, 0 };

            int given = LevyMath.Accrue(6, residents, purses);

            Assert.AreEqual(6, given);
            Assert.AreEqual(4, purses[0]);
            Assert.AreEqual(2, purses[1]);
        }

        [Test]
        public void Accrue_RemainderGoesToTheFullestHab()
        {
            var residents = new[] { 2, 1 };
            var purses = new[] { 0, 0 };

            LevyMath.Accrue(5, residents, purses);

            Assert.AreEqual(4, purses[0], "2/3 of 5 is 3, plus remainder 1");
            Assert.AreEqual(1, purses[1]);
        }

        [Test]
        public void Accrue_EmptyHabsGetNothing()
        {
            var residents = new[] { 0, 3, 0 };
            var purses = new[] { 9, 0, 4 };

            int given = LevyMath.Accrue(3, residents, purses);

            Assert.AreEqual(3, given);
            Assert.AreEqual(9, purses[0]);
            Assert.AreEqual(3, purses[1]);
            Assert.AreEqual(4, purses[2]);
        }

        [Test]
        public void Accrue_NoOccupiedHabs_PlacesNothing()
        {
            var purses = new[] { 0, 0 };
            Assert.AreEqual(0, LevyMath.Accrue(8, new[] { 0, 0 }, purses));
            Assert.AreEqual(0, purses[0]);
        }

        [Test]
        public void Collect_EmptiesThePurse()
        {
            int purse = 7;
            Assert.AreEqual(7, LevyMath.Collect(ref purse));
            Assert.AreEqual(0, purse);
            Assert.AreEqual(0, LevyMath.Collect(ref purse));
        }

        [Test]
        public void Steal_TakesUpToWhatIsThere()
        {
            int purse = 5;
            Assert.AreEqual(3, LevyMath.Steal(ref purse, 3));
            Assert.AreEqual(2, purse);
            Assert.AreEqual(2, LevyMath.Steal(ref purse, 10));
            Assert.AreEqual(0, purse);
            Assert.AreEqual(0, LevyMath.Steal(ref purse, 1));
        }
    }

    public class IceShopSpendTests
    {
        [Test]
        public void TechCompleteCost_NeverChargesIce()
        {
            var list = TechCatalog.All;
            Assert.Greater(list.Count, 10);
            for (int i = 0; i < list.Count; i++)
            {
                var cost = list[i].CompleteCost;
                if (cost == null) continue;
                for (int c = 0; c < cost.Length; c++)
                {
                    Assert.AreNotEqual(ResourceId.WaterIce, cost[c].resource,
                        $"{list[i].DisplayName} still prices ICE as shop currency");
                }
            }
        }

        [Test]
        public void TechIcePrices_FoldedIntoMetals()
        {
            AssertMetals(TechId.LunarRocket, 550);
            AssertMetals(TechId.MarsShip, 1100);
            AssertMetals(TechId.Icebreaker, 1200);
            AssertMetals(TechId.GeneVault, 1400);
            AssertMetals(TechId.ClimateLoom, 1600);
        }

        [Test]
        public void Research_CostsCredOnly()
        {
            AssertPower(TechId.MarsShip, 0);
            AssertPower(TechId.Icebreaker, 0);
            AssertPower(TechId.BeltHauler, 0);
            foreach (TechId id in System.Enum.GetValues(typeof(TechId)))
            {
                var def = TechCatalog.Get(id);
                if (def?.CompleteCost == null) continue;
                foreach (var c in def.CompleteCost)
                    Assert.AreEqual(ResourceId.Metals, c.resource, id.ToString());
            }
        }

        [Test]
        public void SpecialistUpkeep_NeverChargesIce()
        {
            foreach (SpecialistClass cls in System.Enum.GetValues(typeof(SpecialistClass)))
            {
                var data = ScriptableObject.CreateInstance<SpecialistData>();
                try
                {
                    data.specialistClass = cls;
                    data.upkeepPerMinute = new[] { new ResourceAmount(ResourceId.WaterIce, 9) };
                    SpecialistPersonality.Apply(data);
                    var upkeep = data.upkeepPerMinute;
                    if (upkeep == null) continue;
                    for (int i = 0; i < upkeep.Length; i++)
                    {
                        Assert.AreNotEqual(ResourceId.WaterIce, upkeep[i].resource,
                            $"{cls} payroll still spends the ICE tank");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(data);
                }
            }
        }

        [Test]
        public void MedicAndTerraformer_PayMetalsNotIce()
        {
            AssertUpkeepMetals(SpecialistClass.Medic, 1);
            AssertUpkeepMetals(SpecialistClass.TerraformerBot, 1);
        }

        private static void AssertMetals(TechId id, int metals)
        {
            var def = TechCatalog.Get(id);
            Assert.IsNotNull(def);
            Assert.AreEqual(metals, Amount(def.CompleteCost, ResourceId.Metals), id.ToString());
            Assert.AreEqual(0, Amount(def.CompleteCost, ResourceId.WaterIce), id.ToString());
        }

        private static void AssertPower(TechId id, int power)
        {
            var def = TechCatalog.Get(id);
            Assert.IsNotNull(def);
            Assert.AreEqual(power, Amount(def.CompleteCost, ResourceId.Power), id.ToString());
        }

        private static int Amount(ResourceAmount[] cost, ResourceId id)
        {
            if (cost == null) return 0;
            int n = 0;
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i].resource == id)
                    n += cost[i].amount;
            }
            return n;
        }

        private static void AssertUpkeepMetals(SpecialistClass cls, int metals)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            try
            {
                data.specialistClass = cls;
                SpecialistPersonality.Apply(data);
                Assert.AreEqual(metals, Amount(data.upkeepPerMinute, ResourceId.Metals), cls.ToString());
                Assert.AreEqual(0, Amount(data.upkeepPerMinute, ResourceId.WaterIce), cls.ToString());
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }

    public class MarketMathTests
    {
        [Test]
        public void IceReserve_IsAtLeastTwelveAndThreePerColonist()
        {
            Assert.AreEqual(12, MarketMath.IceReserve(0));
            Assert.AreEqual(12, MarketMath.IceReserve(2));
            Assert.AreEqual(12, MarketMath.IceReserve(4));
            Assert.AreEqual(15, MarketMath.IceReserve(5));
            Assert.AreEqual(30, MarketMath.IceReserve(10));
        }

        [Test]
        public void IceToSiphon_IdlesAtOrBelowReserve()
        {
            Assert.AreEqual(0, MarketMath.IceToSiphon(12, 0));
            Assert.AreEqual(0, MarketMath.IceToSiphon(11, 0));
            Assert.AreEqual(0, MarketMath.IceToSiphon(15, 5));
        }

        [Test]
        public void IceToSiphon_TakesOneFromSurplusAndNeverCutsReserve()
        {
            Assert.AreEqual(1, MarketMath.IceToSiphon(13, 0));
            Assert.AreEqual(1, MarketMath.IceToSiphon(80, 0));
            Assert.AreEqual(1, MarketMath.IceToSiphon(16, 5));
        }

        [Test]
        public void RegToSiphon_IsSilentAndRequiresAFatTank()
        {
            Assert.AreEqual(0, MarketMath.RegToSiphon(12, 0, 40), "closed stall must not dump REG");
            Assert.AreEqual(0, MarketMath.RegToSiphon(20, 0, 10));
            Assert.AreEqual(2, MarketMath.RegToSiphon(20, 0, 40));
        }

        [Test]
        public void CreditsFrom_PaysMetalsForIceAndReg()
        {
            Assert.AreEqual(20, MarketMath.CreditsFrom(1, 0));
            Assert.AreEqual(40, MarketMath.CreditsFrom(1, 2));
        }
    }

}
