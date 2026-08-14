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
            float diameter = 1.25f;
            float height = 1.15f;

            CubeArm(root.transform, "Collar_NS", new Vector3(diameter, height, length));
            CubeArm(root.transform, "Collar_EW", new Vector3(length, height, diameter));

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

        private static void CubeArm(Transform parent, string name, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
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
