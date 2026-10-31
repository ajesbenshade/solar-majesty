using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SolarMajesty
{
    /// <summary>
    /// Keeps a moving unit on the natural terrain surface.
    ///
    /// Pathing stays on the flat y = 0 NavMesh (the height field changes only what you see, not
    /// where you can go), so agents are lifted with <see cref="NavMeshAgent.baseOffset"/> and
    /// non-agent movers (fauna, fallback walking) are clamped to the ground height each frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainFollow : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private float _agentBaseOffset;
        private float _groundOffset;

        public static TerrainFollow Attach(GameObject go, float heightAboveGround = 0f)
        {
            if (go == null) return null;
            var follow = go.GetComponent<TerrainFollow>();
            if (follow == null) follow = go.AddComponent<TerrainFollow>();
            follow._groundOffset = heightAboveGround;
            return follow;
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) _agentBaseOffset = _agent.baseOffset;
        }

        private void LateUpdate()
        {
            var bake = TerrainDataBake.Current;
            if (bake == null) return;
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();

            Vector3 p = transform.position;
            float h = bake.SampleHeight(p.x, p.z);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.updatePosition)
            {
                _agent.baseOffset = _agentBaseOffset + h;
            }
            else
            {
                p.y = h + _groundOffset;
                transform.position = p;
            }
        }
    }

    /// <summary>
    /// Grades a construction pad under each placed building (flat footprint + soft apron), so
    /// structures raised outside the pre-flattened yards never float over dips or sink into rises.
    /// Requests are coalesced and applied once per frame (one mesh + collider refresh).
    /// </summary>
    public static class TerrainGrading
    {
        private const float Apron = 3f;
        private const float MaxHalfExtent = 10f;

        private static readonly List<(Vector3 center, Vector2 half)> Pending = new List<(Vector3, Vector2)>(8);
        private static Runner _runner;

        public static void LevelUnder(GameObject building)
        {
            if (building == null || TerrainDataBake.Current == null) return;
            var rends = building.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            bool any = false;
            Bounds b = default;
            foreach (var r in rends)
            {
                if (r == null || r is ParticleSystemRenderer || !r.enabled) continue;
                string n = r.name;
                if (n.Contains("SelectRing") || n.Contains("Label") || n.Contains("StatusOrb") || n.Contains("Vfx"))
                    continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            var half = new Vector2(
                Mathf.Min(MaxHalfExtent, b.extents.x + 0.4f),
                Mathf.Min(MaxHalfExtent, b.extents.z + 0.4f));
            Request(new Vector3(b.center.x, 0f, b.center.z), half);
        }

        /// <summary>Queue a graded pad. <paramref name="center"/>.y is the pad height (0 for yards).</summary>
        public static void Request(Vector3 center, Vector2 halfExtents)
        {
            if (TerrainDataBake.Current == null) return;
            Pending.Add((center, halfExtents));
            if (_runner == null)
            {
                var go = new GameObject("TerrainGradingRunner") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
        }

        // Re-cooking the ground's MeshCollider (295k triangles) on the main thread was the ~75 ms
        // hitch every time a building landed. Physics.BakeMesh cooks it on a worker; assigning the
        // mesh afterwards reuses that cooked data. While a cook runs the mesh must not change, so
        // new pads wait in Pending until it finishes (a frame or two); raycasts meanwhile hit the
        // previous collider, which only differs under the new pad.
        private static System.Threading.Tasks.Task _cook;
        private static MeshCollider _cookCollider;
        private static Mesh _cookMesh;

        /// <summary>Block until a background collider cook finishes. Call before the scene unloads.</summary>
        public static void CompletePending()
        {
            if (_cook == null) return;
            try { _cook.Wait(); }
            catch (System.Exception e) { Debug.LogWarning("[TerrainGrading] Collider cook failed: " + e.Message); }
            _cook = null;
            _cookCollider = null;
            _cookMesh = null;
        }

        private static void Tick()
        {
            if (_cook != null)
            {
                if (!_cook.IsCompleted) return;
                bool ok = !_cook.IsFaulted;
                _cook = null;
                if (ok && _cookCollider != null && _cookMesh != null)
                {
                    _cookCollider.sharedMesh = null;
                    _cookCollider.sharedMesh = _cookMesh;
                }
                _cookCollider = null;
                _cookMesh = null;
            }
            Flush();
        }

        private static void StartCook(MeshCollider col, Mesh mesh)
        {
            var id = mesh.GetEntityId();
            bool convex = col.convex;
            var options = col.cookingOptions;
            _cookCollider = col;
            _cookMesh = mesh;
            // However the scene goes away (our reload, a direct LoadScene, quitting), the ground's
            // OnDestroy waits for the worker before the mesh it is reading can be freed.
            if (col.GetComponent<CookGuard>() == null) col.gameObject.AddComponent<CookGuard>();
            _cook = System.Threading.Tasks.Task.Run(() => Physics.BakeMesh(id, convex, options));
        }

        private static void Flush()
        {
            var bake = TerrainDataBake.Current;
            if (bake == null || Pending.Count == 0)
            {
                Pending.Clear();
                return;
            }
            var ground = GameObject.Find("GroundPlane");
            var filter = ground != null ? ground.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            bool changed = false;
            foreach (var (center, half) in Pending)
            {
                if (!TerrainDataBake.LevelFootprint(bake, center, half, Apron, center.y)) continue;
                changed = true;
                if (mesh != null)
                    TerrainMeshBuilder.RefreshRegion(mesh, bake, center, half + new Vector2(Apron, Apron));
            }
            Pending.Clear();
            if (!changed || ground == null) return;
            var col = ground.GetComponent<MeshCollider>();
            if (col != null && mesh != null)
                StartCook(col, mesh);
        }

        private sealed class Runner : MonoBehaviour
        {
            private void LateUpdate() => Tick();

            private void OnApplicationQuit() => CompletePending();
        }

        /// <summary>Sits on the ground while a cook may be running; see <see cref="StartCook"/>.</summary>
        private sealed class CookGuard : MonoBehaviour
        {
            private void OnDestroy() => CompletePending();
        }
    }
}
