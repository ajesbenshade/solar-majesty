using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Pure placement math shared by the ghost and the zone check.
    /// The stored origin is the footprint's min cell (what <see cref="BuildingPlacer"/> occupies).
    /// The visual centre is what the player is aiming at, so a 6×6 landing pad does not hang
    /// several metres off the cursor the way a corner-anchored footprint does.
    /// </summary>
    public static class PlacementCursor
    {
        public static Vector2Int FootprintOrigin(Vector3 cursor, Vector3 gridOrigin, float cellSize, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = cellSize > 0.01f ? cellSize : 1.5f;
            float ox = (width - 1) * 0.5f * cellSize;
            float oz = (height - 1) * 0.5f * cellSize;
            int x = Mathf.FloorToInt((cursor.x - ox - gridOrigin.x) / cellSize);
            int z = Mathf.FloorToInt((cursor.z - oz - gridOrigin.z) / cellSize);
            return new Vector2Int(x, z);
        }

        public static Vector3 Center(Vector2Int origin, int width, int height, float cellSize, Vector3 gridOrigin)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = cellSize > 0.01f ? cellSize : 1.5f;
            return gridOrigin + new Vector3(
                (origin.x + 0.5f + (width - 1) * 0.5f) * cellSize,
                0f,
                (origin.y + 0.5f + (height - 1) * 0.5f) * cellSize);
        }
    }

    /// <summary>
    /// Cursor-to-ground pick that never reports the camera position. A miss stays a miss.
    /// When a height field is supplied, the hit walks onto that surface so a hill does not
    /// leave the XZ short of the point the player is aiming at.
    /// </summary>
    public static class GroundPick
    {
        public static bool TryPlane(Vector3 origin, Vector3 dir, float planeY, out Vector3 hit)
        {
            hit = default;
            float len = dir.magnitude;
            if (len < 1e-6f) return false;
            dir /= len;
            if (Mathf.Abs(dir.y) < 1e-5f) return false;
            float t = (planeY - origin.y) / dir.y;
            if (t <= 0.05f || t > 4000f) return false;
            hit = origin + dir * t;
            hit.y = planeY;
            return true;
        }

        public static bool TryHit(Ray ray, Func<float, float, float> heightAt, out Vector3 world)
        {
            world = default;
            Vector3 origin = ray.origin;
            Vector3 dir = ray.direction;
            float len = dir.magnitude;
            if (len < 1e-6f) return false;
            dir /= len;

            if (heightAt != null && dir.y < -0.02f && TryPlane(origin, dir, 0f, out Vector3 seed))
            {
                float t = Vector3.Dot(seed - origin, dir);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = origin + dir * t;
                    float h = heightAt(p.x, p.z);
                    float next = (h - origin.y) / dir.y;
                    if (next <= 0.05f || next > 4000f) return false;
                    if (Mathf.Abs(next - t) < 0.05f)
                    {
                        world = origin + dir * next;
                        world.y = h;
                        return true;
                    }
                    t = next;
                }

                Vector3 last = origin + dir * t;
                world = last;
                world.y = heightAt(last.x, last.z);
                return true;
            }

            return TryPlane(origin, dir, 0f, out world);
        }
    }
}
