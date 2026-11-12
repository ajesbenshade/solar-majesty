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
    ///
    /// The band is sized from the body's live bounds so it sits at the waist, stands proud of
    /// the hull, and is tall enough to sample from the isometric play camera. Session 38 on
    /// main 93d2c6f had the right material colour in EditMode, but a thin equator disc (and
    /// on companions a disc high enough to read as a cap) did not photograph as a TeamColour
    /// rim next to the castle's emerald bands.
    /// </summary>
    public static class CivTeamStripe
    {
        public const string StripeName = "TeamStripe";

        /// <summary>Material names start with this so <see cref="IndustrialArtDressing"/> leaves them alone.</summary>
        public const string MaterialPrefix = "SM_Art_TeamStripe_";

        /// <summary>Shortest world-space height a rim may have and still read from the play camera.</summary>
        public const float MinReadableHeight = 0.14f;

        /// <summary>How far the rim must extend past the body's xz extent, each side, in world units.</summary>
        public const float MinReadableRim = 0.14f;

        private const float ThicknessFraction = 0.32f;
        private const float MaxWorldThickness = 0.40f;
        private const float DiameterFactor = 1.28f;

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _lit = null;
        }

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
            AttachOrUpdate(root.transform, c);
        }

        /// <summary>The tax collector's team rim. The gold skirt and eye stay as they are.</summary>
        public static void AttachToCollector(Transform drone, CivModifiers civ)
        {
            if (drone == null) return;
            AttachOrUpdate(drone, CivAccents.TeamStripe(civ));
        }

        /// <summary>A companion's team rim. The body keeps the companion's own colour.</summary>
        public static void AttachToCompanion(Transform body, CivModifiers civ)
        {
            if (body == null) return;
            AttachOrUpdate(body, CivAccents.TeamStripe(civ));
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

        /// <summary>
        /// Place a waist band that is wide and tall enough to read as the castle TeamColour,
        /// or move and recolour one that is already there. A second call never leaves a stale
        /// colour (the first attach may have run before the civ was bound).
        /// </summary>
        private static void AttachOrUpdate(Transform parent, Color c)
        {
            if (!TryBodyBounds(parent, out Bounds worldBounds))
                worldBounds = new Bounds(parent.position, new Vector3(0.7f, 0.5f, 0.7f));

            float xz = Mathf.Max(worldBounds.size.x, worldBounds.size.z);
            float height = Mathf.Max(0.01f, worldBounds.size.y);
            float worldDiameter = Mathf.Max(xz * DiameterFactor, xz + 2f * MinReadableRim);
            float worldThickness = Mathf.Clamp(
                Mathf.Max(MinReadableHeight, height * ThicknessFraction),
                MinReadableHeight,
                MaxWorldThickness);

            var existing = Find(parent);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = StripeName;
                ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
            }

            // Local space, so a later turn of the unit carries the band with it.
            go.transform.localPosition = parent.InverseTransformPoint(worldBounds.center);
            go.transform.localRotation = Quaternion.identity;
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
            rend.sharedMaterial = Material(c, SourceMaterial(parent, go));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rend.receiveShadows = true;
        }

        private static bool TryBodyBounds(Transform parent, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var rends = parent.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null || rend is ParticleSystemRenderer) continue;
                if (rend.gameObject.name == StripeName) continue;
                if (!any)
                {
                    bounds = rend.bounds;
                    any = true;
                }
                else bounds.Encapsulate(rend.bounds);
            }
            return any;
        }

        /// <summary>
        /// A material already rendering on this unit, so the rim uses the same shader that
        /// already shows in the player (the gold skirt and the body both did). Shader.Find
        /// alone is the fallback.
        /// </summary>
        private static Material SourceMaterial(Transform parent, GameObject stripe)
        {
            var rends = parent.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null || rend.gameObject == stripe) continue;
                if (rend.gameObject.name == StripeName) continue;
                var mat = rend.sharedMaterial;
                if (mat != null && mat.shader != null) return mat;
            }
            return null;
        }

        private static Transform Find(Transform root)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == StripeName) return all[i];
            return null;
        }

        private static Material Material(Color c, Material source)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            Material m;
            if (source != null && source.shader != null)
                m = new Material(source) { name = MaterialPrefix + key };
            else
            {
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                           ?? Shader.Find("Standard");
                m = new Material(_lit) { name = MaterialPrefix + key };
            }

            // A cloned body or skirt map would muddy a solid team-colour rim.
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
            if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null);

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.42f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.08f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = m;
            return m;
        }
    }
}
