using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Guild tax a hero is carrying but has not handed in yet (Majesty 2 TaxCashCard).
    /// Half of every earning goes in; once it reaches the satiety barrier (250) the hero walks to
    /// its guild and pays it into the till, where a tax collector picks it up. Carried tax is at
    /// risk: a downed hero drops part of it, a scrapped one loses all of it.
    /// Pure C# so the rules are testable; <see cref="SpecialistAgent"/> owns one.
    /// </summary>
    public sealed class DutyPurse
    {
        public int Carry { get; private set; }

        public void Add(int tax)
        {
            if (tax > 0) Carry += tax;
        }

        /// <summary>Full enough to make the walk to the guild.</summary>
        public bool WantsToPay(EconomyTuning t) =>
            t != null && t.taxCarryEnabled && Carry >= Mathf.Max(1, t.taxCarryBarrier);

        /// <summary>Hand everything in. Returns the amount paid.</summary>
        public int Pay()
        {
            int n = Carry;
            Carry = 0;
            return n;
        }

        /// <summary>Drop a share (hero downed). Returns the amount lost.</summary>
        public int Drop(float fraction)
        {
            int lost = Mathf.Clamp(Mathf.FloorToInt(Carry * Mathf.Clamp01(fraction)), 0, Carry);
            Carry -= lost;
            return lost;
        }

        public void Restore(int amount) => Carry = Mathf.Max(0, amount);
    }
}
