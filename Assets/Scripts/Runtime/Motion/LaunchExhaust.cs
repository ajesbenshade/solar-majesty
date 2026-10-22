using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// The Colony Lifter's engines during the launch camera beat: a flickering flame under the bells,
    /// an orange glow on the pad, a ground cloud at ignition and a smoke trail up the climb. Everything
    /// runs on the beat's unscaled clock, so it looks the same at every game speed, and is torn down in
    /// one call when the beat ends. Sizes come from <see cref="CameraBeatTuning"/> in lifter heights.
    /// </summary>
    public sealed class LaunchExhaust
    {
        private struct Puff
        {
            public Transform T;
            public Renderer R;
            public Vector3 Velocity;
            public float Age, Life, Size0, Size1, Squash;
            public Color Hot, Smoke;
            public bool Live;
        }

        private static readonly Color Hot = new Color(1f, 0.62f, 0.2f, 0.95f);
        private static readonly Color White = new Color(1f, 0.93f, 0.75f, 0.95f);
        private static readonly Color WarmGrey = new Color(0.82f, 0.76f, 0.68f, CameraBeatTuning.SmokeAlpha);
        // Flame, outside in: a soft orange sheath, a yellow body, a pale hot core.
        private static readonly Color FlameOuter = new Color(1f, 0.45f, 0.1f, 0.6f);
        private static readonly Color FlameMid = new Color(1f, 0.72f, 0.25f, 0.8f);
        private static readonly Color FlameCore = new Color(1f, 0.92f, 0.6f, 0.95f);

        private readonly Transform _lifter;
        private readonly float _h;
        private readonly float _baseOffset;
        private readonly Puff[] _puffs = new Puff[CameraBeatTuning.MaxPuffs];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private GameObject _root;
        private Transform _flame;
        private Light _glow;
        private Material _mat;
        private int _next;
        private float _groundDebt;
        private Vector3 _lastNozzle;
        private bool _hasLast;
        private readonly Color _dust;
        private readonly Color _smoke;

        /// <param name="lifter">The craft; the flame and glow ride under it.</param>
        /// <param name="height">Its height in metres.</param>
        /// <param name="baseOffset">World offset from its pivot to the bottom of its bells.</param>
        /// <param name="ground">The world's ground colour: the pad cloud is its dust, not white smoke.</param>
        public LaunchExhaust(Transform lifter, float height, float baseOffset, Color ground)
        {
            _lifter = lifter;
            _h = Mathf.Max(0.5f, height);
            _baseOffset = baseOffset;
            Color d = Color.Lerp(ground, WarmGrey, 0.6f);
            d.a = CameraBeatTuning.SmokeAlpha;
            _dust = d;
            Color k = Color.Lerp(WarmGrey, ground, 0.2f);
            k.a = CameraBeatTuning.SmokeAlpha;
            _smoke = k;
        }

        private Vector3 Nozzle => _lifter.position + Vector3.up * _baseOffset;

        /// <summary>Light the engines.</summary>
        public void Ignite()
        {
            if (_root != null || _lifter == null) return;
            _root = new GameObject("BeatExhaust");
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _mat = new Material(shader) { name = "SM_BeatExhaust" };
                ColonyVisualUtility.ApplyTransparent(_mat);
            }

            var flame = new GameObject("BeatFlame").transform;
            flame.SetParent(_root.transform, false);
            Part(flame, CameraBeatTuning.FlameWidth, CameraBeatTuning.FlameLength, FlameOuter, 1);
            Part(flame, CameraBeatTuning.FlameWidth * 0.66f, CameraBeatTuning.FlameLength * 0.75f, FlameMid, 2);
            Part(flame, CameraBeatTuning.FlameWidth * 0.34f, CameraBeatTuning.FlameLength * 0.45f, FlameCore, 3);
            _flame = flame;

            var glow = new GameObject("BeatGlow");
            glow.transform.SetParent(_root.transform, false);
            _glow = glow.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = new Color(1f, 0.55f, 0.2f);
            _glow.range = CameraBeatTuning.GlowRange * _h;
            _glow.intensity = 0f;
            _glow.shadows = LightShadows.None;
            _lastNozzle = Nozzle;
            _hasLast = true;
        }

        /// <summary>
        /// Advance by unscaled seconds. <paramref name="thrust"/> 0..1 sizes the flame;
        /// <paramref name="groundCloud"/> keeps the pad cloud billowing.
        /// </summary>
        public void Tick(float dt, float thrust, bool groundCloud, float padY)
        {
            if (_root == null || _lifter == null) return;
            Vector3 nozzle = Nozzle;
            float flicker = Mathf.PerlinNoise(Time.unscaledTime * 17f, 0.37f);

            if (_flame != null)
            {
                _flame.position = nozzle;
                float len = Mathf.Lerp(0.35f, 1f, thrust) * (0.85f + flicker * 0.4f);
                _flame.localScale = new Vector3(0.9f + flicker * 0.2f, len, 0.9f + flicker * 0.2f);
            }
            if (_glow != null)
            {
                _glow.transform.position = nozzle + Vector3.down * (0.15f * _h);
                _glow.intensity = CameraBeatTuning.GlowIntensity * thrust * (0.75f + flicker * 0.5f);
            }

            if (groundCloud && dt > 0f)
            {
                _groundDebt += dt * CameraBeatTuning.GroundPuffRate;
                Vector3 pad = new Vector3(nozzle.x, padY + 0.08f * _h, nozzle.z);
                while (_groundDebt >= 1f)
                {
                    _groundDebt -= 1f;
                    Vector2 r = Random.insideUnitCircle.normalized;
                    var dir = new Vector3(r.x, 0.12f, r.y);
                    Emit(pad + dir * (0.25f * _h), dir * (CameraBeatTuning.GroundPuffSpeed * _h * Random.Range(0.6f, 1.2f)),
                        CameraBeatTuning.GroundPuffStartSize, CameraBeatTuning.GroundPuffEndSize,
                        CameraBeatTuning.PuffLife * 1.2f, Hot, _dust, CameraBeatTuning.GroundPuffSquash);
                }
            }

            // Lay the trail along the path climbed since last frame, so it reads as one column.
            if (_hasLast && thrust > 0f)
            {
                float step = CameraBeatTuning.PuffSpacing * _h;
                float climbed = Vector3.Distance(_lastNozzle, nozzle);
                if (climbed >= step)
                {
                    int n = Mathf.Min(24, Mathf.FloorToInt(climbed / step));
                    for (int i = 1; i <= n; i++)
                    {
                        Vector3 at = Vector3.Lerp(_lastNozzle, nozzle, i / (float)n) + Vector3.down * (0.12f * _h);
                        Vector2 j = Random.insideUnitCircle * 0.12f;
                        Emit(at, new Vector3(j.x, -0.35f, j.y) * _h, CameraBeatTuning.PuffStartSize,
                            CameraBeatTuning.PuffEndSize, CameraBeatTuning.PuffLife, White, _smoke, 1f);
                    }
                    _lastNozzle = nozzle;
                }
            }

            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffs[i].Live) continue;
                ref Puff p = ref _puffs[i];
                p.Age += dt;
                float u = p.Age / p.Life;
                if (u >= 1f)
                {
                    p.Live = false;
                    p.T.gameObject.SetActive(false);
                    continue;
                }
                p.Velocity *= Mathf.Exp(-1.6f * dt); // smoke loses its push
                p.T.position += p.Velocity * dt;
                float size = Mathf.Lerp(p.Size0, p.Size1, 1f - (1f - u) * (1f - u)) * _h;
                p.T.localScale = new Vector3(size, size * p.Squash, size);
                Color c = Color.Lerp(p.Hot, p.Smoke, Mathf.Clamp01(u * 5f));
                c.a = p.Smoke.a * Mathf.Pow(1f - u, 1.4f) + (u < 0.2f ? (p.Hot.a - p.Smoke.a) * (1f - u * 5f) : 0f);
                _block.SetColor("_BaseColor", c);
                _block.SetColor("_Color", c);
                p.R.SetPropertyBlock(_block);
            }
        }

        private void Emit(Vector3 at, Vector3 velocity, float size0, float size1, float life, Color hot, Color smoke,
            float squash)
        {
            int i = _next;
            _next = (_next + 1) % _puffs.Length;
            ref Puff p = ref _puffs[i];
            if (p.T == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "BeatPuff";
                ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
                go.transform.SetParent(_root.transform, false);
                p.T = go.transform;
                p.R = go.GetComponent<Renderer>();
                if (_mat != null) p.R.sharedMaterial = _mat;
                p.R.shadowCastingMode = ShadowCastingMode.Off;
                p.R.receiveShadows = false;
            }
            p.T.gameObject.SetActive(true);
            p.T.position = at;
            p.T.localScale = new Vector3(size0, size0 * squash, size0) * _h;
            p.Squash = squash;
            p.Velocity = velocity;
            p.Age = 0f;
            p.Life = life * Random.Range(0.8f, 1.2f);
            p.Size0 = size0;
            p.Size1 = size1 * Random.Range(0.8f, 1.25f);
            p.Hot = hot;
            p.Smoke = smoke;
            p.Live = true;
        }

        private void Part(Transform parent, float width, float length, Color c, int order)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "BeatFlamePart";
            ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            // A capsule is 2 units tall: hang it below the bells.
            go.transform.localScale = new Vector3(width * _h, length * _h * 0.5f, width * _h);
            go.transform.localPosition = Vector3.down * (length * _h * 0.5f);
            var r = go.GetComponent<Renderer>();
            if (_mat != null) r.sharedMaterial = _mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = order; // outside in: the core draws last, over its sheath
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_Color", c);
            r.SetPropertyBlock(_block);
        }

        /// <summary>Remove everything the engines made.</summary>
        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            if (_mat != null) Object.Destroy(_mat);
            _root = null;
            _mat = null;
            _flame = null;
            _glow = null;
            for (int i = 0; i < _puffs.Length; i++) _puffs[i] = default;
        }
    }
}
