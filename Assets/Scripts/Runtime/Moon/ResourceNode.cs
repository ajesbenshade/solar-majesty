using UnityEngine;

namespace SolarMajesty
{
    public enum ResourceNodeType
    {
        Regolith = 0,
        Metals = 1,
        Ice = 2,
        Fissile = 3
    }

    /// <summary>
    /// Harvestable world deposit. Extract flags near this node deplete remaining yield.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceNodeType nodeType = ResourceNodeType.Regolith;
        [SerializeField] private int remaining = 40;
        [SerializeField] private float harvestRadius = 7f;

        public ResourceNodeType NodeType => nodeType;
        public int Remaining => remaining;
        public bool IsDepleted => remaining <= 0;
        public float HarvestRadius => harvestRadius;
        public Vector3 WorldPosition => transform.position;

        public void Configure(ResourceNodeType type, int yield, float radius, Color? soilColor = null)
        {
            nodeType = type;
            remaining = Mathf.Max(1, yield);
            harvestRadius = Mathf.Max(3f, radius);
            gameObject.name = $"Node_{type}";
            BuildMarker(soilColor ?? new Color(0.52f, 0.48f, 0.4f));
        }

        /// <summary>Consume up to amount; returns what was actually taken.</summary>
        public int Harvest(int amount)
        {
            if (amount <= 0 || remaining <= 0) return 0;
            int take = Mathf.Min(amount, remaining);
            remaining -= take;
            if (remaining <= 0)
                ApplyDepletedLook();
            RefreshYieldLabel();
            return take;
        }

        private TextMesh _yieldLabel;

        private void BuildMarker(Color soilColor)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _yieldLabel = null;

            Color body = nodeType switch
            {
                ResourceNodeType.Metals => new Color(0.28f, 0.26f, 0.24f),
                ResourceNodeType.Ice => new Color(0.78f, 0.86f, 0.92f),
                ResourceNodeType.Fissile => new Color(0.95f, 0.42f, 0.08f),
                _ => soilColor
            };

            // Flattened outcrop — capsules/spheres, never greybox cubes.
            var mound = Prim(PrimitiveType.Sphere, "Mound",
                new Vector3(0f, 0.28f, 0f),
                nodeType == ResourceNodeType.Fissile
                    ? new Vector3(1.85f, 0.58f, 1.7f)
                    : new Vector3(2.45f, 0.46f, 2.1f),
                Quaternion.Euler(0f, 22f, 0f));
            SetColor(mound, Color.Lerp(body, soilColor, 0.28f), 0.08f);

            int boulders = nodeType == ResourceNodeType.Metals ? 5 : 4;
            for (int i = 0; i < boulders; i++)
            {
                float a = i * (Mathf.PI * 2f / boulders) + 0.28f;
                float rad = 0.78f + (i % 2) * 0.18f;
                var rock = Prim(PrimitiveType.Capsule, "Boulder_" + i,
                    new Vector3(Mathf.Cos(a) * rad, 0.26f, Mathf.Sin(a) * rad * 0.88f),
                    new Vector3(0.38f + i * 0.05f, 0.26f + (i % 2) * 0.07f, 0.34f),
                    Quaternion.Euler(16f * i, 48f * i, 10f));
                SetColor(rock, Color.Lerp(body, new Color(0.16f, 0.11f, 0.08f), 0.4f), 0.1f);
            }

            if (nodeType == ResourceNodeType.Fissile)
            {
                var glow = Prim(PrimitiveType.Sphere, "Glow",
                    new Vector3(0f, 0.82f, 0f),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    Quaternion.identity);
                SetColor(glow, new Color(1f, 0.55f, 0.12f), 0.45f);
            }
            else if (nodeType == ResourceNodeType.Metals)
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.15f;
                    var ore = Prim(PrimitiveType.Capsule, "Ore_" + i,
                        new Vector3(Mathf.Cos(a) * 0.48f, 0.42f, Mathf.Sin(a) * 0.48f),
                        new Vector3(0.28f, 0.42f, 0.22f),
                        Quaternion.Euler(22f * i, 55f * i, 14f));
                    SetColor(ore, new Color(0.18f, 0.17f, 0.16f), 0.38f);
                }
            }
            else if (nodeType == ResourceNodeType.Ice)
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.2f + 0.4f;
                    var crystal = Prim(PrimitiveType.Capsule, "Crystal_" + i,
                        new Vector3(Mathf.Cos(a) * 0.42f, 0.52f, Mathf.Sin(a) * 0.42f),
                        new Vector3(0.16f, 0.38f + i * 0.06f, 0.16f),
                        Quaternion.Euler(12f, 40f * i, 8f));
                    SetColor(crystal, new Color(0.86f, 0.92f, 0.97f), 0.62f);
                }
            }

            ColonyVisualUtility.SnapToGround(gameObject);
            EnsureYieldLabel();
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

        private void EnsureYieldLabel()
        {
            if (_yieldLabel != null)
            {
                RefreshYieldLabel();
                return;
            }

            var go = new GameObject("YieldLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            _yieldLabel = go.AddComponent<TextMesh>();
            _yieldLabel.anchor = TextAnchor.MiddleCenter;
            _yieldLabel.alignment = TextAlignment.Center;
            _yieldLabel.characterSize = 0.12f;
            _yieldLabel.fontSize = 36;
            _yieldLabel.fontStyle = FontStyle.Bold;
            _yieldLabel.color = new Color(0.92f, 0.93f, 0.9f);
            RefreshYieldLabel();
        }

        private void RefreshYieldLabel()
        {
            if (_yieldLabel == null) return;
            string tag = nodeType switch
            {
                ResourceNodeType.Metals => "MET",
                ResourceNodeType.Ice => "ICE",
                ResourceNodeType.Fissile => "PWR",
                _ => "REG"
            };
            _yieldLabel.text = IsDepleted ? $"{tag} —" : $"{tag} {remaining}";
            _yieldLabel.color = IsDepleted
                ? new Color(0.55f, 0.52f, 0.48f)
                : new Color(0.92f, 0.93f, 0.9f);
        }

        private void ApplyDepletedLook()
        {
            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || rends[i].GetComponent<TextMesh>() != null) continue;
                SetColor(rends[i].gameObject, new Color(0.35f, 0.33f, 0.3f));
            }
            gameObject.name = $"Node_{nodeType}_Depleted";
            RefreshYieldLabel();
        }

        private void Update()
        {
            if (_yieldLabel == null) return;
            if (Camera.main == null) return;
            _yieldLabel.transform.rotation = Quaternion.LookRotation(
                _yieldLabel.transform.position - Camera.main.transform.position);
        }

        private static void SetColor(GameObject go, Color c, float smoothness = 0.12f)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
