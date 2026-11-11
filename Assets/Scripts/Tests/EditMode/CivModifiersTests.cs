using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The civ maths: the neutral civ is the identity, the arithmetic is whole-number and does not
    /// depend on the order modifiers are listed in, and the catalog's civs give the doc's numbers.
    /// </summary>
    public class CivModifiersTests
    {
        private static readonly int[] IntSamples = { 0, 1, 3, 7, 50, 140, 150, 333, 3400, -25, int.MaxValue, int.MinValue };
        private static readonly float[] FloatSamples = { 0f, 0.1f, 1f / 3f, 4f, 18f, 2.7f, -1.5f, 12345.678f };

        private static CivModifiers Build(params CivModifier[] mods) =>
            new CivModifiers(new CivDefinition { id = CivId.LumenConcord, modifiers = new List<CivModifier>(mods) });

        private static void AssertIdentity(CivModifiers civ, string label)
        {
            Assert.IsTrue(civ.IsIdentity, label);
            foreach (CivStat stat in Enum.GetValues(typeof(CivStat)))
            {
                Assert.IsFalse(civ.Changes(stat), $"{label} {stat}");
                foreach (int v in IntSamples)
                {
                    Assert.AreEqual(v, civ.Apply(stat, v), $"{label} {stat} {v}");
                    Assert.AreEqual(v, civ.ApplyPrice(stat, v), $"{label} {stat} price {v}");
                }
                foreach (float v in FloatSamples)
                    Assert.IsTrue(v.Equals(civ.Apply(stat, v)), $"{label} {stat} {v} must come back bit for bit");
            }
            foreach (BuildingCategory b in Enum.GetValues(typeof(BuildingCategory)))
                Assert.AreEqual(CivDefinition.SharedArtKey, civ.ArtKeyFor(b), $"{label} {b}");
            foreach (SpecialistClass c in Enum.GetValues(typeof(SpecialistClass)))
                Assert.IsTrue(civ.UnitFor(c).IsBase, $"{label} {c}");
        }

        [Test]
        public void NeutralCiv_IsTheIdentityForEveryStat()
        {
            CivCatalog.UseDatabase(null);
            try
            {
                AssertIdentity(CivModifiers.For(CivId.None), "None");
                AssertIdentity(new CivModifiers(null), "null definition");
                AssertIdentity(new CivModifiers(new CivDefinition()), "empty definition");
                AssertIdentity(CivModifiers.For((CivId)99), "unknown id");
                Assert.AreEqual(CivId.None, CivModifiers.For((CivId)99).Id);
            }
            finally { CivCatalog.ReloadDatabase(); }
        }

        [Test]
        public void ModifiersThatChangeNothing_LeaveTheStatUntouched()
        {
            var civ = Build(
                CivModifier.Add(CivStat.HireCost, 0),
                CivModifier.Percent(CivStat.HireCost, 100),
                CivModifier.Add(CivStat.None, 5));
            Assert.IsFalse(civ.Changes(CivStat.HireCost));
            Assert.AreEqual(155, civ.ApplyPrice(CivStat.HireCost, 155), "an unchanged price is not re-rounded");
        }

        [Test]
        public void Add_IsExact()
        {
            var civ = Build(CivModifier.Add(CivStat.CommonsHealth, 60), CivModifier.Add(CivStat.WatchtowerArmingCost, -150));
            Assert.AreEqual(200, civ.Apply(CivStat.CommonsHealth, 140));
            Assert.AreEqual(250, civ.Apply(CivStat.WatchtowerArmingCost, 400));
            Assert.AreEqual(140, civ.Apply(CivStat.GunRange, 140), "other stats are untouched");
        }

        [Test]
        public void Multiply_IsWholePercent_RoundingHalfAwayFromZero()
        {
            var civ = Build(CivModifier.Percent(CivStat.StartingTreasury, 85), CivModifier.Percent(CivStat.TradeIncome, 50));
            Assert.AreEqual(2890, civ.Apply(CivStat.StartingTreasury, 3400));
            Assert.AreEqual(9, civ.Apply(CivStat.StartingTreasury, 10), "8.5 rounds up");
            Assert.AreEqual(3, civ.Apply(CivStat.StartingTreasury, 3), "2.55 rounds up");
            Assert.AreEqual(1, civ.Apply(CivStat.StartingTreasury, 1), "0.85 rounds up");
            Assert.AreEqual(0, civ.Apply(CivStat.StartingTreasury, 0));
            Assert.AreEqual(2, civ.Apply(CivStat.TradeIncome, 3), "1.5 rounds away from zero");
            Assert.AreEqual(-2, civ.Apply(CivStat.TradeIncome, -3), "-1.5 rounds away from zero");
            Assert.AreEqual(1, civ.Apply(CivStat.TradeIncome, 1), "0.5 rounds away from zero");
        }

        [Test]
        public void AddsApplyBeforeMultipliers()
        {
            var civ = Build(CivModifier.Percent(CivStat.HireCost, 50), CivModifier.Add(CivStat.HireCost, 10));
            Assert.AreEqual(55, civ.Apply(CivStat.HireCost, 100));
        }

        [Test]
        public void Price_RoundsUpToTheNextTen_OnlyWhenChanged()
        {
            var civ = Build(CivModifier.Percent(CivStat.HireCost, 90));
            Assert.AreEqual(230, civ.ApplyPrice(CivStat.HireCost, 250), "225 rounds up");
            Assert.AreEqual(90, civ.ApplyPrice(CivStat.HireCost, 100), "a round price stays");
            Assert.AreEqual(10, civ.ApplyPrice(CivStat.HireCost, 1));
            Assert.AreEqual(0, civ.ApplyPrice(CivStat.HireCost, 0));
            Assert.AreEqual(255, civ.ApplyPrice(CivStat.BuildingDailyTax, 255), "unchanged stat");
        }

        [Test]
        public void Result_DoesNotDependOnTheOrderModifiersAreListed()
        {
            var mods = new[]
            {
                CivModifier.Add(CivStat.TradeIncome, 7),
                CivModifier.Percent(CivStat.TradeIncome, 85),
                CivModifier.Percent(CivStat.TradeIncome, 120),
                CivModifier.Add(CivStat.TradeIncome, -3)
            };
            // (333 + 7 - 3) * 85 * 120 / 10000 = 343.74
            int expected = 344;
            var order = new[] { 0, 1, 2, 3 };
            int checkedOrders = 0;
            do
            {
                var civ = Build(mods[order[0]], mods[order[1]], mods[order[2]], mods[order[3]]);
                Assert.AreEqual(expected, civ.Apply(CivStat.TradeIncome, 333), string.Join(",", order));
                Assert.AreEqual(4.08f, civ.Apply(CivStat.TradeIncome, 0f), 1e-6f, string.Join(",", order));
                checkedOrders++;
            } while (NextPermutation(order));
            Assert.AreEqual(24, checkedOrders);
        }

        private static bool NextPermutation(int[] a)
        {
            int i = a.Length - 2;
            while (i >= 0 && a[i] >= a[i + 1]) i--;
            if (i < 0) return false;
            int j = a.Length - 1;
            while (a[j] <= a[i]) j--;
            (a[i], a[j]) = (a[j], a[i]);
            Array.Reverse(a, i + 1, a.Length - i - 1);
            return true;
        }

        [Test]
        public void SameInputs_AlwaysGiveTheSameAnswer()
        {
            var a = CivModifiers.For(CivId.TetherExchange);
            var b = CivModifiers.For(CivId.TetherExchange);
            for (int v = 0; v < 5000; v += 37)
            {
                Assert.AreEqual(a.Apply(CivStat.StartingTreasury, v), b.Apply(CivStat.StartingTreasury, v));
                Assert.AreEqual(a.Apply(CivStat.StartingTreasury, v), a.Apply(CivStat.StartingTreasury, v));
            }
        }

        [Test]
        public void FloatStats_UseTheSameMathsInThousandths()
        {
            var civ = Build(CivModifier.Add(CivStat.GunRange, 3), CivModifier.Percent(CivStat.GunDamage, 125));
            Assert.AreEqual(21f, civ.Apply(CivStat.GunRange, 18f));
            Assert.AreEqual(5f, civ.Apply(CivStat.GunDamage, 4f));
            Assert.AreEqual(0.125f, civ.Apply(CivStat.GunDamage, 0.1f), 1e-6f);
        }

        [Test]
        public void HugeValues_ClampInsteadOfWrapping()
        {
            var civ = Build(CivModifier.Percent(CivStat.TradeIncome, 400), CivModifier.Add(CivStat.HireCost, 10));
            Assert.AreEqual(int.MaxValue, civ.Apply(CivStat.TradeIncome, int.MaxValue));
            Assert.AreEqual(int.MaxValue, civ.Apply(CivStat.HireCost, int.MaxValue));
        }

        [Test]
        public void TooManyMultipliersOnOneStat_IgnoreTheExtras()
        {
            var mods = new List<CivModifier>();
            for (int i = 0; i < CivModifiers.MaxMultipliersPerStat + 2; i++)
                mods.Add(CivModifier.Percent(CivStat.TradeIncome, 200));
            var civ = Build(mods.ToArray()); // logs a warning per extra
            Assert.AreEqual(4, civ.Apply(CivStat.TradeIncome, 1));
            Assert.IsFalse(Build(CivModifier.Percent(CivStat.TradeIncome, CivModifiers.MaxPercent + 1)).Changes(CivStat.TradeIncome));
        }

        [Test]
        public void Service_KeepsItsOwnCopyOfTheDefinition()
        {
            var def = new CivDefinition
            {
                id = CivId.BastionMandate,
                modifiers = new List<CivModifier> { CivModifier.Add(CivStat.CommonsHealth, 60) },
                buildingArt = new List<CivArtEntry> { new CivArtEntry(BuildingCategory.Defense, "mandate.casemate") }
            };
            var civ = new CivModifiers(def);
            def.modifiers.Clear();
            def.buildingArt.Clear();
            Assert.AreEqual(200, civ.Apply(CivStat.CommonsHealth, 140));
            Assert.AreEqual("mandate.casemate", civ.ArtKeyFor(BuildingCategory.Defense));
        }

        [Test]
        public void BuildingArt_FallsBackToTheSharedBuilding()
        {
            var civ = new CivModifiers(new CivDefinition
            {
                id = CivId.TetherExchange,
                buildingArt = new List<CivArtEntry>
                {
                    new CivArtEntry(BuildingCategory.Market, "exchange.ring_bazaar"),
                    new CivArtEntry(BuildingCategory.Market, "ignored.second_entry"),
                    new CivArtEntry(BuildingCategory.Farm, "")
                }
            });
            Assert.AreEqual("exchange.ring_bazaar", civ.ArtKeyFor(BuildingCategory.Market));
            Assert.IsTrue(civ.HasArtFor(BuildingCategory.Market));
            Assert.AreEqual(CivDefinition.SharedArtKey, civ.ArtKeyFor(BuildingCategory.Farm), "a blank key is no entry");
            Assert.AreEqual(CivDefinition.SharedArtKey, civ.ArtKeyFor(BuildingCategory.Commons));
            Assert.IsFalse(civ.HasArtFor(BuildingCategory.Commons));
        }

        [Test]
        public void Accent_BelongsToTheOwner_NotTheCiv()
        {
            var first = CivModifiers.For(CivId.LumenConcord);
            var second = CivModifiers.For(CivId.LumenConcord, Color.magenta);
            Assert.IsFalse(first.Accent.HasValue);
            Assert.AreEqual(Color.magenta, second.Accent.Value);
            Assert.AreEqual(first.TeamPrimary, second.TeamPrimary);
            Assert.AreEqual(first.Apply(CivStat.HireCost, 150), second.Apply(CivStat.HireCost, 150));
        }

        // ---- The catalog's civs give the design doc's numbers (draft values) ------------------

        [Test]
        public void LumenConcord_Numbers()
        {
            CivCatalog.UseDatabase(null);
            try
            {
                var civ = CivModifiers.For(CivId.LumenConcord);
                Assert.AreEqual(360, civ.Apply(CivStat.BountyTreasuryCost, 400));
                Assert.AreEqual(720, civ.Apply(CivStat.BountyTreasuryCost, 800));
                Assert.AreEqual(120, civ.ApplyPrice(CivStat.HireCost, 150));
                Assert.AreEqual(400, civ.ApplyPrice(CivStat.HireCost, 500));
                Assert.AreEqual(200, civ.Apply(CivStat.BuildingDailyTax, 250));
                Assert.AreEqual(40, civ.Apply(CivStat.BuildingDailyTax, 50));
                Assert.AreEqual(50, civ.Apply(CivStat.HeroStartingPurse, 50), "the doubled purse was dropped");
            }
            finally { CivCatalog.ReloadDatabase(); }
        }

        [Test]
        public void TetherExchange_Numbers()
        {
            CivCatalog.UseDatabase(null);
            try
            {
                var civ = CivModifiers.For(CivId.TetherExchange);
                Assert.AreEqual(3, civ.Apply(CivStat.CollectorsPerCommons, 2));
                Assert.AreEqual(240, civ.Apply(CivStat.TradeIncome, 200));
                Assert.AreEqual(2520, civ.Apply(CivStat.TradeIncome, 2100));
                Assert.AreEqual(300, civ.Apply(CivStat.TradeIncome, 250));
                Assert.AreEqual(2890, civ.Apply(CivStat.StartingTreasury, 3400));
                Assert.AreEqual(2550, civ.Apply(CivStat.StartingTreasury, 3000));
                Assert.AreEqual(2720, civ.Apply(CivStat.StartingTreasury, 3200));
            }
            finally { CivCatalog.ReloadDatabase(); }
        }

        [Test]
        public void BastionMandate_Numbers()
        {
            CivCatalog.UseDatabase(null);
            try
            {
                var civ = CivModifiers.For(CivId.BastionMandate);
                Assert.AreEqual(200, civ.Apply(CivStat.CommonsHealth, 140));
                Assert.AreEqual(21f, civ.Apply(CivStat.GunRange, 18f));
                Assert.AreEqual(4f, civ.Apply(CivStat.GunDamage, 4f), "gun damage moved onto the Bulwark and Picket");
                Assert.AreEqual(250, civ.ApplyPrice(CivStat.WatchtowerArmingCost, 400));
                Assert.AreEqual(180, civ.ApplyPrice(CivStat.HireCost, 150));
                Assert.AreEqual(600, civ.ApplyPrice(CivStat.HireCost, 500));
            }
            finally { CivCatalog.ReloadDatabase(); }
        }
    }
}
