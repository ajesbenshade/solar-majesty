using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Runtime greybox silhouettes for specialists / stalkers. Brain loop unchanged.
    /// Prefabs under Resources/Units are preferred; these builders are the fallback + editor source.
    /// </summary>
    public static class UnitPlaceholderFactory
    {
        public static readonly Color ScoutTint = new Color(0.35f, 0.85f, 1f);
        public static readonly Color EngineerTint = new Color(1f, 0.55f, 0.15f);
        public static readonly Color DefenseTint = new Color(0.85f, 0.22f, 0.22f);
        public static readonly Color StalkerTint = new Color(0.42f, 0.07f, 0.1f);

        public static GameObject BuildScout()
        {
            // Tall thin body + antenna — explores.
            var root = new GameObject("Unit_ScoutDrone");
            var body = Capsule("Body", root.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.55f, 1.35f, 0.55f), ScoutTint);
            var head = Sphere("Sensor", root.transform, new Vector3(0f, 2.55f, 0f), new Vector3(0.35f, 0.35f, 0.35f), ScoutTint * 1.05f);
            var ant = Cylinder("Antenna", root.transform, new Vector3(0.15f, 3.05f, 0f), new Vector3(0.06f, 0.45f, 0.06f), Color.white);
            return root;
        }

        public static GameObject BuildEngineer()
        {
            // Squat body + toolbox — builders.
            var root = new GameObject("Unit_EngineerBot");
            Capsule("Body", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.95f, 0.95f), EngineerTint);
            Cube("Toolbox", root.transform, new Vector3(0.75f, 0.85f, 0f), new Vector3(0.55f, 0.4f, 0.45f), new Color(0.25f, 0.25f, 0.28f));
            Cube("Visor", root.transform, new Vector3(0f, 1.45f, 0.42f), new Vector3(0.55f, 0.18f, 0.12f), new Color(0.2f, 0.75f, 1f));
            return root;
        }

        public static GameObject BuildDefense()
        {
            // Wide chassis + shoulder block — combat.
            var root = new GameObject("Unit_DefenseMech");
            Capsule("Body", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.15f, 1.1f, 1.0f), DefenseTint);
            Cube("Shoulder", root.transform, new Vector3(0.7f, 1.55f, 0f), new Vector3(0.7f, 0.45f, 0.7f), new Color(0.35f, 0.12f, 0.12f));
            Cube("Shield", root.transform, new Vector3(-0.75f, 1.2f, 0.15f), new Vector3(0.2f, 1.1f, 0.85f), new Color(0.55f, 0.55f, 0.6f));
            return root;
        }

        public static GameObject BuildDustStalker()
        {
            // Low elongated predator silhouette.
            var root = new GameObject("Unit_DustStalker");
            Sphere("Body", root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.55f, 1.1f), StalkerTint);
            Sphere("Head", root.transform, new Vector3(0f, 0.45f, 0.65f), new Vector3(0.55f, 0.4f, 0.55f), StalkerTint * 1.1f);
            Cube("Spine", root.transform, new Vector3(0f, 0.55f, -0.35f), new Vector3(0.25f, 0.2f, 0.9f), new Color(0.2f, 0.05f, 0.05f));
            return root;
        }

        public static GameObject BuildForClass(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return BuildScout();
                case SpecialistClass.EngineerBot: return BuildEngineer();
                case SpecialistClass.DefenseMech: return BuildDefense();
                default: return BuildScout();
            }
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }

        private static GameObject Capsule(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static GameObject Sphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static void Tint(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            // Instance material so batch tint works in Play.
            var mat = rend.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.color = color;
        }
    }
}
