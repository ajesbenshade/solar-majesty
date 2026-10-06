using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class StatusAndAbilityTests
    {
        // ------------------------------------------------------------- statuses (perks)

        [Test]
        public void Poison_TicksPerPeriod_ThenExpires()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Poison, 2f, 3f, 1f);
            Assert.AreEqual(0f, s.Tick(0.5f), 1e-4f);
            Assert.AreEqual(-2f, s.Tick(0.5f), 1e-4f, "first tick at 1 s");
            Assert.AreEqual(-4f, s.Tick(2f), 1e-4f, "two more ticks");
            Assert.IsFalse(s.Has(StatusKind.Poison));
            Assert.AreEqual(0f, s.Tick(5f));
        }

        [Test]
        public void Regen_HealsPositive()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Regen, 0.03f, 20f, 2f);
            Assert.AreEqual(0.03f * 5f, s.Tick(10f), 1e-4f);
        }

        [Test]
        public void Reapply_RefreshesInsteadOfStacking()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Slow, 0.5f, 8f);
            s.Apply(StatusKind.Slow, 0.3f, 12f);
            Assert.AreEqual(1, s.Count);
            Assert.AreEqual(0.5f, s.Magnitude(StatusKind.Slow), 1e-4f, "stronger magnitude kept");
            s.Tick(10f);
            Assert.IsTrue(s.Has(StatusKind.Slow), "longer duration kept");
        }

        [Test]
        public void Stun_ZeroesSpeedAndDamage_AndImmunityBlocksIt()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Stun, 1f, 3f);
            Assert.AreEqual(0f, s.SpeedMul);
            Assert.AreEqual(0f, s.OutgoingMul);

            var brave = new StatusEffects();
            brave.Apply(StatusKind.StunImmune, 1f, 30f);
            Assert.IsFalse(brave.Apply(StatusKind.Stun, 1f, 3f));
            Assert.IsFalse(brave.Stunned);

            s.Apply(StatusKind.StunImmune, 1f, 30f);
            Assert.IsFalse(s.Stunned, "berserk breaks an active stun");
        }

        [Test]
        public void Multipliers_CombineSlowHasteArmorWeaken()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Slow, 0.5f, 10f);
            s.Apply(StatusKind.Haste, 0.5f, 10f);
            s.Apply(StatusKind.Armor, 0.25f, 10f);
            s.Apply(StatusKind.Weaken, 0.25f, 10f);
            Assert.AreEqual(0.75f, s.SpeedMul, 1e-4f);
            Assert.AreEqual(0.75f, s.IncomingMul, 1e-4f);
            Assert.AreEqual(0.75f, s.OutgoingMul, 1e-4f);
        }

        // ------------------------------------------------------------- abilities (unit actions)

        [Test]
        public void Book_PicksTheBiggestReadyStrike_ThenRespectsCooldowns()
        {
            var t = new AbilityTuning();
            var book = new AbilityBook();
            Assert.IsTrue(book.TryPick(t, SpecialistClass.DefenseMech, 10, AbilityTrigger.Strike, FaunaKind.Stalker, 0f, out var first));
            Assert.AreEqual("mech_maim", first.id, "x5 maim beats x3 power strike at level 10");
            book.NoteUsed(t, first, 0f);

            Assert.IsFalse(book.TryPick(t, SpecialistClass.DefenseMech, 10, AbilityTrigger.Strike, FaunaKind.Stalker, 1f, out _),
                "global cooldown");
            Assert.IsTrue(book.TryPick(t, SpecialistClass.DefenseMech, 10, AbilityTrigger.Strike, FaunaKind.Stalker, 2f, out var second));
            Assert.AreEqual("mech_power_strike", second.id, "maim is cooling");
        }

        [Test]
        public void Book_LevelGatesAbilities()
        {
            var t = new AbilityTuning();
            var book = new AbilityBook();
            Assert.IsTrue(book.TryPick(t, SpecialistClass.DefenseMech, 1, AbilityTrigger.Strike, FaunaKind.Stalker, 0f, out var a));
            Assert.AreEqual("mech_power_strike", a.id);
            Assert.IsFalse(book.TryPick(t, SpecialistClass.DefenseMech, 1, AbilityTrigger.Hurt, null, 0f, out _), "Bulwark is level 3");
            Assert.IsTrue(book.TryPick(t, SpecialistClass.DefenseMech, 3, AbilityTrigger.Hurt, null, 0f, out var b));
            Assert.AreEqual(StatusKind.Armor, b.status);
        }

        [Test]
        public void Book_FiltersByTargetKind()
        {
            var t = new AbilityTuning();
            var book = new AbilityBook();
            Assert.IsFalse(book.TryPick(t, SpecialistClass.Medic, 1, AbilityTrigger.Strike, FaunaKind.Stalker, 0f, out _),
                "Purge only hits junk bots (Majesty undead)");
            Assert.IsTrue(book.TryPick(t, SpecialistClass.Medic, 1, AbilityTrigger.Strike, FaunaKind.JunkBot, 0f, out var p));
            Assert.AreEqual("medic_purge", p.id);
            Assert.IsTrue(book.TryPick(t, SpecialistClass.ScoutDrone, 1, AbilityTrigger.Strike, FaunaKind.Stalker, 0f, out var b));
            Assert.AreEqual("scout_beastslayer", b.id);
        }

        [Test]
        public void Disabled_PicksNothing()
        {
            var t = new AbilityTuning { enabled = false };
            Assert.IsFalse(new AbilityBook().TryPick(t, SpecialistClass.DefenseMech, 10, AbilityTrigger.Strike, FaunaKind.Stalker, 0f, out _));
        }

        [Test]
        public void Defaults_EveryClassHasAMove_AndIdsAreUnique()
        {
            var t = new AbilityTuning();
            var ids = new HashSet<string>();
            var classes = new HashSet<SpecialistClass>();
            foreach (var a in t.abilities)
            {
                Assert.IsTrue(ids.Add(a.id), $"duplicate id {a.id}");
                classes.Add(a.specialistClass);
                Assert.GreaterOrEqual(a.minLevel, 1);
                Assert.LessOrEqual(a.minLevel, OverseerRules.LevelCap);
            }
            foreach (SpecialistClass cls in System.Enum.GetValues(typeof(SpecialistClass)))
                Assert.IsTrue(classes.Contains(cls), $"{cls} has no ability");
        }
    }
}
