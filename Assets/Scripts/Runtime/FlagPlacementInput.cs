using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player posts / adjusts bounty flags only. Never commands specialists.
    /// F1 Explore · F2 ClearThreat · F3 Build · F4 Extract · F5 Defend · I Research Site · O Outpost · U Terraform
    /// Runs after <see cref="GameLoop"/> so a click that selects a pole is consumed before this posts.
    /// </summary>
    [DefaultExecutionOrder(10)]
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

        private FlagHandle _posted;

        /// <summary>The pole the player clicked. +/- edits this bounty. Null when none is selected.</summary>
        public FlagHandle SelectedPosted
        {
            get
            {
                if (_posted != null && (_flags == null || !_flags.TryGet(_posted.RuntimeId, out _)))
                {
                    _posted = null;
                    ApplyMarkerSelection(null);
                }
                return _posted;
            }
        }

        public bool HasSelectedPosted => SelectedPosted != null;

        public void SelectPosted(FlagHandle handle)
        {
            if (handle == null || _flags == null || !_flags.TryGet(handle.RuntimeId, out _))
            {
                ClearPostedSelection();
                return;
            }

            bool changed = !ReferenceEquals(_posted, handle);
            _posted = handle;
            ApplyMarkerSelection(handle);
            if (!changed) return;
            string name = handle.Data != null && !string.IsNullOrEmpty(handle.Data.displayName)
                ? handle.Data.displayName
                : "Flag";
            Debug.Log($"[Flags] Selected existing {name} — no new flag, no escrow.");
        }

        public void ClearPostedSelection()
        {
            if (_posted == null) return;
            _posted = null;
            ApplyMarkerSelection(null);
        }

        /// <summary>
        /// Written orders for the next flag posted (HUD text box). Parsed once at post time into
        /// <see cref="FlagHandle.Orders"/>, then cleared so they do not leak onto later flags.
        /// </summary>
        public string PendingOrders
        {
            get => _pendingOrders;
            set
            {
                value ??= "";
                if (value == _pendingOrders) return;
                _pendingOrders = value;
                _pendingParsed = string.IsNullOrWhiteSpace(value) ? null : FlagOrdersParser.Parse(value);
            }
        }

        /// <summary>Read-back of <see cref="PendingOrders"/> ("L9+ · no scouts"), empty if not understood.</summary>
        public string PendingOrdersSummary => _pendingParsed != null ? _pendingParsed.Summary() : "";

        private string _pendingOrders = "";
        private FlagOrders _pendingParsed;
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

        /// <summary>The bounty the next post will escrow. The − / + buttons on the flag panel use this.</summary>
        public void NudgeBounty(float delta)
        {
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

        /// <summary>Palette lookup by type, for save restore.</summary>
        public FlagData FlagFor(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return exploreFlag;
                case FlagType.ClearThreat: return clearThreatFlag;
                case FlagType.Build: return buildFlag;
                case FlagType.Extract: return extractFlag;
                case FlagType.DefendArea: return defendFlag;
                case FlagType.ResearchSite: return researchSiteFlag;
                case FlagType.EstablishOutpost: return outpostFlag;
                case FlagType.Terraform: return terraformFlag;
                default: return null;
            }
        }

        /// <summary>
        /// Re-post a flag from a save with its marker. Deliberately skips escrow: the metals were
        /// already reserved before the save was written, so charging again would double-bill.
        /// Remaining work is applied by <see cref="FlagManager.RestoreProgress"/> after this returns.
        /// </summary>
        public FlagHandle RestoreFlag(
            FlagData data, Vector3 world, float bountyAmount, int escrow, FlagOrders orders = null)
        {
            if (_flags == null || data == null) return null;

            FlagData prev = _selected;
            _selected = data;
            FlagHandle handle = _flags.Post(data, world, bountyAmount);
            handle.EscrowMetals = escrow;
            if (orders != null && !string.IsNullOrWhiteSpace(orders.text))
                handle.Orders = orders; // restored verbatim — never re-parsed or re-asked
            if (_loop != null)
                handle.Risk = Mathf.Clamp01(data.baseRisk + _loop.LocalThreatAt(world) * 0.5f);
            SpawnMarker(handle, world);
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

            // +/- edits the selected pole even when the flag tool is closed. With the tool open and
            // nothing selected, it sets the bounty the next click will post. Speed is comma / period.
            if (!InputBindings.TextEntryActive)
                HandleBountyKeys();

            if (!enabledPlacement) return;

            // Typing flag orders: no flag hotkeys; mouse placement below still works.
            if (!InputBindings.TextEntryActive)
            {
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
            }

            if (_selected == null) return;
            // Place on release so a held LMB does not also post a flag.
            if (placeKey == KeyCode.Mouse0)
            {
                if (!Input.GetMouseButtonUp(0)) return;
                if (_cam != null && _cam.SuppressWorldClick) return;
                if (_loop != null && _loop.WorldClickUsedBySelection) return;
                // A click on a HUD panel (picking a flag type, pressing ±) is not a map click.
                if (_loop != null && _loop.PointerOverHud) return;
            }
            else if (InputBindings.TextEntryActive || !Input.GetKeyDown(placeKey))
            {
                return;
            }

            if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;

            if (!TryGround(out Vector3 world)) return;
            if (_grid != null)
                world = _grid.SnapToCellCenter(world);

            // Clicking a pole that is already standing selects it. A second post would escrow again.
            FlagHandle rayHit = FlagMarker.ClosestUnderRay(ViewRay());
            if (FlagClick.Resolve(_flags.Flags, world, rayHit, out FlagHandle existing) ==
                FlagClickAction.SelectExisting)
            {
                SelectPosted(existing);
                return;
            }

            FlagHandle handle = TryPost(_selected, world, bounty);
            if (handle == null)
                Debug.Log("[Flags] Cannot post — not enough metals in the stockpile.");
            else
                ClearPostedSelection();
        }

        private void AttachPendingOrders(FlagHandle handle)
        {
            if (string.IsNullOrWhiteSpace(_pendingOrders)) return;
            handle.Orders = _pendingParsed ?? FlagOrdersParser.Parse(_pendingOrders);
            string read = handle.Orders.Summary();
            _loop?.LogOverseer(string.IsNullOrEmpty(read)
                ? $"Orders posted: \"{_pendingOrders}\" — not understood; heroes will ignore them."
                : $"Orders posted: {read}");
            if (!handle.Orders.HasRules)
                LayaFlagOrders.TryUnderstand(handle, _loop); // optional local model, async
            PendingOrders = "";
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
            AttachPendingOrders(handle);
            SpawnMarker(handle, world);
            DemoAudio.PlayFlagPost(world);
            _loop?.NotifyFlagPosted(handle);
            Debug.Log($"[Flags] Posted {data.flagType} bounty=${handle.CurrentBounty:F0} escrow={escrow} EU at {world}");
            return handle;
        }

        private void HandleBountyKeys()
        {
            bool plus = Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus);
            bool minus = Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
            if (!plus && !minus) return;

            float step = Mathf.Max(bountyStep, MajestyEconomy.FlagBountyStep);
            float delta = (plus ? step : 0f) + (minus ? -step : 0f);

            if (HasSelectedPosted)
            {
                TryStepSelectedBounty(delta);
                return;
            }

            // No pole selected: +/- sets the next post only while the flag tool is open.
            if (!enabledPlacement) return;
            NudgeBounty(delta);
        }

        private void TryStepSelectedBounty(float delta)
        {
            var flag = SelectedPosted;
            if (flag == null) return;
            float before = flag.CurrentBounty;
            int escrowBefore = flag.EscrowMetals;
            if (!FlagClick.TryStepBounty(_flags, _loop != null ? _loop.Economy : null, flag, delta))
            {
                _loop?.LogOverseer("Not enough EU to raise that bounty.");
                return;
            }

            if (Mathf.Approximately(flag.CurrentBounty, before))
                return;
            _loop?.RefreshFlagInterest();
            Debug.Log(
                $"[Flags] {flag.Data.flagType} bounty ${before:F0} -> ${flag.CurrentBounty:F0} " +
                $"escrow {escrowBefore} -> {flag.EscrowMetals} EU");
        }

        private Ray ViewRay()
        {
            Camera cam = null;
            if (_cam != null)
                cam = _cam.GetComponent<Camera>();
            if (cam == null)
                cam = Camera.main;
            return cam != null ? cam.ScreenPointToRay(Input.mousePosition) : new Ray(Vector3.zero, Vector3.down);
        }

        private void ApplyMarkerSelection(FlagHandle handle)
        {
            if (_markerRoot == null) return;
            for (int i = 0; i < _markerRoot.childCount; i++)
            {
                var marker = _markerRoot.GetChild(i).GetComponent<FlagMarker>();
                if (marker == null) continue;
                marker.SetSelected(handle != null && ReferenceEquals(marker.Handle, handle));
            }
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
                return _cam.TryGetMouseGroundPoint(out world);

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
