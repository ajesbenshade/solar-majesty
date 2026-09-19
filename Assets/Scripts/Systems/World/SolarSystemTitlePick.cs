using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Title-screen planet click → campaign action. Menu navigation only;
    /// never issues specialist orders.
    /// </summary>
    public static class SolarSystemTitlePick
    {
        public enum Outcome
        {
            Locked = 0,
            StartNewOnBody = 1,
            ContinueCurrent = 2,
            SwitchBody = 3
        }

        public static Outcome Resolve(
            CelestialBodyId clicked,
            CelestialBodyId currentBody,
            bool saveExists,
            bool unlocked,
            bool cheatUnlock)
        {
            if (!unlocked && !cheatUnlock)
                return Outcome.Locked;
            if (!saveExists)
                return Outcome.StartNewOnBody;
            if (clicked == currentBody)
                return Outcome.ContinueCurrent;
            return Outcome.SwitchBody;
        }

        /// <summary>
        /// Geometric pick against a moving sphere. Title timescale is 0, so Physics
        /// colliders stay parked at spawn and must not be used for clicks.
        /// </summary>
        public static bool RayHitsSphere(
            Vector3 origin,
            Vector3 direction,
            Vector3 center,
            float radius,
            out float distance)
        {
            distance = 0f;
            if (radius <= 0f) return false;
            Vector3 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            Vector3 oc = origin - center;
            float b = Vector3.Dot(oc, dir);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float disc = b * b - c;
            if (disc < 0f) return false;
            float s = Mathf.Sqrt(disc);
            float t = -b - s;
            if (t < 0f) t = -b + s;
            if (t < 0f) return false;
            distance = t;
            return true;
        }
    }
}
