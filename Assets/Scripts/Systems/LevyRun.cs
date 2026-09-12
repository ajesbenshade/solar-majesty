using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty levy math. Tax accrues on occupied HABs; a Courier walks it to Commons.
    /// No new FlagType. Does not rewrite SpecialistBrain.
    /// </summary>
    public static class LevyRun
    {
        public const float SitStealSeconds = 48f;
        public const int SitStealAmount = 2;
        public const float CollectArrive = 3.4f;
        public const float DepositArrive = 4.2f;
        /// <summary>Haul may drop at a watchtower when Commons is farther than this.</summary>
        public const float FarFromCommons = 24f;
        public const float TowerPreferSlack = 4f;

        public static int Accrue(int population, bool overcrowded)
        {
            if (population <= 0) return 0;
            float scale = overcrowded ? 0.65f : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(population * Settlement.TaxPerCitizen * scale));
        }

        /// <summary>Split <paramref name="total"/> across stops by resident weights. Leftover goes to the heaviest stop.</summary>
        public static int[] SplitByWeights(int total, int[] weights)
        {
            if (total <= 0 || weights == null || weights.Length == 0)
                return weights == null ? System.Array.Empty<int>() : new int[weights.Length];

            int sum = 0;
            int heaviest = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                int w = Mathf.Max(0, weights[i]);
                sum += w;
                if (w > weights[heaviest]) heaviest = i;
            }

            var dest = new int[weights.Length];
            if (sum <= 0)
            {
                dest[0] = total;
                return dest;
            }

            int used = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                dest[i] = total * weights[i] / sum;
                used += dest[i];
            }

            dest[heaviest] += total - used;
            return dest;
        }

        /// <summary>
        /// Drop at the tower when Commons is far and the tower is meaningfully closer.
        /// Distances &lt; 0 mean that chest is missing.
        /// </summary>
        public static bool PreferWatchtower(float distCommons, float distTower)
        {
            if (distTower < 0f) return false;
            if (distCommons < 0f) return true;
            return distCommons > FarFromCommons && distTower + TowerPreferSlack < distCommons;
        }
    }
}
