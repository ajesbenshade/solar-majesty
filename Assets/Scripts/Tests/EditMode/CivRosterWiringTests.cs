using System;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The roster in live play: the data GameLoop hands every spawn (<c>DataForClass</c>), the
    /// ability table its bots read (<see cref="GameLoop.Abilities"/>), and the near-home strike
    /// rule GameLoop uses. Built on a GameLoop that never wakes, with the factory class data.
    /// </summary>
    public class CivRosterWiringTests
    {
        private GameLoop _loop;

        [SetUp]
        public void SetUp()
        {
            CivCatalog.UseDatabase(null);
            var go = new GameObject("civ-roster-loop");
            go.SetActive(false);
            _loop = go.AddComponent<GameLoop>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_loop != null)
            {
                _loop.ReleaseRosterForTests();
                UnityEngine.Object.DestroyImmediate(_loop.gameObject);
            }
            CivCatalog.ReloadDatabase();
        }

        private void Arm(CivId civ) => _loop.ArmRosterForTests(CivModifiers.For(civ));

        private static ClassAbilityDef Find(AbilityTuning t, string id)
        {
            Assert.IsTrue(CivRoster.TryFind(t, id, out var def), $"no ability '{id}'");
            return def;
        }

        private static SpecialistData Fresh(SpecialistClass cls)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            data.specialistClass = cls;
            SpecialistPersonality.Apply(data);
            return data;
        }

        // ---- Spawn data ------------------------------------------------------------------

        [Test]
        public void ConcordDefenseMech_SpawnsAsOathblade_SharedDataUntouched()
        {
            Arm(CivId.LumenConcord);
            var spawn = _loop.SpawnDataForTests(SpecialistClass.DefenseMech);
            Assert.AreEqual("Oathblade", spawn.displayName);
            Assert.AreEqual(0.18f, spawn.baseGreed, 1e-5f);
            Assert.AreEqual(3f, spawn.moveSpeed, 1e-5f, "unlisted numbers stay");
            Assert.AreEqual(SpecialistClass.DefenseMech, spawn.specialistClass, "saves keep the base class");

            var shared = _loop.SharedDataForTests(SpecialistClass.DefenseMech);
            Assert.AreNotSame(shared, spawn);
            Assert.AreEqual("Defense Mech", shared.displayName);
            Assert.AreEqual(0.22f, shared.baseGreed, 1e-5f, "the shared class data is never written to");

            Assert.AreSame(spawn, _loop.SpawnDataForTests(SpecialistClass.DefenseMech), "one copy per class");
            Assert.AreSame(_loop.SharedDataForTests(SpecialistClass.TerraformerBot),
                _loop.SpawnDataForTests(SpecialistClass.TerraformerBot), "a role the Concord does not list is the base class");
        }

        [Test]
        public void NeutralCiv_SpawnsEveryClassExactlyAsToday()
        {
            Arm(CivId.None);
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
            {
                var spawn = _loop.SpawnDataForTests(cls);
                Assert.AreSame(_loop.SharedDataForTests(cls), spawn, cls.ToString());
                var today = Fresh(cls);
                try
                {
                    Assert.AreEqual(JsonUtility.ToJson(today), JsonUtility.ToJson(spawn), cls.ToString());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(today);
                }
            }
        }

        [TestCase(CivId.LumenConcord)]
        [TestCase(CivId.TetherExchange)]
        [TestCase(CivId.BastionMandate)]
        [TestCase(CivId.PathfinderAccord)]
        public void EveryCiv_SpawnsItsSignatureNames_AndBaseClassesElsewhere(CivId id)
        {
            Arm(id);
            var civ = _loop.Civ;
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
            {
                var spawn = _loop.SpawnDataForTests(cls);
                Assert.AreEqual(cls, spawn.specialistClass);
                if (civ.HasUnitFor(cls))
                    Assert.AreEqual(civ.UnitFor(cls).displayName, spawn.displayName, $"{id} {cls}");
                else
                    Assert.AreSame(_loop.SharedDataForTests(cls), spawn, $"{id} {cls}");
            }
        }

        [Test]
        public void SwitchingCiv_ResolvesTheRosterAgain()
        {
            Arm(CivId.LumenConcord);
            Assert.AreEqual("Oathblade", _loop.SpawnDataForTests(SpecialistClass.DefenseMech).displayName);
            _loop.SetCivForTests(CivModifiers.For(CivId.BastionMandate));
            var bulwark = _loop.SpawnDataForTests(SpecialistClass.DefenseMech);
            Assert.AreEqual("Bulwark", bulwark.displayName);
            Assert.AreEqual(2.7f, bulwark.moveSpeed, 1e-5f);
            Assert.AreEqual(0.22f, bulwark.baseGreed, 1e-5f, "the Concord's greed does not carry over");
            _loop.SetCivForTests(CivModifiers.For(CivId.None));
            Assert.AreSame(_loop.SharedDataForTests(SpecialistClass.DefenseMech),
                _loop.SpawnDataForTests(SpecialistClass.DefenseMech));
        }

        // ---- Abilities through GameLoop -------------------------------------------------

        [Test]
        public void NeutralCiv_ReadsTheSharedAbilityTable()
        {
            Arm(CivId.None);
            var t = _loop.Abilities;
            var defaults = AbilityTuning.Defaults();
            Assert.AreEqual(defaults.Length, t.abilities.Length);
            for (int i = 0; i < defaults.Length; i++)
            {
                Assert.AreEqual(defaults[i].id, t.abilities[i].id);
                Assert.AreEqual(defaults[i].minLevel, t.abilities[i].minLevel, defaults[i].id);
                Assert.AreEqual(defaults[i].cooldownSeconds, t.abilities[i].cooldownSeconds, defaults[i].id);
                Assert.AreEqual(defaults[i].aoeRadius, t.abilities[i].aoeRadius, defaults[i].id);
            }
            Assert.AreSame(t, _loop.Abilities);
        }

        [Test]
        public void RosterAbilityChanges_TakeEffectThroughTheGameLoopTable()
        {
            Arm(CivId.LumenConcord);
            Assert.AreEqual(1, Find(_loop.Abilities, "surveyor_arc_bolt").minLevel, "Lumen Archer: Arc Bolt at level 1");

            _loop.SetCivForTests(CivModifiers.For(CivId.TetherExchange));
            Assert.AreEqual(1, Find(_loop.Abilities, "geologist_core_shot").minLevel, "Assayer: Core Shot at level 1");
            Assert.AreEqual(3, Find(_loop.Abilities, "courier_hauler").minLevel, "Runner: Hauler Mech at level 3");
            Assert.AreEqual(3, Find(_loop.Abilities, "surveyor_arc_bolt").minLevel, "the Concord's change does not carry over");

            _loop.SetCivForTests(CivModifiers.For(CivId.BastionMandate));
            Assert.AreEqual(1, Find(_loop.Abilities, "mech_bulwark").minLevel, "Bulwark: armour at level 1");
            Assert.AreEqual(4f, Find(_loop.Abilities, "terra_thermal").aoeRadius, 1e-5f, "Siege Lancer: 4 m blast");

            _loop.SetCivForTests(CivModifiers.For(CivId.PathfinderAccord));
            Assert.AreEqual(25f, Find(_loop.Abilities, "surveyor_snare").cooldownSeconds, 1e-5f, "Cartographer: Snare Shot 25 s");

            _loop.SetCivForTests(CivModifiers.For(CivId.None));
            var shared = _loop.Abilities;
            Assert.AreEqual(3, Find(shared, "surveyor_arc_bolt").minLevel);
            Assert.AreEqual(3, Find(shared, "mech_bulwark").minLevel);
            Assert.AreEqual(3f, Find(shared, "terra_thermal").aoeRadius, 1e-5f);
            Assert.AreEqual(30f, Find(shared, "surveyor_snare").cooldownSeconds, 1e-5f, "the shared table is never written to");
        }

        [Test]
        public void LumenArcher_ReachesForArcBolt_AtLevelOne()
        {
            Arm(CivId.None);
            var book = new AbilityBook();
            Assert.IsTrue(book.TryPick(_loop.Abilities, SpecialistClass.SurveyorBot, 1, AbilityTrigger.Strike,
                FaunaKind.Stalker, 0f, out var today));
            Assert.AreEqual("surveyor_snare", today.id, "today a level-1 Surveyor only has Snare Shot");

            _loop.SetCivForTests(CivModifiers.For(CivId.LumenConcord));
            book.Reset();
            Assert.IsTrue(book.TryPick(_loop.Abilities, SpecialistClass.SurveyorBot, 1, AbilityTrigger.Strike,
                FaunaKind.Stalker, 0f, out var archer));
            Assert.AreEqual("surveyor_arc_bolt", archer.id, "the bigger blow is open at level 1");
        }

        [Test]
        public void Cartographer_SnareShot_IsReadyAgainAfter25Seconds()
        {
            Arm(CivId.PathfinderAccord);
            var t = _loop.Abilities;
            var book = new AbilityBook();
            book.NoteUsed(t, Find(t, "surveyor_snare"), 0f);
            Assert.AreEqual(25f, book.ReadyIn("surveyor_snare", 0f), 1e-4f);
        }

        // ---- Near-home strike bonus -----------------------------------------------------

        [Test]
        public void NearHomeBonus_At19m_NotAt21m_OnlyForFlaggedUnits()
        {
            Arm(CivId.BastionMandate);
            var civ = _loop.Civ;
            Assert.AreEqual(1.25f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.DefenseMech, 19f), 1e-6f, "Bulwark at 19 m");
            Assert.AreEqual(1.25f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.ScoutDrone, 19f), 1e-6f, "Picket at 19 m");
            Assert.AreEqual(1.25f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.DefenseMech, 20f), 1e-6f, "the 20 m edge counts");
            Assert.AreEqual(1f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.DefenseMech, 21f), "Bulwark at 21 m");
            Assert.AreEqual(1f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.ScoutDrone, 21f), "Picket at 21 m");
            Assert.AreEqual(1f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.DefenseMech, -1f), "no live Commons");
            Assert.AreEqual(1f, CivRules.StrikeDamageMulAt(civ, SpecialistClass.EngineerBot, 19f), "Sapper has no bonus");
            Assert.IsTrue(CivRules.IsNearHome(19f));
            Assert.IsFalse(CivRules.IsNearHome(21f));

            _loop.SetCivForTests(CivModifiers.For(CivId.LumenConcord));
            Assert.AreEqual(1f, CivRules.StrikeDamageMulAt(_loop.Civ, SpecialistClass.DefenseMech, 19f), "Oathblade has no bonus");
        }

        // ---- Pathfinder class rules through the colony's civ ----------------------------

        [Test]
        public void Pathfinder_StriderAndStakehand_ReadThroughTheColonyCiv()
        {
            Arm(CivId.PathfinderAccord);
            Assert.AreEqual("Strider", _loop.SpawnDataForTests(SpecialistClass.CourierBot).displayName);
            Assert.AreEqual(1.35f, CivRules.OutpostFlagWork(_loop.Civ, SpecialistClass.CourierBot), 1e-5f);
            Assert.AreEqual("Stakehand", _loop.SpawnDataForTests(SpecialistClass.EngineerBot).displayName);
            Assert.AreEqual(32f, CivRules.BuildLabourReach(_loop.Civ, SpecialistClass.EngineerBot), 1e-5f);
        }

        [Test]
        public void GuidedEarthStart_StaysNeutral_EvenWithPathfinderPicked()
        {
            Assert.AreEqual(CivId.None, CivChoice.ForNewGame(CivId.PathfinderAccord, CelestialBodyId.Earth, guidedStart: true));
            Assert.AreEqual(CivId.PathfinderAccord, CivChoice.ForNewGame(CivId.PathfinderAccord, CelestialBodyId.Mars, guidedStart: true));
            Assert.AreEqual(CivId.PathfinderAccord, CivChoice.ForNewGame(CivId.PathfinderAccord, CelestialBodyId.Earth, guidedStart: false));
        }
    }
}
