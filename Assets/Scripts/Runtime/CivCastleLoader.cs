using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>One civ castle model: where it lives and how it is checked.</summary>
    public sealed class CivCastleSpec
    {
        /// <summary><see cref="CivDefinition.castleKey"/>.</summary>
        public string Key;
        /// <summary>Resources path of the FBX (3D Artist, PR #130).</summary>
        public string ResourcePath;
        /// <summary>CommonsTower_0..TowerCount-1 must exist.</summary>
        public int TowerCount;
        /// <summary>
        /// A tower anchor that stands on the gate side (Blender -Y). After loading it must sit at
        /// local z &lt; 0 (the gate faces -Z, toward the play camera at yaw 45°) with the sign of x in
        /// <see cref="FrontAnchorSignX"/> (0 = on the centre line), which also catches a mirror.
        /// </summary>
        public string FrontAnchor;
        public int FrontAnchorSignX;
        /// <summary>Multiplied over the Trim slot (the shared trim sheet is neutral).</summary>
        public Color TrimTint;
    }

    /// <summary>
    /// The four civ castles (3D Artist, PR #130). <see cref="CivId.None"/> has no castle key and
    /// keeps the procedural Commons (<see cref="HeroBuildingKits.BuildCommons"/>).
    /// </summary>
    public static class CivCastleCatalog
    {
        public const string CoreModulePath = "Citadels/SM_Core_Module";
        public const string TrimTexturePrefix = "Citadels/Textures/SM_Citadel_Trim_";

        /// <summary>Anchors every castle supplies (plus CommonsTower_0..n and Core_Socket).</summary>
        public static readonly string[] CommonAnchors = { "CommonsKeep", "CommonsDome", "CitadelCrownY", "Core_Socket" };

        /// <summary>Parts of the shared Core module that idle-spin.</summary>
        public static readonly string[] CoreParts = { "Core_Collar", "Core_Crystal", "Core_HaloA", "Core_HaloB" };

        // Trim tints follow the 3D Artist's review renders (render_review.py): ivory, a light
        // graphite, sage. The Arcology's hull is teal-black #142226 on the dark panels; a single
        // multiply over the whole sheet needs a lighter teal-grey to land there without
        // blacking out the pewter panels.
        private static readonly CivCastleSpec[] Specs =
        {
            new CivCastleSpec { Key = "castle.ziggurat_keep", ResourcePath = "Citadels/SM_Citadel_Ziggurat", TowerCount = 4,
                FrontAnchor = "CommonsTower_0", FrontAnchorSignX = 1, TrimTint = new Color(0.957f, 0.922f, 0.847f) },
            new CivCastleSpec { Key = "castle.ring_citadel", ResourcePath = "Citadels/SM_Citadel_Ring", TowerCount = 6,
                FrontAnchor = "CommonsTower_3", FrontAnchorSignX = 0, TrimTint = new Color(0.431f, 0.463f, 0.518f) },
            new CivCastleSpec { Key = "castle.dome_bunker", ResourcePath = "Citadels/SM_Citadel_Dome", TowerCount = 8,
                FrontAnchor = "CommonsTower_0", FrontAnchorSignX = 1, TrimTint = new Color(0.663f, 0.722f, 0.627f) },
            new CivCastleSpec { Key = "castle.stilt_arcology", ResourcePath = "Citadels/SM_Citadel_Arcology", TowerCount = 4,
                FrontAnchor = "CommonsTower_2", FrontAnchorSignX = -1, TrimTint = new Color(0.45f, 0.58f, 0.60f) },
        };

        public static IReadOnlyList<CivCastleSpec> All => Specs;

        /// <summary>The castle for <paramref name="key"/>, or null (empty key = the procedural Commons).</summary>
        public static CivCastleSpec ForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < Specs.Length; i++)
                if (Specs[i].Key == key) return Specs[i];
            return null;
        }

        public static CivCastleSpec For(CivId civ)
        {
            var def = CivCatalog.Get(civ);
            return def == null || def.id == CivId.None ? null : ForKey(def.castleKey);
        }
    }

    /// <summary>
    /// Draws the Commons as the colony's civ castle (lifts the 2026-09-17 "Commons is procedural"
    /// lock for civs; see the civ design doc, section 7). Loads the FBX from Resources like the
    /// hero kits and dens, places it 1:1 (CASTLE_SCALE 1.44 is baked in, pivot at ground centre),
    /// turns it so the gate faces -Z, attaches the shared Core module where the model has none,
    /// paints the slots (Trim, Carbon, TeamColour = civ colour, Accent = player accent,
    /// CoreEmission) with per-civ material instances, and idle-spins the Core.
    ///
    /// The named anchors the rest of the game looks up (CommonsTower_*, CommonsKeep, CommonsDome,
    /// CitadelCrownY) come from the model as empties. Gameplay never touched the procedural
    /// meshes: hit points, the select proxy collider and the treasury live on ColonyStructure,
    /// so nothing else changes. Returns false (procedural Commons) for the neutral civ or if
    /// the model is missing.
    /// </summary>
    public static class CivCastleLoader
    {
        /// <summary>
        /// Yaw applied on top of the FBX import rotation. The castles are authored with the gate
        /// at Blender -Y; Blender's FBX export (-Z forward, Y up) plus Unity's handedness flip lands
        /// Blender -Y on Unity +Z, so the model is turned 180° to put the gate on -Z.
        /// CivCastleTests.Castle_GateFacesMinusZ checks the result through each castle's front anchor.
        /// </summary>
        public const float ImportYawDegrees = 180f;

        /// <summary>Core idle spin, degrees per second.</summary>
        public const float CrystalSpin = 9f;
        public const float HaloASpin = 14f;
        public const float HaloBSpin = -10f;

        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>Build the castle for <paramref name="civ"/> under <paramref name="root"/>. False = use the procedural Commons.</summary>
        public static bool TryBuild(Transform root, CivModifiers civ)
        {
            if (root == null || civ == null || civ.Id == CivId.None) return false;
            var spec = CivCastleCatalog.ForKey(civ.CastleKey);
            if (spec == null) return false;
            GameObject prefab = BuildingVisualCatalog.LoadByPath(spec.ResourcePath);
            if (prefab == null)
            {
                WarnOnce(spec.Key, "[CivCastle] " + spec.ResourcePath + " missing; procedural Commons.");
                return false;
            }

            var wrap = new GameObject("CivCastle_" + spec.Key.Replace("castle.", ""));
            wrap.transform.SetParent(root, false);
            wrap.transform.localPosition = Vector3.zero;
            wrap.transform.localRotation = Quaternion.Euler(0f, ImportYawDegrees, 0f);
            var model = Object.Instantiate(prefab, wrap.transform, false);
            model.name = prefab.name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = prefab.transform.localRotation;
            model.transform.localScale = prefab.transform.localScale;

            // The castle keeps its own LODGroup on the model root (the importer makes one from the
            // _LOD0.._LOD2 names; add one if not). Destroying it and grouping the castle on the
            // building root instead broke in play mode: Object.Destroy is deferred, so for a frame
            // both groups held the same renderers and Unity warned once per renderer (Session 33).
            var group = model.GetComponent<LODGroup>();
            foreach (var g in model.GetComponentsInChildren<LODGroup>(true))
                if (g != group) ColonyVisualUtility.DestroyNow(g);
            if (group == null) group = model.AddComponent<LODGroup>();
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
                ColonyVisualUtility.DestroyNow(c);

            AttachCoreIfMissing(model.transform);

            var castle = root.gameObject.AddComponent<CivCastle>();
            castle.Civ = civ.Id;
            castle.CastleKey = spec.Key;
            castle.Model = model.transform;
            var l0 = new List<Renderer>();
            var l1 = new List<Renderer>();
            var l2 = new List<Renderer>();
            var core = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (n.EndsWith("_LOD0")) l0.Add(r);
                else if (n.EndsWith("_LOD1")) l1.Add(r);
                else if (n.EndsWith("_LOD2")) l2.Add(r);
                else core.Add(r);
            }
            castle.Lod0 = l0.ToArray();
            castle.Lod1 = l1.ToArray();
            castle.Lod2 = l2.ToArray();
            castle.Core = core.ToArray();
            castle.Group = group;
            castle.ConfigureLods();

            ApplySlots(model, spec, civ);
            SpinCore(root, model.transform);
            return true;
        }

        /// <summary>
        /// Height of the crown above the building root: the model's CitadelCrownY anchor, or the
        /// procedural Commons' constant.
        /// </summary>
        public static float CrownHeight(Transform root)
        {
            var a = root != null ? FindDeep(root, "CitadelCrownY") : null;
            return a != null ? root.InverseTransformPoint(a.position).y : HeroBuildingKits.CitadelCrownY;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindDeep(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ---------------------------------------------------------------- Core module

        /// <summary>The Arcology ships without the Core; the other three carry it under Core_Socket.</summary>
        private static void AttachCoreIfMissing(Transform model)
        {
            var socket = FindDeep(model, "Core_Socket");
            if (socket == null || FindDeep(socket, "Core_Crystal") != null) return;
            var corePrefab = BuildingVisualCatalog.LoadByPath(CivCastleCatalog.CoreModulePath);
            if (corePrefab == null)
            {
                WarnOnce("core", "[CivCastle] " + CivCastleCatalog.CoreModulePath + " missing; castle has no Core.");
                return;
            }
            var core = Object.Instantiate(corePrefab, socket, false);
            core.name = corePrefab.name;
            // Both FBX roots carry the same import rotation; line the module up with the castle.
            core.transform.position = socket.position;
            core.transform.rotation = model.rotation;
            foreach (var c in core.GetComponentsInChildren<Collider>(true))
                ColonyVisualUtility.DestroyNow(c);
        }

        private static void SpinCore(Transform root, Transform model)
        {
            Spin(root, FindDeep(model, "Core_Crystal"), CrystalSpin);
            Spin(root, FindDeep(model, "Core_HaloA"), HaloASpin);
            Spin(root, FindDeep(model, "Core_HaloB"), HaloBSpin);
        }

        private static void Spin(Transform root, Transform part, float degreesPerSecond)
        {
            if (part == null) return;
            // About world up (the castle stands upright), whatever the part's own import axes.
            Vector3 axis = part.InverseTransformDirection(Vector3.up);
            KitLife.Spin(root, part, degreesPerSecond, axis);
        }

        // ---------------------------------------------------------------- materials

        public enum Slot { Trim, Carbon, TeamColour, Accent, CoreEmission, ChargerAmber, Unknown }

        /// <summary>Material names start with this; IndustrialArtDressing leaves them alone.</summary>
        public const string MaterialPrefix = "SM_Art_Citadel_";

        public static readonly Color CarbonColour = new Color(0.118f, 0.125f, 0.141f); // #1E2024
        /// <summary>The Core's cyan glow (HeroBuildingKits' CoreCyanEmit).</summary>
        public static readonly Color CoreGlow = new Color(0.10f, 1.60f, 2.30f);
        /// <summary>
        /// HAB charger pods. Solid amber, no trim sheet: base (0.89, 0.45, 0.08) and emission
        /// (1, 0.55, 0.10) at strength 3.5, the ChargerAmber slot on the civ-building FBX.
        /// </summary>
        public static readonly Color ChargerAmberColour = new Color(0.89f, 0.45f, 0.08f);
        public static readonly Color ChargerAmberGlow = new Color(3.5f, 1.925f, 0.35f);
        /// <summary>Window strip glow relative to the Core (3D Artist: about 12x dimmer).</summary>
        public const float WindowGlowFraction = 1f / 12f;

        /// <summary>Which castle slot an imported material stands for, by its FBX name.</summary>
        public static Slot SlotOf(Material m)
        {
            if (m == null) return Slot.Unknown;
            string n = m.name;
            if (n.StartsWith(MaterialPrefix)) n = n.Substring(MaterialPrefix.Length);
            if (n.StartsWith("TeamColour", System.StringComparison.OrdinalIgnoreCase)) return Slot.TeamColour;
            if (n.StartsWith("Trim", System.StringComparison.OrdinalIgnoreCase)) return Slot.Trim;
            if (n.StartsWith("Carbon", System.StringComparison.OrdinalIgnoreCase)) return Slot.Carbon;
            if (n.StartsWith("Accent", System.StringComparison.OrdinalIgnoreCase)) return Slot.Accent;
            if (n.StartsWith("CoreEmission", System.StringComparison.OrdinalIgnoreCase)) return Slot.CoreEmission;
            if (n.StartsWith("ChargerAmber", System.StringComparison.OrdinalIgnoreCase)) return Slot.ChargerAmber;
            return Slot.Unknown;
        }

        /// <summary>The colour a slot is painted for <paramref name="civ"/> (base colour; CoreEmission's is its lens).</summary>
        public static Color SlotColour(Slot slot, CivCastleSpec spec, CivModifiers civ)
        {
            switch (slot)
            {
                case Slot.Trim: return spec != null ? spec.TrimTint : Color.white;
                case Slot.Carbon: return CarbonColour;
                case Slot.TeamColour: return CivAccents.CivColour(civ.Id) ?? civ.TeamSecondary;
                case Slot.Accent: return CivAccents.ForCastle(civ.Accent, civ.Id);
                case Slot.CoreEmission: return new Color(0.05f, 0.48f, 0.62f);
                case Slot.ChargerAmber: return ChargerAmberColour;
                default: return Color.white;
            }
        }

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit;
        private static Texture2D _base, _metal, _normal, _emit;

        /// <summary>
        /// Paint every renderer from its FBX slot name. Shared by the castle and by
        /// <see cref="CivBuildingLoader"/>. Never writes the imported material: those assets stay untouched.
        /// </summary>
        public static void ApplySlots(GameObject model, CivCastleSpec spec, CivModifiers civ)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                if (src == null || src.Length == 0) continue;
                var next = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    Slot slot = SlotOf(src[i]);
                    if (slot == Slot.Unknown)
                    {
                        WarnOnce("slot:" + (src[i] != null ? src[i].name : "null"),
                            "[CivCastle] " + r.name + " has an unknown material slot '" + (src[i] != null ? src[i].name : "null") + "'.");
                        slot = r.name.StartsWith("Core_") ? Slot.Carbon : Slot.Trim;
                    }
                    next[i] = MaterialFor(slot, spec, civ);
                }
                r.sharedMaterials = next;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        /// <summary>
        /// One material instance per (slot, civ, accent), shared by every castle of that look.
        /// Never the imported FBX material: those are shared assets and stay untouched.
        /// </summary>
        public static Material MaterialFor(Slot slot, CivCastleSpec spec, CivModifiers civ)
        {
            Color col = SlotColour(slot, spec, civ);
            string key = slot + "|" + (spec != null ? spec.Key : "") + "|" + ColorUtility.ToHtmlStringRGBA(col);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            EnsureAssets();
            var m = new Material(_lit) { name = MaterialPrefix + slot + "_" + civ.Id };
            SetColour(m, col);
            // ChargerAmber is a solid pod light. CoreEmission keeps the sheet's metal and normal
            // but not the colour map; every other slot uses the shared trim sheet.
            bool sheet = slot != Slot.CoreEmission && slot != Slot.ChargerAmber;
            if (_base != null && sheet && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", _base);
            if (slot != Slot.ChargerAmber && _metal != null && m.HasProperty("_MetallicGlossMap"))
            {
                m.SetTexture("_MetallicGlossMap", _metal);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f);
            }
            if (slot != Slot.ChargerAmber && _normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", _normal);
                m.EnableKeyword("_NORMALMAP");
            }
            Color glow = Color.black;
            if (slot == Slot.CoreEmission) glow = CoreGlow;
            else if (slot == Slot.ChargerAmber) glow = ChargerAmberGlow;
            else if (slot == Slot.Trim)
            {
                Color civCol = CivAccents.CivColour(civ.Id) ?? Color.white;
                glow = Color.Lerp(Color.white, civCol, 0.6f) * (CoreGlow.maxColorComponent * WindowGlowFraction);
            }
            if (glow.maxColorComponent > 0f && m.HasProperty("_EmissionColor"))
            {
                if (slot != Slot.ChargerAmber && _emit != null && m.HasProperty("_EmissionMap"))
                    m.SetTexture("_EmissionMap", _emit);
                m.SetColor("_EmissionColor", glow);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Base colour of a material made by <see cref="MaterialFor"/>.</summary>
        public static Color BaseColour(Material m)
        {
            if (m == null) return Color.clear;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
        }

        private static void SetColour(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void EnsureAssets()
        {
            if (_lit == null)
                _lit = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                       ?? Shader.Find("Standard");
            if (_base == null) _base = Resources.Load<Texture2D>(CivCastleCatalog.TrimTexturePrefix + "BaseColor");
            if (_metal == null) _metal = Resources.Load<Texture2D>(CivCastleCatalog.TrimTexturePrefix + "MetallicSmoothness");
            if (_normal == null) _normal = Resources.Load<Texture2D>(CivCastleCatalog.TrimTexturePrefix + "Normal");
            if (_emit == null) _emit = Resources.Load<Texture2D>(CivCastleCatalog.TrimTexturePrefix + "Emission");
        }

        private static void WarnOnce(string key, string message)
        {
            if (Warned.Add(key)) Debug.LogWarning(message);
        }
    }
}
