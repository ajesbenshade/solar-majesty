using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player places buildings only (Overseer). Never commands specialists.
    /// Keys 1–4 select building data when assigned; LMB commits when build tool active.
    /// </summary>
    public class BuildingPlacementInput : MonoBehaviour
    {
        [SerializeField] private BuildingData[] catalog;
        [SerializeField] private int selectedIndex;
        [SerializeField] private bool enabledPlacement;

        private BuildingPlacer _placer;
        private ResourceManager _resources;
        private IsoGrid _grid;
        private IsometricCameraController _cam;
        private Transform _buildingRoot;
        private GameObject _ghost;
        private Renderer _ghostRend;

        public bool EnabledPlacement
        {
            get => enabledPlacement;
            set
            {
                enabledPlacement = value;
                if (!value && _ghost != null)
                    _ghost.SetActive(false);
            }
        }

        public BuildingData Selected =>
            catalog != null && selectedIndex >= 0 && selectedIndex < catalog.Length
                ? catalog[selectedIndex]
                : null;

        public void Initialize(
            BuildingPlacer placer,
            ResourceManager resources,
            IsoGrid grid,
            IsometricCameraController cam,
            BuildingData[] buildings,
            Transform buildingRoot = null)
        {
            _placer = placer;
            _resources = resources;
            _grid = grid;
            _cam = cam;
            catalog = buildings;
            _buildingRoot = buildingRoot;
        }

        private void Update()
        {
            if (_placer == null) return;

            // Hotkeys always switch selection; placement only when tool enabled.
            if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Select(3);

            if (!enabledPlacement || Selected == null)
            {
                if (_ghost != null) _ghost.SetActive(false);
                return;
            }

            if (!TryGround(out Vector3 world)) return;
            Vector2Int cell = _grid != null ? _grid.WorldToCell(world) : Vector2Int.zero;
            Vector3 snapped = _grid != null ? _grid.CellToWorld(cell) : world;

            EnsureGhost();
            _ghost.SetActive(true);
            // Mesh pivots are ground-based; no vertical fudge needed.
            _ghost.transform.position = snapped;

            bool valid = _placer.CanFit(Selected, cell) &&
                         (_resources == null || _resources.CanAfford(Selected.buildCost));
            Color c = valid ? new Color(0.3f, 1f, 0.4f, 0.45f) : new Color(1f, 0.25f, 0.2f, 0.45f);
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", c);
                else if (r.material.HasProperty("_Color"))
                    r.material.color = c;
            }

            if (Input.GetKeyDown(KeyCode.Mouse0) && valid && !Input.GetMouseButton(1))
            {
                if (_placer.TryPlace(Selected, cell, snapped, out ConstructionOrder order, out string fail))
                {
                    SpawnBuildingVisual(order);
                    Debug.Log($"[Build] Placed {Selected.displayName} @ {cell}");
                }
                else
                {
                    Debug.Log($"[Build] Failed: {fail}");
                }
            }
        }

        private void Select(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Length || catalog[index] == null)
                return;
            selectedIndex = index;
            enabledPlacement = true;
        }

        private void SpawnBuildingVisual(ConstructionOrder order)
        {
            GameObject prefab = order.Data.prefab != null
                ? order.Data.prefab
                : BuildingVisualCatalog.LoadPrefab(order.Data.category);

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, order.WorldPosition, Quaternion.identity, _buildingRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(_buildingRoot, true);
                go.transform.position = order.WorldPosition + Vector3.up * 0.75f;
                go.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f);
            }

            go.name = $"Bld_{order.Data.displayName}_{order.Id}";
        }

        private void EnsureGhost()
        {
            // Rebuild ghost when selection changes to match mesh kit.
            if (_ghost != null)
            {
                if (Selected != null && _ghost.name == $"Ghost_{Selected.category}")
                    return;
                Object.Destroy(_ghost);
                _ghost = null;
                _ghostRend = null;
            }

            GameObject prefab = Selected != null
                ? (Selected.prefab != null ? Selected.prefab : BuildingVisualCatalog.LoadPrefab(Selected.category))
                : null;

            if (prefab != null)
            {
                _ghost = Instantiate(prefab);
                _ghost.name = Selected != null ? $"Ghost_{Selected.category}" : "BuildGhost";
                foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                    Object.Destroy(col);
            }
            else
            {
                _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ghost.name = "BuildGhost";
                _ghost.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f);
                Object.Destroy(_ghost.GetComponent<Collider>());
            }

            _ghostRend = _ghost.GetComponentInChildren<Renderer>();
            // Soft ghost tint
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                var c = new Color(0.4f, 1f, 0.5f, 0.4f);
                if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", c);
                else if (r.material.HasProperty("_Color"))
                    r.material.color = c;
            }
        }

        private bool TryGround(out Vector3 world)
        {
            if (_cam != null)
            {
                _cam.TryGetMouseGroundPoint(out world);
                return true;
            }

            world = Vector3.zero;
            var main = Camera.main;
            if (main == null) return false;
            Ray ray = main.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
            return true;
        }
    }
}
