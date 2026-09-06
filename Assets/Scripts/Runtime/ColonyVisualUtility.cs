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
        /// White tube hits the orange collar at this Y / diameter — no step, no gap.
        /// </summary>
        public const float DockY = 1.12f;
        public const float DockBore = 1.42f;
        /// <summary>Orange ring sits this far outside the hull so the tube meets it flush.</summary>
        public const float DockCollarOut = 0.03f;

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
        /// Rotate so the thinnest world AABB axis becomes up — seats Copilot rocks with a
        /// flat cut face down instead of standing that face vertical.
        /// </summary>
        public static void SeatFlatOnGround(GameObject root)
        {
            if (root == null) return;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (!TryWorldBounds(root, out Bounds b)) return;
                Vector3 e = b.size;
                // Already sitting flat (height is the thin axis).
                if (e.y <= e.x * 1.08f && e.y <= e.z * 1.08f)
                    return;

                if (e.x <= e.z)
                    root.transform.rotation = Quaternion.Euler(0f, 0f, 90f) * root.transform.rotation;
                else
                    root.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * root.transform.rotation;
            }
        }

        private static bool TryWorldBounds(GameObject root, out Bounds b)
        {
            b = default;
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return false;
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
            return any;
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

        /// <summary>White paneled 2×2 hub side in meters. Must stay under the 3 m cell.</summary>
        public const float AirlockHubSide = 1.56f;

        /// <summary>
        /// White paneled 2×2 square hub (cell-safe <see cref="AirlockHubSide"/>).
        /// Round white stubs + one orange collar only on docked faces (live arms
        /// start hidden). Not a hex, not a wrap-around carbon/orange door box —
        /// still16 read as a dark rectangular joint because Dress_HubDoor covered
        /// the white plates. RefreshTubes never stacks a fourth CampusTubeRoot
        /// corridor in the HAB gap.
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
        private static readonly Color HubCarbon = new Color(0.10f, 0.11f, 0.12f);
        private static readonly Color HubGraphite = new Color(0.20f, 0.21f, 0.22f);
        private static readonly Color HubCyan = new Color(0.22f, 0.84f, 0.98f);
        /// <summary>Hold sheet-white at Game-tab distance so the 2×2 does not flatten into dirt.</summary>
        private static readonly Color HubWhiteEmit = new Color(0.34f, 0.34f, 0.36f);
        private static readonly Color HubOrangeEmit = new Color(0.55f, 0.16f, 0.02f);

        private static void SpawnAirlockHub(Transform parent)
        {
            // Stay smaller than the 3 m cell so docked faces have room for a short
            // white tube. Wrap-around carbon doors (0.92 m) painted the hub as the
            // still16 dark box — unused faces are clean white plates now.
            const float y = 0.96f;
            const float side = AirlockHubSide;
            const float tall = 1.52f;
            float half = side * 0.5f;

            DressCube(parent, "Dress_HubPlinth", new Vector3(0f, 0.06f, 0f),
                new Vector3(side + 0.10f, 0.10f, side + 0.10f), HubGraphite);
            DressCube(parent, "Dress_HubSkirt", new Vector3(0f, 0.14f, 0f),
                new Vector3(side + 0.04f, 0.05f, side + 0.04f), HubWhite, HubWhiteEmit);

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hub.name = "Dress_AirlockHub";
            hub.transform.SetParent(parent, false);
            hub.transform.localPosition = new Vector3(0f, y, 0f);
            hub.transform.localScale = new Vector3(side, tall, side);
            TintPrimitive(hub, HubWhite, HubWhiteEmit);

            // Recessed face plates so the square reads paneled, not a flat fridge.
            // No Dress_HubDoor — those wrap plates ate the white language.
            float inset = 0.03f;
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
                    ? new Vector3(side * 0.82f, tall * 0.78f, 0.045f)
                    : new Vector3(0.045f, tall * 0.78f, side * 0.82f);
                DressCube(parent, "Dress_HubPanel_" + i, plates[i], plate, HubWhite, HubWhiteEmit);

                Vector3 p = plates[i];
                Vector3 hSeam = ns
                    ? new Vector3(side * 0.78f, 0.048f, 0.07f)
                    : new Vector3(0.07f, 0.048f, side * 0.78f);
                Vector3 vSeam = ns
                    ? new Vector3(0.048f, tall * 0.74f, 0.07f)
                    : new Vector3(0.07f, tall * 0.74f, 0.048f);
                DressCube(parent, "Dress_HubSeamH_" + i,
                    new Vector3(p.x * 1.05f, y + 0.16f, p.z * 1.05f), hSeam, HubCarbon);
                DressCube(parent, "Dress_HubSeamV_" + i,
                    new Vector3(p.x * 1.05f, y, p.z * 1.05f), vSeam, HubCarbon);

                // Small inset hatch — panel language, not a wrap door / unused stub.
                Vector3 hatch = ns
                    ? new Vector3(0.28f, 0.28f, 0.03f)
                    : new Vector3(0.03f, 0.28f, 0.28f);
                DressCube(parent, "Dress_HubInset_" + i,
                    new Vector3(p.x * 1.04f, y - 0.18f, p.z * 1.04f), hatch, HubCarbon);
            }

            DressCube(parent, "Dress_HubRoof", new Vector3(0f, y + tall * 0.5f + 0.035f, 0f),
                new Vector3(side + 0.04f, 0.06f, side + 0.04f), HubWhite, HubWhiteEmit);
            DressCube(parent, "Dress_HubHatch", new Vector3(0f, y + tall * 0.5f + 0.09f, 0f),
                new Vector3(0.36f, 0.045f, 0.36f), HubCarbon);
            DressCube(parent, "Dress_HubVisor", new Vector3(0f, y + 0.42f, half + 0.055f),
                new Vector3(0.48f, 0.055f, 0.035f), HubCyan);

            float[] cx = { -half + 0.05f, -half + 0.05f, half - 0.05f, half - 0.05f };
            float[] cz = { -half + 0.05f, half - 0.05f, -half + 0.05f, half - 0.05f };
            for (int i = 0; i < 4; i++)
            {
                DressCube(parent, "Dress_HubCorner_" + i,
                    new Vector3(cx[i], y, cz[i]),
                    new Vector3(0.09f, tall + 0.02f, 0.09f), HubCarbon);
            }

            float yaw = 40f + (parent.position.x + parent.position.z) * 13f;
            HeroBuildingKits.BuildJunctionTurret(parent, new Vector3(0f, y + tall * 0.5f + 0.09f, 0f), yaw, 0.68f);
        }

        /// <summary>
        /// Round white stub from the square hub to one cell face. One orange collar
        /// at the Lego face (the docked joint). Live unused faces stay off so they
        /// do not read as dark unused stubs.
        /// </summary>
        private static void SpawnDockStub(
            Transform parent, string name, Vector3 axis, float cellSpan, float diameter, bool startActive)
        {
            Vector3 dir = axis.normalized;
            const float y = DockY;
            float hubClear = AirlockHubSide * 0.5f + 0.03f;
            float face = cellSpan * 0.5f;
            float stubLen = Mathf.Max(0.40f, face - hubClear);
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
            TintPrimitive(tube, HubWhite, HubWhiteEmit);

            var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rib.name = name + "_Rib";
            rib.transform.SetParent(group.transform, false);
            rib.transform.localPosition = mid;
            rib.transform.localRotation = rot;
            rib.transform.localScale = new Vector3(diameter * 1.06f, 0.035f, diameter * 1.06f);
            Object.Destroy(rib.GetComponent<Collider>());
            TintPrimitive(rib, HubCarbon);

            // White lip at the hub so the tube reads as attached, not a dark slot.
            Vector3 lipPos = dir * (hubClear + 0.02f) + new Vector3(0f, y, 0f);
            var lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lip.name = name + "_Lip";
            lip.transform.SetParent(group.transform, false);
            lip.transform.localPosition = lipPos;
            lip.transform.localRotation = rot;
            lip.transform.localScale = new Vector3(diameter * 1.05f, 0.03f, diameter * 1.05f);
            Object.Destroy(lip.GetComponent<Collider>());
            TintPrimitive(lip, HubWhite, HubWhiteEmit);

            // Proud orange ring on the hub face — still18 hid the Lego-face sliver
            // in the HAB join. Child of the arm so unused faces stay clean plates.
            Vector3 hubRingPos = dir * (hubClear + 0.03f) + new Vector3(0f, y, 0f);
            var hubRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hubRing.name = name + "_HubCollar";
            hubRing.transform.SetParent(group.transform, false);
            hubRing.transform.localPosition = hubRingPos;
            hubRing.transform.localRotation = rot;
            hubRing.transform.localScale = new Vector3(diameter * 1.38f, 0.085f, diameter * 1.38f);
            Object.Destroy(hubRing.GetComponent<Collider>());
            TintPrimitive(hubRing, HubOrange, HubOrangeEmit);

            // One orange collar at the cell face — the docked Lego joint.
            Vector3 collarPos = dir * (face - 0.04f) + new Vector3(0f, y, 0f);
            var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collar.name = name + "_Collar";
            collar.transform.SetParent(group.transform, false);
            collar.transform.localPosition = collarPos;
            collar.transform.localRotation = rot;
            collar.transform.localScale = new Vector3(diameter * 1.32f, 0.08f, diameter * 1.32f);
            Object.Destroy(collar.GetComponent<Collider>());
            TintPrimitive(collar, HubOrange, HubOrangeEmit);

            if (!startActive)
                group.SetActive(false);
        }

        /// <summary>
        /// Orange collar + graphite well on a hull face at DockY / DockBore.
        /// Group name is the toggle prefix (HabPort_N, CommonsPort_E, …).
        /// Live groups start hidden — unused cardinals were the still5 orange rings.
        /// RefreshTubes enables docked faces only (white sleeve + one collar).
        /// </summary>
        public static GameObject PlaceHullPort(
            Transform parent, string name, Vector3 outward, float hullDist, bool startActive = false)
        {
            Vector3 dir = outward.normalized;
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            Vector3 at = dir * hullDist + new Vector3(0f, DockY, 0f);

            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;

            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            well.name = name + "_Well";
            well.transform.SetParent(group.transform, false);
            well.transform.localPosition = at - dir * 0.04f;
            well.transform.localRotation = rot;
            well.transform.localScale = new Vector3(DockBore * 0.92f, 0.07f, DockBore * 0.92f);
            Object.Destroy(well.GetComponent<Collider>());
            TintPrimitive(well, HubGraphite);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = name + "_Ring";
            ring.transform.SetParent(group.transform, false);
            ring.transform.localPosition = at + dir * DockCollarOut;
            ring.transform.localRotation = rot;
            ring.transform.localScale = new Vector3(DockBore * 1.18f, 0.045f, DockBore * 1.18f);
            Object.Destroy(ring.GetComponent<Collider>());
            TintPrimitive(ring, HubOrange);

            if (!startActive)
                group.SetActive(false);
            return group;
        }

        private static void DressCube(
            Transform parent, string name, Vector3 pos, Vector3 scale, Color color, Color emission = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            TintPrimitive(go, color, emission);
        }

        private static void TintPrimitive(GameObject go, Color color, Color emission = default)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            EnsureLitShader();
            if (_lit == null) return;
            var mat = new Material(_lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.38f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.06f);
            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
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
