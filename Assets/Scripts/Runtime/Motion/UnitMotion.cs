using UnityEngine;

namespace SolarMajesty
{
    public enum LocomotionKind
    {
        /// <summary>Legged walker: gait bounce, heel-strike dip, weight shift.</summary>
        Walker = 0,
        /// <summary>Hovering drone: floats, banks hard, no ground contact.</summary>
        Hover = 1,
        /// <summary>Tracked or wheeled: no bounce, pitches under acceleration, treads scroll.</summary>
        Tracked = 2,
        /// <summary>Many-legged fauna: faster, looser gait with a side-to-side scuttle.</summary>
        Scuttle = 3,
        /// <summary>Ash hopper: ballistic hops, not a walk bob.</summary>
        Hop = 4
    }

    /// <summary>
    /// Velocity-driven procedural locomotion.
    ///
    /// The units are robots and machines, so believable motion does not need skeletal animation —
    /// it needs weight. This reads the NavMeshAgent's velocity and drives bob, lean, bank, and
    /// squash on a dedicated motion root, which is why an unrigged Blender export can still read
    /// as walking, hovering, or driving without a single authored animation clip.
    ///
    /// Gait phase advances with distance travelled rather than time, so steps stay locked to the
    /// ground at any speed (including 2x and 3x game speed) instead of sliding.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitMotion : MonoBehaviour
    {
        private const string MotionRootName = "MotionRoot";

        [SerializeField] private LocomotionKind kind = LocomotionKind.Walker;
        [SerializeField] private float bodyHeight = 1f;

        private Transform _root;
        private Vector3 _rootBaseLocalPos;
        private Vector3 _rootBaseScale;

        private Vector3 _lastPosition;
        private float _gaitPhase;
        private float _speed;
        private float _speedSmoothed;
        private float _turnRate;
        private float _lastYaw;

        // Spring-damper state for the lean, so direction changes overshoot and settle.
        private Vector2 _lean;
        private Vector2 _leanVelocity;

        private float _idleSeed;
        private Renderer[] _treadRenderers;
        private float _treadOffset;
        private bool _suspended;

        public LocomotionKind Kind => kind;

        /// <summary>Freeze motion (downed robots should not keep jauntily bobbing).</summary>
        public void SetSuspended(bool suspended) => _suspended = suspended;

        /// <summary>
        /// Attach and adopt the current visual children. Call immediately after the mesh is
        /// spawned and before rings, labels, or status orbs are added, so only the body moves.
        /// </summary>
        public static UnitMotion Attach(GameObject go, LocomotionKind kind, float bodyHeight = 1f)
        {
            if (go == null) return null;

            var motion = go.GetComponent<UnitMotion>();
            if (motion == null) motion = go.AddComponent<UnitMotion>();
            motion.kind = kind;
            motion.bodyHeight = Mathf.Max(0.2f, bodyHeight);
            motion.AdoptVisuals();
            return motion;
        }

        public static LocomotionKind KindFor(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone:
                case SpecialistClass.CourierBot:
                    return LocomotionKind.Hover;
                case SpecialistClass.GeologistBot:
                case SpecialistClass.HarvesterBot:
                case SpecialistClass.TerraformerBot:
                case SpecialistClass.DefenseMech:
                    return LocomotionKind.Tracked;
                default:
                    return LocomotionKind.Walker;
            }
        }

        public static LocomotionKind KindFor(FaunaKind fauna)
        {
            switch (fauna)
            {
                case FaunaKind.Wisp:
                case FaunaKind.Leech:
                    return LocomotionKind.Hover;
                case FaunaKind.Hopper:
                    return LocomotionKind.Hop;
                default:
                    return LocomotionKind.Scuttle;
            }
        }

        private void Awake()
        {
            _idleSeed = Random.Range(0f, 100f);
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;
            if (_root == null) AdoptVisuals();
        }

