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

        /// <summary>Store screenshots without overhead tags (they pile up when robots stand together).</summary>
        public static bool HideForCapture;

        private void LateUpdate()
        {
            if (_agent == null) return;
            bool show = !HideForCapture;
            if (_orb != null && _orb.gameObject.activeSelf != show) _orb.gameObject.SetActive(show);
            if (_label != null && _label.gameObject.activeSelf != show) _label.gameObject.SetActive(show);
            if (!show) return;
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
            if (_agent.IsIncapacitated)
            {
                c = new Color(0.95f, 0.32f, 0.26f);
                text = _agent.RecoverSecondsLeft > 0.05f
                    ? Loc.F("DOWN {0:F0}s", _agent.RecoverSecondsLeft)
                    : Loc.F("L{0} DOWN", _agent.Level);
            }
            else if (_agent.LevyCarried > 0)
            {
                c = new Color(0.98f, 0.82f, 0.22f);
                text = Loc.F("LEVY {0}", _agent.LevyCarried);
            }
            else if (_agent.DutyCarry > 0 && _agent.LastReason == "pay_duty")
            {
                c = new Color(0.98f, 0.82f, 0.22f);
                text = Loc.F("EU {0}", _agent.DutyCarry);
            }
            else if (!string.IsNullOrEmpty(_agent.RefusalChip))
            {
                c = new Color(0.96f, 0.42f, 0.08f);
                text = _agent.RefusalChip;
            }
            else
            {
                switch (_agent.CurrentAction)
            {
                case SpecialistAction.Rest:
                    c = new Color(0.45f, 0.75f, 1f);
                    text = _agent.Status != null && _agent.Status.Contains("seeking") ? Loc.S("INN") : Loc.S("REST");
                    break;
                case SpecialistAction.Flee:
                    c = new Color(0.95f, 0.32f, 0.26f);
                    text = Loc.S("FLEE");
                    break;
                case SpecialistAction.Hunt:
                    c = new Color(0.95f, 0.35f, 0.2f);
                    text = _agent.Status == "engaging" ? Loc.S("FIGHT") : Loc.S("HUNT");
                    break;
                case SpecialistAction.Repair:
                    c = new Color(0.45f, 0.85f, 1f);
                    text = Loc.S("FIX");
                    break;
                case SpecialistAction.Wander:
                    c = new Color(0.72f, 0.78f, 0.55f);
                    text = _agent.LastReason != null && _agent.LastReason.Contains("party_follow") ? Loc.S("PARTY")
                        : _agent.LastReason != null && _agent.LastReason.Contains("workshop") ? Loc.S("DUTY")
                        : _agent.LastReason != null && _agent.LastReason.Contains("patrol") ? Loc.S("PATROL")
                        : _agent.LastReason != null && _agent.LastReason.Contains("tinker") ? Loc.S("TOWN")
                        : _agent.LastReason != null && _agent.LastReason.Contains("triage") ? Loc.S("HEAL")
                        : _agent.LastReason != null && _agent.LastReason.Contains("levy_") ? Loc.S("LEVY")
                        : Loc.S("SCOUT");
                    break;
                case SpecialistAction.PursueFlag:
                    c = new Color(1f, 0.55f, 0.15f);
                    text = _agent.Status != null && _agent.Status.StartsWith("working")
                        ? Loc.S("WORK")
                        : Loc.S("PURSUE");
                    break;
                default:
                    c = new Color(0.7f, 0.7f, 0.75f);
                    text = Loc.S("IDLE");
                    break;
            }
            }

            SetColor(_orbRend, c);
            // Short action chip only — scores stay on F8 debug HUD.
            _label.text = text;
            _label.color = c;
            _label.gameObject.SetActive(true);

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

        private static void SetColor(Renderer rend, Color c) => IndustrialArtDressing.SetUrpColor(rend, c);
    }
}
