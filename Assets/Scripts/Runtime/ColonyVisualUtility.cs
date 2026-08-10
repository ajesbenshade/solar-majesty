using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Thin greybox helpers: URP Lit remap (avoid pink FBX mats) + translucent ghost mats.
    /// </summary>
    public static class ColonyVisualUtility
    {
        private static Shader _lit;
        private static Material _ghostTemplate;
        private static Material _footprintValid;
        private static Material _footprintInvalid;

        private static readonly Color WhiteShell = new Color(0.82f, 0.84f, 0.86f);
        private static readonly Color BlackBand = new Color(0.06f, 0.06f, 0.07f);
        private static readonly Color Graphite = new Color(0.22f, 0.23f, 0.24f);
        private static readonly Color OrangeAccent = new Color(0.95f, 0.42f, 0.08f);
        private static readonly Color Steel = new Color(0.48f, 0.5f, 0.53f);

        public static void EnsureUrpMaterials(GameObject root)
        {
            if (root == null) return;
            EnsureLitShader();
            if (_lit == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                var mats = rend.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                var next = new Material[mats.Length];
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    var src = mats[m];
                    if (src == null || IsBrokenOrBuiltin(src))
                    {
                        next[m] = CreatePaletteMaterial(GuessPalette(src != null ? src.name : rend.name));
                        changed = true;
                    }
                    else if (src.shader != _lit && !src.shader.name.Contains("Universal Render Pipeline"))
                    {
                        // Keep albedo tint when remapping into URP Lit.
                        Color c = WhiteShell;
                        if (src.HasProperty("_Color")) c = src.color;
                        else if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                        next[m] = CreateLit(c, src.name + "_URP");
                        changed = true;
                    }
                    else
                    {
                        next[m] = src;
                    }
                }

                if (changed)
                    rend.sharedMaterials = next;
            }
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

        private static bool IsBrokenOrBuiltin(Material mat)
        {
            if (mat.shader == null) return true;
            string n = mat.shader.name;
            return n == "Hidden/InternalErrorShader" ||
                   n.Contains("Error") ||
                   n.StartsWith("Standard") ||
                   n.StartsWith("Legacy");
        }

        private static Color GuessPalette(string name)
        {
            if (string.IsNullOrEmpty(name)) return WhiteShell;
            string n = name.ToLowerInvariant();
            if (n.Contains("orange")) return OrangeAccent;
            if (n.Contains("black")) return BlackBand;
            if (n.Contains("graphite") || n.Contains("grey") || n.Contains("gray")) return Graphite;
            if (n.Contains("steel") || n.Contains("metal")) return Steel;
            return WhiteShell;
        }

        private static Material CreatePaletteMaterial(Color color) =>
            CreateLit(color, "SM_Palette");

        private static Material CreateLit(Color color, string name)
        {
            EnsureLitShader();
            if (_lit == null) return null;
            var mat = new Material(_lit) { name = name };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.35f);
            return mat;
        }

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
