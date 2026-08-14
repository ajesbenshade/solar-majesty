using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Thin visual helpers: industrial mesh dressing, ghost tints, ground snap, FBX orientation.
    /// </summary>
    public static class ColonyVisualUtility
    {
        private static Shader _lit;
        private static Material _ghostTemplate;
        private static Material _footprintValid;
        private static Material _footprintInvalid;

        public static void EnsureUrpMaterials(GameObject root)
        {
            IndustrialArtDressing.Apply(root);
        }

        public static void ApplyGhostTint(GameObject ghost, bool valid)
        {
            if (ghost == null) return;
            EnsureGhostTemplate();
            Color tint = valid
                ? new Color(0.25f, 1f, 0.45f, 0.38f)
                : new Color(1f, 0.28f, 0.22f, 0.42f);

            var renderers = ghost.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;
                    ForceTransparent(mat);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", tint);
                    if (mat.HasProperty("_Color"))
                        mat.color = tint;
                }
            }
        }

        public static Material GetFootprintMaterial(bool valid)
        {
            EnsureFootprintMaterials();
            return valid ? _footprintValid : _footprintInvalid;
        }

        /// <summary>
        /// Lift/drop an object so its renderer bounds sit on groundY (fixes Blender center pivots).
        /// </summary>
        public static void SnapToGround(GameObject root, float groundY = 0f)
        {
            if (root == null) return;
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null)
                    b.Encapsulate(rends[i].bounds);
            }

            float dy = groundY - b.min.y;
            if (Mathf.Abs(dy) < 0.001f) return;
            root.transform.position += new Vector3(0f, dy, 0f);
        }

        /// <summary>
        /// Parent an FBX/unit prefab under an upright locomotion root.
        /// Import axis correction (-90° X) stays on the visual child so NavMeshAgent
        /// on the root cannot wipe it (which lays bots on their sides).
        /// </summary>
        public static GameObject AttachImportVisual(GameObject prefab, Transform parent)
        {
            if (prefab == null || parent == null) return null;
            var visual = Object.Instantiate(prefab, parent, false);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = prefab.transform.localRotation;
            visual.transform.localScale = Vector3.one;
            return visual;
        }

        /// <summary>
        /// Instantiate an FBX/prefab while keeping Unity's import axis correction (-90° X from Blender),
        /// then apply an optional yaw around world up. Using Quaternion.identity here lays meshes on their side.
        /// Prefer <see cref="AttachImportVisual"/> for units that also get a NavMeshAgent.
        /// </summary>
        public static GameObject InstantiateOriented(
            GameObject prefab,
            Vector3 position,
            Transform parent = null,
            float yawDegrees = 0f)
        {
            if (prefab == null) return null;
            // Apply import orientation first, then yaw around world up (a * b ⇒ b first).
            Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f) * prefab.transform.rotation;
            return Object.Instantiate(prefab, position, rot, parent);
        }

        /// <summary>Apply yaw around world up without wiping the mesh import orientation.</summary>
        public static void SetYawKeepingImport(Transform t, Quaternion importRotation, float yawDegrees)
        {
            if (t == null) return;
            t.rotation = Quaternion.Euler(0f, yawDegrees, 0f) * importRotation;
        }

        /// <summary>
        /// 2-axis-symmetric airlock junction sized in world meters to fill its 2×2 footprint
        /// and overlap neighboring module ports so the campus clicks together.
        /// </summary>
        public static GameObject SpawnPlusConnector(Vector3 position, Transform parent, float worldSpan)
        {
            var root = new GameObject("PlusConnector");
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            float span = Mathf.Max(ColonyLayout.DefaultCellSize * 2f, worldSpan);
            float length = span + 1.7f; // overlap well into neighboring hull sockets
            float diameter = 1.15f;

            SpawnAirlockHub(root.transform);
            CorrugatedArm(root.transform, "Dress_Collar_NS", Vector3.forward, length, diameter);
            CorrugatedArm(root.transform, "Dress_Collar_EW", Vector3.right, length, diameter);

            GameObject prefab = BuildingVisualCatalog.LoadConnector();
            if (prefab != null)
            {
                var a = InstantiateOriented(prefab, position, root.transform, 0f);
                a.name = "Arm_NS";
                FitTubeToSpan(a, length * 0.92f, diameter * 0.85f);
                var b = InstantiateOriented(prefab, position, root.transform, 90f);
                b.name = "Arm_EW";
                FitTubeToSpan(b, length * 0.92f, diameter * 0.85f);
            }

            EnsureUrpMaterials(root);
            SnapToGround(root);
            return root;
        }

        private static void SpawnAirlockHub(Transform parent)
        {
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hub.name = "Dress_AirlockHub";
            hub.transform.SetParent(parent, false);
            hub.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            hub.transform.localScale = new Vector3(1.72f, 1.55f, 1.72f);
            TintPrimitive(hub, new Color(0.88f, 0.89f, 0.91f));

            // Orange edge frame (hollow) — mockup square airlock, not a solid orange box.
            float y = 0.85f;
            OrangeStrip(parent, "Dress_Frame_N", new Vector3(0f, y, 0.88f), new Vector3(1.9f, 1.7f, 0.12f));
            OrangeStrip(parent, "Dress_Frame_S", new Vector3(0f, y, -0.88f), new Vector3(1.9f, 1.7f, 0.12f));
            OrangeStrip(parent, "Dress_Frame_E", new Vector3(0.88f, y, 0f), new Vector3(0.12f, 1.7f, 1.9f));
            OrangeStrip(parent, "Dress_Frame_W", new Vector3(-0.88f, y, 0f), new Vector3(0.12f, 1.7f, 1.9f));

            float yaw = 40f + (parent.position.x + parent.position.z) * 13f;
            HeroBuildingKits.BuildJunctionTurret(parent, new Vector3(0f, 1.68f, 0f), yaw, 0.92f);
        }

        private static void OrangeStrip(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            TintPrimitive(go, new Color(0.96f, 0.42f, 0.08f));
        }

        private static void CorrugatedArm(
            Transform parent, string name, Vector3 axis, float length, float diameter)
        {
            bool ns = Mathf.Abs(axis.z) >= Mathf.Abs(axis.x);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            go.transform.localScale = ns
                ? new Vector3(diameter, diameter, length)
                : new Vector3(length, diameter, diameter);
            TintPrimitive(go, new Color(0.72f, 0.74f, 0.76f));

            int ribs = 5;
            for (int i = 0; i < ribs; i++)
            {
                float t = (i + 0.5f) / ribs - 0.5f;
                var rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rib.name = name + "_Rib";
                rib.transform.SetParent(parent, false);
                rib.transform.localPosition = ns
                    ? new Vector3(0f, 0.85f, t * length * 0.85f)
                    : new Vector3(t * length * 0.85f, 0.85f, 0f);
                rib.transform.localScale = ns
                    ? new Vector3(diameter * 1.12f, diameter * 1.12f, 0.12f)
                    : new Vector3(0.12f, diameter * 1.12f, diameter * 1.12f);
                Object.Destroy(rib.GetComponent<Collider>());
                TintPrimitive(rib, new Color(0.14f, 0.14f, 0.15f));
            }
        }

        private static void TintPrimitive(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            EnsureLitShader();
            if (_lit == null) return;
            var mat = new Material(_lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.32f);
            rend.sharedMaterial = mat;
        }

        private static void FitTubeToSpan(GameObject tube, float length, float diameter)
        {
            if (tube == null) return;
            var rends = tube.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null)
                    b.Encapsulate(rends[i].bounds);
            }

            float sx = diameter / Mathf.Max(0.05f, b.size.x);
            float sy = diameter / Mathf.Max(0.05f, b.size.y);
            float sz = length / Mathf.Max(0.05f, b.size.z);
            Vector3 ls = tube.transform.localScale;
            tube.transform.localScale = new Vector3(ls.x * sx, ls.y * sy, ls.z * sz);
        }

        private static void EnsureLitShader()
        {
            if (_lit != null) return;
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null)
                _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        private static void EnsureGhostTemplate()
        {
            if (_ghostTemplate != null) return;
            EnsureLitShader();
            if (_lit == null) return;
            _ghostTemplate = new Material(_lit) { name = "SM_GhostLit" };
            ForceTransparent(_ghostTemplate);
            _ghostTemplate.SetColor("_BaseColor", new Color(0.3f, 1f, 0.4f, 0.35f));
        }

        private static void EnsureFootprintMaterials()
        {
            if (_footprintValid != null) return;
            EnsureLitShader();
            Shader sh = _lit != null ? _lit : Shader.Find("Sprites/Default");
            _footprintValid = new Material(sh) { name = "SM_FootprintValid" };
            _footprintInvalid = new Material(sh) { name = "SM_FootprintInvalid" };
            ForceTransparent(_footprintValid);
            ForceTransparent(_footprintInvalid);
            if (_footprintValid.HasProperty("_BaseColor"))
            {
                _footprintValid.SetColor("_BaseColor", new Color(0.2f, 1f, 0.45f, 0.28f));
                _footprintInvalid.SetColor("_BaseColor", new Color(1f, 0.25f, 0.2f, 0.32f));
            }
            else
            {
                _footprintValid.color = new Color(0.2f, 1f, 0.45f, 0.28f);
                _footprintInvalid.color = new Color(1f, 0.25f, 0.2f, 0.32f);
            }
        }

        public static void ApplyTransparent(Material mat) => ForceTransparent(mat);

        private static void ForceTransparent(Material mat)
        {
            if (mat == null) return;
            // URP Lit surface type Transparent
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }
}
