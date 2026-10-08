using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// An Engineer at health 0.49–0.65 used to alternate flee_to_inn and workshop_duty
    /// every think and never settle. Enter flee on the panic gate; stay until health
    /// clears the higher resume line. Every class uses that same latch.
    /// </summary>
    public class FleeHysteresisTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private SpecialistData Make(SpecialistClass cls)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = cls;
            data.baseGreed = 0.6f;
            data.courage = 0.5f;
            data.workaholicBias = 0.4f;
            data.explorePreference = 0.5f;
            data.buildPreference = 0.5f;
            data.combatPreference = 0.5f;
            data.extractPreference = 0.5f;
            data.defendPreference = 0.5f;
            return data;
        }

        private SpecialistContext Engineer(HeroMotives motives, float health)
        {
            return new SpecialistContext
            {
                Data = Make(SpecialistClass.EngineerBot),
                Position = new Vector3(12f, 0f, 8f),
                HealthNormalized = health,
                SafetyPosition = new Vector3(0f, 0f, -6f),
                VocationPosition = new Vector3(4f, 0f, 2f),
                HasWorkshop = true,
                WorkshopPosition = new Vector3(4f, 0f, 2f),
                CurrentAction = SpecialistAction.Wander,
                Motives = motives,
                CourageEffective = 0.5f
            };
        }

        private FlagHandle ExploreFlag()
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = FlagType.Explore;
            data.displayName = "Explore";
            data.minBounty = 40f;
            data.maxBounty = 5000f;
            return new FlagHandle
            {
                Data = data,
                WorldPosition = new Vector3(18f, 0f, 6f),
                CurrentBounty = 400f,
                RuntimeId = new object()
            };
        }

        [Test]
        public void Engineer_AtHealth055_DoesNotOscillateFleeAndWorkshop()
        {
            var brain = new SpecialistBrain();
            Assert.Greater(brain.Tuning.fleeResumeHealth, 0.65f);
            Assert.Greater(brain.Tuning.fleeResumeHealth, brain.Tuning.panicHealthCeiling);
            Assert.Greater(brain.Tuning.decisionDwellSeconds, 0.6f);

            var motives = new HeroMotives();
            var ctx = Engineer(motives, 0.55f);
            var flags = new List<FlagHandle> { ExploreFlag() };

            int flips = 0;
            SpecialistAction previous = SpecialistAction.Idle;
            bool fled = false;
            for (int i = 0; i < 8; i++)
            {
                // Danger chatters around the nervous line. Health stays in the old dead band.
                float danger = (i % 2 == 0) ? 0.55f : 0.05f;
                var raw = brain.Evaluate(ctx, flags, danger);
                var decision = motives.Commit(raw, brain.Tuning, ctx.HealthNormalized, i * 0.5f, atInn: false);
                if (i > 0 && decision.Action != previous)
                    flips++;
                if (decision.Action == SpecialistAction.Flee)
                {
                    fled = true;
                    Assert.AreEqual("flee_to_inn", decision.Reason);
                }
                Assert.AreNotEqual(SpecialistAction.PursueFlag, decision.Action,
                    "a $400 Explore flag must not win while the flee latch is holding");
                if (fled)
                    Assert.AreNotEqual("workshop_duty", decision.Reason);
                previous = decision.Action;
                ctx.CurrentAction = decision.Action;
            }

            Assert.IsTrue(fled, "the dangerous ticks must still enter flee");
            Assert.LessOrEqual(flips, 1, "flee and workshop_duty must not alternate");
            Assert.AreEqual(SpecialistAction.Flee, previous);

            // Healed past the resume line: the latch drops and workshop duty can return.
            ctx.HealthNormalized = 0.92f;
            ctx.AtInn = true;
            float now = 8f;
            var healed = motives.Commit(
                brain.Evaluate(ctx, flags, 0.05f), brain.Tuning, ctx.HealthNormalized, now, atInn: true);
            Assert.AreNotEqual(SpecialistAction.Flee, healed.Action);
            Assert.IsFalse(motives.FleeLatched);
        }

        [Test]
        public void EveryClass_StaysFleeingThroughTheOldDeadBand()
        {
            var brain = new SpecialistBrain();
            var classes = new[]
            {
                SpecialistClass.EngineerBot,
                SpecialistClass.ScoutDrone,
                SpecialistClass.DefenseMech,
                SpecialistClass.Medic,
                SpecialistClass.HarvesterBot,
                SpecialistClass.SurveyorBot,
                SpecialistClass.TerraformerBot,
                SpecialistClass.CourierBot,
                SpecialistClass.GeologistBot,
                SpecialistClass.SentinelMech
            };

            for (int i = 0; i < classes.Length; i++)
            {
                var cls = classes[i];
                var motives = new HeroMotives();
                motives.Commit(
                    BrainDecision.Flee(Vector3.zero, 0.9f, "flee_to_inn"),
                    brain.Tuning, 0.5f, 0f, atInn: false);
                Assert.IsTrue(motives.FleeLatched, cls.ToString());

                var ctx = Engineer(motives, 0.70f);
                ctx.Data.specialistClass = cls;
                ctx.CurrentAction = SpecialistAction.Flee;
                var held = brain.Evaluate(ctx, new List<FlagHandle>(), 0.05f);
                Assert.AreEqual(SpecialistAction.Flee, held.Action, cls + " must keep the same flee latch");
                Assert.AreEqual("flee_to_inn", held.Reason, cls.ToString());

                motives.Reset();
                ctx.Motives = motives;
                ctx.HealthNormalized = 0.70f;
                ctx.CurrentAction = SpecialistAction.Idle;
                var open = brain.Evaluate(ctx, new List<FlagHandle>(), 0.05f);
                Assert.AreNotEqual(SpecialistAction.Flee, open.Action,
                    cls + " at 0.70 with no latch is above every class enter line");
            }
        }
    }
}
