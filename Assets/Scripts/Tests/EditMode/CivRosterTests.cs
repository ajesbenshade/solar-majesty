using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Civ rosters: a role a civ does not list is the base class, and each listed change lands on the right number.</summary>
    public class CivRosterTests
    {
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

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

        /// <summary>A class as the game builds it today: the shared personality table.</summary>
        private SpecialistData Today(SpecialistClass cls)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _made.Add(data);
            data.specialistClass = cls;
            SpecialistPersonality.Apply(data);
            return data;
        }

        private static ClassAbilityDef Ability(string id)
        {
            foreach (var a in AbilityTuning.Defaults())
                if (a.id == id) return a;
            Assert.Fail($"no ability '{id}'");
            return default;
        }

        [Test]
        public void MissingEntry_ReturnsTheBaseClass()
        {
            var concord = CivModifiers.For(CivId.LumenConcord);
            var unit = concord.UnitFor(SpecialistClass.TerraformerBot); // the Concord does not list this role
            Assert.IsFalse(concord.HasUnitFor(SpecialistClass.TerraformerBot));
            Assert.IsTrue(unit.IsBase);
            Assert.AreEqual(SpecialistClass.TerraformerBot, unit.baseClass);

            var data = Today(SpecialistClass.TerraformerBot);
            string before = JsonUtility.ToJson(data);
            unit.ApplyTo(data);
            Assert.AreEqual(before, JsonUtility.ToJson(data), "the base class is left exactly as it is today");
        }

        [Test]
        public void NeutralCiv_FieldsEveryBaseClassUnchanged()
        {
            var none = CivModifiers.For(CivId.None);
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
            {
                var unit = none.UnitFor(cls);
                Assert.IsTrue(unit.IsBase, cls.ToString());
                Assert.AreEqual(cls, unit.baseClass);
                var data = Today(cls);
                string before = JsonUtility.ToJson(data);
                unit.ApplyTo(data);
                Assert.AreEqual(before, JsonUtility.ToJson(data), cls.ToString());
            }
        }

        [Test]
        public void EveryPlayableCiv_HasFiveSignatureUnits_OnDistinctRoles()
        {
            foreach (var def in CivCatalog.Playable())
            {
                Assert.AreEqual(5, def.roster.Count, def.id.ToString());
                var civ = new CivModifiers(def);
                foreach (var unit in def.roster)
                {
                    Assert.IsNotEmpty(unit.displayName, def.id.ToString());
                    Assert.AreSame(civ.UnitFor(unit.baseClass), civ.UnitFor(unit.baseClass));
                    Assert.AreEqual(unit.displayName, civ.UnitFor(unit.baseClass).displayName);
                }
            }
        }

        [Test]
        public void UnitChange_IsIgnoredForAnotherClass()
        {
            var oathblade = CivModifiers.For(CivId.LumenConcord).UnitFor(SpecialistClass.DefenseMech);
            var medic = Today(SpecialistClass.Medic);
            string before = JsonUtility.ToJson(medic);
            oathblade.ApplyTo(medic);
            Assert.AreEqual(before, JsonUtility.ToJson(medic));
        }

        // ---- Each changed number (draft values from the design doc) ----------------------------

        [Test]
        public void Concord_Roster()
        {
            var civ = CivModifiers.For(CivId.LumenConcord);

            var mech = Today(SpecialistClass.DefenseMech);
            Assert.AreEqual(0.22f, mech.baseGreed, 1e-5f, "today's value the doc quotes");
            civ.UnitFor(SpecialistClass.DefenseMech).ApplyTo(mech);
            Assert.AreEqual("Oathblade", mech.displayName);
            Assert.AreEqual(0.18f, mech.baseGreed, 1e-5f);
            Assert.AreEqual(3f, mech.moveSpeed, 1e-5f, "unlisted numbers stay");

            var medic = Today(SpecialistClass.Medic);
            Assert.AreEqual(0.06f, medic.combatPreference, 1e-5f);
            civ.UnitFor(SpecialistClass.Medic).ApplyTo(medic);
            Assert.AreEqual("Chorister", medic.displayName);
            Assert.AreEqual(0.35f, medic.combatPreference, 1e-5f);

            var engineer = Today(SpecialistClass.EngineerBot);
            Assert.AreEqual(0.88f, engineer.baseGreed, 1e-5f);
            civ.UnitFor(SpecialistClass.EngineerBot).ApplyTo(engineer);
            Assert.AreEqual("Mason", engineer.displayName);
            Assert.AreEqual(0.70f, engineer.baseGreed, 1e-5f);

            var archer = civ.UnitFor(SpecialistClass.SurveyorBot);
            Assert.AreEqual("Lumen Archer", archer.displayName);
            Assert.AreEqual(3, Ability("surveyor_arc_bolt").minLevel);
            Assert.IsTrue(archer.TryGetAbilityChange("surveyor_arc_bolt", out var bolt));
            Assert.AreEqual(1, bolt.ApplyTo(Ability("surveyor_arc_bolt")).minLevel);
            Assert.AreEqual(1, bolt.ApplyTo(Ability("surveyor_snare")).minLevel, "other abilities are untouched");

            Assert.AreEqual("herald_rally", civ.UnitFor(SpecialistClass.ScoutDrone).behaviourTag);
        }

        [Test]
        public void Exchange_Roster()
        {
            var civ = CivModifiers.For(CivId.TetherExchange);

            var medic = Today(SpecialistClass.Medic);
            Assert.AreEqual(3.6f, medic.moveSpeed, 1e-5f);
            Assert.AreEqual(0.42f, medic.courage, 1e-5f);
            civ.UnitFor(SpecialistClass.Medic).ApplyTo(medic);
            Assert.AreEqual("Fixer", medic.displayName);
            Assert.AreEqual(4.0f, medic.moveSpeed, 1e-5f);
            Assert.AreEqual(0.35f, medic.courage, 1e-5f);

            var harvester = Today(SpecialistClass.HarvesterBot);
            Assert.AreEqual(1.28f, harvester.workRate, 1e-5f);
            civ.UnitFor(SpecialistClass.HarvesterBot).ApplyTo(harvester);
            Assert.AreEqual("Prospector", harvester.displayName);
            Assert.AreEqual(1.40f, harvester.workRate, 1e-5f);

            var courier = Today(SpecialistClass.CourierBot);
            Assert.AreEqual(0.72f, courier.extractPreference, 1e-5f);
            var runner = civ.UnitFor(SpecialistClass.CourierBot);
            runner.ApplyTo(courier);
            Assert.AreEqual("Runner", courier.displayName);
            Assert.AreEqual(0.85f, courier.extractPreference, 1e-5f);
            Assert.AreEqual(6, Ability("courier_hauler").minLevel);
            Assert.IsTrue(runner.TryGetAbilityChange("courier_hauler", out var hauler));
            Assert.AreEqual(3, hauler.ApplyTo(Ability("courier_hauler")).minLevel);

            var geologist = Today(SpecialistClass.GeologistBot);
            var assayer = civ.UnitFor(SpecialistClass.GeologistBot);
            assayer.ApplyTo(geologist);
            Assert.AreEqual("Assayer", geologist.displayName);
            Assert.AreEqual(1.0f, geologist.extractPreference, 1e-5f);
            Assert.AreEqual(3, Ability("geologist_core_shot").minLevel);
            Assert.IsTrue(assayer.TryGetAbilityChange("geologist_core_shot", out var shot));
            Assert.AreEqual(1, shot.ApplyTo(Ability("geologist_core_shot")).minLevel);

            Assert.AreEqual("gate_warden_escort", civ.UnitFor(SpecialistClass.SentinelMech).behaviourTag);
        }

        [Test]
        public void Mandate_Roster()
        {
            var civ = CivModifiers.For(CivId.BastionMandate);

            var mech = Today(SpecialistClass.DefenseMech);
            var bulwark = civ.UnitFor(SpecialistClass.DefenseMech);
            bulwark.ApplyTo(mech);
            Assert.AreEqual("Bulwark", mech.displayName);
            Assert.AreEqual(2.7f, mech.moveSpeed, 1e-5f);
            Assert.AreEqual(3, Ability("mech_bulwark").minLevel);
            Assert.IsTrue(bulwark.TryGetAbilityChange("mech_bulwark", out var armour));
            Assert.AreEqual(1, armour.ApplyTo(Ability("mech_bulwark")).minLevel);
            Assert.IsTrue(bulwark.nearHomeBonus, "Bulwark strikes harder near home");
            Assert.IsEmpty(bulwark.behaviourTag);

            var scout = Today(SpecialistClass.ScoutDrone);
            Assert.AreEqual(1f, scout.explorePreference, 1e-5f);
            Assert.AreEqual(0.18f, scout.defendPreference, 1e-5f);
            var picket = civ.UnitFor(SpecialistClass.ScoutDrone);
            picket.ApplyTo(scout);
            Assert.AreEqual("Picket", scout.displayName);
            Assert.AreEqual(0.8f, scout.explorePreference, 1e-5f);
            Assert.AreEqual(0.5f, scout.defendPreference, 1e-5f);
            Assert.IsTrue(picket.nearHomeBonus, "Picket strikes harder near home");

            var engineer = Today(SpecialistClass.EngineerBot);
            Assert.AreEqual(0.22f, engineer.courage, 1e-5f);
            civ.UnitFor(SpecialistClass.EngineerBot).ApplyTo(engineer);
            Assert.AreEqual("Sapper", engineer.displayName);
            Assert.AreEqual(0.40f, engineer.courage, 1e-5f);

            var lancer = civ.UnitFor(SpecialistClass.TerraformerBot);
            Assert.AreEqual("Siege Lancer", lancer.displayName);
            Assert.AreEqual(3f, Ability("terra_thermal").aoeRadius, 1e-5f);
            Assert.IsTrue(lancer.TryGetAbilityChange("terra_thermal", out var lance));
            Assert.AreEqual(4f, lance.ApplyTo(Ability("terra_thermal")).aoeRadius, 1e-5f);
            Assert.AreEqual(1, lance.ApplyTo(Ability("terra_thermal")).minLevel, "the unlock level is unchanged");

            Assert.AreEqual("field_welder_repair", civ.UnitFor(SpecialistClass.Medic).behaviourTag);
        }

        [Test]
        public void Pathfinder_Roster()
        {
            var civ = CivModifiers.For(CivId.PathfinderAccord);

            var medic = Today(SpecialistClass.Medic);
            Assert.AreEqual(0.90f, medic.defendPreference, 1e-5f);
            Assert.AreEqual(0.18f, medic.explorePreference, 1e-5f);
            civ.UnitFor(SpecialistClass.Medic).ApplyTo(medic);
            Assert.AreEqual("Trail Medic", medic.displayName);
            Assert.AreEqual(0.60f, medic.defendPreference, 1e-5f);
            Assert.AreEqual(0.50f, medic.explorePreference, 1e-5f);

            var courier = Today(SpecialistClass.CourierBot);
            Assert.AreEqual(0.92f, courier.explorePreference, 1e-5f);
            civ.UnitFor(SpecialistClass.CourierBot).ApplyTo(courier);
            Assert.AreEqual("Strider", courier.displayName);
            Assert.AreEqual(1.0f, courier.explorePreference, 1e-5f);

            Assert.AreEqual("Vanguard", civ.UnitFor(SpecialistClass.SentinelMech).displayName);
            Assert.AreEqual("vanguard_forward_post", civ.UnitFor(SpecialistClass.SentinelMech).behaviourTag);
            Assert.AreEqual("Cartographer", civ.UnitFor(SpecialistClass.SurveyorBot).displayName);
            Assert.AreEqual("Stakehand", civ.UnitFor(SpecialistClass.EngineerBot).displayName);
            foreach (SpecialistClass c in Enum.GetValues(typeof(SpecialistClass)))
                Assert.IsFalse(civ.UnitFor(c).nearHomeBonus, $"{c}: the near-home bonus is the Mandate's");
        }

        [Test]
        public void NearHomeBonus_IsAChange_AndSurvivesAClone()
        {
            var unit = new CivUnitDefinition { baseClass = SpecialistClass.DefenseMech, nearHomeBonus = true };
            Assert.IsFalse(unit.IsBase);
            Assert.IsTrue(unit.Clone().nearHomeBonus);
        }

        [Test]
        public void FirstEntryForARole_Wins()
        {
            var civ = new CivModifiers(new CivDefinition
            {
                id = CivId.LumenConcord,
                roster = new List<CivUnitDefinition>
                {
                    new CivUnitDefinition { baseClass = SpecialistClass.Medic, displayName = "First" },
                    new CivUnitDefinition { baseClass = SpecialistClass.Medic, displayName = "Second" },
                    new CivUnitDefinition { baseClass = SpecialistClass.ScoutDrone } // no change: not an entry
                }
            });
            Assert.AreEqual("First", civ.UnitFor(SpecialistClass.Medic).displayName);
            Assert.IsFalse(civ.HasUnitFor(SpecialistClass.ScoutDrone));
        }
    }
}
