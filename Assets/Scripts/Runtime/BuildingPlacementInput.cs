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
            if (!InputBindings.TextEntryActive) // typing flag orders
            {
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
            }

            if (!enabledPlacement || Selected == null)
            {
                if (_ghost != null) _ghost.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }

            // Over a HUD panel (e.g. the build list) there is no map under the cursor: hide the
            // ghost, and the click that picks a building cannot also place one behind the list.
            if (_loop != null && _loop.PointerOverHud)
            {
                if (_ghost != null) _ghost.SetActive(false);
                if (_footprint != null) _footprint.SetActive(false);
                return;
            }

            if (!TryGround(out Vector3 world)) return;
            // Centre the footprint on the cursor. The cell under the cursor is the min corner,
            // which parks a 6×6 pad several metres off the pointer.
            Vector2Int cell = _grid != null
                ? PlacementCursor.FootprintOrigin(
                    world, _grid.Origin, _grid.CellSize, Selected.footprintWidth, Selected.footprintHeight)
                : Vector2Int.zero;

            Vector3 snapped = FootprintWorldCenter(cell, Selected);

            EnsureGhost();
            EnsureFootprint();
            _ghost.SetActive(true);
            _footprint.SetActive(true);
            _ghost.transform.position = snapped;
            // Gameplay stays on the flat grid (snapped.y is 0). The ghost sits on the
            // visible surface so a pad aimed at a trade ring on a hill is not buried.
            ColonyVisualUtility.SnapToGround(_ghost, TerrainDataBake.GroundHeight(snapped.x, snapped.z));

            string block = null;
            bool valid;
            if (_loop != null)
            {
                block = _loop.PlacementBlockReason(Selected, cell);
                valid = block == null;
            }
            else
            {
                valid = _placer.CanFit(Selected, cell) &&
                        (_resources == null || _resources.CanAfford(_placer.CostFor(Selected)));
                if (valid && _placer.ExtraPlacementRule != null)
                    valid = _placer.ExtraPlacementRule(cell, Selected);
                if (!valid) block = "Can't build there.";
            }
            _blockReason = valid ? null : block;
            ColonyVisualUtility.ApplyGhostTint(_ghost, valid);
            UpdateFootprint(cell, valid);

            // Place on release so a held LMB does not also commit a building.
            if (Input.GetMouseButtonUp(0) && valid && !Input.GetMouseButton(1) &&
                (_cam == null || !_cam.SuppressWorldClick) &&
                (_loop == null || !_loop.WorldClickUsedBySelection))
            {
                if (_placer.TryPlace(Selected, cell, snapped, out ConstructionOrder order, out string fail))
                {
                    GameObject built = SpawnBuildingVisual(order);
                    SpawnConstructionSite(order, built);
                    DemoAudio.PlayBuildPlace(snapped);
                    Debug.Log($"[Build] Placed {Selected.displayName} @ {cell}");

                    // One building per pick: drop the ghost and hand back the normal pointer.
                    // Shift-click keeps the tool armed to place several.
                    bool keepPlacing = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (!keepPlacing)
                    {
                        _ghost.SetActive(false);
                        _footprint.SetActive(false);
                        if (_loop != null) _loop.SetTool(OverseerTool.None);
                        else enabledPlacement = false;
                    }
                }
                else
                {
                    Debug.Log($"[Build] Failed: {fail}");
                }
            }
            else if (Input.GetMouseButtonUp(0) && !valid && _loop != null && Time.time >= _zoneHintAt)
            {
                if (!string.IsNullOrEmpty(_blockReason))
                {
                    _zoneHintAt = Time.time + 2.5f;
                    _loop.LogOverseer(_blockReason);
                }
            }
        }

        private float _zoneHintAt;
        private string _blockReason;
        private GUIStyle _refuseStyle;

        private void OnGUI()
        {
            if (!enabledPlacement || _ghost == null || !_ghost.activeInHierarchy) return;
            if (_loop != null && !_loop.IsPlaying) return;
            if (string.IsNullOrEmpty(_blockReason)) return;

            Camera cam = _cam != null ? _cam.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(_ghost.transform.position + Vector3.up * 3f);
            if (sp.z < 0f) return;

            if (_refuseStyle == null)
            {
                _refuseStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                };
                _refuseStyle.normal.textColor = new Color(1f, 0.88f, 0.45f);
            }

            var content = new GUIContent(_blockReason);
            Vector2 size = _refuseStyle.CalcSize(content);
            size.x += 18f;
            size.y += 8f;
            var rect = new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y - 6f, size.x, size.y);
            rect.x = Mathf.Clamp(rect.x, 8f, Mathf.Max(8f, Screen.width - rect.width - 8f));
            rect.y = Mathf.Clamp(rect.y, 8f, Mathf.Max(8f, Screen.height - rect.height - 8f));

            Color prev = GUI.color;
            GUI.color = new Color(0.05f, 0.04f, 0.02f, 0.86f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, content, _refuseStyle);
            GUI.color = prev;
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

        private GameObject SpawnBuildingVisual(ConstructionOrder order)
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
            return go;
        }

        private void SpawnConstructionSite(ConstructionOrder order, GameObject building)
        {
            if (order.Data != null)
            {
                float cell = _grid != null ? _grid.CellSize : ColonyLayout.DefaultCellSize;
                var pad = order.WorldPosition;
                pad.y = 0f;
                TerrainGrading.Request(pad, new Vector2(
                    order.Data.footprintWidth * cell * 0.5f + 0.4f,
                    order.Data.footprintHeight * cell * 0.5f + 0.4f));
            }
            var site = new GameObject($"Site_{order.Id}");
            site.transform.SetParent(_buildingRoot, true);
            site.transform.position = order.WorldPosition + Vector3.up * 0.05f;
            var vis = site.AddComponent<ConstructionSiteVisual>();
            vis.Bind(order, building);
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
            Vector3 mid = PlacementCursor.Center(
                cell, Selected.footprintWidth, Selected.footprintHeight, cellSize, _grid.Origin);
            float footY = TerrainDataBake.GroundHeight(mid.x, mid.z) + 0.08f;
            _footprint.transform.position = new Vector3(mid.x, footY, mid.z);
            _footprint.transform.localScale = new Vector3(w * 0.98f, 0.06f, h * 0.98f);

            var rend = _footprint.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = ColonyVisualUtility.GetFootprintMaterial(valid);
        }

        private Vector3 FootprintWorldCenter(Vector2Int origin, BuildingData data)
        {
            if (_grid == null || data == null)
                return Vector3.zero;
            return PlacementCursor.Center(
                origin, data.footprintWidth, data.footprintHeight, _grid.CellSize, _grid.Origin);
        }

        private bool TryGround(out Vector3 world)
        {
            if (_cam != null)
                return _cam.TryGetMouseGroundPoint(out world);

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
