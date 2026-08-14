using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Small industrial scatter around placed modules so Earth / Luna / Mars campuses
    /// read as dressed outposts without new FBX kits.
    /// </summary>
    public static class CampusDressing
    {
        private const int MaxProps = 28;
        private static int _count;

        public static void Reset() => _count = 0;

        public static void DressPlaced(BuildingData data, GameObject go, CelestialBodyProfile body)
        {
            if (data == null || go == null) return;
            if (data.category == BuildingCategory.Utility) return;
            if (_count >= MaxProps) return;

            Vector3 origin = go.transform.position;
            float yaw = _count * 1.618f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 2.55f;
            Transform parent = go.transform.parent;

            SpawnCrate(origin + offset, body, parent);
            _count++;

            if (data.category == BuildingCategory.Power ||
                data.category == BuildingCategory.Palace ||
                data.category == BuildingCategory.LandingPad)
            {
                SpawnPylon(origin - offset * 0.55f, body, parent);
                _count++;
            }
        }

        private static void SpawnCrate(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dress_Crate";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.28f;
            go.transform.localScale = new Vector3(0.55f, 0.42f, 0.7f);
            go.transform.rotation = Quaternion.Euler(0f, world.x * 17f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.78f, 0.8f, 0.82f), body.RockColor, 0.35f)
                : new Color(0.78f, 0.8f, 0.82f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnPylon(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Pylon";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.55f;
            go.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(0.96f, 0.42f, 0.08f));
            ColonyVisualUtility.SnapToGround(go);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Beacon";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            cap.transform.localScale = new Vector3(1.4f, 0.45f, 1.4f);
            Object.Destroy(cap.GetComponent<Collider>());
            Color glow = body != null
                ? Color.Lerp(new Color(0.96f, 0.42f, 0.08f), body.SunColor, 0.25f)
                : new Color(0.96f, 0.42f, 0.08f);
            Tint(cap, glow);
        }

        private static void Tint(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.28f);
            rend.sharedMaterial = mat;
        }
    }
}
