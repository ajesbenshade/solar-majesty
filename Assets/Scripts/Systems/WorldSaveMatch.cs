using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Nearest-point match for Continue restore. Skip if the seed drifted.</summary>
    public static class WorldSaveMatch
    {
        public const float MaxDist = 12f;

        public static int Nearest(Vector3 at, Vector3[] points, float maxDist)
        {
            if (points == null || points.Length == 0) return -1;
            int best = -1;
            float bestSq = maxDist * maxDist;
            for (int i = 0; i < points.Length; i++)
            {
                float dx = points[i].x - at.x;
                float dz = points[i].z - at.z;
                float dSq = dx * dx + dz * dz;
                if (dSq <= bestSq)
                {
                    bestSq = dSq;
                    best = i;
                }
            }

            return best;
        }
    }
}
