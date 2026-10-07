using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class ClassTemperamentTests
    {
        // ------------------------------------------------------------- flag appeal

        [Test]
        public void FromMajesty_WarriorShrugsOffDanger_RangerRoams_MageStaysHome()
        {
            var warrior = ClassAllure.FromMajesty(SpecialistClass.DefenseMech, 6f, 0.1f, 1.9f, 1.5f, 0.9f);
            var ranger = ClassAllure.FromMajesty(SpecialistClass.ScoutDrone, 2f, 0.3f, 0.6f, 0.3f, 1.4f);
            var mage = ClassAllure.FromMajesty(SpecialistClass.SurveyorBot, 8f, 0.1f, 2.2f, 0.8f, 0.6f);

            Assert.Less(warrior.dangerMul, 1f);
            Assert.Less(ranger.distanceMul, 1f);
            Assert.Greater(mage.distanceMul, 1f);
            Assert.Greater(warrior.attackMul, warrior.exploreMul, "warriors lean to attack flags");
            Assert.Greater(ranger.exploreMul, ranger.attackMul, "rangers lean to explore flags");
        }

        [Test]
        public void Allure_UnlistedClass_IsNeutral()
        {
            var t = new SpecialistBrainTuning();
            var a = t.AllureFor(SpecialistClass.EngineerBot);
            Assert.AreEqual(1f, a.distanceMul);
            Assert.AreEqual(1f, a.dangerMul);
            Assert.AreEqual(1f, a.KindMul(FlagType.ClearThreat));
        }

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private float Score(SpecialistBrainTuning t, SpecialistClass cls, float risk, float dist)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            var fd = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data); _created.Add(fd);
            data.specialistClass = cls;
            data.baseGreed = 0.3f; data.courage = 0.5f; data.workaholicBias = 0.4f;
            data.explorePreference = 0.5f; data.buildPreference = 0.5f; data.combatPreference = 0.5f;
            data.extractPreference = 0.5f; data.defendPreference = 0.5f;
            fd.flagType = FlagType.Build;
            var ctx = new SpecialistContext
            {
                Data = data, Position = Vector3.zero, HealthNormalized = 1f,
                SafetyPosition = new Vector3(0, 0, -5), CurrentAction = SpecialistAction.Idle
            };
            var flag = new FlagHandle
            {
                Data = fd, WorldPosition = new Vector3(dist, 0, 0), CurrentBounty = 300f, Risk = risk, RuntimeId = new object()
            };
            new SpecialistBrain { Tuning = t }.WouldTakeFlag(ctx, flag, 0f, out float s);
            return s;
        }

        [Test]
        public void DangerMul_SameRiskyFlag_ScoresHigherForTheBraveClass()
        {
            var t = new SpecialistBrainTuning
            {
                classAllure = new[]
                {
                    new ClassAllure { specialistClass = SpecialistClass.DefenseMech, distanceMul = 1f, dangerMul = 0.6f, attackMul = 1f, defendMul = 1f, exploreMul = 1f },
                    new ClassAllure { specialistClass = SpecialistClass.Medic, distanceMul = 1f, dangerMul = 1.4f, attackMul = 1f, defendMul = 1f, exploreMul = 1f },
                }
            };
            Assert.Greater(Score(t, SpecialistClass.DefenseMech, 0.6f, 10f), Score(t, SpecialistClass.Medic, 0.6f, 10f));
        }

        [Test]
        public void DistanceMul_SameFarFlag_ScoresHigherForTheRoamer()
        {
            var t = new SpecialistBrainTuning
            {
                classAllure = new[]
                {
                    new ClassAllure { specialistClass = SpecialistClass.ScoutDrone, distanceMul = 0.7f, dangerMul = 1f, attackMul = 1f, defendMul = 1f, exploreMul = 1f },
                    new ClassAllure { specialistClass = SpecialistClass.SurveyorBot, distanceMul = 1.3f, dangerMul = 1f, attackMul = 1f, defendMul = 1f, exploreMul = 1f },
                }
            };
            Assert.Greater(Score(t, SpecialistClass.ScoutDrone, 0f, 35f), Score(t, SpecialistClass.SurveyorBot, 0f, 35f));
        }

        // ------------------------------------------------------------- shopping

        private static ClassShopPriority Need(ShopNeed armor, ShopNeed weapon, ShopNeed health, ShopNeed magic, ShopNeed accessory) =>
            new ClassShopPriority { specialistClass = SpecialistClass.DefenseMech, armor = armor, weapon = weapon, healthPotion = health, magicPotion = magic, accessory = accessory };

        [Test]
        public void Market_MinNeed_NeverBuysMagicPotions()
        {
            var p = Need(ShopNeed.Max, ShopNeed.High, ShopNeed.High, ShopNeed.Min, ShopNeed.Min);
            var item = ShopCatalog.PreferredMarketBuy(SpecialistClass.DefenseMech, 1000, 1f, ShopItemId.RegenNecklace, p);
            Assert.IsNull(item);
        }

        [Test]
        public void Market_MaxHealthNeed_DrinksEarlier()
        {
            var max = Need(ShopNeed.Medium, ShopNeed.Medium, ShopNeed.Max, ShopNeed.Min, ShopNeed.Min);
            var med = Need(ShopNeed.Medium, ShopNeed.Medium, ShopNeed.Medium, ShopNeed.Min, ShopNeed.Min);
            Assert.AreEqual(ShopItemId.HealthPotion,
                ShopCatalog.PreferredMarketBuy(SpecialistClass.HarvesterBot, 100, 0.8f, ShopItemId.RegenNecklace, max)?.Id);
            Assert.IsNull(ShopCatalog.PreferredMarketBuy(SpecialistClass.HarvesterBot, 100, 0.8f, ShopItemId.RegenNecklace, med));
        }

        [Test]
        public void Market_LowerNeed_OnlyWithMoneyToSpare()
        {
            var p = Need(ShopNeed.Medium, ShopNeed.Medium, ShopNeed.Min, ShopNeed.Lower, ShopNeed.Min);
            Assert.IsNull(ShopCatalog.PreferredMarketBuy(SpecialistClass.Medic, 60, 1f, ShopItemId.RegenNecklace, p));
            Assert.AreEqual(ShopItemId.MagicPotion,
                ShopCatalog.PreferredMarketBuy(SpecialistClass.Medic, 400, 1f, ShopItemId.RegenNecklace, p)?.Id);
        }

        [Test]
        public void Blacksmith_ArmorFirstClass_BuysArmorBeforeItsFirstWeapon()
        {
            var armorFirst = Need(ShopNeed.Max, ShopNeed.High, ShopNeed.Medium, ShopNeed.Min, ShopNeed.Medium);
            var item = ShopCatalog.BestBlacksmithBuy(SpecialistClass.DefenseMech, 1000, ShopItemId.None, ShopItemId.None, armorFirst);
            Assert.AreEqual(ShopItemKind.PermanentSuit, item.Kind);

            var neutral = ShopCatalog.BestBlacksmithBuy(SpecialistClass.DefenseMech, 1000, ShopItemId.None, ShopItemId.None);
            Assert.AreEqual(ShopItemKind.Weapon, neutral.Kind, "original rule: first weapon first");
        }

        [Test]
        public void Defaults_FightersSkipMagic_CasterLovesIt()
        {
            var t = new EconomyTuning();
            Assert.AreEqual(ShopNeed.Min, t.ShopPriorityFor(SpecialistClass.DefenseMech).magicPotion);
            Assert.AreEqual(ShopNeed.Max, t.ShopPriorityFor(SpecialistClass.Medic).magicPotion);
            Assert.AreEqual(ShopNeed.Medium, t.ShopPriorityFor(SpecialistClass.EngineerBot).armor, "unlisted = neutral");
        }
    }
}
