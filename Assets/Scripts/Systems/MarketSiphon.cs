using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty marketplace: surplus lunch/regolith becomes CRED.
    /// Never drains the ICE tank below the colonist reserve.
    /// </summary>
    public static class MarketSiphon
    {
        public const int IcePerCredit = 2;
        public const int RegPerCredit = 4;
        public const int MaxIceCredits = 6;
        public const int MaxRegCredits = 3;
        public const int RegReserve = 24;

        public static int IceReserve(int population) =>
            Mathf.Max(12, 3 * Mathf.Max(0, population));

        public static int IceCredits(int ice, int population)
        {
            int surplus = ice - IceReserve(population);
            if (surplus < IcePerCredit) return 0;
            return Mathf.Min(MaxIceCredits, surplus / IcePerCredit);
        }

        public static int RegCredits(int reg)
        {
            int surplus = reg - RegReserve;
            if (surplus < RegPerCredit) return 0;
            return Mathf.Min(MaxRegCredits, surplus / RegPerCredit);
        }

        public static bool TrySiphon(
            ResourceManager resources,
            int population,
            out int credits,
            out int iceSpent,
            out int regSpent)
        {
            credits = 0;
            iceSpent = 0;
            regSpent = 0;
            if (resources == null) return false;

            int icePay = IceCredits(resources.Get(ResourceId.WaterIce), population);
            int regPay = RegCredits(resources.Get(ResourceId.Regolith));
            if (icePay <= 0 && regPay <= 0) return false;

            iceSpent = icePay * IcePerCredit;
            regSpent = regPay * RegPerCredit;
            if (iceSpent > 0 && !resources.TrySpend(ResourceId.WaterIce, iceSpent))
            {
                iceSpent = 0;
                icePay = 0;
            }

            if (regSpent > 0 && !resources.TrySpend(ResourceId.Regolith, regSpent))
            {
                regSpent = 0;
                regPay = 0;
            }

            credits = icePay + regPay;
            if (credits <= 0) return false;
            resources.Add(ResourceId.Metals, credits);
            return true;
        }
    }
}
