using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Instanced undergrowth for a world: tens of thousands of grass tufts, flowers, pebbles,
    /// crystals and ice shards with no GameObjects. Instances are bucketed into map chunks (one
    /// draw per chunk and kind, at most <see cref="MaxPerBatch"/> instances), culled against the
    /// game camera each frame, and thinned when the camera zooms far out. Buildings clear the
    /// cover under their footprint through <see cref="ClearRect"/>.
    /// </summary>
    public sealed class GroundCover : MonoBehaviour
    {
        public const float ChunkSize = 32f;
        public const int MaxPerBatch = 1000;
        public const string MaterialPath = "ShaderKeep/SM_Keep_GroundCover";
        public const string ShaderName = "SolarMajesty/GroundCover";

        private static readonly int TintId = Shader.PropertyToID("_Tint");

        private sealed class Batch
        {
            public Mesh Mesh;
            public bool Shadows;
            public bool Fine;
            public Bounds Bounds;
            public bool HasBounds;
            public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
            public readonly List<Vector4> Tints = new List<Vector4>(64);
            public Matrix4x4[] MatrixArray;
            public MaterialPropertyBlock Props;
            public bool Dirty = true;
        }

        private readonly Dictionary<long, Batch> _open = new Dictionary<long, Batch>(512);
        private readonly List<Batch> _batches = new List<Batch>(1024);
        private readonly Plane[] _planes = new Plane[6];
        private Material _material;

        /// <summary>Instances currently planted.</summary>
        public int InstanceCount { get; private set; }
        public int BatchCount => _batches.Count;
        /// <summary>Draw calls issued last frame (after culling).</summary>
        public int LastDrawCount { get; private set; }

        public static Material LoadMaterial()
        {
            Material m = null;
            var keep = Resources.Load<Material>(MaterialPath);
            if (keep != null) m = new Material(keep);
            else
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null) m = new Material(shader);
            }
            if (m != null)
            {
                m.name = "SM_GroundCover";
                m.enableInstancing = true;
            }
            return m;
        }

        public void Clear()
        {
            _batches.Clear();
            _open.Clear();
            InstanceCount = 0;
        }

        /// <summary>Small, numerous kinds that are thinned when the camera zooms far out.</summary>
        public static bool IsFine(GroundCoverKind kind) =>
            kind == GroundCoverKind.GrassTuft || kind == GroundCoverKind.Pebbles ||
            kind == GroundCoverKind.Twigs || kind == GroundCoverKind.Rubble;

        public void Add(GroundCoverKind kind, Vector3 position, Quaternion rotation, Vector3 scale, Color tint, bool shadows)
        {
            int cx = Mathf.FloorToInt(position.x / ChunkSize);
            int cz = Mathf.FloorToInt(position.z / ChunkSize);
            long key = (((long)(cx + 4096) * 8192 + (cz + 4096)) * 64 + (int)kind) * 2 + (shadows ? 1 : 0);
            if (!_open.TryGetValue(key, out var batch) || batch.Matrices.Count >= MaxPerBatch)
            {
                batch = new Batch { Mesh = GroundCoverMeshes.Get(kind), Shadows = shadows, Fine = IsFine(kind) };
                _batches.Add(batch);
                _open[key] = batch;
            }
            batch.Matrices.Add(Matrix4x4.TRS(position, rotation, scale));
            batch.Tints.Add(new Vector4(tint.r, tint.g, tint.b, 1f));
            float reach = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * 1.3f;
            var b = new Bounds(position + Vector3.up * scale.y * 0.5f, new Vector3(reach * 2f, reach * 2f, reach * 2f));
            if (batch.HasBounds) batch.Bounds.Encapsulate(b);
            else
            {
                batch.Bounds = b;
                batch.HasBounds = true;
            }
            batch.Dirty = true;
            InstanceCount++;
        }

        /// <summary>Remove every instance whose root lies in the axis-aligned rectangle (world XZ).</summary>
        public int ClearRect(Vector3 center, float halfX, float halfZ) =>
            Remove(center, halfX, halfZ, false);

        /// <summary>Remove every instance whose root lies within <paramref name="radius"/> (world XZ).</summary>
        public int ClearDisc(Vector3 center, float radius) => Remove(center, radius, radius, true);

        private int Remove(Vector3 c, float halfX, float halfZ, bool round)
        {
            int removed = 0;
            float r2 = halfX * halfX;
            for (int bi = 0; bi < _batches.Count; bi++)
            {
                var batch = _batches[bi];
                if (!batch.HasBounds) continue;
                var bb = batch.Bounds;
                if (bb.max.x < c.x - halfX || bb.min.x > c.x + halfX || bb.max.z < c.z - halfZ || bb.min.z > c.z + halfZ)
                    continue;
                for (int i = batch.Matrices.Count - 1; i >= 0; i--)
                {
                    Vector4 p = batch.Matrices[i].GetColumn(3);
                    float dx = p.x - c.x, dz = p.z - c.z;
                    bool inside = round ? dx * dx + dz * dz <= r2 : Mathf.Abs(dx) <= halfX && Mathf.Abs(dz) <= halfZ;
                    if (!inside) continue;
                    batch.Matrices.RemoveAt(i);
                    batch.Tints.RemoveAt(i);
                    batch.Dirty = true;
                    removed++;
                }
            }
            InstanceCount -= removed;
            return removed;
        }

        private void LateUpdate()
        {
            LastDrawCount = 0;
            if (_batches.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (_material == null)
            {
                _material = LoadMaterial();
                if (_material == null) return;
            }

            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            // Zoomed far out, fine cover becomes sub-pixel noise: draw a random subset of it.
            float fineKeep = 1f;
            if (cam.orthographic)
                fineKeep = Mathf.Lerp(1f, 0.4f, Mathf.InverseLerp(24f, 52f, cam.orthographicSize));

            for (int i = 0; i < _batches.Count; i++)
            {
                var batch = _batches[i];
                int n = batch.Matrices.Count;
                if (n == 0) continue;
                var bounds = batch.Bounds;
                if (batch.Shadows) bounds.Expand(8f);
                if (!GeometryUtility.TestPlanesAABB(_planes, bounds)) continue;

                if (batch.Dirty || batch.MatrixArray == null)
                {
                    batch.MatrixArray = batch.Matrices.ToArray();
                    batch.Props = new MaterialPropertyBlock();
                    batch.Props.SetVectorArray(TintId, batch.Tints);
                    batch.Dirty = false;
                }

                int count = batch.Fine ? Mathf.Max(1, Mathf.RoundToInt(n * fineKeep)) : n;
                var rp = new RenderParams(_material)
                {
                    camera = cam,
                    layer = gameObject.layer,
                    worldBounds = batch.Bounds,
                    shadowCastingMode = batch.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    matProps = batch.Props
                };
                Graphics.RenderMeshInstanced(rp, batch.Mesh, 0, batch.MatrixArray, count);
                LastDrawCount++;
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
