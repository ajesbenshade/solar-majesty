using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// World-space progress bar for a pending ConstructionOrder.
    /// </summary>
    public class ConstructionSiteVisual : MonoBehaviour
    {
        private ConstructionOrder _order;
        private Transform _fill;

        public void Bind(ConstructionOrder order)
        {
            _order = order;
            EnsureBar();
        }

        private void EnsureBar()
        {
            if (_fill != null) return;
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = "ProgressBase";
            baseGo.transform.SetParent(transform, false);
            baseGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            baseGo.transform.localScale = new Vector3(2.2f, 0.12f, 0.35f);
            Object.Destroy(baseGo.GetComponent<Collider>());
            SetColor(baseGo, new Color(0.1f, 0.1f, 0.12f, 0.9f));

            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillGo.name = "ProgressFill";
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(-1.05f, 0.2f, 0f);
            fillGo.transform.localScale = new Vector3(0.05f, 0.18f, 0.28f);
            Object.Destroy(fillGo.GetComponent<Collider>());
            SetColor(fillGo, new Color(1f, 0.65f, 0.15f, 0.95f));
            _fill = fillGo.transform;
        }

        private void Update()
        {
            if (_order == null || _fill == null) return;

            float p = _order.RequiredSeconds > 0f
                ? Mathf.Clamp01(_order.ProgressSeconds / _order.RequiredSeconds)
                : 1f;

            float width = Mathf.Max(0.05f, 2.1f * p);
            _fill.localScale = new Vector3(width, 0.18f, 0.28f);
            _fill.localPosition = new Vector3(-1.05f + width * 0.5f, 0.2f, 0f);

            if (_order.IsComplete || (_order.Data != null && p >= 0.999f))
            {
                // Site complete — leave fill full briefly then destroy marker.
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
