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

        /// <summary>
        /// Shared Lego dock axis. Module sleeves, hull ports, and airlock arms must
        /// share this height and bore or the isometric view reads as a miss.
        /// </summary>
        public const float DockY = 1.12f;
        public const float DockBore = 1.42f;

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
        /// Uniform scale so renderer bounds hit targetHeight. Does not use instance IDs.
        /// </summary>
        public static void ScaleToHeight(GameObject root, float targetHeight)
        {
            if (root == null || targetHeight < 0.2f) return;
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = default;
            bool any = false;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || rends[i] is ParticleSystemRenderer) continue;
                if (!any)
                {
                    b = rends[i].bounds;
                    any = true;
                }
                else b.Encapsulate(rends[i].bounds);
            }
            if (!any) return;

            float h = b.size.y;
            if (h < 0.15f) return;
            float s = Mathf.Clamp(targetHeight / h, 0.7f, 6f);
            if (Mathf.Abs(s - 1f) < 0.04f) return;
            root.transform.localScale *= s;
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
        /// White paneled 2×2 square hub. Round white stubs + orange collars only on
        /// docked faces (live arms start hidden). Not a hex, not an orange box.
        /// </summary>
        public static GameObject SpawnPlusConnector(
            Vector3 position, Transform parent, float worldSpan, bool showAllArms = false)
        {
            var root = new GameObject("PlusConnector");
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            float span = Mathf.Max(ColonyLayout.DefaultCellSize * 2f, worldSpan);

            SpawnAirlockHub(root.transform);
            SpawnDockStub(root.transform, "Dress_TubeArm_N", Vector3.forward, span, DockBore, showAllArms);
            SpawnDockStub(root.transform, "Dress_TubeArm_S", Vector3.back, span, DockBore, showAllArms);
            SpawnDockStub(root.transform, "Dress_TubeArm_E", Vector3.right, span, DockBore, showAllArms);
            SpawnDockStub(root.transform, "Dress_TubeArm_W", Vector3.left, span, DockBore, showAllArms);
            // Do not overlay SM_ModularTubeConnector and do not run IndustrialArtDressing
            // here — "airlock" in a mesh name was painting the hub solid orange.

            SnapToGround(root);
            return root;
        }

        private static readonly Color HubWhite = new Color(0.99f, 0.99f, 1f);
        private static readonly Color HubOrange = new Color(0.96f, 0.42f, 0.08f);
        private static readonly Color HubCarbon = new Color(0.12f, 0.13f, 0.14f);
        private static readonly Color HubGraphite = new Color(0.20f, 0.21f, 0.22f);
        private static readonly Color HubCyan = new Color(0.22f, 0.84f, 0.98f);

        private static void SpawnAirlockHub(Transform parent)
        {
            // Stay smaller than the 3 m cell so docked faces have room for a short
            // white tube. A 2.4 m cube filled the cell and read as an orange/white box.
            const float y = 0.96f;
            const float side = 1.68f;
            const float tall = 1.70f;
            float half = side * 0.5f;

            DressCube(parent, "Dress_HubPlinth", new Vector3(0f, 0.08f, 0f),
                new Vector3(side + 0.22f, 0.14f, side + 0.22f), HubCarbon);
            DressCube(parent, "Dress_HubSkirt", new Vector3(0f, 0.18f, 0f),
                new Vector3(side + 0.08f, 0.07f, side + 0.08f), HubGraphite);

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hub.name = "Dress_AirlockHub";
            hub.transform.SetParent(parent, false);
            hub.transform.localPosition = new Vector3(0f, y, 0f);
            hub.transform.localScale = new Vector3(side, tall, side);
            TintPrimitive(hub, HubWhite);

            // Recessed face plates so the square reads paneled, not a flat fridge.
            float inset = 0.035f;
            Vector3[] plates =
            {
                new Vector3(0f, y, half + inset),
                new Vector3(0f, y, -half - inset),
                new Vector3(half + inset, y, 0f),
                new Vector3(-half - inset, y, 0f)
            };
            for (int i = 0; i < 4; i++)
            {
                bool ns = Mathf.Abs(plates[i].z) >= Mathf.Abs(plates[i].x);
                Vector3 plate = ns
                    ? new Vector3(side * 0.72f, tall * 0.72f, 0.04f)
                    : new Vector3(0.04f, tall * 0.72f, side * 0.72f);
                DressCube(parent, "Dress_HubPanel_" + i, plates[i], plate, HubWhite);
            }

            DressCube(parent, "Dress_HubRoof", new Vector3(0f, y + tall * 0.5f + 0.04f, 0f),
                new Vector3(side + 0.08f, 0.07f, side + 0.08f), HubCarbon);
            DressCube(parent, "Dress_HubHatch", new Vector3(0f, y + tall * 0.5f + 0.10f, 0f),
                new Vector3(0.44f, 0.05f, 0.44f), HubGraphite);
            DressCube(parent, "Dress_HubVisor", new Vector3(0f, y + 0.48f, half + 0.06f),
                new Vector3(0.52f, 0.06f, 0.04f), HubCyan);

            float[] cx = { -half + 0.06f, -half + 0.06f, half - 0.06f, half - 0.06f };
            float[] cz = { -half + 0.06f, half - 0.06f, -half + 0.06f, half - 0.06f };
            for (int i = 0; i < 4; i++)
            {
                DressCube(parent, "Dress_HubCorner_" + i,
                    new Vector3(cx[i], y, cz[i]),
                    new Vector3(0.12f, tall + 0.04f, 0.12f), HubCarbon);
            }

            Vector3[] faces =
            {
                new Vector3(0f, 0f, half),
                new Vector3(0f, 0f, -half),
                new Vector3(half, 0f, 0f),
                new Vector3(-half, 0f, 0f)
            };
            for (int i = 0; i < 4; i++)
            {
                bool ns = Mathf.Abs(faces[i].z) >= Mathf.Abs(faces[i].x);
                Vector3 p = faces[i];
                Vector3 door = ns ? new Vector3(0.58f, 0.68f, 0.04f) : new Vector3(0.04f, 0.68f, 0.58f);
                Vector3 hSeam = ns ? new Vector3(side * 0.78f, 0.035f, 0.05f) : new Vector3(0.05f, 0.035f, side * 0.78f);
                Vector3 vSeam = ns ? new Vector3(0.035f, tall * 0.78f, 0.05f) : new Vector3(0.05f, tall * 0.78f, 0.035f);
                DressCube(parent, "Dress_HubDoor_" + i, new Vector3(p.x * 1.01f, DockY, p.z * 1.01f), door, HubCarbon);
                DressCube(parent, "Dress_HubSeamH_" + i, new Vector3(p.x * 1.02f, y + 0.22f, p.z * 1.02f), hSeam, HubCarbon);
                DressCube(parent, "Dress_HubSeamV_" + i, new Vector3(p.x * 1.02f, y, p.z * 1.02f), vSeam, HubCarbon);
            }

            float yaw = 40f + (parent.position.x + parent.position.z) * 13f;
            HeroBuildingKits.BuildJunctionTurret(parent, new Vector3(0f, y + tall * 0.5f + 0.10f, 0f), yaw, 0.72f);
        }

        /// <summary>
        /// Round white stub from the square hub to one cell face. Orange collar at the joint only.
        /// Live unused faces stay off so they do not read as orange stubs.
        /// </summary>
        private static void SpawnDockStub(
            Transform parent, string name, Vector3 axis, float cellSpan, float diameter, bool startActive)
        {
            Vector3 dir = axis.normalized;
            const float y = DockY;
            // Hub half is 0.84 m — leave a visible white corridor to the 1.5 m cell face.
            const float hubClear = 0.86f;
            float face = cellSpan * 0.5f;
            float stubLen = Mathf.Max(0.36f, face - hubClear);
            Vector3 mid = dir * (hubClear + stubLen * 0.5f) + new Vector3(0f, y, 0f);
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);

            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;

            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = name + "_Tube";
            tube.transform.SetParent(group.transform, false);
            tube.transform.localPosition = mid;
            tube.transform.localRotation = rot;
            tube.transform.localScale = new Vector3(diameter, stubLen * 0.5f, diameter);
            Object.Destroy(tube.GetComponent<Collider>());
            TintPrimitive(tube, HubWhite);

            var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rib.name = name + "_Rib";
            rib.transform.SetParent(group.transform, false);
            rib.transform.localPosition = mid;
            rib.transform.localRotation = rot;
            rib.transform.localScale = new Vector3(diameter * 1.08f, 0.04f, diameter * 1.08f);
            Object.Destroy(rib.GetComponent<Collider>());
            TintPrimitive(rib, HubCarbon);

            // Gasket at the hub, not a second orange ring at the cell face — that
            // ring sat off the round hull and read as a missed port in isometric.
            Vector3 gasketPos = dir * (hubClear + 0.03f) + new Vector3(0f, y, 0f);
            var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collar.name = name + "_Collar";
            collar.transform.SetParent(group.transform, false);
            collar.transform.localPosition = gasketPos;
            collar.transform.localRotation = rot;
            collar.transform.localScale = new Vector3(diameter * 1.12f, 0.04f, diameter * 1.12f);
            Object.Destroy(collar.GetComponent<Collider>());
            TintPrimitive(collar, HubOrange);

            if (!startActive)
                group.SetActive(false);
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
