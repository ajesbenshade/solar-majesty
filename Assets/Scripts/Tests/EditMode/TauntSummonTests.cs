using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class TauntSummonTests
    {
        [Test]
        public void DefenseMech_GetsChallengeTaunt_AtLevelThree()
        {
            var t = new AbilityTuning();
            ClassAbilityDef challenge = default;
            foreach (var a in t.abilities)
                if (a.id == "mech_challenge") challenge = a;
            Assert.IsTrue(challenge.taunts);
            Assert.AreEqual(3, challenge.minLevel, "Majesty level 5 -> our 3");
            Assert.AreEqual(5f, challenge.aoeRadius, 1e-4f, "Majesty taunt area 5");
            Assert.AreEqual(15f, challenge.cooldownSeconds, 1e-4f);
            Assert.Greater(challenge.tauntDuration, 0f);
        }

        [Test]
        public void Courier_SummonsEscort_ThenHaulerAtLevelSix()
        {
            var t = new AbilityTuning();
            var low = new AbilityBook();
            Assert.IsTrue(low.TryPick(t, SpecialistClass.CourierBot, 1, AbilityTrigger.Engage, null, 0f, out var a));
            Assert.AreEqual("escort_drone", a.summonId);

            var high = new AbilityBook();
            Assert.IsTrue(high.TryPick(t, SpecialistClass.CourierBot, 6, AbilityTrigger.Engage, null, 0f, out var b));
            Assert.AreEqual("hauler_mech", b.summonId, "the bigger summon first (longer cooldown)");
        }

        [Test]
        public void SummonCap_SkipsToTheNextAbility()
        {
            var t = new AbilityTuning();
            var book = new AbilityBook();
            // Hauler already out (at cap): the escort is still allowed.
            Assert.IsTrue(book.TryPick(t, SpecialistClass.CourierBot, 6, AbilityTrigger.Engage, null, 0f, out var a,
                d => d.summonId != "hauler_mech"));
            Assert.AreEqual("escort_drone", a.summonId);
            // Both out: nothing to summon.
            Assert.IsFalse(book.TryPick(t, SpecialistClass.CourierBot, 6, AbilityTrigger.Engage, null, 0f, out _,
                d => string.IsNullOrEmpty(d.summonId)));
        }

        [Test]
        public void Companions_ExistForEverySummon_AndTheHeavyOneTaunts()
        {
            var t = new AbilityTuning();
            foreach (var a in t.abilities)
            {
                if (string.IsNullOrEmpty(a.summonId)) continue;
                Assert.IsTrue(t.TryGetCompanion(a.summonId, out var c), $"{a.id} -> {a.summonId}");
                Assert.Greater(c.hull, 1f, "Majesty summons outlast their master");
                Assert.GreaterOrEqual(a.summonCap, 1);
            }
            Assert.IsTrue(t.TryGetCompanion("hauler_mech", out var hauler));
            Assert.IsTrue(hauler.taunts);
            Assert.Greater(hauler.hull, 2f);
        }
    }
}
