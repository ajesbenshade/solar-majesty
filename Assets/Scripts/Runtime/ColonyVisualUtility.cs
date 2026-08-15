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
            float diameter = 1.38f;

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

        private static readonly Color HubWhite = new Color(0.88f, 0.89f, 0.91f);
        private static readonly Color HubOrange = new Color(0.96f, 0.42f, 0.08f);
        private static readonly Color HubCarbon = new Color(0.12f, 0.13f, 0.14f);
        private static readonly Color HubGraphite = new Color(0.20f, 0.21f, 0.22f);
        private static readonly Color HubCyan = new Color(0.22f, 0.84f, 0.98f);

        private static void SpawnAirlockHub(Transform parent)
        {
            const float y = 0.88f;
            DressCube(parent, "Dress_HubPlinth", new Vector3(0f, 0.10f, 0f), new Vector3(1.95f, 0.18f, 1.95f), HubCarbon);
            DressCube(parent, "Dress_HubSkirt", new Vector3(0f, 0.24f, 0f), new Vector3(1.82f, 0.10f, 1.82f), HubGraphite);

            // Click volume stays on the white hull. Square airlock — not a hex hub.
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hub.name = "Dress_AirlockHub";
            hub.transform.SetParent(parent, false);
            hub.transform.localPosition = new Vector3(0f, y, 0f);
            hub.transform.localScale = new Vector3(1.58f, 1.42f, 1.58f);
            TintPrimitive(hub, HubWhite);

            DressCube(parent, "Dress_HubRoof", new Vector3(0f, 1.62f, 0f), new Vector3(1.68f, 0.08f, 1.68f), HubCarbon);
            DressCube(parent, "Dress_HubHatch", new Vector3(0f, 1.70f, 0f), new Vector3(0.55f, 0.08f, 0.55f), HubOrange);
            DressCube(parent, "Dress_HubVisor", new Vector3(0f, 1.58f, 0.72f), new Vector3(0.62f, 0.08f, 0.06f), HubCyan);

            float[] cx = { -0.72f, -0.72f, 0.72f, 0.72f };
            float[] cz = { -0.72f, 0.72f, -0.72f, 0.72f };
            for (int i = 0; i < 4; i++)
            {
                DressCube(parent, "Dress_HubCorner_" + i,
                    new Vector3(cx[i], y, cz[i]),
                    new Vector3(0.16f, 1.48f, 0.16f), HubCarbon);
            }

            Vector3[] faces =
            {
                new Vector3(0f, 0f, 0.80f),
                new Vector3(0f, 0f, -0.80f),
                new Vector3(0.80f, 0f, 0f),
                new Vector3(-0.80f, 0f, 0f)
            };
            for (int i = 0; i < 4; i++)
            {
                bool ns = Mathf.Abs(faces[i].z) >= Mathf.Abs(faces[i].x);
                Vector3 p = faces[i];
                Vector3 panel = ns ? new Vector3(1.12f, 1.12f, 0.06f) : new Vector3(0.06f, 1.12f, 1.12f);
                Vector3 lip = ns ? new Vector3(0.78f, 0.88f, 0.04f) : new Vector3(0.04f, 0.88f, 0.78f);
                Vector3 door = ns ? new Vector3(0.62f, 0.72f, 0.10f) : new Vector3(0.10f, 0.72f, 0.62f);
                Vector3 seam = ns ? new Vector3(1.20f, 0.05f, 0.08f) : new Vector3(0.08f, 0.05f, 1.20f);
                DressCube(parent, "Dress_HubPanel_" + i, new Vector3(p.x, y, p.z), panel, HubGraphite);
                DressCube(parent, "Dress_HubLip_" + i, new Vector3(p.x * 1.04f, y, p.z * 1.04f), lip, HubWhite);
                DressCube(parent, "Dress_HubDoor_" + i, new Vector3(p.x * 1.06f, y - 0.06f, p.z * 1.06f), door, HubOrange);
                DressCube(parent, "Dress_HubSeam_" + i, new Vector3(p.x * 0.95f, y + 0.28f, p.z * 0.95f), seam, HubCarbon);
            }

            // Orange edge frame (hollow) — mockup square airlock, not a solid orange box.
            OrangeStrip(parent, "Dress_Frame_N", new Vector3(0f, y, 0.92f), new Vector3(1.92f, 1.62f, 0.10f));
            OrangeStrip(parent, "Dress_Frame_S", new Vector3(0f, y, -0.92f), new Vector3(1.92f, 1.62f, 0.10f));
            OrangeStrip(parent, "Dress_Frame_E", new Vector3(0.92f, y, 0f), new Vector3(0.10f, 1.62f, 1.92f));
            OrangeStrip(parent, "Dress_Frame_W", new Vector3(-0.92f, y, 0f), new Vector3(0.10f, 1.62f, 1.92f));

            float yaw = 40f + (parent.position.x + parent.position.z) * 13f;
            HeroBuildingKits.BuildJunctionTurret(parent, new Vector3(0f, 1.78f, 0f), yaw, 0.92f);
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
            TintPrimitive(go, new Color(0.82f, 0.84f, 0.86f));

            int ribs = 7;
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
                    ? new Vector3(diameter * 1.14f, diameter * 1.14f, 0.12f)
                    : new Vector3(0.12f, diameter * 1.14f, diameter * 1.14f);
                Object.Destroy(rib.GetComponent<Collider>());
                bool orangeRing = i % 3 == 1;
                TintPrimitive(rib, orangeRing
                    ? new Color(0.96f, 0.42f, 0.08f)
                    : new Color(0.20f, 0.21f, 0.22f));
            }

            for (int e = -1; e <= 1; e += 2)
            {
                Vector3 collarPos = ns
                    ? new Vector3(0f, 0.85f, e * length * 0.46f)
                    : new Vector3(e * length * 0.46f, 0.85f, 0f);
                Vector3 collarScale = ns
                    ? new Vector3(diameter * 1.22f, diameter * 1.22f, 0.10f)
                    : new Vector3(0.10f, diameter * 1.22f, diameter * 1.22f);
                DressCube(parent, name + "_Collar", collarPos, collarScale, new Color(0.96f, 0.42f, 0.08f));
            }
        }

        private static void DressCube(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            TintPrimitive(go, color);
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
