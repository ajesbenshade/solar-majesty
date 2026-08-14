using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Lightweight construction / combat / launch juice without custom VFX assets.
    /// Uses transparent unlit so pulses actually fade.
    /// </summary>
    public static class DemoVfx
    {
        private static Material _fxMat;

        public static void HitFlash(Transform target, Color color)
        {
            if (target == null) return;
            SpawnPulse(target.position + Vector3.up * 1.2f, Vector3.one * 0.32f, color, 0.28f, 2.4f);
        }

        public static void DeathBurst(Vector3 worldPos, Color color)
        {
            SpawnRing(worldPos, color, 0.5f, 5f);
            for (int i = 0; i < 8; i++)
            {
                float ang = (Mathf.PI * 2f * i) / 8f;
                SpawnBit(
                    worldPos + Vector3.up * 0.45f,
                    new Vector3(Mathf.Cos(ang), 1.15f, Mathf.Sin(ang)) * 3.8f,
                    color,
                    0.2f,
                    0.6f);
            }
        }

        public static void ClaimRing(Vector3 worldPos, Color color)
        {
            SpawnRing(worldPos, color, 0.5f, 5.2f);
        }

        public static void BuildComplete(Vector3 worldPos)
        {
            Color orange = new Color(0.96f, 0.42f, 0.08f);
            SpawnRing(worldPos, orange, 0.55f, 6f);
            SpawnPulse(worldPos + Vector3.up * 1.4f, Vector3.one * 0.45f, Color.white, 0.35f, 2.2f);
            for (int i = 0; i < 6; i++)
            {
                float ang = (Mathf.PI * 2f * i) / 6f;
                SpawnBit(
                    worldPos + Vector3.up * 0.8f,
                    new Vector3(Mathf.Cos(ang) * 1.4f, 2.4f, Mathf.Sin(ang) * 1.4f),
                    orange,
                    0.18f,
                    0.55f);
            }
        }

        public static void ExtractPing(Vector3 worldPos)
        {
            Color green = new Color(0.45f, 0.95f, 0.4f);
            SpawnRing(worldPos, green, 0.4f, 3.6f);
            SpawnPulse(worldPos + Vector3.up * 1.1f, Vector3.one * 0.28f, green, 0.3f, 2f);
        }

        public static void ConstructionSparks(Vector3 worldPos)
        {
            Color orange = new Color(1f, 0.62f, 0.18f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                SpawnBit(
                    worldPos + new Vector3(r.x * 0.6f, 0.4f, r.y * 0.6f),
                    new Vector3(r.x, 2.2f + Random.value, r.y) * 1.6f,
                    orange,
                    0.12f,
                    0.35f);
            }
        }

        public static void WorkSpark(Vector3 worldPos, Color color)
        {
            SpawnPulse(worldPos + Vector3.up * 0.9f, Vector3.one * 0.18f, color, 0.18f, 1.8f);
        }

        /// <summary>Orange exhaust column for staged / departing craft.</summary>
        public static void LaunchPlume(Vector3 worldPos)
        {
            Color hot = new Color(1f, 0.55f, 0.12f);
            Color white = new Color(1f, 0.88f, 0.55f);
            SpawnRing(worldPos, hot, 0.7f, 7f);
            SpawnRing(worldPos + Vector3.up * 0.15f, white, 0.45f, 4.2f);

            var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "VfxPlumeColumn";
            Object.Destroy(column.GetComponent<Collider>());
            column.transform.position = worldPos + Vector3.up * 0.4f;
            column.transform.localScale = new Vector3(0.55f, 0.2f, 0.55f);
            Paint(column, hot);
            var stretch = column.AddComponent<VfxPulse>();
            stretch.lifetime = 0.85f;
            stretch.expand = 1.15f;
            stretch.stretchY = 18f;
            Object.Destroy(column, 0.85f);

            for (int i = 0; i < 12; i++)
            {
                float ang = (Mathf.PI * 2f * i) / 12f;
                Color c = Color.Lerp(hot, white, i / 12f);
                SpawnBit(
                    worldPos + Vector3.up * 0.25f,
                    new Vector3(Mathf.Cos(ang) * 0.55f, 5.2f + i * 0.18f, Mathf.Sin(ang) * 0.55f),
                    c,
                    0.28f,
                    0.85f);
            }
        }

        private static void SpawnRing(Vector3 worldPos, Color color, float lifetime, float expand)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "VfxRing";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = worldPos + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(0.45f, 0.02f, 0.45f);
            Paint(go, color);
            var pulse = go.AddComponent<VfxPulse>();
            pulse.lifetime = lifetime;
            pulse.expand = expand;
            Object.Destroy(go, lifetime);
        }

        private static void SpawnPulse(Vector3 worldPos, Vector3 scale, Color color, float lifetime, float expand)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VfxPulse";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = worldPos;
            go.transform.localScale = scale;
            Paint(go, color);
            var pulse = go.AddComponent<VfxPulse>();
            pulse.lifetime = lifetime;
            pulse.expand = expand;
            Object.Destroy(go, lifetime);
        }

        private static void SpawnBit(Vector3 worldPos, Vector3 velocity, Color color, float size, float lifetime)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VfxBit";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * size;
            Paint(go, color);
            var bit = go.AddComponent<VfxBurstBit>();
            bit.velocity = velocity;
            bit.lifetime = lifetime;
        }

        private static void Paint(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = FxMat();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            rend.SetPropertyBlock(block);
        }

        private static Material FxMat()
        {
            if (_fxMat != null) return _fxMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Lit");
            _fxMat = new Material(shader) { name = "SM_VfxUnlit" };
            ColonyVisualUtility.ApplyTransparent(_fxMat);
            if (_fxMat.HasProperty("_BaseColor"))
                _fxMat.SetColor("_BaseColor", Color.white);
            return _fxMat;
        }
    }

    public sealed class VfxPulse : MonoBehaviour
    {
        public float lifetime = 0.3f;
        public float expand = 2f;
        public float stretchY;
        private float _t;
        private Vector3 _base;
        private Renderer _rend;
        private MaterialPropertyBlock _block;
        private Color _start;

        private void Awake()
        {
            _base = transform.localScale;
            _rend = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_rend != null)
            {
                _rend.GetPropertyBlock(_block);
                _start = _block.GetColor("_BaseColor");
                if (_start.maxColorComponent < 0.01f && _start.a < 0.01f)
                    _start = Color.white;
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float u = Mathf.Clamp01(_t / lifetime);
            Vector3 scale = _base * Mathf.Lerp(1f, expand, u);
            if (stretchY > 0.01f)
                scale.y = _base.y * Mathf.Lerp(1f, stretchY, u);
            transform.localScale = scale;
            if (stretchY > 0.01f)
                transform.position += Vector3.up * (stretchY * 0.35f * Time.deltaTime);

            if (_rend != null)
            {
                Color c = _start;
                c.a = (1f - u) * _start.a;
                _block.SetColor("_BaseColor", c);
                _block.SetColor("_Color", c);
                _rend.SetPropertyBlock(_block);
            }

            if (_t >= lifetime) Destroy(gameObject);
        }
    }

    public sealed class VfxBurstBit : MonoBehaviour
    {
        public Vector3 velocity;
        public float lifetime = 0.5f;
        private float _t;
        private Renderer _rend;
        private MaterialPropertyBlock _block;
        private Color _start;
        private Vector3 _base;

        private void Awake()
        {
            _base = transform.localScale;
            _rend = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_rend != null)
            {
                _rend.GetPropertyBlock(_block);
                _start = _block.GetColor("_BaseColor");
                if (_start.maxColorComponent < 0.01f && _start.a < 0.01f)
                    _start = Color.white;
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float u = Mathf.Clamp01(_t / lifetime);
            velocity += Vector3.down * 9f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.localScale = _base * (1f - u * 0.7f);
            if (_rend != null)
            {
                Color c = _start;
                c.a = (1f - u) * _start.a;
                _block.SetColor("_BaseColor", c);
                _block.SetColor("_Color", c);
                _rend.SetPropertyBlock(_block);
            }
            if (_t >= lifetime) Destroy(gameObject);
        }
    }
}
