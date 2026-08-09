using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Minimal non-sentient lunar fauna. Wanders, raises ThreatPressure when prey is near,
    /// and despawns when a claimed ClearThreat flag is worked down next to it.
    /// No full combat system — pressure + death only so personalities diverge under danger.
    /// </summary>
    public class DustStalkerAgent : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.35f;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float wanderRetargetSeconds = 4f;

        [Header("Aggro / pressure")]
        [SerializeField] private float aggroRange = 11f;
        [Tooltip("Pressure added to ThreatPressure while aggro is active (stacked via peak).")]
        [SerializeField] [Range(0f, 1f)] private float aggroPressure = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float idlePressure = 0.08f;

        [Header("Defeat (via ClearThreat flags)")]
        [SerializeField] private float defeatLinkRadius = 4.5f;
        [SerializeField] private float maxHealth = 28f;
        [SerializeField] private float damagePerSecondWhileCleared = 9f;

        [Header("Phase 2A bite")]
        [SerializeField] private float biteRange = 3.2f;
        [SerializeField] private float biteDamagePerSecond = 0.18f;

        [Header("Visual")]
        [SerializeField] private Color stalkerColor = new Color(0.45f, 0.08f, 0.1f);
        [SerializeField] private float bobAmp = 0.12f;
        [SerializeField] private float bobSpeed = 3.2f;

        private static int _nextSourceId = 1;

        private ThreatPressure _threat;
        private FlagManager _flags;
        private Vector3 _home;
        private Vector3 _wanderTarget;
        private float _wanderTimer;
        private float _health;
        private bool _aggro;
        private object _sourceId;
        private Vector3 _baseScale;
        private float _yBase;
        private TextMesh _label;
        private Renderer _rend;

        public bool IsAlive => _health > 0f;
        public bool IsAggro => _aggro;
        public float Health01 => maxHealth > 0f ? Mathf.Clamp01(_health / maxHealth) : 0f;

        public void Initialize(ThreatPressure threat, FlagManager flags, Vector3 home)
        {
            _threat = threat;
            _flags = flags;
            _home = home;
            _sourceId = _nextSourceId++;
            _health = maxHealth;
            _baseScale = transform.localScale;
            _yBase = transform.position.y;
            transform.position = home;
            PickWanderTarget();
            EnsureVisual();
            gameObject.name = "DustStalker";
        }

        private void Update()
        {
            if (!IsAlive || _threat == null) return;

            float dt = Time.deltaTime;
            TickWander(dt);
            TickAggroAndPressure();
            TickBite(dt);
            TickDefeat(dt);
            TickPresentation(dt);
        }

        private void TickWander(float dt)
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f || Vector3.Distance(Flat(transform.position), Flat(_wanderTarget)) < 0.4f)
                PickWanderTarget();

            Vector3 pos = transform.position;
            Vector3 target = _wanderTarget;
            target.y = pos.y;

            // Slow, readable crawl; slightly faster when aggro (harassing).
            float speed = _aggro ? moveSpeed * 1.25f : moveSpeed;
            transform.position = Vector3.MoveTowards(pos, target, speed * dt);
        }

        private void TickAggroAndPressure()
        {
            _aggro = false;
            var specialists = FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None);
            float aggroSq = aggroRange * aggroRange;
            Vector3 me = Flat(transform.position);

            for (int i = 0; i < specialists.Length; i++)
            {
                var s = specialists[i];
                if (s == null) continue;
                if ((Flat(s.transform.position) - me).sqrMagnitude <= aggroSq)
                {
                    _aggro = true;
                    break;
                }
            }

            // Optional: buildings also count as "prey" via Building root cubes — skip full system;
            // specialists alone are enough for the brain to feel danger.

            float pressure = _aggro ? aggroPressure : idlePressure;
            _threat.Report(_sourceId, pressure);
        }

        private void TickBite(float dt)
        {
            if (!_aggro) return;

            var specialists = FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None);
            Vector3 me = Flat(transform.position);
            SpecialistAgent nearest = null;
            float best = biteRange * biteRange;

            for (int i = 0; i < specialists.Length; i++)
            {
                var s = specialists[i];
                if (s == null || s.IsIncapacitated) continue;
                float sq = (Flat(s.transform.position) - me).sqrMagnitude;
                if (sq <= best)
                {
                    best = sq;
                    nearest = s;
                }
            }

            if (nearest == null) return;

            // Crawl toward prey while biting.
            Vector3 prey = nearest.transform.position;
            prey.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, prey, moveSpeed * 0.85f * dt);
            nearest.ApplyDamage(biteDamagePerSecond * dt);
        }

        private void TickDefeat(float dt)
        {
            if (_flags == null) return;

            // ClearThreat flags near this stalker, while claimed/worked, damage it.
            var list = _flags.Flags;
            Vector3 me = Flat(transform.position);
            float linkSq = defeatLinkRadius * defeatLinkRadius;
            bool beingCleared = false;

            for (int i = 0; i < list.Count; i++)
            {
                FlagHandle f = list[i];
                if (f == null || f.Data == null) continue;
                if (f.Data.flagType != FlagType.ClearThreat) continue;
                if ((Flat(f.WorldPosition) - me).sqrMagnitude > linkSq) continue;

                // Claimed or any remaining work in range = player posted a clear job here.
                if (f.ClaimCount > 0 || _flags.GetWorkRemaining(f) < f.Data.workRequired - 0.05f)
                {
                    beingCleared = true;
                    break;
                }
            }

            if (!beingCleared) return;

            _health -= damagePerSecondWhileCleared * dt;
            // Hurt feedback
            transform.localScale = _baseScale * (1f + Mathf.Sin(Time.time * 18f) * 0.08f);

            if (_health <= 0f)
                Die();
        }

        private void TickPresentation(float dt)
        {
            // Bob for silhouette readability on isometric view.
            Vector3 p = transform.position;
            p.y = _yBase + Mathf.Sin(Time.time * bobSpeed) * bobAmp;
            transform.position = p;

            if (_label != null)
            {
                string state = _aggro ? "AGGRO" : "wander";
                _label.text = $"Stalker\n{state}  HP {Health01:P0}";
                if (Camera.main != null)
                {
                    _label.transform.rotation = Quaternion.LookRotation(
                        _label.transform.position - Camera.main.transform.position);
                }
            }

            // Aggro = brighter red
            if (_rend != null)
            {
                Color c = _aggro
                    ? Color.Lerp(stalkerColor, new Color(1f, 0.15f, 0.1f), 0.55f)
                    : stalkerColor;
                SetColor(_rend, c);
            }
        }

        private void Die()
        {
            _threat?.Clear(_sourceId);
            DemoAudio.PlayStalkerDeath();
            DemoVfx.DeathBurst(transform.position, stalkerColor);
            Debug.Log("[Threat] Dust Stalker defeated — pressure contribution removed.");
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _threat?.Clear(_sourceId);
        }

        private void PickWanderTarget()
        {
            _wanderTimer = wanderRetargetSeconds * Random.Range(0.7f, 1.3f);
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            _wanderTarget = _home + new Vector3(r.x, 0f, r.y);
        }

        private void EnsureVisual()
        {
            _rend = GetComponentInChildren<Renderer>();
            if (_rend == null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(transform, false);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one;
                Object.Destroy(sphere.GetComponent<Collider>());
                _rend = sphere.GetComponent<Renderer>();
            }

            SetColor(_rend, stalkerColor);

            // Only flatten anonymous single-sphere hosts; multi-part placeholders keep authored scale.
            if (transform.childCount <= 1 && transform.localScale == Vector3.one)
                transform.localScale = new Vector3(1.1f, 0.55f, 1.3f);
            _baseScale = transform.localScale;

            if (_label == null)
            {
                var go = new GameObject("Label");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 1.6f;
                _label = go.AddComponent<TextMesh>();
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                _label.characterSize = 0.14f;
                _label.fontSize = 40;
                _label.fontStyle = FontStyle.Bold;
                _label.color = new Color(1f, 0.45f, 0.4f);
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static void SetColor(Renderer rend, Color c)
        {
            if (rend == null) return;
            if (rend.material.HasProperty("_Color"))
                rend.material.color = c;
            else if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", c);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, defeatLinkRadius);
        }
#endif
    }
}
