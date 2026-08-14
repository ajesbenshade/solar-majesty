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
        [SerializeField] private Color defendColor = new Color(0.85f, 0.35f, 1f);
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

        public FlagHandle Handle => _handle;

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
                    default: _baseColor = defaultColor; break;
                }
            }
        }

        private void ApplyColor(Color c)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) return;
            if (_renderer.material.HasProperty("_Color"))
                _renderer.material.color = c;
            else if (_renderer.material.HasProperty("_BaseColor"))
                _renderer.material.SetColor("_BaseColor", c);
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
            }
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
                string pay = $"$ {_handle.CurrentBounty:F0}";
                if (_handle.EscrowMetals > 0)
                    pay += $"  ·  {_handle.EscrowMetals} MET";
                _bountyLabel.text = pay;
            }

            if (_metaLabel != null)
            {
                string type = _handle.Data != null ? _handle.Data.flagType.ToString() : "?";
                float work = _manager != null ? _manager.GetWorkRemaining(_handle) : 0f;
                int claims = _handle.ClaimCount;
                string claimTxt = claims > 0 ? $"CLAIMED x{claims}" : "OPEN";
                string interest = string.IsNullOrEmpty(_handle.InterestLabel)
                    ? (claims > 0 ? claimTxt : "…")
                    : _handle.InterestLabel;
                _metaLabel.text = $"{type}  ·  {interest}\n{claimTxt}  ·  RMB cancel  ·  w {work:F1}";
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
                    if (_claimBadgeRend.material.HasProperty("_Color"))
                        _claimBadgeRend.material.color = badge;
                    else if (_claimBadgeRend.material.HasProperty("_BaseColor"))
                        _claimBadgeRend.material.SetColor("_BaseColor", badge);

                    float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.15f;
                    _claimBadge.localScale = Vector3.one * (0.28f * pulse);
                }
            }

            if (_bountyLabel != null)
                _bountyLabel.color = claimed ? claimedTint : Color.white;
        }

        private void Billboard()
        {
            if (Camera.main == null) return;
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
        }
    }
}
