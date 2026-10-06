using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// A building kit's own description of itself: hull bounds, real roof surfaces, doorways and
    /// vents (<see cref="KitShape"/>), recorded while the kit builds. World architecture
    /// (<see cref="PlanetArchitectureDresser"/>) fits roof gardens, shield caps and radiators to
    /// these instead of guessing a roof from renderer bounds, and ambient effects read the vents.
    /// All positions are in the building root's local space (y up from the ground, footprint centred
    /// on the origin), the space kit builders already work in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitAnchors : MonoBehaviour
    {
        public KitShape Shape;

        /// <summary>The root's anchors, created on first use. Footprint defaults to the building's size.</summary>
        public static KitAnchors Ensure(Transform root, float w = 0f, float d = 0f)
        {
            if (root == null) return null;
            var a = root.GetComponent<KitAnchors>();
            if (a == null) a = root.gameObject.AddComponent<KitAnchors>();
            if (a.Shape == null) a.Shape = new KitShape { W = w, D = d, PlinthTop = 0.12f };
            if (w > 0f) a.Shape.W = w;
            if (d > 0f) a.Shape.D = d;
            return a;
        }

        public static KitShape Of(GameObject root) =>
            root != null && root.TryGetComponent(out KitAnchors a) ? a.Shape : null;

        /// <summary>Main hull bounds (no deck, masts, antennas or beacons).</summary>
        public static void Body(Transform root, Vector3 center, Vector3 size)
        {
            var s = Ensure(root).Shape;
            s.Body = new Bounds(center, size);
            s.HasBody = true;
        }

        public static void Plinth(Transform root, float top) => Ensure(root).Shape.PlinthTop = top;

        public static void RoofFlat(Transform root, Vector3 topCenter, float sizeX, float sizeZ) =>
            Ensure(root).Shape.Roofs.Add(new KitRoof { Kind = RoofKind.Flat, Center = topCenter, Size = new Vector3(sizeX, 0f, sizeZ) });

        public static void RoofDome(Transform root, Vector3 baseCenter, float diameterX, float rise, float diameterZ) =>
            Ensure(root).Shape.Roofs.Add(new KitRoof { Kind = RoofKind.Dome, Center = baseCenter, Size = new Vector3(diameterX, rise, diameterZ) });

        /// <summary>Barrel vault; <paramref name="yaw"/> 0 = axis along Z.</summary>
        public static void RoofVault(Transform root, Vector3 springCenter, float span, float rise, float length, float yaw = 0f) =>
            Ensure(root).Shape.Roofs.Add(new KitRoof { Kind = RoofKind.Vault, Center = springCenter, Size = new Vector3(span, rise, length), Yaw = yaw });

        /// <summary>Lying cylinder; <paramref name="yaw"/> 0 = axis along Z.</summary>
        public static void RoofCylinder(Transform root, Vector3 axisCenter, float diameter, float length, float yaw = 0f) =>
            Ensure(root).Shape.Roofs.Add(new KitRoof { Kind = RoofKind.Cylinder, Center = axisCenter, Size = new Vector3(diameter, diameter, length), Yaw = yaw });

        public static void RoofSlope(Transform root, Vector3 center, float sizeX, float sizeZ, float tiltDeg, float yaw = 0f) =>
            Ensure(root).Shape.Roofs.Add(new KitRoof { Kind = RoofKind.Slope, Center = center, Size = new Vector3(sizeX, 0f, sizeZ), Tilt = tiltDeg, Yaw = yaw });

        /// <summary>A doorway: ground-level centre of the opening and its outward normal.</summary>
        public static void Door(Transform root, Vector3 groundCenter, Vector3 normal, float width = 1.2f, float height = 2.1f) =>
            Ensure(root).Shape.Doors.Add(new KitDoor { Position = groundCenter, Normal = normal.normalized, Width = width, Height = height });

        public static void Emitter(Transform root, EmitterKind kind, Vector3 position, Vector3 direction = default, float scale = 1f) =>
            Ensure(root).Shape.Emitters.Add(new KitEmitter
            {
                Kind = kind, Position = position,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up, Scale = scale
            });
    }
}
