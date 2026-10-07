using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Orbital tool: a ground reticle sized to the armed power, coloured by whether the treasury
    /// can pay at that spot (cost rises with distance from the nearest uplink), and LMB to fire.
    /// Lives on the GameLoop object next to the flag and build inputs.
    /// </summary>
    public sealed class OrbitalTargetingInput : MonoBehaviour
    {
        private const int Segments = 48;

        private GameLoop _loop;
        private IsometricCameraController _cam;
        private LineRenderer _ring;
        private bool _enabled;

        public bool EnabledPlacement
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value) ShowRing(false);
            }
        }

        public OrbitalPowerId? Selected { get; private set; }

        /// <summary>Cost and range multiplier at the cursor, for the HUD strip.</summary>
        public int CursorCost { get; private set; }
        public float CursorMultiplier { get; private set; } = 1f;
        public bool CursorAffordable { get; private set; }

        public void Initialize(GameLoop loop, IsometricCameraController cam)
        {
            _loop = loop;
            _cam = cam;
        }

        public void Select(OrbitalPowerId id) => Selected = id;

        public void ClearSelection()
        {
            Selected = null;
            ShowRing(false);
        }

        private void Update()
        {
            if (!_enabled || _loop == null || !_loop.IsPlaying || !Selected.HasValue)
            {
                ShowRing(false);
                return;
            }

            var id = Selected.Value;
            var tuning = _loop.Orbital?.Tuning;
            if (tuning == null || !tuning.TryGet(id, out var def) || def.target == OrbitalTarget.Colony)
            {
                ShowRing(false);
                return;
            }

            if (_loop.PointerOverHud || !TryGround(out Vector3 world))
            {
                ShowRing(false);
                return;
            }

            float uplink = _loop.UplinkDistance(world);
            CursorMultiplier = tuning.RangeMultiplier(uplink);
            CursorCost = _loop.Orbital.CostAt(id, uplink);
            CursorAffordable = _loop.Orbital.ReadyIn(id) <= 0f &&
                               (_loop.Resources == null || _loop.Resources.Get(ResourceId.Metals) >= CursorCost);
            DrawRing(world, Mathf.Max(1.5f, def.radius),
                CursorAffordable ? new Color(0.45f, 0.85f, 1f, 0.9f) : new Color(1f, 0.32f, 0.22f, 0.9f));

            if (!Input.GetMouseButtonUp(0)) return;
            if (_cam != null && _cam.SuppressWorldClick) return;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;
            _loop.TryCastOrbital(id, world);
        }

        private bool TryGround(out Vector3 world)
        {
            if (_cam != null && _cam.TryGetMouseGroundPoint(out world)) return true;
            world = default;
            var cam = Camera.main;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float d)) return false;
            world = ray.GetPoint(d);
            return true;
        }

        private void DrawRing(Vector3 center, float radius, Color color)
        {
            EnsureRing();
            ShowRing(true);
            _ring.startColor = color;
            _ring.endColor = color;
            for (int i = 0; i < Segments; i++)
            {
                float a = Mathf.PI * 2f * i / Segments;
                _ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.15f, Mathf.Sin(a) * radius));
            }
        }

        private void EnsureRing()
        {
            if (_ring != null) return;
            var go = new GameObject("OrbitalReticle");
            go.transform.SetParent(transform, false);
            _ring = go.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.positionCount = Segments;
            _ring.useWorldSpace = true;
            _ring.widthMultiplier = 0.18f;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) _ring.sharedMaterial = new Material(shader) { name = "SM_OrbitalReticle" };
        }

        private void ShowRing(bool on)
        {
            if (_ring != null && _ring.enabled != on) _ring.enabled = on;
        }
    }
}
