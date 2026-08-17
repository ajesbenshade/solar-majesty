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
        private bool _expansionSpawned;

        public bool IsCleared => cleared;
        public bool IsScouted { get; private set; }
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

        public void Tick(int campusPieces = 0)
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
            {
                MarkCleared();
                return;
            }

            TryExpansionRestock(campusPieces);
        }

        /// <summary>
        /// Campus growth restocks one extra stalker once — expansion response, not a timed wave.
        /// </summary>
        private void TryExpansionRestock(int campusPieces)
        {
            if (cleared || _expansionSpawned || _loop == null) return;
            if (campusPieces < 8) return;
            _expansionSpawned = true;
            Vector3 offset = transform.right * 3.4f;
            var extra = _loop.SpawnStalkerAt(transform.position + offset, transform.parent);
            if (extra != null)
                _spawned.Add(extra);
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
                if (_loop.Flags.GetWorkRemaining(f) < f.PostedWork - 0.05f)
                    return true;
            }
            return false;
        }

        public void MarkScouted()
        {
            if (cleared || IsScouted) return;
            IsScouted = true;
            ApplyScoutedLook();
        }

        private void ApplyScoutedLook()
        {
            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                SetColor(rends[i].gameObject, new Color(0.28f, 0.78f, 0.92f));
            }
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
                DestroyImmediate(transform.GetChild(i).gameObject);

            // Irregular den — crater bowl + rim boulders + bone spines, not a dark cylinder pad.
            var pit = Prim(PrimitiveType.Sphere, "Pit",
                new Vector3(0f, 0.08f, 0f),
                new Vector3(3.5f, 0.2f, 3.05f),
                Quaternion.Euler(0f, 18f, 0f));
            SetColor(pit, pitColor);

            var maw = Prim(PrimitiveType.Sphere, "Maw",
                new Vector3(0.18f, 0.14f, -0.12f),
                new Vector3(1.55f, 0.28f, 1.35f),
                Quaternion.Euler(8f, 12f, 0f));
            SetColor(maw, pitColor * 0.55f);

            var mouth = Prim(PrimitiveType.Capsule, "Mouth",
                new Vector3(0.05f, 0.32f, 0.55f),
                new Vector3(0.95f, 0.42f, 0.7f),
                Quaternion.Euler(72f, 8f, 0f));
            SetColor(mouth, Color.Lerp(pitColor, rimColor, 0.35f));

            for (int i = 0; i < 7; i++)
            {
                float a = i * (Mathf.PI * 2f / 7f) + 0.2f;
                float rad = 1.55f + (i % 3) * 0.18f;
                var rock = Prim(PrimitiveType.Capsule, "Rim_" + i,
                    new Vector3(Mathf.Cos(a) * rad, 0.28f, Mathf.Sin(a) * rad * 0.9f),
                    new Vector3(0.55f + (i % 2) * 0.12f, 0.32f + (i % 3) * 0.06f, 0.42f),
                    Quaternion.Euler(18f * i, 40f * i, 12f));
                SetColor(rock, Color.Lerp(rimColor, new Color(0.32f, 0.14f, 0.08f), 0.35f));
            }

            Color bone = new Color(0.72f, 0.66f, 0.54f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.55f;
                var spine = Prim(PrimitiveType.Capsule, "Spine_" + i,
                    new Vector3(Mathf.Cos(a) * 0.85f, 0.72f, Mathf.Sin(a) * 0.85f),
                    new Vector3(0.12f, 0.48f + i * 0.08f, 0.12f),
                    Quaternion.Euler(18f, 50f * i, 8f));
                SetColor(spine, bone);
            }

            ColonyVisualUtility.SnapToGround(gameObject);
        }

        private GameObject Prim(
            PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
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
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
