using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Shape of a roof surface a kit reports, so world dressing can fit it.</summary>
    public enum RoofKind
    {
        /// <summary>Flat deck. <see cref="KitRoof.Size"/> x/z = usable extent; y ignored.</summary>
        Flat,
        /// <summary>Dome. <see cref="KitRoof.Center"/> = centre of the dome's base ring; Size = diameters (x, rise y, z).</summary>
        Dome,
        /// <summary>Barrel vault / arched hangar. Center = middle of the springing line; Size x = span, y = rise, z = length along the axis (rotated by Yaw).</summary>
        Vault,
        /// <summary>Lying cylinder (tank, lab tube). Center = axis midpoint; Size x = diameter, z = length along the axis (rotated by Yaw).</summary>
        Cylinder,
        /// <summary>Pitched or tilted surface (solar rack, canopy). Center = middle of the surface; Size x/z = extent; Tilt = degrees about local X.</summary>
        Slope
    }

    public enum EmitterKind
    {
        /// <summary>White vapour plume (vents, cooling, Europa heat stacks).</summary>
        Steam,
        /// <summary>Dark exhaust (forges, generators).</summary>
        Smoke,
        /// <summary>Welding / grinding sparks.</summary>
        Sparks,
        /// <summary>Heat shimmer or glow above a reactor or radiator.</summary>
        Heat
    }

    /// <summary>A roof surface in the building's local space (y up from the ground, footprint centred on the origin).</summary>
    public struct KitRoof
    {
        public RoofKind Kind;
        public Vector3 Center;
        public Vector3 Size;
        public float Yaw;
        public float Tilt;

        /// <summary>Highest point of this roof.</summary>
        public float Top => Kind switch
        {
            RoofKind.Flat => Center.y,
            RoofKind.Slope => Center.y + Mathf.Abs(Mathf.Sin(Tilt * Mathf.Deg2Rad)) * Size.z * 0.5f,
            RoofKind.Cylinder => Center.y + Size.x * 0.5f,
            _ => Center.y + Size.y
        };
    }

    /// <summary>A doorway: ground-level centre of the opening and its outward normal. Keep it clear.</summary>
    public struct KitDoor
    {
        public Vector3 Position;
        public Vector3 Normal;
        public float Width;
        public float Height;
    }

    /// <summary>Where a building vents or throws sparks, for ambient effects.</summary>
    public struct KitEmitter
    {
        public EmitterKind Kind;
        public Vector3 Position;
        public Vector3 Direction;
        public float Scale;
    }

    /// <summary>
    /// What a building kit looks like to anything that dresses it afterwards (world architecture,
    /// ambient effects): its footprint, the hull's bounds, its real roof surfaces, doorways and
    /// vents. Kits record this through <c>KitAnchors</c> as they build; a kit that records nothing
    /// gets <see cref="Fallback"/> (face-centre doorways, no known roof), so dressing never guesses
    /// a roof that isn't there.
    /// </summary>
    public sealed class KitShape
    {
        public BuildingCategory Category;
        /// <summary>Footprint in metres (x, z).</summary>
        public float W, D;
        /// <summary>Top of the deck or plinth the hull stands on.</summary>
        public float PlinthTop;
        /// <summary>Main hull bounds (no deck, masts, antennas or beacons), local space.</summary>
        public Bounds Body;
        /// <summary>True when <see cref="Body"/> came from the kit rather than a guess.</summary>
        public bool HasBody;
        public readonly List<KitRoof> Roofs = new List<KitRoof>();
        public readonly List<KitDoor> Doors = new List<KitDoor>();
        public readonly List<KitEmitter> Emitters = new List<KitEmitter>();

        /// <summary>Top of the main hull.</summary>
        public float BodyHeight => HasBody ? Body.max.y : Mathf.Max(0.8f, Body.max.y);

        /// <summary>
        /// Plain box stand-in: hull filling the footprint to <paramref name="h"/>, doorways at the four
        /// face centres, and no roof surfaces (a guessed roof is how floating slabs happen).
        /// </summary>
        public static KitShape Fallback(BuildingCategory category, float w, float d, float h)
        {
            var s = new KitShape { Category = category, W = w, D = d, PlinthTop = 0.12f };
            s.Body = new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(w * 0.8f, h, d * 0.8f));
            s.AddFaceCentreDoors();
            return s;
        }

        /// <summary>Plain box with a flat roof over the whole hull — for tests and offline previews.</summary>
        public static KitShape FlatBox(BuildingCategory category, float w, float d, float h)
        {
            var s = Fallback(category, w, d, h);
            s.HasBody = true;
            s.Roofs.Add(new KitRoof { Kind = RoofKind.Flat, Center = new Vector3(0f, h, 0f), Size = new Vector3(w * 0.8f, 0f, d * 0.8f) });
            return s;
        }

        /// <summary>Doorway lanes at the four face centres (the colony's walking convention).</summary>
        public void AddFaceCentreDoors(float width = 1.4f, float height = 2.1f)
        {
            Doors.Add(new KitDoor { Position = new Vector3(0f, 0f, -D * 0.5f), Normal = Vector3.back, Width = width, Height = height });
            Doors.Add(new KitDoor { Position = new Vector3(0f, 0f, D * 0.5f), Normal = Vector3.forward, Width = width, Height = height });
            Doors.Add(new KitDoor { Position = new Vector3(-W * 0.5f, 0f, 0f), Normal = Vector3.left, Width = width, Height = height });
            Doors.Add(new KitDoor { Position = new Vector3(W * 0.5f, 0f, 0f), Normal = Vector3.right, Width = width, Height = height });
        }

        /// <summary>
        /// True when a box (local centre, full size) would stand in a doorway's walking lane: the
        /// opening's width, from the ground to head height, running <paramref name="reach"/> metres
        /// out from the face and 1 m in.
        /// </summary>
        public bool BlocksDoor(Vector3 center, Vector3 size, float reach = 2.2f)
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                var door = Doors[i];
                Vector3 n = door.Normal.sqrMagnitude > 1e-4f ? door.Normal.normalized : Vector3.back;
                Vector3 r = Vector3.Cross(n, Vector3.up).normalized;
                Vector3 rel = center - door.Position;
                float along = Vector3.Dot(rel, n);
                float across = Vector3.Dot(rel, r);
                // Extent of the box projected on the lane axes (axis-aligned box, conservative).
                float ext = (Mathf.Abs(r.x) * size.x + Mathf.Abs(r.z) * size.z) * 0.5f;
                float extN = (Mathf.Abs(n.x) * size.x + Mathf.Abs(n.z) * size.z) * 0.5f;
                bool acrossHit = Mathf.Abs(across) - ext < door.Width * 0.5f;
                bool alongHit = along + extN > -1f && along - extN < reach;
                bool heightHit = center.y - size.y * 0.5f < Mathf.Max(door.Height, 1.9f) && center.y + size.y * 0.5f > 0.05f;
                if (acrossHit && alongHit && heightHit) return true;
            }
            return false;
        }

        /// <summary>First roof of a kind, or false.</summary>
        public bool TryRoof(RoofKind kind, out KitRoof roof)
        {
            for (int i = 0; i < Roofs.Count; i++)
                if (Roofs[i].Kind == kind) { roof = Roofs[i]; return true; }
            roof = default;
            return false;
        }

        /// <summary>Largest flat roof by area, or false.</summary>
        public bool TryLargestFlatRoof(out KitRoof roof)
        {
            roof = default;
            float best = 0f;
            for (int i = 0; i < Roofs.Count; i++)
            {
                if (Roofs[i].Kind != RoofKind.Flat) continue;
                float a = Roofs[i].Size.x * Roofs[i].Size.z;
                if (a > best) { best = a; roof = Roofs[i]; }
            }
            return best > 0f;
        }

        /// <summary>Copy with every position moved by <paramref name="offset"/> (building root → dressing holder).</summary>
        public KitShape Shifted(Vector3 offset)
        {
            var s = new KitShape
            {
                Category = Category, W = W, D = D, PlinthTop = PlinthTop + offset.y,
                Body = new Bounds(Body.center + offset, Body.size), HasBody = HasBody
            };
            foreach (var r in Roofs) { var c = r; c.Center += offset; s.Roofs.Add(c); }
            foreach (var d in Doors) { var c = d; c.Position += offset; s.Doors.Add(c); }
            foreach (var e in Emitters) { var c = e; c.Position += offset; s.Emitters.Add(c); }
            return s;
        }
    }
}