        /// <summary>Reparent existing renderer children under a motion root we are free to animate.</summary>
        private void AdoptVisuals()
        {
            Transform existing = transform.Find(MotionRootName);
            if (existing != null)
            {
                CacheRoot(existing);
                return;
            }

            var rootGo = new GameObject(MotionRootName);
            rootGo.transform.SetParent(transform, false);

            // Snapshot first: reparenting mutates the child list mid-iteration.
            int count = transform.childCount;
            var toMove = new Transform[count];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == rootGo.transform) continue;
                if (IsExcluded(child.name)) continue;
                toMove[n++] = child;
            }

            for (int i = 0; i < n; i++)
                toMove[i].SetParent(rootGo.transform, worldPositionStays: false);

            CacheRoot(rootGo.transform);
        }

        /// <summary>Overlays and pick volumes must not inherit body motion.</summary>
        private static bool IsExcluded(string name)
        {
            return name == "SelectRing"
                   || name == "SelectProxy"
                   || name == "StatusOrb"
                   || name.Contains("Label")
                   || name.StartsWith("Vfx");
        }

        private void CacheRoot(Transform root)
        {
            _root = root;
            _rootBaseLocalPos = root.localPosition;
            _rootBaseScale = root.localScale;
            _treadRenderers = kind == LocomotionKind.Tracked
                ? root.GetComponentsInChildren<Renderer>(true)
                : null;
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;

            float distance = delta.magnitude;
            _speed = distance / dt;
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, _speed, 1f - Mathf.Exp(-10f * dt));

            float yaw = transform.eulerAngles.y;
            _turnRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;

            if (_suspended)
            {
                ApplyDowned(dt);
                return;
            }

            // Phase advances per metre so the gait never slides, at any game speed.
            _gaitPhase += distance * StrideFrequency();
            if (_gaitPhase > 1000f) _gaitPhase -= 1000f;

            UpdateLean(dt);

            switch (kind)
            {
                case LocomotionKind.Hover:
                    ApplyHover(dt);
                    break;
                case LocomotionKind.Tracked:
                    ApplyTracked(dt, distance);
                    break;
                case LocomotionKind.Scuttle:
                    ApplyGait(dt, bounce: 0.085f, scuttle: 0.14f);
                    break;
                case LocomotionKind.Hop:
                    ApplyHop(dt);
                    break;
                default:
                    ApplyGait(dt, bounce: 0.075f, scuttle: 0.02f);
                    break;
            }
        }

        private float StrideFrequency()
        {
            switch (kind)
            {
                case LocomotionKind.Scuttle: return 3.2f;
                case LocomotionKind.Hop: return 1.15f;
                case LocomotionKind.Hover: return 0.9f;
                case LocomotionKind.Tracked: return 1.4f;
                default: return 1.7f;
            }
        }

        /// <summary>
        /// Critically-damped-ish spring. Forward lean tracks acceleration, roll tracks turn rate,
        /// so a unit visibly leans into a start and banks through a corner.
        /// </summary>
        private void UpdateLean(float dt)
        {
            float leanScale = kind == LocomotionKind.Hover ? 3.2f : 1.5f;
            float bankScale = kind == LocomotionKind.Hover ? 0.16f : 0.05f;

            var target = new Vector2(
                Mathf.Clamp(_speedSmoothed * leanScale, 0f, 14f),
                Mathf.Clamp(-_turnRate * bankScale, -18f, 18f));

            const float stiffness = 90f;
            const float damping = 15f;

            Vector2 accel = (target - _lean) * stiffness - _leanVelocity * damping;
            _leanVelocity += accel * dt;
            _lean += _leanVelocity * dt;
        }

        private void ApplyGait(float dt, float bounce, float scuttle)
        {
            float moving = Mathf.Clamp01(_speedSmoothed / 2.5f);
            float phase = _gaitPhase * Mathf.PI * 2f;

            // Two bounces per stride: one per foot.
            float bob = Mathf.Abs(Mathf.Sin(phase)) * bounce * moving * bodyHeight;

            // Idle hum so a stationary robot never looks like a frozen prop.
            float idle = Mathf.Sin(Time.time * 1.6f + _idleSeed) * 0.008f * bodyHeight * (1f - moving);

            float sway = Mathf.Sin(phase * 0.5f) * scuttle * moving;

            _root.localPosition = _rootBaseLocalPos + new Vector3(sway * 0.15f, bob + idle, 0f);
            _root.localRotation = Quaternion.Euler(_lean.x, 0f, _lean.y + sway * 6f);

            // Slight squash on foot contact sells the weight.
            float squash = 1f - Mathf.Abs(Mathf.Cos(phase)) * 0.035f * moving;
            _root.localScale = new Vector3(
                _rootBaseScale.x * (2f - squash),
                _rootBaseScale.y * squash,
                _rootBaseScale.z * (2f - squash));
        }

        private void ApplyHover(float dt)
        {
            float t = Time.time;
            float float1 = Mathf.Sin(t * 1.9f + _idleSeed) * 0.06f;
            float float2 = Mathf.Sin(t * 3.1f + _idleSeed * 1.7f) * 0.025f;
            float lift = 0.10f * Mathf.Clamp01(_speedSmoothed / 3f);

            _root.localPosition = _rootBaseLocalPos + Vector3.up * ((float1 + float2) * bodyHeight + lift);
            _root.localRotation = Quaternion.Euler(_lean.x, 0f, _lean.y);
            _root.localScale = _rootBaseScale;
        }

        /// <summary>One ballistic bounce per stride so hoppers read as hoppers, not walkers.</summary>
        private void ApplyHop(float dt)
        {
            float moving = Mathf.Clamp01(_speedSmoothed / 1.8f);
            float phase = _gaitPhase * Mathf.PI * 2f;
            float hop = Mathf.Max(0f, Mathf.Sin(phase)) * 0.42f * moving * bodyHeight;
            float idle = Mathf.Sin(Time.time * 2.2f + _idleSeed) * 0.02f * bodyHeight * (1f - moving);
            float squash = 1f - Mathf.Max(0f, -Mathf.Sin(phase)) * 0.12f * moving;

            _root.localPosition = _rootBaseLocalPos + new Vector3(0f, hop + idle, 0f);
            _root.localRotation = Quaternion.Euler(_lean.x * 0.6f - hop * 18f, 0f, _lean.y);
            _root.localScale = new Vector3(
                _rootBaseScale.x * (2f - squash),
                _rootBaseScale.y * squash,
                _rootBaseScale.z * (2f - squash));
        }

        private void ApplyTracked(float dt, float distance)
        {
            // No gait bounce: tracks pitch back under power and settle on the brakes.
            float pitch = Mathf.Clamp(_lean.x * 0.45f, -6f, 6f);
            float jitter = Mathf.Sin(_gaitPhase * Mathf.PI * 2f) * 0.006f *
                           Mathf.Clamp01(_speedSmoothed / 2f) * bodyHeight;

            _root.localPosition = _rootBaseLocalPos + Vector3.up * jitter;
            _root.localRotation = Quaternion.Euler(-pitch, 0f, _lean.y * 0.5f);
            _root.localScale = _rootBaseScale;

            ScrollTreads(distance);
        }

        /// <summary>
        /// Scroll the base map by distance travelled. On an untextured hull this is invisible, but
        /// on any tread material it removes the "sliding statue" read for nearly no cost.
        /// </summary>
        private void ScrollTreads(float distance)
        {
            if (_treadRenderers == null || _treadRenderers.Length == 0 || distance <= 0f) return;

            _treadOffset += distance * 0.9f;
            if (_treadOffset > 1000f) _treadOffset -= 1000f;

            for (int i = 0; i < _treadRenderers.Length; i++)
            {
                var rend = _treadRenderers[i];
                if (rend == null) continue;
                var mat = rend.sharedMaterial;
                if (mat == null || !mat.HasProperty("_BaseMap")) continue;
                mat.SetTextureOffset("_BaseMap", new Vector2(0f, -_treadOffset));
            }
        }

        /// <summary>Downed: sag onto the ground and stay there.</summary>
        private void ApplyDowned(float dt)
        {
            float t = 1f - Mathf.Exp(-6f * dt);
            _root.localPosition = Vector3.Lerp(
                _root.localPosition,
                _rootBaseLocalPos - Vector3.up * bodyHeight * 0.22f,
                t);
            _root.localRotation = Quaternion.Slerp(_root.localRotation, Quaternion.Euler(0f, 0f, 62f), t);
            _root.localScale = Vector3.Lerp(_root.localScale, _rootBaseScale, t);
        }
    }
}
