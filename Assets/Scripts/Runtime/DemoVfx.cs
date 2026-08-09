using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Lightweight hit / death / claim juice without custom VFX assets.
    /// </summary>
    public static class DemoVfx
    {
        public static void HitFlash(Transform target, Color color)
        {
            if (target == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VfxHit";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = target.position + Vector3.up * 1.2f;
            go.transform.localScale = Vector3.one * 0.35f;
            SetColor(go, color);
            Object.Destroy(go, 0.25f);
            var pulse = go.AddComponent<VfxPulse>();
            pulse.lifetime = 0.25f;
            pulse.expand = 2.2f;
        }

        public static void DeathBurst(Vector3 worldPos, Color color)
        {
            for (int i = 0; i < 6; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "VfxDeathBit";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.position = worldPos + Vector3.up * 0.4f;
                go.transform.localScale = Vector3.one * 0.22f;
                SetColor(go, color);
                var bit = go.AddComponent<VfxBurstBit>();
                float ang = (Mathf.PI * 2f * i) / 6f;
                bit.velocity = new Vector3(Mathf.Cos(ang), 1.2f, Mathf.Sin(ang)) * 3.5f;
                bit.lifetime = 0.55f;
            }
        }

        public static void ClaimRing(Vector3 worldPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "VfxClaim";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = worldPos + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
            SetColor(go, color);
            var pulse = go.AddComponent<VfxPulse>();
            pulse.lifetime = 0.45f;
            pulse.expand = 4f;
            Object.Destroy(go, 0.45f);
        }

        private static void SetColor(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", c);
            else if (rend.material.HasProperty("_Color"))
                rend.material.color = c;
        }
    }

    public sealed class VfxPulse : MonoBehaviour
    {
        public float lifetime = 0.3f;
        public float expand = 2f;
        private float _t;
        private Vector3 _base;

        private void Awake() => _base = transform.localScale;

        private void Update()
        {
            _t += Time.deltaTime;
            float u = Mathf.Clamp01(_t / lifetime);
            transform.localScale = _base * Mathf.Lerp(1f, expand, u);
            var rend = GetComponent<Renderer>();
            if (rend != null && rend.material.HasProperty("_BaseColor"))
            {
                Color c = rend.material.GetColor("_BaseColor");
                c.a = 1f - u;
                rend.material.SetColor("_BaseColor", c);
            }
            if (_t >= lifetime) Destroy(gameObject);
        }
    }

    public sealed class VfxBurstBit : MonoBehaviour
    {
        public Vector3 velocity;
        public float lifetime = 0.5f;
        private float _t;

        private void Update()
        {
            _t += Time.deltaTime;
            velocity += Vector3.down * 9f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.localScale *= 0.98f;
            if (_t >= lifetime) Destroy(gameObject);
        }
    }
}
