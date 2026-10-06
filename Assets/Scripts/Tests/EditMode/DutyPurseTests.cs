using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class DutyPurseTests
    {
        [Test]
        public void Purse_WantsToPayOnlyAtTheBarrier()
        {
            var t = new EconomyTuning();
            var p = new DutyPurse();
            p.Add(MajestyEconomy.HeroTax(400f)); // 200
            Assert.IsFalse(p.WantsToPay(t));
            p.Add(MajestyEconomy.HeroTax(100f)); // +50 = 250
            Assert.IsTrue(p.WantsToPay(t));
            Assert.AreEqual(250, p.Pay());
            Assert.AreEqual(0, p.Carry);
        }

        [Test]
        public void Purse_NeverWantsToPay_WhenCarryIsOff()
        {
            var t = new EconomyTuning { taxCarryEnabled = false };
            var p = new DutyPurse();
            p.Add(1000);
            Assert.IsFalse(p.WantsToPay(t));
        }

        [Test]
        public void Drop_LosesAShare_RoundedDown()
        {
            var p = new DutyPurse();
            p.Add(301);
            Assert.AreEqual(150, p.Drop(0.5f));
            Assert.AreEqual(151, p.Carry);
            Assert.AreEqual(151, p.Drop(1f));
            Assert.AreEqual(0, p.Carry);
        }

        // ------------------------------------------------------------- brain

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private SpecialistContext Ctx(SpecialistAction current)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.EngineerBot;
            data.baseGreed = 0.6f; data.courage = 0.5f; data.workaholicBias = 0.4f;
            data.explorePreference = 0.5f; data.buildPreference = 0.5f; data.combatPreference = 0.5f;
            data.extractPreference = 0.5f; data.defendPreference = 0.5f;
            return new SpecialistContext
            {
                Data = data, Position = Vector3.zero, HealthNormalized = 1f,
                SafetyPosition = new Vector3(0, 0, -5), CurrentAction = current,
                HasDutyWalk = true, DutyPosition = new Vector3(20, 0, 0)
            };
        }

        [Test]
        public void Brain_FullPurse_WalksToTheGuild()
        {
            var d = new SpecialistBrain().Evaluate(Ctx(SpecialistAction.Wander), new List<FlagHandle>(), 0.1f);
            Assert.AreEqual("pay_duty", d.Reason);
            Assert.AreEqual(new Vector3(20, 0, 0), d.TargetPosition);
        }

        [Test]
        public void Brain_FinishesTheCurrentFlagBeforePaying()
        {
            var d = new SpecialistBrain().Evaluate(Ctx(SpecialistAction.PursueFlag), new List<FlagHandle>(), 0.1f);
            Assert.AreNotEqual("pay_duty", d.Reason);
        }

        [Test]
        public void Brain_PanicStillBeatsPayingDuty()
        {
            var ctx = Ctx(SpecialistAction.Wander);
            ctx.HealthNormalized = 0.2f;
            var d = new SpecialistBrain().Evaluate(ctx, new List<FlagHandle>(), 0.1f);
            Assert.AreEqual(SpecialistAction.Flee, d.Action);
        }
    }
}
