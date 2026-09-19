using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Nearest-unused matching for Continue restore of seeded world props.
    /// Index-stable when the save sits on the same seed; falls back to nearest xz.
    /// </summary>
    public static class SaveWorldMatch
    {
        public const float IndexLockMeters = 8f;
        public const float NearestMeters = 18f;

        public static int Pick(
            IReadOnlyList<Vector3> world,
            Vector3 query,
            int preferredIndex,
            bool[] used,
            float indexLockMeters = IndexLockMeters,
            float nearestMeters = NearestMeters)
        {
            if (world == null) return -1;
            if (preferredIndex >= 0 &&
                preferredIndex < world.Count &&
                !IsUsed(used, preferredIndex) &&
                FlatSq(world[preferredIndex], query) <= indexLockMeters * indexLockMeters)
            {
                return preferredIndex;
            }

            int best = -1;
            float bestSq = nearestMeters * nearestMeters;
            for (int i = 0; i < world.Count; i++)
            {
                if (IsUsed(used, i)) continue;
                float dSq = FlatSq(world[i], query);
                if (dSq > bestSq) continue;
                bestSq = dSq;
                best = i;
            }

            return best;
        }

        public static float FlatSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static bool IsUsed(bool[] used, int i) =>
            used != null && i >= 0 && i < used.Length && used[i];
    }
}
