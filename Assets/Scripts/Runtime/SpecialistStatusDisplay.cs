using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Overhead state indicator: colored orb + action label for BrainDecision.Action.
    /// Purely presentational — does not affect decisions.
    /// </summary>
    public class SpecialistStatusDisplay : MonoBehaviour
    {
        [SerializeField] private float height = 2.15f;
        [SerializeField] private float orbScale = 0.35f;

        private SpecialistAgent _agent;
        private Transform _orb;
        private Renderer _orbRend;
        private TextMesh _label;
        private Vector3 _orbBaseScale;

        public void Bind(SpecialistAgent agent)
        {
            _agent = agent;
            EnsureVisuals();
            Refresh(force: true);
        }

        private void LateUpdate()
        {
            if (_agent == null) return;
            Refresh(force: false);
            Billboard();
        }

        private void EnsureVisuals()
        {
            if (_orb == null)
            {
                var orbGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orbGo.name = "StatusOrb";
                orbGo.transform.SetParent(transform, false);
                orbGo.transform.localPosition = Vector3.up * height;
                orbGo.transform.localScale = Vector3.one * orbScale;
                Object.Destroy(orbGo.GetComponent<Collider>());
                _orb = orbGo.transform;
                _orbBaseScale = _orb.localScale;
                _orbRend = orbGo.GetComponent<Renderer>();
            }

            if (_label == null)
            {
                var labelGo = new GameObject("StatusLabel");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = Vector3.up * (height + 0.45f);
                _label = labelGo.AddComponent<TextMesh>();
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                _label.characterSize = 0.12f;
                _label.fontSize = 48;
                _label.color = Color.white;
                _label.fontStyle = FontStyle.Bold;
            }
        }

        private void Refresh(bool force)
        {
            if (_agent == null || _orbRend == null || _label == null) return;

            Color c;
            string text;
            switch (_agent.CurrentAction)
            {
                case SpecialistAction.Rest:
                    c = new Color(0.45f, 0.75f, 1f); // cool blue
                    text = "REST";
                    break;
                case SpecialistAction.PursueFlag:
                    c = new Color(1f, 0.55f, 0.15f); // orange pursue
                    text = _agent.Status != null && _agent.Status.StartsWith("working")
                        ? "WORK"
                        : "PURSUE";
                    break;
                default:
                    c = new Color(0.7f, 0.7f, 0.75f); // idle gray
                    text = "IDLE";
                    break;
            }

            SetColor(_orbRend, c);
            // Short action chip only — scores stay on F8 debug HUD.
            _label.text = text;
            _label.color = c;
            _label.gameObject.SetActive(_agent.CurrentAction != SpecialistAction.Idle ||
                                        (_agent.Status != null && _agent.Status.StartsWith("down")));

            // Subtle idle pulse on the orb so state is readable at a glance.
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
            if (_agent.CurrentAction == SpecialistAction.PursueFlag &&
                _agent.Status != null && _agent.Status.StartsWith("working"))
            {
                pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.2f;
            }

            _orb.localScale = _orbBaseScale * pulse;
        }

        private void Billboard()
        {
            if (_label == null || Camera.main == null) return;
            _label.transform.rotation = Quaternion.LookRotation(
                _label.transform.position - Camera.main.transform.position);
        }

        private static void SetColor(Renderer rend, Color c)
        {
            if (rend == null) return;
            if (rend.material.HasProperty("_Color"))
                rend.material.color = c;
            else if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", c);
        }
    }
}
