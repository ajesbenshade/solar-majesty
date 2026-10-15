using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// A mech that just healed at the inn used to trade Rest and its Defend flag every
    /// decision dwell (~2 s). Rest now holds until the leave line, then a cooldown blocks
    /// the next flag claim. A flee that has reached the inn door at wake-up HP (28%)
    /// hands off to rest instead of staying in Flee.
    /// </summary>
    public class RestHysteresisTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private FlagHandle DefendFlag()
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = FlagType.DefendArea;
            data.displayName = "Defend";
            data.minBounty = 40;
            data.maxBounty = 5000;
            return new FlagHandle
            {
                Data = data,
                WorldPosition = new Vector3(6f, 0f, 4f),
                CurrentBounty = 800f,
                RuntimeId = new object()
            };
        }

        [Test]
        public void RestedMech_StaysRestingThroughTheOldBand_ThenWaitsToReclaimDefend()
        {
            var tuning = new SpecialistBrainTuning();
            Assert.Greater(tuning.restLeaveHealth, 0.85f);
            Assert.Greater(tuning.restLeaveHealth, tuning.fleeResumeHealth);
            Assert.Greater(tuning.restFlagCooldownSeconds, tuning.decisionDwellSeconds);
            Assert.Greater(tuning.decisionDwellSeconds, 1.5f);

            var motives = new HeroMotives();
            var inn = new Vector3(0f, 0f, -6f);
            var defend = BrainDecision.Pursue(DefendFlag(), 0.95f, "flag_DefendArea");

            var entered = motives.Commit(
                BrainDecision.Rest(0.8f, "exhausted_or_hurt", inn), tuning, 0.40f, 0f, atInn: true);
            Assert.AreEqual(SpecialistAction.Rest, entered.Action);
            Assert.IsTrue(motives.RestLatched);

            // 0.84–0.90 is the old 0.82 release / 0.85 pull band. Steps of 2 s match the dwell.
            float[] band = { 0.84f, 0.86f, 0.88f, 0.90f };
            for (int i = 0; i < band.Length; i++)
            {
                var decision = motives.Commit(defend, tuning, band[i], (i + 1) * 2f, atInn: true);
                Assert.AreEqual(SpecialistAction.Rest, decision.Action, "hp " + band[i]);
                Assert.AreNotEqual(SpecialistAction.PursueFlag, decision.Action, "hp " + band[i]);
            }

            float leaveAt = 10f;
            var leaving = motives.Commit(defend, tuning, tuning.restLeaveHealth, leaveAt, atInn: true);
            Assert.AreNotEqual(SpecialistAction.PursueFlag, leaving.Action,
                "crossing the leave line must not reclaim the Defend flag on the same think");
            Assert.IsFalse(motives.RestLatched);

            var stillCooling = motives.Commit(
                defend, tuning, tuning.restLeaveHealth, leaveAt + tuning.restFlagCooldownSeconds - 0.05f, atInn: true);
            Assert.AreNotEqual(SpecialistAction.PursueFlag, stillCooling.Action);

            var ready = motives.Commit(
                defend, tuning, tuning.restLeaveHealth,
                leaveAt + tuning.restFlagCooldownSeconds, atInn: true);
            Assert.AreEqual(SpecialistAction.PursueFlag, ready.Action);
            Assert.AreEqual("flag_DefendArea", ready.Reason);
        }

        [Test]
        public void FleeAtTheInnDoor_At28Percent_HandsOffToRest()
        {
            Assert.AreEqual(0.28f, OverseerRules.RecoverHp, 1e-4f);
            var door = ColonyLayout.InnOutpost + new Vector3(KingdomLife.RefugeArrive, 0f, 0f);
            Assert.IsTrue(KingdomLife.AtRefuge(door, false),
                "flee stops on the door (distance <= 12); that spot has to count as the inn");
            Assert.IsFalse(KingdomLife.AtRest(door, false));

            var tuning = new SpecialistBrainTuning();
            var inn = ColonyLayout.InnOutpost;
            var motives = new HeroMotives();
            motives.Commit(BrainDecision.Flee(inn, 0.95f, "flee_to_inn"), tuning, 0.50f, 0f, atInn: false);
            Assert.IsTrue(motives.FleeLatched);

            var handed = motives.Commit(
                BrainDecision.Flee(inn, 0.95f, "flee_to_inn"), tuning, OverseerRules.RecoverHp, 2f, atInn: true);
            Assert.AreEqual(SpecialistAction.Rest, handed.Action);
            Assert.AreNotEqual(SpecialistAction.Flee, handed.Action);
            Assert.IsFalse(motives.FleeLatched);
            Assert.IsTrue(motives.RestLatched);

            var defend = BrainDecision.Pursue(DefendFlag(), 0.95f, "flag_DefendArea");
            var still = motives.Commit(defend, tuning, 0.28f, 4f, atInn: true);
            Assert.AreEqual(SpecialistAction.Rest, still.Action);

            var field = new HeroMotives();
            var stayed = field.Commit(
                BrainDecision.Flee(inn, 0.95f, "flee_to_inn"), tuning, 0.28f, 0f, atInn: false);
            Assert.AreEqual(SpecialistAction.Flee, stayed.Action, "28% HP away from the inn still flees");
        }
    }
}
