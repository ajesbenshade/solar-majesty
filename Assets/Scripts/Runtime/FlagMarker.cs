using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// World marker for a posted bounty flag.
    /// Large bounty readout + claim tint when specialists soft-claim the flag.
    /// </summary>
    public class FlagMarker : MonoBehaviour
    {
        [SerializeField] private Color exploreColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color threatColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField] private Color buildColor = new Color(1f, 0.65f, 0.15f);
        [SerializeField] private Color extractColor = new Color(0.55f, 0.9f, 0.35f);
        [SerializeField] private Color defendColor = new Color(0.95f, 0.48f, 0.18f);
        [SerializeField] private Color defaultColor = Color.yellow;
        [SerializeField] private Color claimedTint = new Color(1f, 0.9f, 0.35f);
        [SerializeField] private float bobAmp = 0.12f;
        [SerializeField] private float bobSpeed = 2.5f;

        private FlagHandle _handle;
        private FlagManager _manager;
        private Renderer _renderer;
        private Vector3 _basePos;
        private Color _baseColor;
        private TextMesh _bountyLabel;
        private TextMesh _metaLabel;
        private Transform _claimBadge;
        private Renderer _claimBadgeRend;
        private bool _selected;
        private GameObject _selectRing;

        public FlagHandle Handle => _handle;

        struct LiveLabel
        {
            public FlagMarker Marker;
            public FlagHandle Handle;
            public FlagLabelLayout.ScreenLabel Label;
        }

        static readonly List<LiveLabel> LiveLabels = new List<LiveLabel>(16);
        static int LayoutFrame = -1;
        const float BountyLocalY = 1.55f;
        const float MetaLocalY = 0.92f;
        float _labelOffsetY;
        bool _hideDetail;

        /// <summary>The flag whose floating label covers this screen point (origin bottom-left, like the mouse).</summary>
        public static FlagHandle LabelAtScreen(Vector2 screen)
        {
            FlagHandle best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < LiveLabels.Count; i++)
            {
                if (LiveLabels[i].Handle == null) continue;
                if (!FlagLabelLayout.Hits(LiveLabels[i].Label, screen)) continue;
                if (LiveLabels[i].Label.Priority < bestPriority) continue;
                bestPriority = LiveLabels[i].Label.Priority;
                best = LiveLabels[i].Handle;
            }
            return best;
        }

        void OnDisable()
        {
            for (int i = LiveLabels.Count - 1; i >= 0; i--)
            {
                if (LiveLabels[i].Marker == this)
                    LiveLabels.RemoveAt(i);
            }
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (selected)
                EnsureSelectRing();
            if (_selectRing != null)
                _selectRing.SetActive(selected);
            RefreshLabels();
        }

        /// <summary>
        /// The flag under the cursor when the closest hit belongs to a pole.
        /// A closer building or the ground hides the pole.
        /// </summary>
        public static FlagHandle ClosestUnderRay(Ray ray, float maxDistance = 500f)
        {
            var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            FlagHandle best = null;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].distance >= bestDist) continue;
                bestDist = hits[i].distance;
                var marker = hits[i].collider.GetComponentInParent<FlagMarker>();
                best = marker != null ? marker.Handle : null;
            }
            return best;
        }

        public void Bind(FlagHandle handle, FlagManager manager)
        {
            _handle = handle;
            _manager = manager;
            _basePos = new Vector3(transform.position.x, 0.6f, transform.position.z);
            transform.position = _basePos;
            CacheBaseColor();
            ApplyColor(_baseColor);
            EnsureHitCollider();
            EnsureLabels();
            EnsureClaimBadge();
            RefreshLabels();
            RefreshClaimVisual();
        }

        private void EnsureHitCollider()
        {
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].GetType() != typeof(BoxCollider))
                    Destroy(cols[i]);
            }

            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 2.2f, 1.2f);
            box.center = new Vector3(0f, 0.9f, 0f);
            box.isTrigger = false;
        }

        private void Update()
        {
            if (_handle == null || _manager == null) return;

            if (!_manager.TryGet(_handle.RuntimeId, out _))
            {
                Destroy(gameObject);
                return;
            }

            // Keep handle XZ in sync; ignore bob for AI distance.
            Vector3 flat = transform.position;
            flat.y = 0f;
            _handle.WorldPosition = flat;

            float y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmp;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);

            RefreshLabels();
            RefreshClaimVisual();
            Billboard();
        }

        public void SetBounty(float bounty)
        {
            if (_handle == null || _manager == null) return;
            _manager.SetBounty(_handle, bounty);
            RefreshLabels();
        }

        private void CacheBaseColor()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _baseColor = defaultColor;
            if (_handle?.Data == null) return;

            if (_handle.Data.bannerColor != default && _handle.Data.bannerColor.a > 0.01f)
                _baseColor = _handle.Data.bannerColor;
            else
            {
                switch (_handle.Data.flagType)
                {
                    case FlagType.ClearThreat: _baseColor = threatColor; break;
                    case FlagType.Explore: _baseColor = exploreColor; break;
                    case FlagType.Build: _baseColor = buildColor; break;
                    case FlagType.Extract: _baseColor = extractColor; break;
                    case FlagType.DefendArea: _baseColor = defendColor; break;
                    case FlagType.ResearchSite: _baseColor = exploreColor; break;
                    case FlagType.EstablishOutpost: _baseColor = extractColor; break;
                    case FlagType.Terraform: _baseColor = buildColor; break;
                    default: _baseColor = defaultColor; break;
                }
            }
        }

        private void ApplyColor(Color c)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) return;
            IndustrialArtDressing.SetUrpColor(_renderer, c);
        }

        private void EnsureLabels()
        {
            if (_bountyLabel == null)
            {
                var go = new GameObject("BountyLabel");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 1.55f;
                _bountyLabel = go.AddComponent<TextMesh>();
                _bountyLabel.anchor = TextAnchor.MiddleCenter;
                _bountyLabel.alignment = TextAlignment.Center;
                _bountyLabel.characterSize = 0.28f;
                _bountyLabel.fontSize = 64;
                _bountyLabel.fontStyle = FontStyle.Bold;
                _bountyLabel.color = Color.white;
                EnsureLabelCollider(go, new Vector3(3.6f, 0.7f, 0.4f));
            }

            if (_metaLabel == null)
            {
                var go = new GameObject("MetaLabel");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.92f;
                _metaLabel = go.AddComponent<TextMesh>();
                _metaLabel.anchor = TextAnchor.MiddleCenter;
                _metaLabel.alignment = TextAlignment.Center;
                _metaLabel.characterSize = 0.14f;
                _metaLabel.fontSize = 36;
                _metaLabel.color = new Color(0.9f, 0.9f, 0.95f);
                EnsureLabelCollider(go, new Vector3(5.4f, 1.15f, 0.4f));
            }
        }

        static void EnsureLabelCollider(GameObject go, Vector3 size)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
            box.isTrigger = false;
        }

        private void EnsureClaimBadge()
        {
            if (_claimBadge != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ClaimBadge";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.15f + Vector3.right * 0.45f;
            go.transform.localScale = Vector3.one * 0.28f;
            Object.Destroy(go.GetComponent<Collider>());
            _claimBadge = go.transform;
            _claimBadgeRend = go.GetComponent<Renderer>();
            go.SetActive(false);
        }

        private void RefreshLabels()
        {
            if (_handle == null) return;

            if (_bountyLabel != null)
            {
                _bountyLabel.text = $"$ {FlagBountySync.Amount(_handle):F0}";
            }

            if (_metaLabel != null)
            {
                string type = _handle.Data != null && !string.IsNullOrEmpty(_handle.Data.displayName)
                    ? _handle.Data.displayName
                    : (_handle.Data != null ? SpecialistFlavor.FlagShort(_handle.Data.flagType) : "?");
                float work = _manager != null ? _manager.GetWorkRemaining(_handle) : 0f;
                int claims = _handle.ClaimCount;
                string claimTxt = claims > 0 ? $"CLAIMED x{claims}" : "OPEN";
                string interest = string.IsNullOrEmpty(_handle.InterestLabel)
                    ? (claims > 0 ? claimTxt : "…")
                    : _handle.InterestLabel;
                string orders = _handle.Orders != null && _handle.Orders.HasRules
                    ? $"\nORDERS  {_handle.Orders.Summary()}"
                    : "";
                string keys = _selected ? "  ·  +/− bounty" : "";
                _metaLabel.text = $"{type}  ·  {interest}\n{claimTxt}  ·  RMB cancel{keys}  ·  w {work:F1}{orders}";
                _metaLabel.color = _handle.InterestCount > 0
                    ? new Color(0.85f, 1f, 0.55f)
                    : new Color(1f, 0.55f, 0.35f);
            }
        }

        private void RefreshClaimVisual()
        {
            if (_handle == null) return;
            bool claimed = _handle.ClaimCount > 0;

            // Tint pole warmer when claimed so players see competition without reading text.
            Color c = claimed
                ? Color.Lerp(_baseColor, claimedTint, 0.55f)
                : _baseColor;
            ApplyColor(c);

            if (_claimBadge != null)
            {
                _claimBadge.gameObject.SetActive(claimed);
                if (claimed && _claimBadgeRend != null)
                {
                    Color badge = new Color(1f, 0.85f, 0.2f);
                    IndustrialArtDressing.SetUrpColor(_claimBadgeRend, badge);

                    float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.15f;
                    _claimBadge.localScale = Vector3.one * (0.28f * pulse);
                }
            }

            if (_bountyLabel != null)
                _bountyLabel.color = _selected || claimed ? claimedTint : Color.white;
        }

        private void EnsureSelectRing()
        {
            if (_selectRing != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SelectRing";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            go.transform.localScale = new Vector3(1.7f, 0.025f, 1.7f);
            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                IndustrialArtDressing.SetUrpColor(rend, new Color(1f, 0.86f, 0.28f));
            _selectRing = go;
        }

        private void LateUpdate()
        {
            if (LayoutFrame == Time.frameCount) return;
            LayoutFrame = Time.frameCount;
            LayoutLiveLabels();
        }

        static void LayoutLiveLabels()
        {
            int n = LiveLabels.Count;
            if (n == 0) return;
            var labels = new FlagLabelLayout.ScreenLabel[n];
            for (int i = 0; i < n; i++)
                labels[i] = LiveLabels[i].Label;
            FlagLabelLayout.Resolve(labels, 6f);
            for (int i = 0; i < n; i++)
            {
                var live = LiveLabels[i];
                live.Label = labels[i];
                LiveLabels[i] = live;
                if (live.Marker != null)
                    live.Marker.ApplyLabelOffset(labels[i]);
            }
        }

        void ApplyLabelOffset(FlagLabelLayout.ScreenLabel label)
        {
            _labelOffsetY = label.OffsetY;
            _hideDetail = label.HideDetail;
            var cam = Camera.main;
            float worldPerPixel = 0.012f;
            if (cam != null && cam.orthographic && Screen.height > 1)
                worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            Vector3 worldLift = up * (label.OffsetY * worldPerPixel);
            PlaceLabel(_bountyLabel, BountyLocalY, worldLift, visible: true);
            PlaceLabel(_metaLabel, MetaLocalY, worldLift, visible: !label.HideDetail);
        }

        void PlaceLabel(TextMesh mesh, float baseY, Vector3 worldLift, bool visible)
        {
            if (mesh == null) return;
            float s = mesh.transform.localScale.y;
            if (s < 0.01f) s = 1f;
            Vector3 localLift = transform.InverseTransformVector(worldLift) / s;
            mesh.transform.localPosition = new Vector3(0f, baseY, 0f) + localLift;
            var rend = mesh.GetComponent<Renderer>();
            if (rend != null) rend.enabled = visible;
            var col = mesh.GetComponent<Collider>();
            if (col != null) col.enabled = visible;
        }

        private void Billboard()
        {
            if (Camera.main == null) return;
            // Measure the unshifted label. LateUpdate applies this frame's declutter lift.
            if (_bountyLabel != null)
                _bountyLabel.transform.localPosition = new Vector3(0f, BountyLocalY, 0f);
            if (_metaLabel != null)
                _metaLabel.transform.localPosition = new Vector3(0f, MetaLocalY, 0f);
            if (_bountyLabel != null)
            {
                _bountyLabel.transform.rotation = Quaternion.LookRotation(
                    _bountyLabel.transform.position - Camera.main.transform.position);
            }
            if (_metaLabel != null)
            {
                _metaLabel.transform.rotation = Quaternion.LookRotation(
                    _metaLabel.transform.position - Camera.main.transform.position);
            }

            RegisterScreenLabel(Camera.main);
        }

        void RegisterScreenLabel(Camera cam)
        {
            for (int i = LiveLabels.Count - 1; i >= 0; i--)
            {
                if (LiveLabels[i].Marker == this)
                    LiveLabels.RemoveAt(i);
            }
            if (cam == null || _handle == null || _bountyLabel == null) return;
            if (!TryLabelScreenRect(cam, out Vector2 center, out float width, out float height)) return;

            int priority = _selected ? 2 : (_handle.ClaimCount > 0 ? 1 : 0);
            LiveLabels.Add(new LiveLabel
            {
                Marker = this,
                Handle = _handle,
                Label = new FlagLabelLayout.ScreenLabel
                {
                    Center = center,
                    Width = width,
                    Height = height,
                    Priority = priority,
                    OffsetY = _labelOffsetY,
                    HideDetail = _hideDetail
                }
            });
        }

        bool TryLabelScreenRect(Camera cam, out Vector2 center, out float width, out float height)
        {
            center = default;
            width = 0f;
            height = 0f;
            bool any = false;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            AccumulateScreen(_bountyLabel, cam, ref any, ref minX, ref minY, ref maxX, ref maxY);
            AccumulateScreen(_metaLabel, cam, ref any, ref minX, ref minY, ref maxX, ref maxY);
            if (!any) return false;
            const float pad = 8f;
            minX -= pad;
            minY -= pad;
            maxX += pad;
            maxY += pad;
            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            width = maxX - minX;
            height = maxY - minY;
            return width > 1f && height > 1f;
        }

        static void AccumulateScreen(
            TextMesh mesh, Camera cam, ref bool any,
            ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            if (mesh == null) return;
            var rend = mesh.GetComponent<Renderer>();
            if (rend == null) return;
            Bounds b = rend.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.z < 0f) continue;
                any = true;
                if (sp.x < minX) minX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y > maxY) maxY = sp.y;
            }
        }
    }
}
