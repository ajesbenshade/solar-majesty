using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Generates and steps a ring of IK legs around a body.
    ///
    /// The meshes have no skeleton, so instead of animating a rig this builds the legs as geometry
    /// and solves them. Feet plant in world space and only lift when the body has walked far enough
    /// from the plant point, which is what makes the walk read as ground contact rather than a
    /// looping cycle. Opposite legs are offset so the gait alternates.
    ///
    /// Two-bone IK with a fixed knee direction is enough at isometric distance; nobody is inspecting
    /// the elbow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralLegs : MonoBehaviour
    {
        private struct Leg
        {
            public Transform Hip;
            public Transform Upper;
            public Transform Lower;
            public Vector3 HipLocalOffset;
            public Vector3 PlantedFoot;
            public Vector3 StepFrom;
            public Vector3 StepTo;
            public float StepT;
            public bool Stepping;
            public float PhaseOffset;
        }

        private Leg[] _legs;
        private float _legLength = 0.55f;
        private float _stepDistance = 0.55f;
        private float _stepSpeed = 5.5f;
        private float _stepHeight = 0.18f;
        private float _bodyRadius = 0.42f;
        private float _hipHeight = 0.45f;
        private bool _suspended;

        public void SetSuspended(bool suspended) => _suspended = suspended;

        /// <summary>Remove generated leg geometry so a rebuild does not stack rigs.</summary>
        public void Teardown()
        {
            Transform parent = transform.Find("MotionRoot");
            if (parent == null) parent = transform;
            Transform root = parent.Find("Legs");
            if (root != null) Destroy(root.gameObject);
            _legs = null;
        }

        /// <summary>Drop any leg rig on this object (hovering fauna should not have one).</summary>
        public static void Remove(GameObject go)
        {
            if (go == null) return;
            var legs = go.GetComponent<ProceduralLegs>();
            if (legs == null) return;
            legs.Teardown();
            Destroy(legs);
        }

        /// <summary>
        /// Build a leg ring. Call after the body mesh exists; legs are added as siblings of the
        /// mesh under the same motion root so they inherit body bob.
        /// </summary>
        public static ProceduralLegs Attach(
            GameObject go,
            int legCount,
            float bodyRadius,
            float hipHeight,
            float legLength,
            Color color)
        {
            if (go == null || legCount <= 0) return null;

            // Fauna kind is assigned after the visual is built, so a rig may already exist with the
            // wrong leg count. Rebuild rather than leaving a stalker's six legs on a tick.
            var existing = go.GetComponent<ProceduralLegs>();
            if (existing != null)
            {
                if (existing._legs != null && existing._legs.Length == legCount)
                    return existing;
                existing.Teardown();
                Destroy(existing);
            }

            var legs = go.AddComponent<ProceduralLegs>();
            legs._bodyRadius = Mathf.Max(0.1f, bodyRadius);
            legs._hipHeight = Mathf.Max(0.1f, hipHeight);
            legs._legLength = Mathf.Max(0.15f, legLength);
            legs._stepDistance = legs._legLength * 0.85f;
            legs._stepHeight = legs._legLength * 0.3f;
            legs.Build(legCount, color);
            return legs;
        }

        private void Build(int legCount, Color color)
        {
            Transform parent = transform.Find("MotionRoot");
            if (parent == null) parent = transform;

            var root = new GameObject("Legs").transform;
            root.SetParent(parent, false);

            _legs = new Leg[legCount];
            float segment = _legLength * 0.5f;

            for (int i = 0; i < legCount; i++)
            {
                // Spread hips around the body, biased to the sides rather than front and back.
                float t = (i + 0.5f) / legCount;
                float angle = t * Mathf.PI * 2f;
                var offset = new Vector3(
                    Mathf.Sin(angle) * _bodyRadius,
                    _hipHeight,
                    Mathf.Cos(angle) * _bodyRadius * 0.65f);

                var hip = new GameObject($"Leg{i}").transform;
                hip.SetParent(root, false);
                hip.localPosition = offset;

                Transform upper = MakeSegment(hip, "Upper", segment, color, 0.055f);
                Transform lower = MakeSegment(upper, "Lower", segment, color, 0.042f);
                lower.localPosition = new Vector3(0f, -segment, 0f);

                Vector3 foot = transform.TransformPoint(new Vector3(offset.x * 1.35f, 0f, offset.z * 1.35f));

                _legs[i] = new Leg
                {
                    Hip = hip,
                    Upper = upper,
                    Lower = lower,
                    HipLocalOffset = offset,
                    PlantedFoot = foot,
                    StepFrom = foot,
                    StepTo = foot,
                    StepT = 1f,
                    Stepping = false,
                    // Alternate: left legs step while right legs plant.
                    PhaseOffset = (i % 2 == 0) ? 0f : 0.5f
                };
            }
        }

        private static Transform MakeSegment(Transform parent, string name, float length, Color color, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader) { name = "SM_Leg" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.55f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
                mat.enableInstancing = true;
                rend.sharedMaterial = mat;
            }

            return go.transform;
        }

        private void LateUpdate()
        {
            if (_legs == null || _legs.Length == 0) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < _legs.Length; i++)
            {
                ref Leg leg = ref _legs[i];
                if (leg.Hip == null) continue;

                Vector3 hipWorld = leg.Hip.position;
                Vector3 rest = transform.TransformPoint(
                    new Vector3(leg.HipLocalOffset.x * 1.35f, 0f, leg.HipLocalOffset.z * 1.35f));
                rest.y = 0f;

                if (leg.Stepping)
                {
                    leg.StepT += dt * _stepSpeed;
                    if (leg.StepT >= 1f)
                    {
                        leg.StepT = 1f;
                        leg.Stepping = false;
                        leg.PlantedFoot = leg.StepTo;
                    }
                    else
                    {
                        // Parabolic arc so the foot lifts and lands rather than sliding.
                        Vector3 flat = Vector3.Lerp(leg.StepFrom, leg.StepTo, leg.StepT);
                        flat.y += Mathf.Sin(leg.StepT * Mathf.PI) * _stepHeight;
                        leg.PlantedFoot = flat;
                    }
                }
                else if (!_suspended)
                {
                    float drift = Vector3.Distance(leg.PlantedFoot, rest);
                    if (drift > _stepDistance && CanStep(i))
                    {
                        leg.Stepping = true;
                        leg.StepT = 0f;
                        leg.StepFrom = leg.PlantedFoot;
                        // Overshoot toward the rest point so the leg does not immediately re-step.
                        leg.StepTo = rest + (rest - leg.PlantedFoot).normalized * _stepDistance * 0.35f;
                        leg.StepTo.y = 0f;
                    }
                }

                SolveTwoBone(ref leg, hipWorld);
            }
        }

        /// <summary>Never lift opposing legs at once, or the body would have nothing to stand on.</summary>
        private bool CanStep(int index)
        {
            int stepping = 0;
            for (int i = 0; i < _legs.Length; i++)
            {
                if (_legs[i].Stepping) stepping++;
            }
            return stepping < Mathf.Max(1, _legs.Length / 3);
        }

        /// <summary>
        /// Two-bone IK. Aims the chain at the foot, then bends the knee by the law of cosines,
        /// clamping when the target is out of reach so the leg straightens instead of snapping.
        /// </summary>
        private void SolveTwoBone(ref Leg leg, Vector3 hipWorld)
        {
            Vector3 toFoot = leg.PlantedFoot - hipWorld;
            float dist = toFoot.magnitude;
            if (dist < 1e-4f) return;

            float segment = _legLength * 0.5f;
            float reach = Mathf.Min(dist, segment * 2f * 0.995f);

            // Interior angle at the hip between the bone and the hip-to-foot line.
            float cos = Mathf.Clamp((reach * reach) / (2f * segment * reach), -1f, 1f);
            float hipAngle = Mathf.Acos(cos) * Mathf.Rad2Deg;

            Vector3 dir = toFoot / dist;
            Quaternion aim = Quaternion.LookRotation(dir, transform.up);

            // Cylinders point along +Y, so rotate the aim into the bone's axis, then bend outward.
            Quaternion toBone = aim * Quaternion.Euler(90f, 0f, 0f);
            Vector3 bendAxis = Vector3.Cross(dir, transform.up).normalized;
            if (bendAxis.sqrMagnitude < 1e-4f) bendAxis = transform.right;

            leg.Upper.rotation = Quaternion.AngleAxis(-hipAngle, bendAxis) * toBone;
            leg.Upper.position = hipWorld + leg.Upper.up * -segment * 0.5f;

            Vector3 knee = hipWorld + (leg.Upper.up * -segment);
            Vector3 kneeToFoot = leg.PlantedFoot - knee;
            if (kneeToFoot.sqrMagnitude < 1e-6f) return;

            leg.Lower.rotation = Quaternion.LookRotation(kneeToFoot.normalized, transform.up) *
                                 Quaternion.Euler(90f, 0f, 0f);
            leg.Lower.position = knee + kneeToFoot.normalized * segment * 0.5f;
        }
    }
}
