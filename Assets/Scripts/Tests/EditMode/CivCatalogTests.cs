using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>The civ table: every saved id has one entry, the rules each civ must keep, and the optional database.</summary>
    public class CivCatalogTests
    {
        private readonly List<ScriptableObject> _made = new List<ScriptableObject>();

        [SetUp]
        public void SetUp() => CivCatalog.UseDatabase(null);

        [TearDown]
        public void TearDown()
        {
            CivCatalog.ReloadDatabase();
            foreach (var so in _made)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            _made.Clear();
        }

        private T Track<T>(T so) where T : ScriptableObject
        {
            _made.Add(so);
            return so;
        }

        [Test]
        public void EverySavedCivId_HasExactlyOneEntry()
        {
            var seen = new HashSet<CivId>();
            foreach (var def in CivCatalog.All)
            {
                Assert.IsNotNull(def);
                Assert.IsTrue(seen.Add(def.id), $"{def.id} is listed twice");
            }
            foreach (CivId id in Enum.GetValues(typeof(CivId)))
                Assert.IsTrue(seen.Contains(id), $"{id} has no entry");
            Assert.AreSame(CivCatalog.Defaults, CivCatalog.All, "no database: the built-in table");
        }

        [Test]
        public void SavedNumbers_NeverMove()
        {
            Assert.AreEqual(0, (int)CivId.None);
            Assert.AreEqual(1, (int)CivId.LumenConcord);
            Assert.AreEqual(2, (int)CivId.TetherExchange);
            Assert.AreEqual(3, (int)CivId.BastionMandate);
            Assert.AreEqual(4, (int)CivId.PathfinderAccord);
        }

        [Test]
        public void NeutralCiv_ChangesNothing()
        {
            var none = CivCatalog.Get(CivId.None);
            Assert.IsNotNull(none);
            Assert.AreEqual(0, none.modifiers.Count);
            Assert.AreEqual(0, none.buildingArt.Count);
            Assert.AreEqual(0, none.roster.Count);
            Assert.AreEqual(CivDefinition.SharedArtKey, none.castleKey);
            Assert.IsFalse(none.playable);
        }

        [Test]
        public void PlayableCivs_AreTheThreeOnTheCards()
        {
            var playable = CivCatalog.Playable();
            Assert.AreEqual(3, playable.Count);
            Assert.AreEqual(CivId.LumenConcord, playable[0].id);
            Assert.AreEqual(CivId.TetherExchange, playable[1].id);
            Assert.AreEqual(CivId.BastionMandate, playable[2].id);
            foreach (var def in playable)
            {
                Assert.IsNotEmpty(def.displayName, def.id.ToString());
                Assert.IsNotEmpty(def.pitch, def.id.ToString());
                Assert.IsNotEmpty(def.summary, def.id.ToString());
                Assert.IsNotEmpty(def.castleKey, def.id.ToString());
            }
        }

        [Test]
        public void EachPlayableCiv_HasPerksAndExactlyOneTradeOff()
        {
            foreach (var def in CivCatalog.Playable())
            {
                int tradeOffs = 0, perks = 0;
                foreach (var m in def.modifiers)
                {
                    Assert.AreNotEqual(CivStat.None, m.stat, def.id.ToString());
                    if (m.tradeOff) tradeOffs++;
                    else perks++;
                }
                Assert.AreEqual(1, tradeOffs, $"{def.id} must have exactly one drawback");
                Assert.GreaterOrEqual(perks, 1, $"{def.id} must have a perk");
            }
        }

        [Test]
        public void FourthCiv_HasItsPerks_ButIsHiddenFromTheCards()
        {
            var d = CivCatalog.Get(CivId.PathfinderAccord);
            Assert.IsNotNull(d);
            Assert.AreEqual("Pathfinder Accord", d.displayName);
            Assert.IsFalse(d.playable, "off the cards until the D1 castle exists");
            Assert.IsFalse(CivCatalog.Playable().Exists(c => c.id == CivId.PathfinderAccord));
            Assert.IsFalse(new CivModifiers(d).IsIdentity, "it still plays with its perks if a save names it");
            int tradeOffs = 0;
            foreach (var m in d.modifiers)
                if (m.tradeOff) tradeOffs++;
            Assert.AreEqual(1, tradeOffs, "one drawback, like the others");
            // D1 Stilt Arcology livery (decision 5): teal-black #142226 and emerald #0D9954.
            AssertColour(Hex(0x14, 0x22, 0x26), d.teamPrimary, "teal-black");
            AssertColour(Hex(0x0D, 0x99, 0x54), d.teamSecondary, "emerald");
        }

        [Test]
        public void Mandate_SecondaryIsOxideRed()
        {
            AssertColour(Hex(0x8A, 0x3B, 0x24), CivCatalog.Get(CivId.BastionMandate).teamSecondary, "oxide red");
        }

        /// <summary>A hex colour compared to the catalog's three-decimal channels.</summary>
        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        private static void AssertColour(Color expected, Color actual, string label)
        {
            Assert.AreEqual(expected.r, actual.r, 0.002f, label);
            Assert.AreEqual(expected.g, actual.g, 0.002f, label);
            Assert.AreEqual(expected.b, actual.b, 0.002f, label);
        }

        [Test]
        public void EmeraldSeparatesFromMandateSage()
        {
            var sage = CivCatalog.Get(CivId.BastionMandate).teamPrimary;
            var emerald = CivCatalog.Get(CivId.PathfinderAccord).teamSecondary;
            Assert.Greater(CivPalette.Distance(sage, emerald), 0.2f);
        }

        [Test]
        public void Multipliers_AreWholePercentsInASaneRange()
        {
            foreach (var def in CivCatalog.All)
            {
                var perStat = new Dictionary<CivStat, int>();
                foreach (var m in def.modifiers)
                {
                    if (m.op != CivModifierOp.Multiply) continue;
                    Assert.That(m.value, Is.InRange(25, 400), $"{def.id} {m.stat}");
                    perStat.TryGetValue(m.stat, out int n);
                    perStat[m.stat] = n + 1;
                    Assert.LessOrEqual(perStat[m.stat], CivModifiers.MaxMultipliersPerStat, $"{def.id} {m.stat}");
                }
            }
        }

        [Test]
        public void NoTeamColour_IsOrIsNearTheReservedCyanOrAmber()
        {
            foreach (var def in CivCatalog.All)
            {
                if (def.id == CivId.None) continue; // today's palette, not a team livery
                Assert.IsFalse(CivPalette.IsReserved(def.teamPrimary), $"{def.id} primary reads as Core cyan or pod amber");
                Assert.IsFalse(CivPalette.IsReserved(def.teamSecondary), $"{def.id} secondary reads as Core cyan or pod amber");
                Assert.Greater(CivPalette.Distance(def.teamPrimary, def.teamSecondary), 0.2f,
                    $"{def.id} needs two colours that can be told apart");
            }
            Assert.IsTrue(CivPalette.IsReserved(CivPalette.CoreCyan));
            Assert.IsTrue(CivPalette.IsReserved(CivPalette.PodAmber));
            Assert.IsTrue(CivPalette.IsReserved(new Color(0.30f, 0.80f, 0.95f)), "a near-cyan is still cyan");
        }

        [Test]
        public void ReservedColours_MatchTheBuildingKit()
        {
            // The kit keeps these private; the civ palette mirrors them.
            Assert.AreEqual(KitColour("Cyan"), CivPalette.CoreCyan);
            Assert.AreEqual(KitColour("ChargeAmber"), CivPalette.PodAmber);
        }

        private static Color KitColour(string field)
        {
            var info = typeof(HeroBuildingKits).GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, $"HeroBuildingKits.{field} was renamed; update CivPalette and this test");
            return (Color)info.GetValue(null);
        }

        [Test]
        public void RosterAbilityChanges_NameRealAbilitiesOfTheSameClass()
        {
            var abilities = AbilityTuning.Defaults();
            foreach (var def in CivCatalog.All)
            {
                var roles = new HashSet<SpecialistClass>();
                foreach (var unit in def.roster)
                {
                    Assert.IsTrue(roles.Add(unit.baseClass), $"{def.id} lists {unit.baseClass} twice");
                    foreach (var change in unit.abilities)
                    {
                        bool found = false;
                        foreach (var a in abilities)
                            found |= a.id == change.abilityId && a.specialistClass == unit.baseClass;
                        Assert.IsTrue(found, $"{def.id} {unit.displayName}: no {unit.baseClass} ability '{change.abilityId}'");
                    }
                }
            }
        }

        [Test]
        public void SavedNumber_ThatIsNotACiv_BecomesNeutral()
        {
            Assert.AreEqual(CivId.BastionMandate, CivCatalog.FromSaved(3));
            Assert.AreEqual(CivId.None, CivCatalog.FromSaved(0));
            Assert.AreEqual(CivId.None, CivCatalog.FromSaved(99));
            Assert.AreEqual(CivId.None, CivCatalog.FromSaved(-1));
            Assert.IsFalse(CivCatalog.IsKnown(99));
        }

        [Test]
        public void DatabaseOverride_WinsEntryByEntry_AndCannotTouchNeutral()
        {
            var db = Track(ScriptableObject.CreateInstance<CivDatabase>());
            db.civs.Add(new CivDefinition { id = CivId.None, displayName = "Hijack" });
            db.civs.Add(new CivDefinition
            {
                id = CivId.TetherExchange,
                displayName = "",
                playable = true,
                modifiers = new List<CivModifier> { CivModifier.Percent(CivStat.TradeIncome, 150) }
            });
            CivCatalog.UseDatabase(db); // the None row is skipped with a warning

            Assert.IsTrue(CivCatalog.UsingDatabase);
            var exchange = CivCatalog.Get(CivId.TetherExchange);
            Assert.AreEqual("Tether Exchange", exchange.displayName, "blank text keeps the built-in name");
            Assert.AreEqual(300, new CivModifiers(exchange).Apply(CivStat.TradeIncome, 200));
            Assert.AreSame(CivCatalog.Default(CivId.None), CivCatalog.Get(CivId.None));
            Assert.AreSame(CivCatalog.Default(CivId.LumenConcord), CivCatalog.Get(CivId.LumenConcord));
            Assert.AreEqual(CivCatalog.Defaults.Count, CivCatalog.All.Count);
        }

        [Test]
        public void ExportedDatabase_ReproducesTheDefaults()
        {
            var db = Track(CivDatabase.FromDefs(CivCatalog.Defaults));
            CivCatalog.UseDatabase(db);
            foreach (var builtIn in CivCatalog.Defaults)
            {
                var got = CivCatalog.Get(builtIn.id);
                Assert.AreEqual(builtIn.displayName, got.displayName);
                Assert.AreEqual(builtIn.modifiers.Count, got.modifiers.Count);
                Assert.AreEqual(builtIn.roster.Count, got.roster.Count);
                Assert.AreEqual(builtIn.teamSecondary, got.teamSecondary);
                Assert.AreEqual(builtIn.playable, got.playable);
            }
        }
    }
}
