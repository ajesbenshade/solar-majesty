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

            Regrade(root, body, style);

            var holder = new GameObject("Dress_Arch");
            holder.transform.SetParent(root.transform, false);
            Vector3 at = root.transform.position;
            holder.transform.position = new Vector3(at.x, 0f, at.z);

            var shape = ShapeFor(root, category, worldW, worldD, archetype, holder.transform);
            int seed = Mathf.RoundToInt(at.x * 7.3f) * 73856093 ^ Mathf.RoundToInt(at.z * 7.3f) * 19349663;
            var parts = PlanetArchitecture.Adapt(body, archetype, shape, seed);
            BuildParts(root.transform, holder.transform, parts, body, style);
        }

        /// <summary>
        /// The kit's own description (<see cref="KitAnchors"/>), moved into the dressing holder's space
        /// (ground at y 0). Kits that record nothing get a stand-in with face-centre doorways and no
        /// roof surfaces, so world parts never land on a guessed roof.
        /// </summary>
        private static KitShape ShapeFor(GameObject root, BuildingCategory category, float w, float d,
            ArchArchetype archetype, Transform holder)
        {
            var kit = KitAnchors.Of(root);
            Vector3 offset = holder.InverseTransformPoint(root.transform.position);
            if (kit != null)
            {
                var s = kit.Shifted(offset);
                s.Category = category;
                if (s.W <= 0f) s.W = w;
                if (s.D <= 0f) s.D = d;
                if (s.Doors.Count == 0) s.AddFaceCentreDoors();
                if (!s.HasBody) s.Body = new Bounds(new Vector3(0f, MeasureRoof(root, w, d, archetype) * 0.5f, 0f),
                    new Vector3(w * 0.8f, MeasureRoof(root, w, d, archetype), d * 0.8f));
                return s;
            }
            return KitShape.Fallback(category, w, d, MeasureRoof(root, w, d, archetype));
        }

        /// <summary>
        /// Static parts merge into one mesh per role (a building's whole Luna berm is one renderer);
        /// moving parts become their own GameObjects under a pivot per motion group, animated by
        /// <see cref="KitLife"/>; emitter parts become <see cref="KitAnchors"/> vents.
        /// </summary>
        private static void BuildParts(Transform root, Transform holder, List<ArchPart> parts, CelestialBodyId body, in ArchStyle style)
        {
            var byRole = new Dictionary<ArchRole, (List<PrimitiveType> types, List<Matrix4x4> mats)>();
            var pivots = new Dictionary<string, Transform>();
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.IsEmitter)
                {
                    Vector3 wp = holder.TransformPoint(part.Position);
                    KitAnchors.Emitter(root, part.EmitKind, root.InverseTransformPoint(wp), part.Euler, part.Size.x);
                    continue;
                }
                if (part.Motion != ArchMotion.None && !string.IsNullOrEmpty(part.MotionGroup))
                {
                    if (!pivots.TryGetValue(part.MotionGroup, out var pivot))
                    {
                        pivot = KitLife.Pivot(holder, "Dress_Arch_" + part.MotionGroup, part.MotionPivot);
                        pivots[part.MotionGroup] = pivot;
                        Register(root, pivot, part);
                    }
                    var go = Build(pivot, part, body, style);
                    if (go != null)
                    {
                        go.transform.localPosition = part.Position - part.MotionPivot;
                        if (part.Motion == ArchMotion.Blink || part.Motion == ArchMotion.Pulse)
                            RegisterGlow(root, go.GetComponent<Renderer>(), part);
                    }
                    continue;
                }
                if (!byRole.TryGetValue(part.Role, out var list))
                {
                    list = (new List<PrimitiveType>(), new List<Matrix4x4>());
                    byRole[part.Role] = list;
                }
                list.types.Add(TypeOf(part.Shape));
                list.mats.Add(Matrix4x4.TRS(part.Position, Quaternion.Euler(part.Euler), UnitScale(part)));
            }

            foreach (var kv in byRole)
            {
                var go = new GameObject("Dress_Arch_" + kv.Key);
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = DetailBatch.MergePrimitives(kv.Value.types, kv.Value.mats, "Arch_" + kv.Key);
                var rend = go.AddComponent<MeshRenderer>();
                Finish(rend, kv.Key, body, style);
            }
        }

        private static void Register(Transform root, Transform pivot, in ArchPart part)
        {
            switch (part.Motion)
            {
                case ArchMotion.Spin: KitLife.Spin(root, pivot, part.MotionAmount, part.MotionAxis); break;
                case ArchMotion.Sweep: KitLife.Sweep(root, pivot, part.MotionAmount, part.MotionPeriod); break;
                case ArchMotion.Bob: KitLife.Bob(root, pivot, part.MotionAmount, part.MotionPeriod); break;
            }
        }

        private static void RegisterGlow(Transform root, Renderer r, in ArchPart part)
        {
            if (r == null) return;
            if (part.Motion == ArchMotion.Blink) KitLife.Blink(root, r, part.MotionPeriod, part.MotionAmount > 0f ? part.MotionAmount : 0.18f);
            else KitLife.Pulse(root, r, part.MotionPeriod, part.MotionAmount > 0f ? part.MotionAmount : 0.45f);
        }

        private static PrimitiveType TypeOf(ArchShape s) => s switch
        {
            ArchShape.Cylinder => PrimitiveType.Cylinder,
            ArchShape.Sphere => PrimitiveType.Sphere,
            _ => PrimitiveType.Cube
        };

        /// <summary>Unity's cylinder is 2 m tall at scale 1; cube and sphere are 1 m.</summary>
        private static Vector3 UnitScale(in ArchPart part)
        {
            Vector3 s = part.Size;
            return part.Shape == ArchShape.Cylinder ? new Vector3(s.x, s.y * 0.5f, s.z) : s;
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

        /// <summary>
        /// Ports and loose dressing don't count as hull. Staged kit parts (<c>Dress_S{n}_…</c>, see
        /// ConstructionStages) are the hull of the detailed builds, so they do — except merged
        /// detail meshes, whose bounds span antennas and masts.
        /// </summary>
        private static bool IsPortName(string n) =>
            SkipRegrade(n) || n.Contains("_Detail_");

        /// <summary>Loose dressing keeps its colours; staged kit parts take the world's palette.</summary>
        private static bool SkipRegrade(string n) =>
            n.Contains("Port") || (n.StartsWith("Dress_") && !IsStagedKitPart(n));

        private static bool IsStagedKitPart(string n) =>
            n.Length > 8 && n[6] == 'S' && n[7] >= '0' && n[7] <= '4' && n[8] == '_';

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
                if (r == null || SkipRegrade(r.name)) continue;
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

        private static GameObject Build(Transform parent, in ArchPart part, CelestialBodyId body, in ArchStyle style)
        {
            var go = GameObject.CreatePrimitive(TypeOf(part.Shape));
            go.name = "Dress_Arch_" + part.Name;
            ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = part.Position;
            go.transform.localRotation = Quaternion.Euler(part.Euler);
            go.transform.localScale = UnitScale(part);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) Finish(rend, part.Role, body, style);
            return go;
        }

        private static void Finish(Renderer rend, ArchRole role, CelestialBodyId body, in ArchStyle style)
        {
            rend.sharedMaterial = MaterialFor(body, role, style);
            bool see = ArchStyle.IsTranslucent(role);
            rend.shadowCastingMode = see || ArchStyle.IsGlowing(role)
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
            bool glow = ArchStyle.IsGlowing(role) || role == ArchRole.Solar;
            bool plated = !see && !ArchStyle.IsGlowing(role) && role != ArchRole.Plant && role != ArchRole.Soil && _hull != null;
            var mat = new Material(plated ? _hull : _lit) { name = $"SM_Arch_{body}_{role}" };
            Color c = style.RoleColor(role);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            bool shiny = role == ArchRole.Glass || role == ArchRole.Ice || role == ArchRole.Foil ||
                         role == ArchRole.Water || role == ArchRole.Solar;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", shiny ? 0.78f : 0.24f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", role == ArchRole.Foil ? 0.7f : role == ArchRole.Metal ? 0.55f : role == ArchRole.Solar ? 0.3f : 0.06f);
            if (role == ArchRole.Soil && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);
            if (role == ArchRole.Frost && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);

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
                c.a = role == ArchRole.Steam ? 0.28f : role == ArchRole.Ice ? 0.62f : role == ArchRole.Water ? 0.72f : 0.45f;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            }
            if (glow && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", style.RoleEmission(role));
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
