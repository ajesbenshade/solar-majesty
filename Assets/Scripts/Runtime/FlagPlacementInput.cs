using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player posts / adjusts bounty flags only. Never commands specialists.
    /// F1 Explore · F2 ClearThreat · F3 Build · F4 Extract · F5 Defend · I Research Site · O Outpost · U Terraform
    /// </summary>
    public class FlagPlacementInput : MonoBehaviour
    {
        [SerializeField] private FlagData exploreFlag;
        [SerializeField] private FlagData clearThreatFlag;
        [SerializeField] private FlagData buildFlag;
        [SerializeField] private FlagData extractFlag;
        [SerializeField] private FlagData defendFlag;
        [SerializeField] private FlagData researchSiteFlag;
        [SerializeField] private FlagData outpostFlag;
        [SerializeField] private FlagData terraformFlag;
        [SerializeField] private float bounty = 50f;
        [SerializeField] private float bountyStep = 15f;
        [SerializeField] private KeyCode placeKey = KeyCode.Mouse0;
        [SerializeField] private bool enabledPlacement = true;

        private FlagManager _flags;
        private IsoGrid _grid;
        private IsometricCameraController _cam;
        private FlagData _selected;
        private Transform _markerRoot;
        private GameLoop _loop;

        public float Bounty => bounty;
        public FlagData SelectedFlag => _selected;
        public FlagData ExploreFlag => exploreFlag;
        public FlagData ClearThreatFlag => clearThreatFlag;
        public FlagData BuildFlag => buildFlag;
        public FlagData ExtractFlag => extractFlag;
        public FlagData DefendFlag => defendFlag;
        public FlagData ResearchSiteFlag => researchSiteFlag;
        public FlagData OutpostFlag => outpostFlag;
        public FlagData TerraformFlag => terraformFlag;

        public bool EnabledPlacement
        {
            get => enabledPlacement;
            set => enabledPlacement = value;
        }

        public void SelectFlag(FlagData data)
        {
            if (data == null) return;
            Select(data);
            enabledPlacement = true;
        }

        public void NudgeBounty(float delta)
        {
            if (TryRepriceHovered(delta))
                return;
            bounty += delta;
            if (_selected != null)
                bounty = Mathf.Clamp(bounty, _selected.minBounty, _selected.maxBounty);
        }

        /// <summary>Programmatic post (Phase 5E attractor) with marker + SFX.</summary>
        public FlagHandle PostFlagAt(FlagData data, Vector3 world, float bountyAmount)
        {
            if (_flags == null || data == null) return null;
            FlagData prev = _selected;
            _selected = data;
            FlagHandle handle = TryPost(data, world, bountyAmount);
            _selected = prev;
            return handle;
        }

        public bool CanAffordSelectedBounty()
        {
            if (_loop?.Economy == null) return true;
            return _loop.Economy.CanAffordBounty(bounty);
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
            Transform markerRoot = null,
            FlagData researchSite = null,
            FlagData outpost = null,
            FlagData terraform = null)
        {
            _flags = flags;
            _grid = grid;
            _cam = cam;
            exploreFlag = explore;
            clearThreatFlag = clearThreat;
            buildFlag = build;
            extractFlag = extract;
            defendFlag = defend;
            researchSiteFlag = researchSite;
            outpostFlag = outpost;
            terraformFlag = terraform;
            _selected = exploreFlag != null ? exploreFlag : clearThreatFlag;
            _markerRoot = markerRoot;
            _loop = GetComponent<GameLoop>();
            bounty = _selected != null ? _selected.defaultBounty : 50f;
        }

        private void Update()
        {
            if (_flags == null) return;
            if (_loop != null && !_loop.IsPlaying) return;

            HandleBountyKeys();

            if (!enabledPlacement) return;

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
            if (Input.GetKeyDown(KeyCode.I) && researchSiteFlag != null)
                Select(researchSiteFlag);
            if (Input.GetKeyDown(KeyCode.O) && outpostFlag != null)
                Select(outpostFlag);
            if (Input.GetKeyDown(KeyCode.U) && terraformFlag != null)
                Select(terraformFlag);

            if (_selected == null) return;
            // Place on release so a held LMB does not also post a flag.
            if (placeKey == KeyCode.Mouse0)
            {
                if (!Input.GetMouseButtonUp(0)) return;
                if (_cam != null && _cam.SuppressWorldClick) return;
                if (_loop != null && _loop.WorldClickUsedBySelection) return;
            }
            else if (!Input.GetKeyDown(placeKey))
            {
                return;
            }

            if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;

            if (!TryGround(out Vector3 world)) return;
            if (_grid != null)
                world = _grid.SnapToCellCenter(world);

            FlagHandle handle = TryPost(_selected, world, bounty);
            if (handle == null)
                Debug.Log("[Flags] Cannot post — not enough metals in the stockpile.");
        }

        private FlagHandle TryPost(FlagData data, Vector3 world, float bountyAmount)
        {
            if (data == null || _flags == null) return null;

            int escrow = 0;
            if (_loop?.Economy != null)
            {
                if (!_loop.Economy.TryEscrowBounty(bountyAmount, out escrow))
                    return null;
            }

            FlagHandle handle = _flags.Post(data, world, bountyAmount);
            handle.EscrowMetals = escrow;
            if (_loop != null)
                handle.Risk = Mathf.Clamp01(data.baseRisk + _loop.LocalThreatAt(world) * 0.5f);
            if (data.flagType == FlagType.ClearThreat && _loop?.World != null)
            {
                var lair = _loop.World.FindNearestLair(world, OverseerRules.ScoutedDenPostRange);
                if (lair != null && lair.IsScouted)
                    _flags.ScalePostedWork(handle, OverseerRules.ScoutedDenWorkMul);
            }
            SpawnMarker(handle, world);
            DemoAudio.PlayFlagPost();
            _loop?.NotifyFlagPosted(handle);
            Debug.Log($"[Flags] Posted {data.flagType} bounty=${handle.CurrentBounty:F0} escrow={escrow} MET at {world}");
            return handle;
        }

        private void HandleBountyKeys()
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                NudgeBounty(bountyStep);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                NudgeBounty(-bountyStep);
            if (_selected != null)
                bounty = Mathf.Clamp(bounty, _selected.minBounty, _selected.maxBounty);
        }

        private bool TryRepriceHovered(float delta)
        {
            if (_loop?.Economy == null || _flags == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 400f)) return false;
            var marker = hit.collider.GetComponentInParent<FlagMarker>();
            if (marker == null || marker.Handle == null || marker.Handle.Data == null) return false;

            var flag = marker.Handle;
            float next = Mathf.Clamp(flag.CurrentBounty + delta, flag.Data.minBounty, flag.Data.maxBounty);
            if (Mathf.Approximately(next, flag.CurrentBounty)) return true;
            if (!_loop.Economy.TryAdjustBountyEscrow(flag, next))
            {
                _loop.LogOverseer("Not enough MET to raise that bounty.");
                return true;
            }
            _flags.SetBounty(flag, next);
            _loop.NotifyFlagPosted(flag);
            return true;
        }

        private void Select(FlagData data)
        {
            _selected = data;
            bounty = Mathf.Clamp(bounty, data.minBounty, data.maxBounty);
            _loop?.NotifyCatalogPicked();
        }

        private void SpawnMarker(FlagHandle handle, Vector3 world)
        {
            GameObject go;
            if (_selected.prefab != null)
            {
                go = ColonyVisualUtility.InstantiateOriented(_selected.prefab, world, _markerRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(_markerRoot, true);
                go.transform.position = world + Vector3.up * 0.6f;
                go.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
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
