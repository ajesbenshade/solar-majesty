using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player places buildings only (Overseer). Never commands specialists.
    /// Keys 1–7 select building data when assigned; LMB commits when build tool active.
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
        private GameObject _footprint;

        public bool EnabledPlacement
        {
            get => enabledPlacement;
            set
            {
                enabledPlacement = value;
                if (!value)
                {
                    if (_ghost != null) _ghost.SetActive(false);
                    if (_footprint != null) _footprint.SetActive(false);
                }
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
            if (Input.GetKeyDown(KeyCode.Alpha5)) Select(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) Select(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) Select(6);

            if (!enabledPlacement || Selected == null)
            {
                if (_ghost != null) _ghost.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }

            if (!TryGround(out Vector3 world)) return;
            Vector2Int cell = _grid != null ? _grid.WorldToCell(world) : Vector2Int.zero;
            Vector3 snapped = _grid != null ? _grid.CellToWorld(cell) : world;

            EnsureGhost();
            EnsureFootprint();
            _ghost.SetActive(true);
            _footprint.SetActive(true);
            _ghost.transform.position = snapped;

            bool valid = _placer.CanFit(Selected, cell) &&
                         (_resources == null || _resources.CanAfford(Selected.buildCost));
            if (valid && _placer.ExtraPlacementRule != null)
                valid = _placer.ExtraPlacementRule(cell, Selected);
            ColonyVisualUtility.ApplyGhostTint(_ghost, valid);
            UpdateFootprint(cell, valid);

            if (Input.GetKeyDown(KeyCode.Mouse0) && valid && !Input.GetMouseButton(1))
            {
                if (_placer.TryPlace(Selected, cell, snapped, out ConstructionOrder order, out string fail))
                {
                    SpawnBuildingVisual(order);
                    SpawnConstructionSite(order);
                    DemoAudio.PlayBuildPlace();
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
            float scale = ColonyLayout.ScaleForCategory(order.Data.category);
            if (prefab != null)
            {
                go = Instantiate(prefab, order.WorldPosition, Quaternion.identity, _buildingRoot);
                go.transform.localScale = Vector3.one * scale;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(_buildingRoot, true);
                go.transform.position = order.WorldPosition + Vector3.up * (0.75f * scale);
                go.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f) * scale;
            }

            go.name = $"Bld_{order.Data.displayName}_{order.Id}";
            ColonyVisualUtility.EnsureUrpMaterials(go);
            CampusNavMesh.AddObstacle(go);
        }

        private void SpawnConstructionSite(ConstructionOrder order)
        {
            var site = new GameObject($"Site_{order.Id}");
            site.transform.SetParent(_buildingRoot, true);
            site.transform.position = order.WorldPosition + Vector3.up * 0.05f;
            var vis = site.AddComponent<ConstructionSiteVisual>();
            vis.Bind(order);
        }

        private void EnsureGhost()
        {
            if (_ghost != null)
            {
                if (Selected != null && _ghost.name == $"Ghost_{Selected.category}")
                    return;
                Destroy(_ghost);
                _ghost = null;
            }

            GameObject prefab = Selected != null
                ? (Selected.prefab != null ? Selected.prefab : BuildingVisualCatalog.LoadPrefab(Selected.category))
                : null;

            float scale = Selected != null
                ? ColonyLayout.ScaleForCategory(Selected.category)
                : ColonyLayout.ModuleScale;

            if (prefab != null)
            {
                _ghost = Instantiate(prefab);
                _ghost.name = Selected != null ? $"Ghost_{Selected.category}" : "BuildGhost";
                _ghost.transform.localScale = Vector3.one * scale;
                foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                    Destroy(col);
                ColonyVisualUtility.EnsureUrpMaterials(_ghost);
            }
            else
            {
                _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ghost.name = "BuildGhost";
                _ghost.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f) * scale;
                Destroy(_ghost.GetComponent<Collider>());
            }

            ColonyVisualUtility.ApplyGhostTint(_ghost, true);
        }

        private void EnsureFootprint()
        {
            if (_footprint != null) return;
            _footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _footprint.name = "BuildFootprint";
            Destroy(_footprint.GetComponent<Collider>());
            var rend = _footprint.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = ColonyVisualUtility.GetFootprintMaterial(true);
        }

        private void UpdateFootprint(Vector2Int cell, bool valid)
        {
            if (_footprint == null || Selected == null || _grid == null) return;

            float cellSize = _grid.CellSize;
            float w = Selected.footprintWidth * cellSize;
            float h = Selected.footprintHeight * cellSize;
            // Footprint anchored at placement cell (same origin BuildingPlacer uses).
            Vector3 origin = _grid.CellToWorld(cell);
            // CellToWorld is cell center; shift to footprint AABB center.
            float ox = (Selected.footprintWidth - 1) * 0.5f * cellSize;
            float oz = (Selected.footprintHeight - 1) * 0.5f * cellSize;
            _footprint.transform.position = new Vector3(origin.x + ox, 0.05f, origin.z + oz);
            _footprint.transform.localScale = new Vector3(w * 0.98f, 0.06f, h * 0.98f);

            var rend = _footprint.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = ColonyVisualUtility.GetFootprintMaterial(valid);
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
