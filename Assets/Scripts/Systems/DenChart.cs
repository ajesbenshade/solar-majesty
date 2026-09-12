using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Explore / chart math for dens. No FlagType added. Does not rewrite SpecialistBrain.
    /// Unscouted dens stay fogged until a survey disc, pulse, or nearby scout charts them.
    /// </summary>
    public static class DenChart
    {
        public const float PassiveChartRadius = 16f;

        public static bool InDisc(Vector3 at, Vector3 den, float radius)
        {
            if (radius <= 0f) return false;
            float dx = den.x - at.x;
            float dz = den.z - at.z;
            return dx * dx + dz * dz <= radius * radius;
        }

        public static bool IsChartClass(SpecialistClass cls) =>
            cls == SpecialistClass.ScoutDrone || cls == SpecialistClass.SurveyorBot;
    }
}
