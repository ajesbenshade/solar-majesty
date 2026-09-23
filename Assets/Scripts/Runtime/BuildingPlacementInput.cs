using System.Collections.Generic;
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
        private readonly List<int> _visible = new List<int>();

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

        /// <summary>Build-menu rows in hotkey order. The list is reused; do not hold it.</summary>
        public List<int> VisibleIndices
        {
            get
            {
                DemoSlice.CollectVisible(catalog, _visible);
                return _visible;
            }
        }

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

            // Hotkeys 1–0 pick the build menu's rows in order, in every mode.
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectVisibleSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectVisibleSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectVisibleSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectVisibleSlot(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectVisibleSlot(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectVisibleSlot(5);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SelectVisibleSlot(6);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SelectVisibleSlot(7);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SelectVisibleSlot(8);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SelectVisibleSlot(9);

            if (!enabledPlacement || Selected == null)
            {
                if (_ghost != null) _ghost.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }

            if (!TryGround(out Vector3 world)) return;
            Vector2Int cell = _grid != null ? _grid.WorldToCell(world) : Vector2Int.zero;

            // Free placement: the ghost follows the cursor cell; nothing snaps to sockets.
            Vector3 snapped = FootprintWorldCenter(cell, Selected);

            EnsureGhost();
            EnsureFootprint();
            _ghost.SetActive(true);
            _footprint.SetActive(true);
            _ghost.transform.position = snapped;
            ColonyVisualUtility.SnapToGround(_ghost);

            bool valid = _placer.CanFit(Selected, cell) &&
                         (_resources == null || _resources.CanAfford(_placer.CostFor(Selected)));
            if (valid && _placer.ExtraPlacementRule != null)
                valid = _placer.ExtraPlacementRule(cell, Selected);
            ColonyVisualUtility.ApplyGhostTint(_ghost, valid);
            UpdateFootprint(cell, valid);

            // Place on release so a held LMB does not also commit a building.
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
            _loop?.NotifyCatalogPicked();
        }

        private void SelectVisibleSlot(int slot)
        {
            DemoSlice.CollectVisible(catalog, _visible);
            if (slot < 0 || slot >= _visible.Count) return;
            Select(_visible[slot]);
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
            TerrainGrading.LevelUnder(go);
            CampusNavMesh.AddObstacle(go);
            _loop?.NotifyBuildingPlaced(order.Data, go, order.WorldPosition);
            _loop?.NotifyCampusExpanded();
        }

        private void SpawnConstructionSite(ConstructionOrder order)
        {
            if (order.Data != null)
            {
                float cell = _grid != null ? _grid.CellSize : ColonyLayout.DefaultCellSize;
                TerrainGrading.Request(order.WorldPosition, new Vector2(
                    order.Data.footprintWidth * cell * 0.5f + 0.4f,
                    order.Data.footprintHeight * cell * 0.5f + 0.4f));
            }
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
                if (Selected != null && _ghost.name == GhostName(Selected))
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
            _ghost.name = GhostName(Selected);
            RobotGuildDress.Apply(_ghost, Selected);
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

        private static string GhostName(BuildingData data)
        {
            if (data == null) return "Ghost";
            return $"Ghost_{data.category}_{data.displayName}";
        }
    }
}
