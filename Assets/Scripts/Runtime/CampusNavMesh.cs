using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 2B: runtime NavMesh bake (walkable ground only) + carved campus obstacles.
    /// </summary>
    public class CampusNavMesh : MonoBehaviour
    {
        private NavMeshSurface _surface;
        private bool _built;

        public bool IsReady => _built && _surface != null;

        public void Build(IsoGrid grid)
        {
            if (grid == null) return;

            EnsureSurface(grid);
            _surface.BuildNavMesh();
            _built = true;
            Debug.Log("[CampusNavMesh] Runtime NavMesh built (ground walkable, buildings carve).");
        }

        public static void AddObstacle(GameObject go, float minHeight = 3f)
        {
            if (go == null) return;
            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.shape = NavMeshObstacleShape.Box;

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Bounds b = rend.bounds;
                Vector3 lossy = go.transform.lossyScale;
                float sx = Mathf.Max(0.01f, Mathf.Abs(lossy.x));
                float sy = Mathf.Max(0.01f, Mathf.Abs(lossy.y));
                float sz = Mathf.Max(0.01f, Mathf.Abs(lossy.z));
                obstacle.center = go.transform.InverseTransformPoint(b.center);
                obstacle.size = new Vector3(
                    Mathf.Max(1.2f, b.size.x / sx),
                    Mathf.Max(minHeight, b.size.y / sy),
                    Mathf.Max(1.2f, b.size.z / sz));
            }
            else
            {
                obstacle.center = new Vector3(0f, minHeight * 0.5f, 0f);
                obstacle.size = new Vector3(3f, minHeight, 3f);
            }
        }

        public bool SamplePosition(Vector3 approx, out Vector3 onMesh)
        {
            if (NavMesh.SamplePosition(approx, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                onMesh = hit.position;
                return true;
            }
            onMesh = approx;
            return false;
        }

        private void EnsureSurface(IsoGrid grid)
        {
            if (_surface != null) return;

            var walkRoot = new GameObject("NavWalkable");
            walkRoot.transform.SetParent(transform, false);

            // Dedicated walkable ground (do not collect building render meshes as walkable).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "NavGround";
            ground.transform.SetParent(walkRoot.transform, false);
            float worldW = grid.WorldWidth;
            float worldH = grid.WorldHeight;
            ground.transform.position = new Vector3(worldW * 0.5f, 0.01f, worldH * 0.5f);
            ground.transform.localScale = new Vector3(worldW / 10f, 1f, worldH / 10f);
            // Invisible — visual GroundPlane already exists from GameLoop.
            var rend = ground.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            _surface = walkRoot.AddComponent<NavMeshSurface>();
            _surface.collectObjects = CollectObjects.Children;
            _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            _surface.overrideVoxelSize = true;
            _surface.voxelSize = 0.45f;
        }
    }
}
