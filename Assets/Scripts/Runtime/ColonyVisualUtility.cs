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

        public static void DestroyNow(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

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

            // Planet architecture (berms, skirts) is set into the ground on purpose; seat on the hull.
            Bounds b = default;
            bool any = false;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || rends[i].name.StartsWith("Dress_Arch_")) continue;
                if (!any) { b = rends[i].bounds; any = true; }
                else b.Encapsulate(rends[i].bounds);
            }
            if (!any) return;

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
            if (prefab.name.StartsWith("SM_Unit_"))
                UnitClipPlayer.Bind(visual, prefab.name);
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
