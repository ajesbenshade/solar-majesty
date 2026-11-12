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
        public void PlayableCivs_AreTheFourOnTheCards()
        {
            var playable = CivCatalog.Playable();
            Assert.AreEqual(4, playable.Count);
            Assert.AreEqual(CivId.LumenConcord, playable[0].id);
            Assert.AreEqual(CivId.TetherExchange, playable[1].id);
            Assert.AreEqual(CivId.BastionMandate, playable[2].id);
            Assert.AreEqual(CivId.PathfinderAccord, playable[3].id);
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
        public void FourthCiv_IsOnTheCards_WithItsStiltArcology()
        {
            var d = CivCatalog.Get(CivId.PathfinderAccord);
            Assert.IsNotNull(d);
            Assert.AreEqual("Pathfinder Accord", d.displayName);
            Assert.IsTrue(d.playable, "D1 Stilt Arcology is in Resources/Citadels");
            Assert.AreEqual("castle.stilt_arcology", d.castleKey);
            Assert.IsTrue(CivCatalog.Playable().Exists(c => c.id == CivId.PathfinderAccord));
            Assert.IsFalse(new CivModifiers(d).IsIdentity);
            int tradeOffs = 0;
            foreach (var m in d.modifiers)
                if (m.tradeOff) tradeOffs++;
            Assert.AreEqual(1, tradeOffs);
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
            Assert.AreEqual("Ring Citadel", exchange.castleName, "blank castle name keeps the built-in home");
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
                Assert.AreEqual(builtIn.castleName, got.castleName);
                Assert.AreEqual(builtIn.modifiers.Count, got.modifiers.Count);
                Assert.AreEqual(builtIn.roster.Count, got.roster.Count);
                Assert.AreEqual(builtIn.teamSecondary, got.teamSecondary);
                Assert.AreEqual(builtIn.playable, got.playable);
            }
        }
    }
}
