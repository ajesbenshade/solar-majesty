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
        public void Housing_IsThreeBedsPerHabPlusBonus()
        {
            var s = Make(out _, coreHabs: 2);
            s.BonusBeds = 4;

            Assert.AreEqual(2 * Settlement.HousingPerHab + 4, s.Housing);
        }

        [Test]
        public void SeedStarterCrew_DropsTwoColonistsOnce()
        {
            var s = Make(out _, coreHabs: 1);

            Assert.AreEqual(Settlement.StarterColonists, s.SeedStarterCrew());
            Assert.AreEqual(Settlement.StarterColonists, s.Population);
            Assert.AreEqual(0, s.SeedStarterCrew(), "starter crew must not arrive twice");
        }

        [Test]
        public void SeedStarterCrew_IsCappedByAvailableBeds()
        {
            var s = Make(out _);
            s.BonusBeds = 1;

            Assert.AreEqual(1, s.SeedStarterCrew());
        }

        [Test]
        public void Tax_AccruesWithoutPayingTheStockpile()
        {
            var s = Make(out ResourceManager res, coreHabs: 2);
            res.Set(ResourceId.WaterIce, 100);
            s.SeedStarterCrew();

            s.Tick(s.TaxInterval);

            Assert.AreEqual(Settlement.StarterColonists * Settlement.TaxPerCitizen, s.LastTax);
            Assert.AreEqual(s.LastTax, s.UncollectedLevy);
            Assert.AreEqual(0, res.Get(ResourceId.Metals), "levy sits until Haul walks it home");
        }

        [Test]
        public void Tax_IsThinnerWhenOvercrowded()
        {
            var s = Make(out ResourceManager res, coreHabs: 1);
            res.Set(ResourceId.WaterIce, 100);
            s.RestorePopulation(3);

            Assert.IsTrue(s.Overcrowded);
            s.Tick(s.TaxInterval);

            Assert.AreEqual(Mathf.RoundToInt(3 * Settlement.TaxPerCitizen * 0.65f), s.LastTax);
            Assert.AreEqual(s.LastTax, s.UncollectedLevy);
            Assert.AreEqual(0, res.Get(ResourceId.Metals));
        }

        [Test]
        public void Levy_DeliversIntoTheStockpile()
        {
            var s = Make(out ResourceManager res, coreHabs: 1);
            s.SeedStarterCrew();
            s.Tick(s.TaxInterval);
            int sitting = s.TakeUncollectedLevy();
            s.NoteLevyDelivered(sitting);
            Assert.AreEqual(sitting, res.Get(ResourceId.Metals));
            Assert.AreEqual(sitting, s.LastDelivered);
            Assert.AreEqual(0, s.UncollectedLevy);
        }

        [Test]
        public void LifeSupport_KillsOneColonistWhenIceRunsOut()
        {
            var s = Make(out ResourceManager res, coreHabs: 2);
            res.Set(ResourceId.WaterIce, 0);
            s.SeedStarterCrew();
            int before = s.Population;

            s.Tick(OverseerRules.LifeSupportFailSeconds);

            Assert.AreEqual(before - 1, s.Population);
            Assert.IsTrue(s.ConsumeLifeSupportFail());
            Assert.IsFalse(s.ConsumeLifeSupportFail(), "the fail flag is consumed once");
        }

        [Test]
        public void LifeSupport_LeavesColonistsAloneWhenIceIsStocked()
        {
            var s = Make(out ResourceManager res, coreHabs: 2);
            res.Set(ResourceId.WaterIce, OverseerRules.IceDeathThreshold);
            s.SeedStarterCrew();

            s.Tick(s.TaxInterval);

            Assert.AreEqual(Settlement.StarterColonists, s.Population);
        }

        [Test]
        public void CanBirth_RequiresASpareBedAndIce()
        {
            var s = Make(out ResourceManager res, coreHabs: 1);
            res.Set(ResourceId.WaterIce, 100);
            s.RestorePopulation(3);

            Assert.IsFalse(s.CanBirth, "full beds should halt births");

            s.RegisterPlaced(BuildingCategory.Habitat);
            Assert.IsTrue(s.CanBirth);

            res.Set(ResourceId.WaterIce, 0);
            Assert.IsFalse(s.CanBirth, "no ice should halt births");
        }

        [Test]
        public void Camps_ProduceOnTheProductionTick()
        {
            var s = Make(out ResourceManager res);
            s.RegisterPlaced(BuildingCategory.Farm);
            s.RegisterPlaced(BuildingCategory.Mine);

            s.Tick(s.ProductionInterval);

            Assert.AreEqual(3, res.Get(ResourceId.WaterIce), "one farm yields 3 ice");
            Assert.AreEqual(4, res.Get(ResourceId.Metals), "one mine yields 4 metals");
        }

        [Test]
        public void Camps_ProduceLessWhenPowerIsShort()
        {
            var s = Make(out ResourceManager res);
            s.RegisterPlaced(BuildingCategory.Mine);
            s.ProductionScale = OverseerRules.PowerShortWork;

            s.Tick(s.ProductionInterval);

            Assert.AreEqual(Mathf.RoundToInt(4 * OverseerRules.PowerShortWork), res.Get(ResourceId.Metals));
        }

        [Test]
        public void Demolishing_HousingEvictsSurplusPopulation()
        {
            var s = Make(out _, coreHabs: 2);
            s.RestorePopulation(6);

            s.Unregister(BuildingCategory.Habitat);

            Assert.AreEqual(3, s.Population, "colonists cannot outnumber beds");
        }

        [Test]
        public void IsSustainable_NeedsCommonsPopulationStockpileAndIncome()
        {
            var s = Make(out ResourceManager res, coreHabs: 4);
            s.SetPopulationGoal(4);
            res.Set(ResourceId.WaterIce, 50);
            res.Set(ResourceId.Metals, 50);
            res.Set(ResourceId.Regolith, 50);
            s.RestorePopulation(4);
            s.SetIncomeRates(OverseerRules.SustainMetPerMin, OverseerRules.SustainIcePerMin);

            Assert.IsFalse(s.IsSustainable, "no Commons means no sustain");

            s.RegisterPlaced(BuildingCategory.Commons);
            Assert.IsTrue(s.IsSustainable);

            s.SetIncomeRates(0f, OverseerRules.SustainIcePerMin);
            Assert.IsFalse(s.IsSustainable, "metals income below the floor breaks sustain");
        }

        [Test]
        public void EverHadHab_LatchesForTheLossCondition()
        {
            var s = Make(out _);

            Assert.IsFalse(s.EverHadHab);
            s.RegisterPlaced(BuildingCategory.Habitat);
            Assert.IsTrue(s.EverHadHab);
            s.Unregister(BuildingCategory.Habitat);
            Assert.IsTrue(s.EverHadHab, "losing the last HAB must still count as having had one");
        }

        [Test]
        public void AddVillageHab_WithoutCrew_MatchesTheStillExtinctLatch()
        {
            var s = Make(out _);
            s.AddVillageHab();

            Assert.IsTrue(s.EverHadHab, "stamp HAB latches EverHadHab");
            Assert.AreEqual(0, s.Population, "stamp used to skip SeedStarterCrew — extinct");
            Assert.Greater(s.Housing, 0);
            Assert.AreEqual(Settlement.StarterColonists, s.SeedStarterCrew());
            Assert.Greater(s.Population, 0);
        }

        [Test]
        public void StillCaptureHold_FreezesLifeSupportDeaths()
        {
            StillCaptureHold.Arm();
            try
            {
                var s = Make(out ResourceManager res, coreHabs: 1);
                res.Set(ResourceId.WaterIce, 0);
                s.SeedStarterCrew();
                int before = s.Population;

                s.Tick(OverseerRules.LifeSupportFailSeconds + 5f);

                Assert.AreEqual(before, s.Population, "still shutter must not kill colonists");
                Assert.IsFalse(s.ConsumeLifeSupportFail());
            }
            finally
            {
                StillCaptureHold.Disarm();
            }
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
            Assert.AreEqual(18, OverseerRules.GreedAsk(null));
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
            Assert.AreEqual(25, OverseerRules.RefabMetals(null));
        }
    }
}
