using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Paints the colony's team stripe: the civ's second colour, the same colour
    /// <see cref="CivCastleLoader"/> puts on a castle's TeamColour slot
    /// (<see cref="CivAccents.TeamStripe(CivModifiers)"/>).
    ///
    /// Scout drones already have an orange slot (hip mark and beacon). That slot is retinted,
    /// and a chest band is added so the colour reads from the play camera. Tax collectors and
    /// companions have no such slot, so they get the same band. Other units and buildings are
    /// left alone. The neutral civ keeps a scout's authored orange (that is already the stripe)
    /// and paints collectors and companions with today's orange.
    /// A player accent is never used.
    /// </summary>
    public static class CivTeamStripe
    {
        public const string StripeName = "TeamStripe";

        /// <summary>Material names start with this so <see cref="IndustrialArtDressing"/> leaves them alone.</summary>
        public const string MaterialPrefix = "SM_Art_TeamStripe_";

        // Slim scout cuirass sits near y 1.43 and is about 0.44 wide. A disc a little wider
        // shows a rim past the chest from the isometric camera.
        private static readonly Vector3 ScoutBandLocal = new Vector3(0f, 1.36f, 0f);
        private static readonly Vector3 ScoutBandScale = new Vector3(0.72f, 0.045f, 0.72f);

        // Collector body is a 0.62-wide sphere. The gold skirt sits at y -0.2; this rim is the equator.
        private static readonly Vector3 CollectorBandLocal = new Vector3(0f, 0.08f, 0f);
        private static readonly Vector3 CollectorBandScale = new Vector3(0.78f, 0.04f, 0.78f);

        // Companion body is a unit capsule (radius 0.5) or unit cube. The band is in that local space,
        // then the companion's own scale carries it.
        private static readonly Vector3 CompanionBandLocal = new Vector3(0f, 0.35f, 0f);
        private static readonly Vector3 CompanionBandScale = new Vector3(1.4f, 0.1f, 1.4f);

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit;

        /// <summary>
        /// Scout drones only. Other classes keep their orange. The neutral civ keeps the authored
        /// orange slot, which is already today's stripe, and grows no extra band.
        /// </summary>
        public static void ApplyToUnit(GameObject root, SpecialistClass cls, CivModifiers civ)
        {
            if (root == null || cls != SpecialistClass.ScoutDrone) return;
            if (civ == null || civ.Id == CivId.None) return;

            Color c = CivAccents.TeamStripe(civ);
            PaintOrangeSlots(root, c);
            if (Find(root.transform) == null)
                Attach(root.transform, ScoutBandLocal, ScoutBandScale, c);
        }

        /// <summary>The tax collector's team rim. The gold skirt and eye stay as they are.</summary>
        public static void AttachToCollector(Transform drone, CivModifiers civ)
        {
            if (drone == null || Find(drone) != null) return;
            Attach(drone, CollectorBandLocal, CollectorBandScale, CivAccents.TeamStripe(civ));
        }

        /// <summary>A companion's team rim. The body keeps the companion's own colour.</summary>
        public static void AttachToCompanion(Transform body, CivModifiers civ)
        {
            if (body == null || Find(body) != null) return;
            Attach(body, CompanionBandLocal, CompanionBandScale, CivAccents.TeamStripe(civ));
        }

        /// <summary>Base colour of a stripe material made here.</summary>
        public static Color BaseColour(Material m)
        {
            if (m == null) return Color.clear;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
        }

        private static void PaintOrangeSlots(GameObject root, Color c)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                if (rend == null || rend.gameObject.name == StripeName) continue;
                var mats = rend.sharedMaterials;
                int count = mats != null ? mats.Length : 0;
                for (int m = 0; m < count; m++)
                {
                    if (!IsOrangeSlot(mats[m])) continue;
                    var block = new MaterialPropertyBlock();
                    if (count <= 1) rend.GetPropertyBlock(block);
                    else rend.GetPropertyBlock(block, m);
                    block.SetColor("_BaseColor", c);
                    block.SetColor("_Color", c);
                    // The orange slot glows. Keep that glow in the team colour, or a bronze
                    // scout still reads as the old orange lamp.
                    block.SetColor("_EmissionColor", new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f));
                    if (count <= 1) rend.SetPropertyBlock(block);
                    else rend.SetPropertyBlock(block, m);
                }
            }
        }

        /// <summary>The library slot IndustrialArtDressing builds from an SM_Orange face.</summary>
        private static bool IsOrangeSlot(Material mat)
        {
            if (mat == null || string.IsNullOrEmpty(mat.name)) return false;
            if (mat.name.StartsWith(MaterialPrefix, System.StringComparison.Ordinal)) return false;
            return mat.name.IndexOf("orange", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Attach(Transform parent, Vector3 localPos, Vector3 localScale, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = StripeName;
            ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = Material(c);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }

        private static Transform Find(Transform root)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == StripeName) return all[i];
            return null;
        }

        private static Material Material(Color c)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c);
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
            if (m.HasProperty("_EmissionColor"))
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
