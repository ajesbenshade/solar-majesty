using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class HeroMotivesTests
    {
        private readonly List<Object> _created = new List<Object>();
        private SpecialistBrainTuning _t;

        [SetUp]
        public void SetUp() => _t = new SpecialistBrainTuning { maxWorkTasks = 3, maxRelaxTasks = 2, minTaskSeconds = 1f };

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private SpecialistContext Ctx(HeroMotives m, float health = 1f)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.EngineerBot;
            data.baseGreed = 0.6f; data.courage = 0.5f; data.workaholicBias = 0.4f;
            data.explorePreference = 0.5f; data.buildPreference = 0.5f; data.combatPreference = 0.5f;
            data.extractPreference = 0.5f; data.defendPreference = 0.5f;
            return new SpecialistContext
            {
                Data = data, Position = Vector3.zero, HealthNormalized = health,
                SafetyPosition = new Vector3(0, 0, -5), CurrentAction = SpecialistAction.Idle, Motives = m
            };
        }

        private FlagHandle Flag(float bounty, float x = 10f)
        {
            var fd = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(fd);
            fd.flagType = FlagType.Build;
            return new FlagHandle { Data = fd, WorldPosition = new Vector3(x, 0, 0), CurrentBounty = bounty, RuntimeId = new object() };
        }

        [Test]
        public void WorkTasks_TriggerABreak_ThenReturnToWork()
        {
            var m = new HeroMotives();
            m.Tick(0.1f, _t, SpecialistClass.EngineerBot, 1f, 0f);
            float now = 0f;
            for (int i = 0; i < 3; i++) { m.NoteDecision(_t, SpecialistAction.PursueFlag, new object(), now += 2f); }
            Assert.IsTrue(m.Relaxing);

            m.NoteDecision(_t, SpecialistAction.Rest, null, now += 2f);
            m.NoteDecision(_t, SpecialistAction.Wander, null, now += 2f);
            Assert.IsFalse(m.Relaxing, "break ends after maxRelaxTasks");
            Assert.AreEqual(0, m.WorkTasks);
        }

        [Test]
        public void SameFlagRepeated_IsOneTask()
        {
            var m = new HeroMotives();
            m.Tick(0.1f, _t, SpecialistClass.EngineerBot, 1f, 0f);
            var key = new object();
            for (int i = 0; i < 10; i++) m.NoteDecision(_t, SpecialistAction.PursueFlag, key, i * 2f);
            Assert.AreEqual(1, m.WorkTasks);
        }

        [Test]
        public void Break_EndsByTimeout()
        {
            var m = new HeroMotives();
            m.Tick(0.1f, _t, SpecialistClass.EngineerBot, 1f, 0f);
            for (int i = 0; i < 3; i++) m.NoteDecision(_t, SpecialistAction.PursueFlag, new object(), (i + 1) * 2f);
            Assert.IsTrue(m.Relaxing);
            m.Tick(_t.relaxMaxSeconds + 1f, _t, SpecialistClass.EngineerBot, 1f, 0f);
            Assert.IsFalse(m.Relaxing);
        }

        [Test]
        public void Safety_FallsSlowlyAndRecoversFast()
        {
            var m = new HeroMotives();
            m.Tick(1f, _t, SpecialistClass.EngineerBot, 0.2f, 0.8f);
            float afterOneSecond = m.Safety;
            Assert.Greater(afterOneSecond, 0.8f, "a scare is tolerated for a moment");
            for (int i = 0; i < 20; i++) m.Tick(1f, _t, SpecialistClass.EngineerBot, 0.2f, 0.8f);
            Assert.Less(m.Safety, _t.safetyFleeBelow);
            Assert.IsTrue(m.Shaken);

            float low = m.Safety;
            m.Tick(1f, _t, SpecialistClass.EngineerBot, 1f, 0f);
            Assert.Greater(m.Safety - low, 0.4f, "recovery is faster than the fall");
        }

        [Test]
        public void SafetyField_NearBuildings_HoldsSafetyUp()
        {
            var open = new HeroMotives();
            var home = new HeroMotives();
            for (int i = 0; i < 30; i++)
            {
                open.Tick(1f, _t, SpecialistClass.EngineerBot, 1f, 0.6f, 0f);
                home.Tick(1f, _t, SpecialistClass.EngineerBot, 1f, 0.6f, 1f);
            }
            Assert.Greater(home.Safety, open.Safety + 0.25f);
        }

        [Test]
        public void Shaken_HurtHeroStaysHome_UntilSafetyRecovers()
        {
            var brain = new SpecialistBrain { Tuning = _t };
            var m = new HeroMotives();
            for (int i = 0; i < 30; i++) m.Tick(1f, _t, SpecialistClass.EngineerBot, 0.5f, 0.9f);
            Assert.IsTrue(m.Shaken);

            var ctx = Ctx(m, 0.55f); // above the legacy flee line, below the panic ceiling
            var decision = brain.Evaluate(ctx, new List<FlagHandle> { Flag(900f) }, 0.1f);
            Assert.AreEqual(SpecialistAction.Flee, decision.Action);

            Assert.AreNotEqual(SpecialistAction.Flee,
                brain.Evaluate(Ctx(null, 0.55f), new List<FlagHandle> { Flag(900f) }, 0.1f).Action,
                "without motives the legacy rules apply");
        }

        [Test]
        public void Relaxing_HeroTakesABreak_ButLavishBountyStillTempts()
        {
            var brain = new SpecialistBrain { Tuning = _t };
            var m = new HeroMotives();
            m.Tick(0.1f, _t, SpecialistClass.EngineerBot, 1f, 0f);
            for (int i = 0; i < 3; i++) m.NoteDecision(_t, SpecialistAction.PursueFlag, new object(), (i + 1) * 2f);
            Assert.IsTrue(m.Relaxing);

            var ctx = Ctx(m);
            var modest = brain.Evaluate(ctx, new List<FlagHandle> { Flag(600f, 40f) }, 0.1f);
            var lavish = brain.Evaluate(ctx, new List<FlagHandle> { Flag(1000f, 40f) }, 0.1f);

            Assert.AreEqual("relax_break", modest.Reason);
            Assert.AreEqual(SpecialistAction.PursueFlag, lavish.Action);
        }

        [Test]
        public void MotivesDisabled_BehavesLikeLegacy()
        {
            _t.motivesEnabled = false;
            var brain = new SpecialistBrain { Tuning = _t };
            var m = new HeroMotives();
            var ctx = Ctx(m);
            var d = brain.Evaluate(ctx, new List<FlagHandle> { Flag(900f) }, 0.1f);
            Assert.AreEqual(SpecialistAction.PursueFlag, d.Action);
        }

        [Test]
        public void MajestyMapping_KeepsReflexesSurvivable_AndOrdered()
        {
            var warrior = ClassMotiveOverride.FromMajesty(SpecialistClass.DefenseMech, 12, 3, 20f);
            var rogue = ClassMotiveOverride.FromMajesty(SpecialistClass.HarvesterBot, 12, 3, 50f);
            var cleric = ClassMotiveOverride.FromMajesty(SpecialistClass.Medic, 12, 3, 5f);

            Assert.AreEqual(0.45f, warrior.healthReflex, 1e-4f, "median Majesty hero = our default line");
            Assert.AreEqual(0.6f, rogue.healthReflex, 1e-4f, "cautious rogue retreats first (clamped)");
            Assert.AreEqual(0.36f, cleric.healthReflex, 1e-4f, "cleric stays longest");
            foreach (var m in ClassMotiveOverride.Defaults())
            {
                Assert.GreaterOrEqual(m.healthReflex, 0.3f, $"{m.specialistClass} must leave before it goes down");
                Assert.LessOrEqual(m.healthReflex, 0.6f);
            }
        }

        [Test]
        public void Defaults_SurveyorWorksLongerAndRestsLonger_EngineerUsesGlobals()
        {
            var t = new SpecialistBrainTuning();
            Assert.AreEqual(15, t.MaxWorkTasksFor(SpecialistClass.SurveyorBot));
            Assert.AreEqual(5, t.MaxRelaxTasksFor(SpecialistClass.SurveyorBot));
            Assert.AreEqual(t.maxWorkTasks, t.MaxWorkTasksFor(SpecialistClass.EngineerBot));
            Assert.AreEqual(1f - t.panicInjury, t.HealthReflexFor(SpecialistClass.EngineerBot), 1e-4f);
        }

        [Test]
        public void Brain_CautiousClassRetreatsEarlier()
        {
            var brain = new SpecialistBrain();
            var rogue = Ctx(null, 0.58f);
            rogue.Data.specialistClass = SpecialistClass.HarvesterBot;
            var warrior = Ctx(null, 0.58f);
            warrior.Data.specialistClass = SpecialistClass.DefenseMech;

            Assert.AreEqual(SpecialistAction.Flee, brain.Evaluate(rogue, new List<FlagHandle>(), 0.1f).Action);
            Assert.AreNotEqual(SpecialistAction.Flee, brain.Evaluate(warrior, new List<FlagHandle>(), 0.1f).Action);
        }

        [Test]
        public void ClassOverride_ChangesTaskBudgetAndReflex()
        {
            _t.classMotives = new[]
            {
                new ClassMotiveOverride { specialistClass = SpecialistClass.Medic, maxWorkTasks = 8, maxRelaxTasks = 4, healthReflex = 0.5f }
            };
            Assert.AreEqual(8, _t.MaxWorkTasksFor(SpecialistClass.Medic));
            Assert.AreEqual(3, _t.MaxWorkTasksFor(SpecialistClass.EngineerBot));
            Assert.AreEqual(0.5f, _t.HealthReflexFor(SpecialistClass.Medic), 1e-4f);
            Assert.AreEqual(0.45f, _t.HealthReflexFor(SpecialistClass.EngineerBot), 1e-4f);
        }
    }
}
