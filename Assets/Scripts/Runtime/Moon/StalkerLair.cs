using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Procedural stalker den. Owns a budget of fauna; when they die the lair clears.
    /// </summary>
    public class StalkerLair : MonoBehaviour
    {
        [SerializeField] private int stalkerBudget = 2;
        [SerializeField] private float clearRadius = 8f;
        [SerializeField] private bool cleared;

        private readonly List<DustStalkerAgent> _spawned = new List<DustStalkerAgent>(4);
        private GameLoop _loop;

        public bool IsCleared => cleared;
        public int StalkerBudget => stalkerBudget;
        public float ClearRadius => clearRadius;
        public Vector3 WorldPosition => transform.position;
        public IReadOnlyList<DustStalkerAgent> Spawned => _spawned;

        public void Configure(GameLoop loop, int budget, float radius, Color? rimColor = null, Color? pitColor = null)
        {
            _loop = loop;
            stalkerBudget = Mathf.Max(1, budget);
            clearRadius = Mathf.Max(4f, radius);
            cleared = false;
            gameObject.name = "StalkerLair";
            BuildMarker(
                rimColor ?? new Color(0.18f, 0.08f, 0.08f),
                pitColor ?? new Color(0.08f, 0.04f, 0.05f));
        }

        public void SpawnInitial(Transform parent)
        {
            if (cleared || _loop == null) return;
            for (int i = 0; i < stalkerBudget; i++)
            {
                float ang = (Mathf.PI * 2f * i) / stalkerBudget + 0.4f;
                Vector3 offset = new Vector3(Mathf.Cos(ang) * 3.2f, 0f, Mathf.Sin(ang) * 3.2f);
                var stalker = _loop.SpawnStalkerAt(transform.position + offset, parent);
                if (stalker != null)
                    _spawned.Add(stalker);
            }
        }

        public void Tick()
        {
            if (cleared) return;

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] == null || !_spawned[i].IsAlive)
                    _spawned.RemoveAt(i);
            }

            // ClearThreat posted on the den itself depletes the lair even if fauna wandered.
            if (HasActiveClearThreatNearby())
            {
                ForceClear();
                return;
            }

            if (_spawned.Count == 0)
                MarkCleared();
        }

        /// <summary>ClearThreat near this den — kill remaining fauna and silence the lair.</summary>
        public void ForceClear()
        {
            if (cleared) return;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null && _spawned[i].IsAlive)
                    _spawned[i].ApplyClearThreatKill();
            }
            _spawned.Clear();
            MarkCleared();
        }

        private bool HasActiveClearThreatNearby()
        {
            if (_loop == null || _loop.Flags == null) return false;
            var list = _loop.Flags.Flags;
            Vector3 me = transform.position;
            float r = clearRadius;
            float rSq = r * r;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null || f.Data == null) continue;
                if (f.Data.flagType != FlagType.ClearThreat) continue;
                float dx = f.WorldPosition.x - me.x;
                float dz = f.WorldPosition.z - me.z;
                if (dx * dx + dz * dz > rSq) continue;
                // Only deplete once a specialist is actually working the den, not on claim alone.
                if (_loop.Flags.GetWorkRemaining(f) < f.Data.workRequired - 0.05f)
                    return true;
            }
            return false;
        }

        private void MarkCleared()
        {
            if (cleared) return;
            cleared = true;
            gameObject.name = "StalkerLair_Cleared";
            ApplyClearedLook();
            DemoVfx.ClaimRing(transform.position, new Color(0.35f, 0.9f, 0.55f));
            Debug.Log("[Lair] Cleared stalker den.");
        }

        private void BuildMarker(Color rimColor, Color pitColor)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(transform, false);
            rim.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            rim.transform.localScale = new Vector3(4.2f, 0.12f, 4.2f);
            Object.Destroy(rim.GetComponent<Collider>());
            SetColor(rim, rimColor);

            var pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pit.name = "Pit";
            pit.transform.SetParent(transform, false);
            pit.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            pit.transform.localScale = new Vector3(2.6f, 0.04f, 2.6f);
            Object.Destroy(pit.GetComponent<Collider>());
            SetColor(pit, pitColor);

            ColonyVisualUtility.SnapToGround(gameObject);
        }

        private void ApplyClearedLook()
        {
            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                SetColor(rends[i].gameObject, new Color(0.28f, 0.3f, 0.28f));
            }
        }

        private static void SetColor(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            rend.sharedMaterial = mat;
        }
    }
}
