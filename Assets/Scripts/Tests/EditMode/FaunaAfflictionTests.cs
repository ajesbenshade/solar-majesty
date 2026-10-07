using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class FaunaAfflictionTests
    {
        [Test]
        public void EveryFaunaKind_HasAnAffliction()
        {
            var t = new FaunaAfflictionTuning();
            var buf = new List<FaunaAffliction>();
            foreach (FaunaKind kind in System.Enum.GetValues(typeof(FaunaKind)))
            {
                t.For(kind, false, buf);
                Assert.Greater(buf.Count, 0, $"{kind} bites with no affliction");
            }
        }

        [Test]
        public void AlphaStalkers_AddTheAreaHowl()
        {
            var t = new FaunaAfflictionTuning();
            var buf = new List<FaunaAffliction>();
            t.For(FaunaKind.Stalker, false, buf);
            Assert.AreEqual(1, buf.Count);
            t.For(FaunaKind.Stalker, true, buf);
            Assert.AreEqual(2, buf.Count);
            Assert.IsTrue(buf.Exists(a => a.id == "alpha_howl" && a.aoeRadius > 0f));
        }

        [Test]
        public void Disabled_GivesNothing()
        {
            var t = new FaunaAfflictionTuning { enabled = false };
            var buf = new List<FaunaAffliction>();
            t.For(FaunaKind.Hopper, true, buf);
            Assert.AreEqual(0, buf.Count);
        }

        [Test]
        public void Clock_RespectsEachAfflictionsCooldown()
        {
            var t = new FaunaAfflictionTuning();
            var buf = new List<FaunaAffliction>();
            t.For(FaunaKind.Hopper, false, buf);
            var pounce = buf[0];
            var clock = new AfflictionClock();
            Assert.IsTrue(clock.Ready(pounce, 0f));
            clock.Used(pounce, 0f);
            Assert.IsFalse(clock.Ready(pounce, pounce.cooldown - 0.1f));
            Assert.IsTrue(clock.Ready(pounce, pounce.cooldown));
        }

        [Test]
        public void DamageOverTime_StaysAroundATenthOfAHull()
        {
            foreach (var a in FaunaAfflictionTuning.Defaults())
            {
                if (a.status != StatusKind.Poison && a.status != StatusKind.Burn) continue;
                float total = a.magnitude * (a.duration / a.period);
                Assert.Less(total, 0.15f, $"{a.id} total {total:0.000}");
                Assert.Greater(total, 0.04f, $"{a.id} total {total:0.000}");
            }
        }

        [Test]
        public void ClearHarmful_RemovesAfflictionsButKeepsBuffs()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Poison, 0.01f, 8f, 1f);
            s.Apply(StatusKind.Slow, 0.5f, 5f);
            s.Apply(StatusKind.Stun, 1f, 2f);
            s.Apply(StatusKind.Armor, 0.25f, 30f);
            s.Apply(StatusKind.Regen, 0.03f, 20f, 2f);
            Assert.AreEqual(3, s.ClearHarmful());
            Assert.IsTrue(s.Has(StatusKind.Armor));
            Assert.IsTrue(s.Has(StatusKind.Regen));
            Assert.IsFalse(s.Has(StatusKind.Poison));
        }
    }
}
