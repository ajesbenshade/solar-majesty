using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Paints the colony's team colour on the live-play pieces that still wear the shared
    /// safety orange: hired heroes and classes (not the engineer), military units, flags,
    /// rally and selection markers, and construction ghosts.
    ///
    /// The colour is the civ's second colour, the same colour a castle paints on its TeamColour
    /// slot (<see cref="CivAccents.CivColour"/>). A player accent is never used. The neutral civ
    /// (guided Earth start, old saves) keeps today's orange.
    ///
    /// Scout drones, tax collectors and companions are a separate stripe and are not touched here.
    /// Engineers keep the authored orange slot: that is intentional, and this pass does not
    /// override it. Castle slots and the procedural Commons are not touched.
    /// </summary>
    public static class CivTeamMark
    {
        public const string MarkName = "TeamMark";

        /// <summary>Material names start with this so <see cref="IndustrialArtDressing"/> leaves them alone.</summary>
        public const string MaterialPrefix = "SM_Art_TeamMark_";

        private const string GhostCopyPrefix = "SM_GhostCopy_";

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _lit = null;
        }

        /// <summary>
        /// Hired classes and military units wear a team mark. Scouts are painted elsewhere.
        /// Engineers keep today's orange slot.
        /// </summary>
        public static bool Wears(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone:
                case SpecialistClass.EngineerBot:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>The team colour for <paramref name="civ"/>. Neutral and unknown ids stay today's orange.</summary>
        public static Color Colour(CivId civ)
        {
            if (civ == CivId.None) return KitOrange();
            return CivAccents.CivColour(civ) ?? KitOrange();
        }

        /// <summary>As <see cref="Colour(CivId)"/> for the owner that is playing. Null is the neutral orange.</summary>
        public static Color Colour(CivModifiers civ)
        {
            if (civ == null || civ.Id == CivId.None) return KitOrange();
            return CivAccents.CivColour(civ.Id) ?? civ.TeamSecondary;
        }

        /// <summary>The team colour of the civ currently bound for building and marker spawn.</summary>
        public static Color ColourBound() => Colour(ModularBuildingFactory.BoundCiv);

        /// <summary>
        /// <paramref name="neutralToday"/> when the owner is the neutral civ, otherwise the team
        /// colour with the same alpha. Selection rings pass today's orange; a flag's select ring
        /// passes today's gold.
        /// </summary>
        public static Color Owned(CivModifiers civ, Color neutralToday)
        {
            if (civ == null || civ.Id == CivId.None) return neutralToday;
            Color c = Colour(civ);
            c.a = neutralToday.a;
            return c;
        }

        /// <summary>
        /// Retint the orange slots and add a band so the civ reads from the play camera.
        /// A neutral owner keeps the authored orange and grows no band. Scouts and engineers
        /// are left exactly as they are.
        /// </summary>
        public static void ApplyToHero(GameObject root, SpecialistClass cls, CivModifiers civ)
        {
            if (root == null || !Wears(cls)) return;
            if (civ == null || civ.Id == CivId.None) return;

            Color c = Colour(civ);
            PaintOrangeSlots(root, c);
            if (Find(root.transform) == null && TryBounds(root.transform, out Bounds bounds))
            {
                float diameter = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 1.12f, 0.5f, 2.8f);
                AttachDisc(root.transform, bounds.center, diameter, 0.05f, c, shadows: true);
            }
        }

        /// <summary>A selection or rally marker. Neutral keeps <paramref name="neutralToday"/>.</summary>
        public static void PaintMarker(Renderer rend, CivModifiers civ, Color neutralToday)
        {
            if (rend == null) return;
            bool named = civ != null && civ.Id != CivId.None;
            rend.sharedMaterial = MarkerMaterial(Owned(civ, neutralToday), glow: named);
        }

        /// <summary>
        /// A rim at the foot of a bounty flag so the colony reads without replacing the banner
        /// colour (that still says what kind of flag it is). A neutral flag grows no rim.
        /// </summary>
        public static void AttachFlagRim(Transform flag, CivModifiers civ)
        {
            if (flag == null || civ == null || civ.Id == CivId.None) return;
            if (Find(flag) != null) return;

            Vector3 at = flag.position;
            var rend = flag.GetComponentInChildren<Renderer>();
            if (rend != null && rend.gameObject.name != MarkName)
                at.y = rend.bounds.min.y + 0.04f;
            AttachDisc(flag, at, 1.15f, 0.04f, Colour(civ), shadows: false);
        }

        /// <summary>
        /// Placement ghost. A named civ retints orange slots (so the trim stops glowing the old
        /// orange) and adds a rim. The rest of the ghost stays the valid/invalid tint. A neutral
        /// ghost is tinted exactly as it is today and grows no rim.
        /// </summary>
        public static void PaintGhost(GameObject ghost, bool valid, CivId civ)
        {
            if (ghost == null) return;
            bool named = civ != CivId.None;
            Color validity = valid
                ? new Color(0.25f, 1f, 0.45f, 0.38f)
                : new Color(1f, 0.28f, 0.22f, 0.42f);
            Color team = Colour(civ);

            if (named && Find(ghost.transform) == null && TryBounds(ghost.transform, out Bounds bounds))
            {
                float diameter = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 1.04f, 1.2f, 40f);
                var at = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
                AttachDisc(ghost.transform, at, diameter, 0.05f, team, shadows: false);
            }

            var renderers = ghost.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                if (rend == null || rend.gameObject.name == MarkName) continue;
                TintGhost(rend, validity, named ? team : (Color?)null);
            }
        }

        /// <summary>Base colour of a material made here, or of a shared slot.</summary>
        public static Color BaseColour(Material m)
        {
            if (m == null) return Color.clear;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
        }

        /// <summary>True for the shared orange slot (by name) or a part painted in today's kit orange.</summary>
        public static bool IsKitOrange(Material mat)
        {
            if (mat == null || string.IsNullOrEmpty(mat.name)) return false;
            if (mat.name.StartsWith(MaterialPrefix, System.StringComparison.Ordinal)) return false;
            if (mat.name.StartsWith(GhostCopyPrefix, System.StringComparison.Ordinal) &&
                mat.name.IndexOf("orange", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (mat.name.IndexOf("orange", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            Color c = BaseColour(mat);
            return c.a > 0.01f && CivPalette.Distance(c, KitOrange()) < 0.08f;
        }

        private static Color KitOrange()
        {
            var neutral = CivCatalog.Get(CivId.None);
            return neutral != null ? neutral.teamSecondary : new Color(0.96f, 0.42f, 0.08f);
        }

        private static void PaintOrangeSlots(GameObject root, Color c)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                if (rend == null || rend.gameObject.name == MarkName) continue;
                if (SkipOverlay(rend.gameObject.name)) continue;
                var mats = rend.sharedMaterials;
                int count = mats != null ? mats.Length : 0;
                for (int m = 0; m < count; m++)
                {
                    if (!IsKitOrange(mats[m])) continue;
                    var block = new MaterialPropertyBlock();
                    if (count <= 1) rend.GetPropertyBlock(block);
                    else rend.GetPropertyBlock(block, m);
                    block.SetColor("_BaseColor", c);
                    block.SetColor("_Color", c);
                    // The orange slot glows. Keep that glow in the team colour, or the lamp
                    // still reads as the old orange.
                    block.SetColor("_EmissionColor", new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f));
                    if (count <= 1) rend.SetPropertyBlock(block);
                    else rend.SetPropertyBlock(block, m);
                }
            }
        }

        private static bool SkipOverlay(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("SelectRing") || name.Contains("StatusOrb") ||
                   name.Contains("Label") || name.Contains("SelectProxy");
        }

        private static void TintGhost(Renderer rend, Color validity, Color? team)
        {
            var src = rend.sharedMaterials;
            int count = src != null ? src.Length : 0;
            if (count == 0) return;
            var next = new Material[count];
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                var mat = src[i];
                if (mat == null)
                {
                    next[i] = null;
                    continue;
                }
                bool orange = team.HasValue && IsKitOrange(mat);
                Color tint = orange
                    ? new Color(team.Value.r, team.Value.g, team.Value.b, validity.a)
                    : validity;
                if (mat.name.StartsWith(GhostCopyPrefix, System.StringComparison.Ordinal))
                {
                    ApplyGhostColour(mat, tint, orange ? team : null);
                    next[i] = mat;
                }
                else
                {
                    var copy = new Material(mat) { name = GhostCopyPrefix + mat.name };
                    ApplyGhostColour(copy, tint, orange ? team : null);
                    next[i] = copy;
                    changed = true;
                }
            }
            if (changed) rend.sharedMaterials = next;
        }

        private static void ApplyGhostColour(Material mat, Color tint, Color? emission)
        {
            ColonyVisualUtility.ApplyTransparent(mat);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (!emission.HasValue || !mat.HasProperty("_EmissionColor")) return;
            Color e = emission.Value;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(e.r * 0.55f, e.g * 0.55f, e.b * 0.55f, 1f));
        }

        private static bool TryBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null || rend is ParticleSystemRenderer) continue;
                if (rend.gameObject.name == MarkName || SkipOverlay(rend.gameObject.name)) continue;
                if (!any)
                {
                    bounds = rend.bounds;
                    any = true;
                }
                else bounds.Encapsulate(rend.bounds);
            }
            return any;
        }

        private static void AttachDisc(Transform parent, Vector3 worldCenter, float worldDiameter, float worldThickness, Color c, bool shadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = MarkName;
            ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldCenter, Quaternion.identity);
            Vector3 s = parent.lossyScale;
            float sx = Mathf.Abs(s.x) > 0.0001f ? s.x : 1f;
            float sy = Mathf.Abs(s.y) > 0.0001f ? s.y : 1f;
            float sz = Mathf.Abs(s.z) > 0.0001f ? s.z : 1f;
            // Unity's cylinder is 1 unit across and 2 units tall.
            go.transform.localScale = new Vector3(
                worldDiameter / sx,
                worldThickness / (2f * sy),
                worldDiameter / sz);
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = MarkerMaterial(c, glow: true);
            rend.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            rend.receiveShadows = shadows;
        }

        private static Transform Find(Transform root)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == MarkName) return all[i];
            return null;
        }

        private static Material MarkerMaterial(Color c, bool glow)
        {
            string key = (glow ? "g" : "f") + ColorUtility.ToHtmlStringRGBA(c);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            if (_lit == null)
                _lit = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                       ?? Shader.Find("Standard");
            var m = new Material(_lit) { name = MaterialPrefix + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.42f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.08f);
            if (glow && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = m;
            return m;
        }
    }
}
