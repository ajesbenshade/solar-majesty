using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Where a committed Clear Threat hero walks. The flag pole is the den centre,
    /// inside the mound, and a path to that point does not move a hero who is
    /// already standing on the colony mesh.
    /// </summary>
    public static class ClearThreatTravel
    {
        /// <summary>DenBody yaw 45°, mouth on local -Z. Euler(0, 45, 0) * back.</summary>
        public static Vector3 DefaultMouthDirection
        {
            get
            {
                Vector3 back = Quaternion.Euler(0f, 45f, 0f) * Vector3.back;
                back.y = 0f;
                return back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;
            }
        }

        /// <summary>
        /// A stand just outside the footprint, along the mouth. Creatures spawn on a
        /// 5.5 m ring, and this point is still inside melee of the ones by the opening.
        /// </summary>
        public static Vector3 ApproachPoint(Vector3 den, Vector3 mouthDirection)
        {
            Vector3 mouth = mouthDirection;
            mouth.y = 0f;
            if (mouth.sqrMagnitude < 0.0001f)
                mouth = DefaultMouthDirection;
            else
                mouth.Normalize();

            float stand = OverseerRules.DenFootprintMeters * 0.5f + 0.45f;
            Vector3 at = den + mouth * stand;
            at.y = den.y;
            return at;
        }
    }

    /// <summary>
    /// Progress toward one Clear Threat. A class, not a struct, so the repath bit
    /// cannot be lost on a copy. The first sample only records distance.
    /// </summary>
    public sealed class ClearThreatStuckWatch
    {
        public const float ProgressMeters = 0.5f;

        public float Seconds;
        public float Mark = -1f;
        public bool Repathed;
        public bool Released;

        public void Note(float distance, float dt, out bool repath, out bool release) =>
            Note(distance, dt, false, false, out repath, out release);

        /// <summary>
        /// Stuck only when the hero is neither closing, moving, nor in a fight.
        /// Combat or any movement resets the window. A frozen hero still repaths once, then lets go.
        /// </summary>
        public void Note(float distance, float dt, bool moved, bool inCombat, out bool repath, out bool release)
        {
            repath = false;
            release = false;
            if (Released || dt <= 0f) return;

            if (Mark < 0f)
            {
                Mark = distance;
                return;
            }

            bool closer = distance < Mark - ProgressMeters;
            if (closer || moved || inCombat)
            {
                Seconds = 0f;
                Repathed = false;
                if (distance < Mark)
                    Mark = distance;
                return;
            }

            Seconds += dt;
            if (Seconds < OverseerRules.ClearThreatStuckSeconds) return;

            if (!Repathed)
            {
                Repathed = true;
                Seconds = 0f;
                Mark = distance;
                repath = true;
                return;
            }

            Released = true;
            release = true;
        }

        /// <summary>Arrival or a fight. Does not forgive a release that already happened.</summary>
        public void ClearTimer()
        {
            Seconds = 0f;
            Mark = -1f;
            Repathed = false;
        }

        /// <summary>A new claim. The hero may travel again.</summary>
        public void Reset()
        {
            ClearTimer();
            Released = false;
        }
    }
}
