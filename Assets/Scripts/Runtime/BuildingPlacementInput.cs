using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player places buildings only (Overseer). Never commands specialists.
    /// Keys 1–9 and 0 select building data when assigned; LMB commits when build tool active.
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
        private GameLoop _loop;

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

        public BuildingData[] Catalog => catalog;
        public int SelectedIndex => selectedIndex;

        public void SelectBuilding(int index) => Select(index);

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
            _loop = GetComponent<GameLoop>();
        }

        private void Update()
        {
            if (_placer == null) return;
            if (_loop != null && !_loop.IsPlaying)
            {
                if (_ghost != null) _ghost.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }

            // Hotkeys always switch selection; placement only when tool enabled.
            if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Select(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) Select(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) Select(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) Select(6);
            if (Input.GetKeyDown(KeyCode.Alpha8)) Select(7);
            if (Input.GetKeyDown(KeyCode.Alpha9)) Select(8);
            if (Input.GetKeyDown(KeyCode.Alpha0)) Select(9);

            if (!enabledPlacement || Selected == null)
            {
                if (_ghost != null) _ghost.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }

            if (!TryGround(out Vector3 world)) return;
            Vector2Int cell = _grid != null ? _grid.WorldToCell(world) : Vector2Int.zero;

            // Lego snap: airlocks → module sockets; modules → airlock ends.
            if (_placer.TrySnapDock(Selected, cell, out Vector2Int snappedCell))
                cell = snappedCell;

            Vector3 snapped = FootprintWorldCenter(cell, Selected);

            EnsureGhost();
            EnsureFootprint();
            _ghost.SetActive(true);
            _footprint.SetActive(true);
            _ghost.transform.position = snapped;
            ColonyVisualUtility.SnapToGround(_ghost);

            bool valid = _placer.CanFit(Selected, cell) &&
                         (_resources == null || _resources.CanAfford(Selected.buildCost));
            if (valid && _placer.ExtraPlacementRule != null)
                valid = _placer.ExtraPlacementRule(cell, Selected);
            ColonyVisualUtility.ApplyGhostTint(_ghost, valid);
            UpdateFootprint(cell, valid);

            // Place on release so LMB drag-pan does not also commit a building.
            if (Input.GetMouseButtonUp(0) && valid && !Input.GetMouseButton(1) &&
                (_cam == null || !_cam.SuppressWorldClick) &&
                (_loop == null || !_loop.WorldClickUsedBySelection))
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
            float cell = _grid != null ? _grid.CellSize : ColonyLayout.DefaultCellSize;
            GameObject go = ModularBuildingFactory.Spawn(
                order.Data.category,
                order.WorldPosition,
                _buildingRoot,
                order.Data.footprintWidth,
                order.Data.footprintHeight,
                cell);

            go.name = $"Bld_{order.Data.displayName}_{order.Id}";
            CampusNavMesh.AddObstacle(go);
            _loop?.NotifyBuildingPlaced(order.Data, go, order.WorldPosition);
            _loop?.NotifyCampusExpanded();
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

            if (Selected == null) return;

            float cell = _grid != null ? _grid.CellSize : ColonyLayout.DefaultCellSize;
            _ghost = ModularBuildingFactory.Spawn(
                Selected.category,
                Vector3.zero,
                null,
                Selected.footprintWidth,
                Selected.footprintHeight,
                cell,
                ghost: true);
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

        private Vector3 FootprintWorldCenter(Vector2Int origin, BuildingData data)
        {
            if (_grid == null || data == null)
                return Vector3.zero;
            Vector3 corner = _grid.CellToWorld(origin);
            float cs = _grid.CellSize;
            return corner + new Vector3(
                (data.footprintWidth - 1) * 0.5f * cs,
                0f,
                (data.footprintHeight - 1) * 0.5f * cs);
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
