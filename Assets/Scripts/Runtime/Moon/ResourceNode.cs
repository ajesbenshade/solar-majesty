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
            return take;
        }

        private void BuildMarker(Color soilColor)
        {
            // Clear prior children if reconfigured.
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            Color body = nodeType switch
            {
                ResourceNodeType.Metals => new Color(0.22f, 0.24f, 0.28f),
                ResourceNodeType.Ice => new Color(0.72f, 0.82f, 0.9f),
                ResourceNodeType.Fissile => new Color(0.95f, 0.42f, 0.08f),
                _ => soilColor
            };

            var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mound.name = "Mound";
            mound.transform.SetParent(transform, false);
            mound.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            mound.transform.localScale = nodeType == ResourceNodeType.Fissile
                ? new Vector3(1.6f, 0.7f, 1.6f)
                : new Vector3(2.2f, 0.55f, 2.2f);
            Object.Destroy(mound.GetComponent<Collider>());
            SetColor(mound, body);

            if (nodeType == ResourceNodeType.Fissile)
            {
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "Glow";
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                glow.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                Object.Destroy(glow.GetComponent<Collider>());
                SetColor(glow, new Color(1f, 0.55f, 0.12f));
            }
            else if (nodeType == ResourceNodeType.Metals)
            {
                for (int i = 0; i < 3; i++)
                {
                    var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rock.name = $"Ore_{i}";
                    rock.transform.SetParent(transform, false);
                    float a = i * 2.1f;
                    rock.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.55f, 0.45f, Mathf.Sin(a) * 0.55f);
                    rock.transform.localScale = new Vector3(0.45f, 0.55f, 0.35f);
                    rock.transform.localRotation = Quaternion.Euler(12f * i, 40f * i, 8f);
                    Object.Destroy(rock.GetComponent<Collider>());
                    SetColor(rock, new Color(0.12f, 0.12f, 0.14f));
                }
            }

            ColonyVisualUtility.SnapToGround(gameObject);
        }

        private void ApplyDepletedLook()
        {
            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                SetColor(rends[i].gameObject, new Color(0.35f, 0.33f, 0.3f));
            }
            gameObject.name = $"Node_{nodeType}_Depleted";
        }

        private static void SetColor(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
