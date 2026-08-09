using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player posts / adjusts bounty flags only. Never commands specialists.
    /// F1 Explore · F2 ClearThreat · F3 Build · F4 Extract · F5 DefendArea · LMB place · +/- bounty
    /// </summary>
    public class FlagPlacementInput : MonoBehaviour
    {
        [SerializeField] private FlagData exploreFlag;
        [SerializeField] private FlagData clearThreatFlag;
        [SerializeField] private FlagData buildFlag;
        [SerializeField] private FlagData extractFlag;
        [SerializeField] private FlagData defendFlag;
        [SerializeField] private float bounty = 50f;
        [SerializeField] private float bountyStep = 15f;
        [SerializeField] private KeyCode placeKey = KeyCode.Mouse0;
        [SerializeField] private bool enabledPlacement = true;

        private FlagManager _flags;
        private IsoGrid _grid;
        private IsometricCameraController _cam;
        private FlagData _selected;
        private Transform _markerRoot;

        public float Bounty => bounty;
        public FlagData SelectedFlag => _selected;
        public bool EnabledPlacement
        {
            get => enabledPlacement;
            set => enabledPlacement = value;
        }

        public void Initialize(
            FlagManager flags,
            IsoGrid grid,
            IsometricCameraController cam,
            FlagData explore,
            FlagData clearThreat,
            FlagData build = null,
            FlagData extract = null,
            FlagData defend = null,
            Transform markerRoot = null)
        {
            _flags = flags;
            _grid = grid;
            _cam = cam;
            exploreFlag = explore;
            clearThreatFlag = clearThreat;
            buildFlag = build;
            extractFlag = extract;
            defendFlag = defend;
            _selected = exploreFlag != null ? exploreFlag : clearThreatFlag;
            _markerRoot = markerRoot;
            bounty = _selected != null ? _selected.defaultBounty : 50f;
        }

        private void Update()
        {
            if (!enabledPlacement || _flags == null) return;

            if (Input.GetKeyDown(KeyCode.F1) && exploreFlag != null)
                Select(exploreFlag);
            if (Input.GetKeyDown(KeyCode.F2) && clearThreatFlag != null)
                Select(clearThreatFlag);
            if (Input.GetKeyDown(KeyCode.F3) && buildFlag != null)
                Select(buildFlag);
            if (Input.GetKeyDown(KeyCode.F4) && extractFlag != null)
                Select(extractFlag);
            if (Input.GetKeyDown(KeyCode.F5) && defendFlag != null)
                Select(defendFlag);

            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                bounty += bountyStep;
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                bounty -= bountyStep;

            if (_selected != null)
                bounty = Mathf.Clamp(bounty, _selected.minBounty, _selected.maxBounty);

            if (_selected == null) return;
            if (!Input.GetKeyDown(placeKey)) return;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;

            if (!TryGround(out Vector3 world)) return;
            if (_grid != null)
                world = _grid.SnapToCellCenter(world);

            FlagHandle handle = _flags.Post(_selected, world, bounty);
            SpawnMarker(handle, world);
            DemoAudio.PlayFlagPost();
            Debug.Log($"[Flags] Posted {_selected.flagType} bounty={handle.CurrentBounty:F0} at {world}");
        }

        private void Select(FlagData data)
        {
            _selected = data;
            bounty = Mathf.Clamp(bounty, data.minBounty, data.maxBounty);
        }

        private void SpawnMarker(FlagHandle handle, Vector3 world)
        {
            GameObject go;
            if (_selected.prefab != null)
            {
                go = Instantiate(_selected.prefab, world, Quaternion.identity, _markerRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(_markerRoot, true);
                go.transform.position = world + Vector3.up * 0.6f;
                go.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
                Object.Destroy(go.GetComponent<Collider>());
            }

            go.name = $"Flag_{_selected.flagType}_{handle.RuntimeId}";
            var marker = go.GetComponent<FlagMarker>();
            if (marker == null) marker = go.AddComponent<FlagMarker>();
            marker.Bind(handle, _flags);
        }

        private bool TryGround(out Vector3 world)
        {
            if (_cam != null)
            {
                _cam.TryGetMouseGroundPoint(out world);
                return true;
            }

            var main = Camera.main;
            if (main == null)
            {
                world = Vector3.zero;
                return false;
            }

            Ray ray = main.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }

            world = Vector3.zero;
            return false;
        }
    }
}
