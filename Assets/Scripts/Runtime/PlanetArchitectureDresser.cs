using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Builds <see cref="PlanetArchitecture"/> parts onto a spawned building and re-grades its
    /// kit colours for the world: printed regolith walls on Mars, ice shields on Europa, spin
    /// rings in the Belt, regolith berms on Luna, gardens on Earth. Parts live under a
    /// <c>Dress_Arch</c> child (IndustrialArtDressing skips Dress_ names) with no colliders, so
    /// placement and pathing are unchanged. See Docs/PLANET_ARCHITECTURE.md.
    /// </summary>
    public static class PlanetArchitectureDresser
    {
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
        private static MaterialPropertyBlock _block;
        private static Shader _hull, _lit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _block = null;
        }

        /// <summary>
        /// Dress a building that has already been snapped to the ground (ground = world y 0).
        /// No-op for retired categories, when the setting is off, or before a body is bound.
        /// </summary>
        public static void Dress(GameObject root, BuildingCategory category, CelestialBodyId body,
            float worldW, float worldD)
        {
            if (root == null || !DemoSettings.PlanetArchitecture) return;
            if (BuildingPlacer.IsRetired(category)) return;

            var archetype = PlanetArchitecture.ArchetypeOf(category);
            var style = PlanetArchitecture.Style(body);
            float roof = MeasureRoof(root, worldW, worldD, archetype);

            Regrade(root, body, style);

            var holder = new GameObject("Dress_Arch");
            holder.transform.SetParent(root.transform, false);
            Vector3 at = root.transform.position;
            holder.transform.position = new Vector3(at.x, 0f, at.z);

            int seed = Mathf.RoundToInt(at.x * 7.3f) * 73856093 ^ Mathf.RoundToInt(at.z * 7.3f) * 19349663;
            var parts = PlanetArchitecture.Adapt(body, archetype, worldW, worldD, roof, seed);
            for (int i = 0; i < parts.Count; i++)
                Build(holder.transform, parts[i], body, style);
        }

        /// <summary>
        /// Height of the kit's main roof: the highest top among pieces that span a good part of
        /// the footprint (masts, antennae and beacons are narrow and don't count).
        /// </summary>
        private static float MeasureRoof(GameObject root, float w, float d, ArchArchetype a)
        {
            float top = 0f;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || IsPortName(r.name)) continue;
                Bounds b = r.bounds;
                if (b.size.x < w * 0.4f || b.size.z < d * 0.4f) continue;
                if (b.max.y > top) top = b.max.y;
            }
            if (top > 0.3f) return Mathf.Min(top, 8f);
            return a switch
            {
                ArchArchetype.Pad => 0.4f,
                ArchArchetype.Wonder => 5f,
                ArchArchetype.Hub => 3.2f,
                _ => 2.6f
            };
        }

        private static bool IsPortName(string n) =>
            n.Contains("Port") || n.StartsWith("Dress_");

        /// <summary>Kit colours → this world's palette and dust, per renderer (materials stay shared).</summary>
        private static void Regrade(GameObject root, CelestialBodyId body, in ArchStyle style)
        {
            // Mars is the tuned reference look: its kit colours and dust stay exactly as authored.
            if (body == CelestialBodyId.Mars) return;
            _block ??= new MaterialPropertyBlock();
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || IsPortName(r.name)) continue;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty("_BaseColor")) continue;
                bool remap = PlanetArchitecture.TryRemapMaterial(body, mat.name, mat.GetColor("_BaseColor"), out Color c);
                bool dust = mat.HasProperty("_DustAmount");
                if (!remap && !dust) continue;
                r.GetPropertyBlock(_block);
                if (remap)
                {
                    _block.SetColor("_BaseColor", c);
                    // The orange slot glows a little; keep that glow in the new trim colour.
                    if (mat.HasProperty("_EmissionColor") && mat.GetColor("_EmissionColor").maxColorComponent > 0.01f)
                        _block.SetColor("_EmissionColor", c * 0.45f);
                }
                if (dust)
                {
                    _block.SetColor("_DustColor", style.DustColor);
                    _block.SetFloat("_DustAmount", style.DustAmount);
                    if (mat.HasProperty("_WearAmount")) _block.SetFloat("_WearAmount", style.WearAmount);
                }
                r.SetPropertyBlock(_block);
            }
        }

        private static void Build(Transform parent, in ArchPart part, CelestialBodyId body, in ArchStyle style)
        {
            var type = part.Shape switch
            {
                ArchShape.Cylinder => PrimitiveType.Cylinder,
                ArchShape.Sphere => PrimitiveType.Sphere,
                _ => PrimitiveType.Cube
            };
            var go = GameObject.CreatePrimitive(type);
            go.name = "Dress_Arch_" + part.Name;
            ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = part.Position;
            go.transform.localRotation = Quaternion.Euler(part.Euler);
            // Unity's cylinder is 2 m tall at scale 1; cube and sphere are 1 m.
            Vector3 s = part.Size;
            go.transform.localScale = part.Shape == ArchShape.Cylinder ? new Vector3(s.x, s.y * 0.5f, s.z) : s;

            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = MaterialFor(body, part.Role, style);
            bool see = ArchStyle.IsTranslucent(part.Role);
            rend.shadowCastingMode = see
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
            rend.receiveShadows = !see;
        }

        private static Material MaterialFor(CelestialBodyId body, ArchRole role, in ArchStyle style)
        {
            int key = (int)body * 64 + (int)role;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            if (_lit == null)
            {
                _hull = Shader.Find("SolarMajesty/Hull");
                _lit = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                       ?? Shader.Find("Sprites/Default");
            }
            bool see = ArchStyle.IsTranslucent(role);
            bool glow = role == ArchRole.Glow;
            bool plated = !see && !glow && role != ArchRole.Plant && _hull != null;
            var mat = new Material(plated ? _hull : _lit) { name = $"SM_Arch_{body}_{role}" };
            Color c = style.RoleColor(role);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            bool shiny = role == ArchRole.Glass || role == ArchRole.Ice || role == ArchRole.Foil;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", shiny ? 0.78f : 0.24f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", role == ArchRole.Foil ? 0.7f : 0.06f);

            if (plated)
            {
                // Printed / sintered shells show courses rather than panels; hulls get seams.
                bool shell = role == ArchRole.Shell;
                SetF(mat, "_PanelScale", shell ? 0.35f : 0.9f);
                SetF(mat, "_PanelWidth", shell ? 0.03f : 0.018f);
                SetF(mat, "_PanelDarken", shell ? 0.18f : 0.28f);
                SetF(mat, "_PanelBevel", 0.2f);
                SetF(mat, "_WearAmount", style.WearAmount);
                SetF(mat, "_WearScale", 6.5f);
                if (mat.HasProperty("_WearColor")) mat.SetColor("_WearColor", new Color(0.42f, 0.37f, 0.32f, 1f));
                if (mat.HasProperty("_DustColor")) mat.SetColor("_DustColor", style.DustColor);
                SetF(mat, "_DustAmount", style.DustAmount);
                SetF(mat, "_DustSharpness", 3.6f);
                SetF(mat, "_EmissionBandWidth", 0f);
            }
            if (see)
            {
                ColonyVisualUtility.ApplyTransparent(mat);
                c.a = role == ArchRole.Steam ? 0.28f : role == ArchRole.Ice ? 0.62f : 0.45f;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            }
            if (glow && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", style.GlowEmission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = mat;
            return mat;
        }

        private static void SetF(Material m, string p, float v)
        {
            if (m.HasProperty(p)) m.SetFloat(p, v);
        }
    }
}
