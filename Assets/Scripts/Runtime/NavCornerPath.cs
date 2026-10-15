using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SolarMajesty
{
    /// <summary>
    /// A route the hero walks itself, corner by corner. SetDestination can sit pending
    /// forever on a carved river, and a straight line walks into the gorge and stops.
    /// </summary>
    public static class NavCornerPath
    {
        public const float StallReplanSeconds = 8f;
        public const int ApproachCount = 8;
        /// <summary>A river with no dry way around can be forded if it is no wider than this.</summary>
        public const float MaxFordMeters = 28f;

        public static bool TryBuild(Vector3 from, Vector3 goal, float sampleRadius, out Vector3[] corners, out NavMeshPathStatus status)
        {
            corners = Array.Empty<Vector3>();
            status = NavMeshPathStatus.PathInvalid;
            if (sampleRadius < 1f) sampleRadius = 1f;

            Vector3 start = Probe(from);
            Vector3 end = Probe(goal);
            if (!NavMesh.SamplePosition(start, out NavMeshHit hs, sampleRadius, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(end, out NavMeshHit he, sampleRadius, NavMesh.AllAreas))
                return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(hs.position, he.position, NavMesh.AllAreas, path))
                return false;
            status = path.status;
            if (path.status == NavMeshPathStatus.PathInvalid || path.corners == null || path.corners.Length == 0)
                return false;
            corners = path.corners;
            return true;
        }

        /// <summary>
        /// A complete dry path to a point on the den's near ring, inside attack range.
        /// When the river splits the mesh, one short ford is inserted and the walk continues.
        /// </summary>
        public static bool TryDenRoute(
            Vector3 from,
            Vector3 den,
            float attackRange,
            int attempt,
            Func<Vector3, bool> isWater,
            out Vector3[] corners)
        {
            corners = null;
            float ring = Mathf.Clamp(attackRange * 0.7f, 4f, Mathf.Max(4f, attackRange - 0.75f));
            Vector3 pref = from - den;
            pref.y = 0f;
            if (pref.sqrMagnitude < 0.01f) pref = Vector3.back;
            pref.Normalize();
            float baseDeg = Mathf.Atan2(pref.x, pref.z) * Mathf.Rad2Deg;

            int n = ApproachCount;
            int startTry = ((attempt % n) + n) % n;
            for (int k = 0; k < n; k++)
            {
                int i = (startTry + k) % n;
                float ang = (baseDeg + i * (360f / n)) * Mathf.Deg2Rad;
                Vector3 candidate = den + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * ring;
                if (isWater != null && isWater(candidate)) continue;
                if (!TryBuild(from, candidate, 8f, out Vector3[] built, out NavMeshPathStatus st))
                    continue;
                if (st != NavMeshPathStatus.PathComplete) continue;
                if (Flat(built[built.Length - 1], den) > attackRange + 1.25f) continue;
                corners = built;
                return true;
            }

            if (isWater == null) return false;
            return TryFord(from, den, ring, isWater, out corners);
        }

        static bool TryFord(Vector3 from, Vector3 den, float ring, Func<Vector3, bool> isWater, out Vector3[] corners)
        {
            corners = null;
            Vector3 dir = den - from;
            dir.y = 0f;
            float len = dir.magnitude;
            if (len < 2f) return false;
            dir /= len;

            const float Step = 1.25f;
            int enter = -1;
            Vector3 near = from;
            Vector3 far = den;
            bool exited = false;
            int steps = Mathf.CeilToInt(len / Step);
            for (int s = 0; s <= steps; s++)
            {
                Vector3 p = from + dir * Mathf.Min(len, s * Step);
                bool wet = isWater(p);
                if (wet && enter < 0)
                {
                    enter = s;
                    near = from + dir * Mathf.Max(0f, (s - 1) * Step);
                }
                if (!wet && enter >= 0)
                {
                    far = p;
                    exited = true;
                    break;
                }
            }
            if (enter < 0 || !exited) return false;
            if (Flat(near, far) > MaxFordMeters || Flat(near, far) < 0.5f) return false;

            var list = new List<Vector3>(16);
            if (Flat(from, near) > 2f &&
                TryBuild(from, near, 8f, out Vector3[] toBank, out NavMeshPathStatus bankStatus) &&
                bankStatus == NavMeshPathStatus.PathComplete)
            {
                list.AddRange(toBank);
            }
            else
            {
                list.Add(near);
            }

            list.Add(far);

            Vector3 approach = far;
            Vector3 outDir = far - den;
            outDir.y = 0f;
            if (outDir.sqrMagnitude > 0.25f)
                approach = den + outDir.normalized * ring;
            if (isWater(approach)) approach = far;

            if (Flat(far, approach) > 2f &&
                TryBuild(far, approach, 8f, out Vector3[] after, out NavMeshPathStatus afterStatus) &&
                afterStatus == NavMeshPathStatus.PathComplete)
            {
                list.AddRange(after);
            }
            else if (Flat(approach, den) <= ring + 1.5f)
            {
                list.Add(approach);
            }

            if (Flat(list[list.Count - 1], den) > ring + 4f) return false;
            corners = list.ToArray();
            return true;
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static Vector3 Probe(Vector3 p) => new Vector3(p.x, 0.02f, p.z);
    }
}
