using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Minimal flat grid for the planetary sandbox. Presentation is isometric via the camera;
    /// world space remains axis-aligned XZ for simple agent movement.
    /// </summary>
    public class IsoGrid : MonoBehaviour
    {
        [SerializeField] private int width = 256;
        [SerializeField] private int height = 256;
        [SerializeField] private float cellSize = 1.5f;
        [SerializeField] private Vector3 origin = Vector3.zero;
        [SerializeField] private bool drawGizmos = false;
        [SerializeField] private Color gizmoColor = new Color(0.35f, 0.7f, 1f, 0.35f);

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public float WorldWidth => width * cellSize;
        public float WorldHeight => height * cellSize;

        /// <summary>4× cells on each axis vs the original 64 → 16× playable area.</summary>
        public void Resize(int cellsX, int cellsZ)
        {
            width = Mathf.Max(8, cellsX);
            height = Mathf.Max(8, cellsZ);
        }

        public bool InBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return origin + new Vector3(
                (cell.x + 0.5f) * cellSize,
                0f,
                (cell.y + 0.5f) * cellSize);
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = world - origin;
            int x = Mathf.FloorToInt(local.x / cellSize);
            int y = Mathf.FloorToInt(local.z / cellSize);
            return new Vector2Int(x, y);
        }

        public Vector3 SnapToCellCenter(Vector3 world)
        {
            Vector2Int cell = WorldToCell(world);
            cell.x = Mathf.Clamp(cell.x, 0, width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, height - 1);
            return CellToWorld(cell);
        }

        public Vector3 ClampToBounds(Vector3 world)
        {
            float minX = origin.x;
            float minZ = origin.z;
            float maxX = origin.x + width * cellSize;
            float maxZ = origin.z + height * cellSize;
            world.x = Mathf.Clamp(world.x, minX, maxX);
            world.z = Mathf.Clamp(world.z, minZ, maxZ);
            world.y = 0f;
            return world;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = gizmoColor;
            for (int x = 0; x <= width; x++)
            {
                Vector3 a = origin + new Vector3(x * cellSize, 0.02f, 0f);
                Vector3 b = origin + new Vector3(x * cellSize, 0.02f, height * cellSize);
                Gizmos.DrawLine(a, b);
            }
            for (int y = 0; y <= height; y++)
            {
                Vector3 a = origin + new Vector3(0f, 0.02f, y * cellSize);
                Vector3 b = origin + new Vector3(width * cellSize, 0.02f, y * cellSize);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
