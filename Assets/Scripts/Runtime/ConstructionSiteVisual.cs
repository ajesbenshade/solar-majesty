using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// World-space progress bar, yellow gantry crane, and incomplete cladding for a
    /// pending ConstructionOrder. Billboard is the bar only — the crane stays world-aligned.
    /// </summary>
    public class ConstructionSiteVisual : MonoBehaviour
    {
        private static readonly Color CraneYellow = new Color(0.95f, 0.82f, 0.12f);
        private static readonly Color CladWhite = new Color(0.82f, 0.84f, 0.86f);
        private static readonly Color Carbon = new Color(0.12f, 0.12f, 0.13f);

        private ConstructionOrder _order;
        private Transform _fill;
        private Transform _billboard;
        private GameObject _cladding;
        private float _sparkTimer;

        public void Bind(ConstructionOrder order)
        {
            _order = order;
            EnsureBillboard();
            EnsureBar();
            EnsureCrane();
            EnsureCladding();
        }

        private void EnsureBillboard()
        {
            if (_billboard != null) return;
            var go = new GameObject("ProgressBillboard");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _billboard = go.transform;
        }

        private void EnsureBar()
        {
            if (_fill != null) return;
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = "ProgressBase";
            baseGo.transform.SetParent(_billboard, false);
            baseGo.transform.localPosition = new Vector3(0f, 3.35f, 0f);
            baseGo.transform.localScale = new Vector3(2.2f, 0.12f, 0.35f);
            Object.Destroy(baseGo.GetComponent<Collider>());
            SetColor(baseGo, new Color(0.1f, 0.1f, 0.12f, 0.9f));

            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillGo.name = "ProgressFill";
            fillGo.transform.SetParent(_billboard, false);
            fillGo.transform.localPosition = new Vector3(-1.05f, 3.4f, 0f);
            fillGo.transform.localScale = new Vector3(0.05f, 0.18f, 0.28f);
            Object.Destroy(fillGo.GetComponent<Collider>());
            SetColor(fillGo, CraneYellow);
            _fill = fillGo.transform;
        }

        private void EnsureCrane()
        {
            var mast = Prim("CraneMast", PrimitiveType.Cube,
                new Vector3(1.65f, 2.2f, 1.35f), new Vector3(0.18f, 4.4f, 0.18f), CraneYellow);
            Object.Destroy(mast.GetComponent<Collider>());

            var jib = Prim("CraneJib", PrimitiveType.Cube,
                new Vector3(0.15f, 4.25f, 1.35f), new Vector3(3.2f, 0.14f, 0.18f), CraneYellow);
            Object.Destroy(jib.GetComponent<Collider>());

            var hook = Prim("CraneHook", PrimitiveType.Cube,
                new Vector3(-1.15f, 3.55f, 1.35f), new Vector3(0.12f, 1.2f, 0.12f), Carbon);
            Object.Destroy(hook.GetComponent<Collider>());

            var cab = Prim("CraneCab", PrimitiveType.Cube,
                new Vector3(1.65f, 3.55f, 1.35f), new Vector3(0.55f, 0.4f, 0.5f), Carbon);
            Object.Destroy(cab.GetComponent<Collider>());
        }

        private void EnsureCladding()
        {
            _cladding = new GameObject("IncompleteCladding");
            _cladding.transform.SetParent(transform, false);

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Clad_" + i;
                panel.transform.SetParent(_cladding.transform, false);
                panel.transform.localPosition = new Vector3(Mathf.Cos(ang) * 1.4f, 1.1f, Mathf.Sin(ang) * 1.4f);
                panel.transform.localScale = new Vector3(0.08f, 1.8f, 1.5f);
                panel.transform.localRotation = Quaternion.Euler(0f, -i * 90f, 0f);
                Object.Destroy(panel.GetComponent<Collider>());
                SetColor(panel, i % 2 == 0 ? CladWhite : Carbon);
            }
        }

        private GameObject Prim(string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            SetColor(go, color);
            return go;
        }

        private void Update()
        {
            if (_order == null || _fill == null) return;

            float p = _order.RequiredSeconds > 0f
                ? Mathf.Clamp01(_order.ProgressSeconds / _order.RequiredSeconds)
                : 1f;

            float width = Mathf.Max(0.05f, 2.1f * p);
            _fill.localScale = new Vector3(width, 0.18f, 0.28f);
            _fill.localPosition = new Vector3(-1.05f + width * 0.5f, 3.4f, 0f);

            if (_cladding != null)
                _cladding.SetActive(p < 0.92f);

            if (_billboard != null && Camera.main != null)
            {
                Vector3 fwd = Camera.main.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f)
                    _billboard.rotation = Quaternion.LookRotation(fwd);
            }

            _sparkTimer -= Time.deltaTime;
            if (_sparkTimer <= 0f)
            {
                _sparkTimer = 0.65f;
                DemoVfx.ConstructionSparks(transform.position + Vector3.up * 0.6f);
            }

            if (_order.IsComplete || (_order.Data != null && p >= 0.999f))
            {
                Destroy(gameObject, 0.4f);
                enabled = false;
            }
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
}
