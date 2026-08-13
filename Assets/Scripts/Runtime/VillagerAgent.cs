using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Colonist that connects village HABs and works camps. Not player-commanded.
    /// </summary>
    public class VillagerAgent : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2.4f;

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
            transform.position = Vector3.MoveTowards(p, dest, moveSpeed * dt);
        }

        public static VillagerAgent Spawn(Transform parent, Vector3 home, Vector3 work)
        {
            var go = new GameObject("Villager");
            go.transform.SetParent(parent, false);
            go.transform.position = home + Vector3.up * 0.4f;

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
