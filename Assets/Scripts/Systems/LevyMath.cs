using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// HAB purse split / collect / steal. Tax no longer teleports into the stockpile —
    /// Couriers walk it home. Does not touch SpecialistBrain.ScoreFlag.
    /// </summary>
    public static class LevyMath
    {
        /// <summary>
        /// Split <paramref name="total"/> MET across occupied HABs by resident count.
        /// Remainder goes to the fullest HAB. Returns how much was placed.
        /// </summary>
        public static int Accrue(int total, int[] residents, int[] purses)
        {
            if (total <= 0 || residents == null || purses == null) return 0;
            int n = Mathf.Min(residents.Length, purses.Length);
            if (n <= 0) return 0;

            int people = 0;
            int richest = -1;
            int richestRes = -1;
            for (int i = 0; i < n; i++)
            {
                if (residents[i] <= 0) continue;
                people += residents[i];
                if (residents[i] > richestRes)
                {
                    richestRes = residents[i];
                    richest = i;
                }
            }

            if (people <= 0 || richest < 0) return 0;

            int given = 0;
            for (int i = 0; i < n; i++)
            {
                if (residents[i] <= 0) continue;
                int share = total * residents[i] / people;
                purses[i] += share;
                given += share;
            }

            int remainder = total - given;
            if (remainder > 0)
            {
                purses[richest] += remainder;
                given += remainder;
            }

            return given;
        }

        public static int Collect(ref int purse)
        {
            int take = Mathf.Max(0, purse);
            purse = 0;
            return take;
        }

        public static int Steal(ref int purse, int amount)
        {
            if (amount <= 0 || purse <= 0) return 0;
            int take = Mathf.Min(amount, purse);
            purse -= take;
            return take;
        }
    }
}
