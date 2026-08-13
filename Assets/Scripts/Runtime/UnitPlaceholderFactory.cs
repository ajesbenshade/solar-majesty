using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Unit builders: prefer Blender SM_Unit_* FBX meshes, else industrial primitive fallbacks.
    /// Prefabs under Resources/Units wrap these for Play Mode.
    /// </summary>
    public static class UnitPlaceholderFactory
    {
        public static readonly Color ScoutTint = new Color(0.35f, 0.85f, 1f);
        public static readonly Color EngineerTint = new Color(1f, 0.55f, 0.15f);
        public static readonly Color DefenseTint = new Color(0.85f, 0.22f, 0.22f);
        public static readonly Color MedicTint = new Color(0.92f, 0.96f, 1f);
        public static readonly Color StalkerTint = new Color(0.42f, 0.07f, 0.1f);

        private static readonly Color WhiteShell = new Color(0.86f, 0.88f, 0.9f);
        private static readonly Color BlackBand = new Color(0.06f, 0.06f, 0.07f);
        private static readonly Color OrangeAccent = new Color(0.95f, 0.42f, 0.08f);
        private static readonly Color Steel = new Color(0.48f, 0.5f, 0.53f);

        public static GameObject BuildScout()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadScout(), "Unit_ScoutDrone");
            if (mesh != null) return mesh;

            // Tall probe drone — white hull, cyan sensor, whip antenna.
            var root = new GameObject("Unit_ScoutDrone");
            Capsule("Body", root.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.5f, 1.25f, 0.5f), ScoutTint);
            Cylinder("Band", root.transform, new Vector3(0f, 1.55f, 0f), new Vector3(0.58f, 0.08f, 0.58f), BlackBand);
            Sphere("Sensor", root.transform, new Vector3(0f, 2.55f, 0f), new Vector3(0.38f, 0.38f, 0.38f), WhiteShell);
            Cube("Visor", root.transform, new Vector3(0f, 2.55f, 0.18f), new Vector3(0.28f, 0.12f, 0.08f), ScoutTint);
            Cylinder("Antenna", root.transform, new Vector3(0.18f, 3.15f, 0f), new Vector3(0.05f, 0.5f, 0.05f), Steel);
            Cube("Beacon", root.transform, new Vector3(-0.22f, 2.95f, 0f), new Vector3(0.1f, 0.1f, 0.1f), OrangeAccent);
            return root;
        }

        public static GameObject BuildEngineer()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadEngineer(), "Unit_EngineerBot");
            if (mesh != null) return mesh;

            // Squat builder — orange shell, toolbox, orange service stripe.
            var root = new GameObject("Unit_EngineerBot");
            Capsule("Body", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.9f, 0.95f), EngineerTint);
            Cylinder("Band", root.transform, new Vector3(0f, 0.85f, 0f), new Vector3(1.05f, 0.1f, 1.05f), BlackBand);
            Cube("Toolbox", root.transform, new Vector3(0.78f, 0.85f, 0f), new Vector3(0.5f, 0.38f, 0.42f), BlackBand);
            Cube("Stripe", root.transform, new Vector3(0.78f, 0.95f, 0.22f), new Vector3(0.52f, 0.08f, 0.08f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 1.4f, 0.42f), new Vector3(0.55f, 0.16f, 0.1f), new Color(0.25f, 0.85f, 1f));
            Cube("Arm", root.transform, new Vector3(-0.7f, 1.05f, 0.15f), new Vector3(0.35f, 0.18f, 0.18f), Steel);
            return root;
        }

        public static GameObject BuildDefense()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadDefense(), "Unit_DefenseMech");
            if (mesh != null) return mesh;

            // Wide combat chassis — red hull, shield plate, shoulder block.
            var root = new GameObject("Unit_DefenseMech");
            Capsule("Body", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.1f, 1.05f, 0.95f), DefenseTint);
            Cylinder("Band", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(1.2f, 0.1f, 1.05f), BlackBand);
            Cube("Shoulder", root.transform, new Vector3(0.72f, 1.55f, 0f), new Vector3(0.65f, 0.42f, 0.65f), WhiteShell);
            Cube("ShoulderAccent", root.transform, new Vector3(0.72f, 1.7f, 0.28f), new Vector3(0.5f, 0.08f, 0.12f), OrangeAccent);
            Cube("Shield", root.transform, new Vector3(-0.78f, 1.15f, 0.12f), new Vector3(0.18f, 1.15f, 0.9f), Steel);
            Cube("Plating", root.transform, new Vector3(0f, 1.35f, 0.45f), new Vector3(0.7f, 0.35f, 0.12f), BlackBand);
            return root;
        }

        public static GameObject BuildMedic()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadForClass(SpecialistClass.Medic), "Unit_Medic");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_Medic");
            Capsule("Body", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 1.05f, 0.7f), WhiteShell);
            Cylinder("Band", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.78f, 0.08f, 0.78f), BlackBand);
            Cube("CrossH", root.transform, new Vector3(0f, 1.55f, 0.38f), new Vector3(0.42f, 0.1f, 0.08f), ScoutTint);
            Cube("CrossV", root.transform, new Vector3(0f, 1.55f, 0.38f), new Vector3(0.1f, 0.42f, 0.08f), ScoutTint);
            Sphere("Kit", root.transform, new Vector3(0.42f, 0.95f, 0.05f), new Vector3(0.28f, 0.22f, 0.28f), MedicTint);
            Cube("Beacon", root.transform, new Vector3(-0.22f, 2.05f, 0f), new Vector3(0.1f, 0.1f, 0.1f), OrangeAccent);
            return root;
        }

        public static GameObject BuildDustStalker()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadStalker(), "Unit_DustStalker");
            if (mesh != null) return mesh;

            // Low predator — dark carapace, glowing eyes, spine ridges.
            var root = new GameObject("Unit_DustStalker");
            Sphere("Body", root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(1.7f, 0.55f, 1.15f), StalkerTint);
            Sphere("Head", root.transform, new Vector3(0f, 0.48f, 0.7f), new Vector3(0.55f, 0.4f, 0.55f), StalkerTint * 1.15f);
            Cube("Spine", root.transform, new Vector3(0f, 0.58f, -0.35f), new Vector3(0.22f, 0.18f, 0.95f), BlackBand);
            Cube("RidgeA", root.transform, new Vector3(0f, 0.72f, 0.05f), new Vector3(0.12f, 0.28f, 0.18f), BlackBand);
            Cube("RidgeB", root.transform, new Vector3(0f, 0.68f, -0.35f), new Vector3(0.1f, 0.22f, 0.16f), BlackBand);
            Sphere("EyeL", root.transform, new Vector3(-0.16f, 0.55f, 0.92f), new Vector3(0.12f, 0.1f, 0.1f), OrangeAccent);
            Sphere("EyeR", root.transform, new Vector3(0.16f, 0.55f, 0.92f), new Vector3(0.12f, 0.1f, 0.1f), OrangeAccent);
            Cube("LegFL", root.transform, new Vector3(-0.55f, 0.18f, 0.35f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            Cube("LegFR", root.transform, new Vector3(0.55f, 0.18f, 0.35f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            Cube("LegBL", root.transform, new Vector3(-0.5f, 0.18f, -0.4f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            Cube("LegBR", root.transform, new Vector3(0.5f, 0.18f, -0.4f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            return root;
        }

        public static GameObject BuildForClass(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return BuildScout();
                case SpecialistClass.EngineerBot: return BuildEngineer();
                case SpecialistClass.DefenseMech: return BuildDefense();
                case SpecialistClass.Medic: return BuildMedic();
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

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (lit != null)
            {
                var mat = new Material(lit) { name = go.name + "_Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.color = color;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.42f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
                rend.sharedMaterial = mat;
                return;
            }

            var fallback = rend.material;
            if (fallback.HasProperty("_BaseColor"))
                fallback.SetColor("_BaseColor", color);
            else if (fallback.HasProperty("_Color"))
                fallback.color = color;
        }
    }
}
