using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Colonist that connects village HABs and works camps. Not player-commanded.
    /// Wears an authored suit from <see cref="ColonistArt"/> (falls back to the capsule placeholder).
    /// </summary>
    public class VillagerAgent : MonoBehaviour
    {
        private const float TurnDegreesPerSecond = 540f;
        private static int s_spawned;

        [SerializeField] private float moveSpeed = 2.4f;

        private Animator _anim;

        private Vector3 _home;
        private Vector3 _work;
        private bool _hasWork;
        private float _retarget;

        public void Bind(Vector3 home, Vector3 work)
        {
            _home = home;
            _work = work;
            _hasWork = (work - home).sqrMagnitude > 1f;
            _retarget = 3f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _retarget -= dt;
            Vector3 dest = _hasWork && _retarget > 0f ? _work : _home;
            if (_retarget <= -4f)
                _retarget = 8f;

            Vector3 p = transform.position;
            dest.y = p.y;
            Vector3 next = Vector3.MoveTowards(p, dest, moveSpeed * dt);
            transform.position = next;

            Vector3 step = next - p;
            step.y = 0f;
            if (step.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(step.normalized, Vector3.up),
                    TurnDegreesPerSecond * dt);
            if (_anim != null)
                ColonistArt.Drive(_anim, dt > 0f ? step.magnitude / dt : 0f);
        }

        public static VillagerAgent Spawn(Transform parent, Vector3 home, Vector3 work)
        {
            var go = new GameObject("Villager");
            go.transform.SetParent(parent, false);
            go.transform.position = home + Vector3.up * 0.4f;
            Vector3 face = work - home;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);

            var suit = ColonistArt.Attach(go.transform, s_spawned++);
            if (suit != null)
            {
                ColonyVisualUtility.SnapToGround(go);
                var agent = go.AddComponent<VillagerAgent>();
                agent._anim = suit.GetComponentInChildren<Animator>();
                agent.Bind(home, work);
                return agent;
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            Object.Destroy(body.GetComponent<Collider>());

            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Band";
            band.transform.SetParent(go.transform, false);
            band.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            band.transform.localScale = new Vector3(0.42f, 0.06f, 0.42f);
            Object.Destroy(band.GetComponent<Collider>());

            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            var v = go.AddComponent<VillagerAgent>();
            v.Bind(home, work);
            return v;
        }
    }
}
