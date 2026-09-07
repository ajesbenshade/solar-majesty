using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Still / play dressing: a spacesuit figure crosses open dirt between yards.
    /// Not a specialist, not a FlagType, not player-commanded.
    /// </summary>
    public class CampusColonistWalk : MonoBehaviour
    {
        public Vector3 PointA;
        public Vector3 PointB;
        public float Phase;
        public float Speed = 0.42f;

        private void Update()
        {
            float span = Vector3.Distance(PointA, PointB);
            if (span < 0.4f) return;
            float t = Mathf.PingPong(Time.time * Speed / span + Phase, 1f);
            Vector3 pos = Vector3.Lerp(PointA, PointB, t);
            pos.y = 0.02f;
            transform.position = pos;
            Vector3 heading = (t < 0.5f ? PointB - PointA : PointA - PointB);
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
        }
    }
}
