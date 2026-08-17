using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// SpaceX-industrial look for Blender kit meshes: panelled white hull, carbon black,
    /// graphite, safety orange, cyan sensors, solar cells, stalker hide.
    /// Driven by imported SM_* material names (and renderer/object names as fallback).
    /// </summary>
    public static class IndustrialArtDressing
    {
        private enum Slot
        {
            WhiteHull,
            BlackCarbon,
            Graphite,
            Steel,
            Orange,
            Cyan,
            Glass,
            Solar,
            DefenseRed,
            StalkerHide,
            MiteHide,
            LeechHide,
            WispHide,
            CreeperHide,
            HopperHide,
            TickHide
        }

        private static readonly Dictionary<Slot, Material> Mats = new Dictionary<Slot, Material>(16);
        private static Shader _lit;
        private static Texture2D _whiteAlbedo;
        private static Texture2D _whiteNormal;
        private static Texture2D _blackAlbedo;
        private static Texture2D _graphiteAlbedo;
        private static Texture2D _steelAlbedo;
        private static Texture2D _orangeAlbedo;
        private static Texture2D _solarAlbedo;
        private static Texture2D _hideAlbedo;
        private static Texture2D _hideNormal;
        private static Texture2D _redAlbedo;
        private static bool _ready;

        public static void Apply(GameObject root)
        {
            if (root == null) return;
            EnsureLibrary();
            if (_lit == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                if (rend == null || ShouldSkip(rend)) continue;

                var src = rend.sharedMaterials;
                if (src == null || src.Length == 0)
                {
                    rend.sharedMaterial = Get(Slot.WhiteHull);
                    continue;
                }

                var next = new Material[src.Length];
                for (int m = 0; m < src.Length; m++)
                    next[m] = Get(GuessSlot(rend, src[m]));
                rend.sharedMaterials = next;
                rend.shadowCastingMode = ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }

        public static bool HasArt(GameObject root)
        {
            if (root == null) return false;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var mat = rends[i] != null ? rends[i].sharedMaterial : null;
                if (mat != null && mat.name.StartsWith("SM_Art_"))
                    return true;
            }
            return false;
        }

        /// <summary>Multiplies albedo via MPB so shared art materials stay intact (incap / aggro).</summary>
        public static void SetTintOverlay(GameObject root, Color multiply)
        {
            if (root == null) return;
            var block = new MaterialPropertyBlock();
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null || ShouldSkip(rend)) continue;
                rend.GetPropertyBlock(block);
                block.SetColor("_BaseColor", multiply);
                rend.SetPropertyBlock(block);
            }
        }

        public static void ClearTintOverlay(GameObject root) =>
            SetTintOverlay(root, Color.white);

        private static bool ShouldSkip(Renderer rend)
        {
            string n = rend.name;
            if (n.Contains("SelectRing") || n.Contains("StatusOrb") || n.Contains("Label"))
                return true;
            if (n.Contains("Vfx") || n.StartsWith("Dress_") || n.Contains("YieldLabel"))
                return true;
            // Cardinal sockets already have a round white tube + orange collar. "airlock" in the
            // old name mapped the whole sleeve to solid orange.
            if (n.StartsWith("DockSleeve") || n.StartsWith("Airlock_") ||
                n.StartsWith("Dress_TubeArm") || n.StartsWith("CommonsStub") ||
                n.StartsWith("CommonsPort") ||
                n.Contains("AirlockHub"))
                return true;
            if (n.Contains("GroundPlane") || n.Contains("HorizonSkirt") || n.Contains("Footprint"))
                return true;
            Transform t = rend.transform;
            while (t != null)
            {
                if (t.name.StartsWith("Ghost") || t.name.Contains("SelectProxy"))
                    return false;
                if (t.name.Contains("StatusOrb") || t.name == "SelectRing")
                    return true;
                t = t.parent;
            }
            return false;
        }

        private static Slot GuessSlot(Renderer rend, Material src)
        {
            // Prefer imported SM_* slot names so joined FBX submeshes keep hull vs trim vs accent.
            if (TryFromToken(src != null ? src.name : null, out Slot fromMat))
                return fromMat;
            if (TryFromToken(rend.name, out Slot fromLocal))
                return fromLocal;

            string root = rend.transform.root.name.ToLowerInvariant();
            string local = rend.name.ToLowerInvariant();
            if (root.Contains("solar") || local.Contains("solar") || local.Contains("array"))
                return Slot.Solar;
            if (root.Contains("stalker") && !ContainsAny(local, "plate", "bracer", "armor", "white"))
                return Slot.StalkerHide;
            return Slot.WhiteHull;
        }

        private static bool TryFromToken(string name, out Slot slot)
        {
            slot = Slot.WhiteHull;
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();

            if (ContainsAny(n, "sm_white")) { slot = Slot.WhiteHull; return true; }
            if (ContainsAny(n, "sm_black")) { slot = Slot.BlackCarbon; return true; }
            if (ContainsAny(n, "sm_graphite")) { slot = Slot.Graphite; return true; }
            if (ContainsAny(n, "sm_orange")) { slot = Slot.Orange; return true; }
            if (ContainsAny(n, "sm_steel")) { slot = Slot.Steel; return true; }
            if (ContainsAny(n, "sm_glass")) { slot = Slot.Glass; return true; }
            if (ContainsAny(n, "sm_cyan", "sm_scout", "sm_medic")) { slot = Slot.Cyan; return true; }
            if (ContainsAny(n, "sm_engineer")) { slot = Slot.Orange; return true; }
            if (ContainsAny(n, "sm_defense")) { slot = Slot.DefenseRed; return true; }
            if (ContainsAny(n, "sm_stalker")) { slot = Slot.StalkerHide; return true; }
            if (ContainsAny(n, "sm_mite", "regolithmite")) { slot = Slot.MiteHide; return true; }
            if (ContainsAny(n, "sm_leech", "wattleech")) { slot = Slot.LeechHide; return true; }
            if (ContainsAny(n, "sm_wisp", "icewisp")) { slot = Slot.WispHide; return true; }
            if (ContainsAny(n, "sm_creeper", "soilcreeper")) { slot = Slot.CreeperHide; return true; }
            if (ContainsAny(n, "sm_hopper", "ashhopper")) { slot = Slot.HopperHide; return true; }
            if (ContainsAny(n, "sm_tick", "rocktick")) { slot = Slot.TickHide; return true; }
            if (ContainsAny(n, "sm_dust")) { slot = Slot.MiteHide; return true; }
            if (ContainsAny(n, "sm_plant")) { slot = Slot.CreeperHide; return true; }
            if (ContainsAny(n, "sm_ice")) { slot = Slot.Cyan; return true; }
            if (ContainsAny(n, "sm_yellow")) { slot = Slot.Orange; return true; }
            if (ContainsAny(n, "sm_concrete")) { slot = Slot.Graphite; return true; }
            if (ContainsAny(n, "sm_regolith", "sm_crater", "crater")) { slot = Slot.Graphite; return true; }
            if (ContainsAny(n, "sm_solar", "solarcell", "pv_cell")) { slot = Slot.Solar; return true; }

            if (ContainsAny(n, "visor", "eyel", "eyer", "eye")) { slot = Slot.Cyan; return true; }
            if (ContainsAny(n, "beacon", "stripe", "hatch", "hazard", "accent")) { slot = Slot.Orange; return true; }
            if (ContainsAny(n, "band", "skid", "tread", "boot", "spine", "ridge", "leg")) { slot = Slot.BlackCarbon; return true; }
            if (ContainsAny(n, "toolbox", "pack", "vent", "plinth", "bogie")) { slot = Slot.Graphite; return true; }
            if (ContainsAny(n, "shield", "plating", "face")) { slot = Slot.DefenseRed; return true; }
            if (ContainsAny(n, "steel", "antenna")) { slot = Slot.Steel; return true; }
            if (ContainsAny(n, "glass")) { slot = Slot.Glass; return true; }
            if (ContainsAny(n, "solar", "array")) { slot = Slot.Solar; return true; }
            return false;
        }

        private static bool ContainsAny(string blob, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (blob.IndexOf(keys[i], System.StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static Material Get(Slot slot)
        {
            if (Mats.TryGetValue(slot, out var mat) && mat != null)
                return mat;
            mat = BuildMaterial(slot);
            Mats[slot] = mat;
            return mat;
        }

        private static void EnsureLibrary()
        {
            if (_ready && _lit != null) return;
            _lit = Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (_lit == null) return;

            _whiteAlbedo = BuildWhiteHull(256);
            _whiteNormal = BuildPanelNormal(256, 0.55f);
            _blackAlbedo = BuildCarbon(128);
            _graphiteAlbedo = BuildBrushed(128, new Color(0.18f, 0.19f, 0.21f), new Color(0.32f, 0.33f, 0.35f));
            _steelAlbedo = BuildBrushed(128, new Color(0.42f, 0.44f, 0.47f), new Color(0.62f, 0.64f, 0.67f));
            _orangeAlbedo = BuildHazard(128);
            _solarAlbedo = BuildSolar(128);
            _hideAlbedo = BuildHide(128);
            _hideNormal = BuildPebbleNormal(128);
            _redAlbedo = BuildPanelTint(128, new Color(0.72f, 0.16f, 0.14f), new Color(0.42f, 0.08f, 0.08f));
            _ready = true;
        }

        private static Material BuildMaterial(Slot slot)
        {
            var mat = new Material(_lit) { name = "SM_Art_" + slot };
            Texture2D albedo = _whiteAlbedo;
            Texture2D normal = _whiteNormal;
            Color tint = Color.white;
            Color emission = Color.black;
            float metallic = 0.12f;
            float smooth = 0.42f;
            Vector2 tile = new Vector2(2.5f, 2.5f);

            switch (slot)
            {
                case Slot.WhiteHull:
                    albedo = _whiteAlbedo;
                    normal = _whiteNormal;
                    metallic = 0.08f;
                    smooth = 0.38f;
                    tile = new Vector2(3f, 3f);
                    break;
                case Slot.BlackCarbon:
                    albedo = _blackAlbedo;
                    normal = _whiteNormal;
                    tint = new Color(0.12f, 0.12f, 0.13f);
                    metallic = 0.35f;
                    smooth = 0.22f;
                    tile = new Vector2(2f, 2f);
                    break;
                case Slot.Graphite:
                    albedo = _graphiteAlbedo;
                    metallic = 0.45f;
                    smooth = 0.32f;
                    break;
                case Slot.Steel:
                    albedo = _steelAlbedo;
                    metallic = 0.72f;
                    smooth = 0.55f;
                    tile = new Vector2(1.6f, 1.6f);
                    break;
                case Slot.Orange:
                    albedo = _orangeAlbedo;
                    metallic = 0.06f;
                    smooth = 0.4f;
                    emission = new Color(1.4f, 0.42f, 0.06f);
                    tile = new Vector2(1.5f, 1.5f);
                    break;
                case Slot.Cyan:
                    albedo = _whiteAlbedo;
                    tint = new Color(0.22f, 0.82f, 0.98f);
                    metallic = 0.05f;
                    smooth = 0.72f;
                    emission = new Color(0.35f, 1.6f, 2.2f);
                    tile = new Vector2(1f, 1f);
                    break;
                case Slot.Glass:
                    albedo = _whiteAlbedo;
                    tint = new Color(0.55f, 0.68f, 0.78f);
                    metallic = 0f;
                    smooth = 0.88f;
                    emission = new Color(0.08f, 0.12f, 0.18f);
                    tile = new Vector2(1f, 1f);
                    break;
                case Slot.Solar:
                    albedo = _solarAlbedo;
                    metallic = 0.35f;
                    smooth = 0.62f;
                    emission = new Color(0.10f, 0.28f, 0.85f);
                    tile = new Vector2(4f, 4f);
                    break;
                case Slot.DefenseRed:
                    albedo = _redAlbedo;
                    metallic = 0.1f;
                    smooth = 0.36f;
                    emission = new Color(0.35f, 0.04f, 0.02f);
                    tile = new Vector2(2f, 2f);
                    break;
                case Slot.StalkerHide:
                    albedo = _hideAlbedo;
                    normal = _hideNormal;
                    metallic = 0.04f;
                    smooth = 0.14f;
                    tile = new Vector2(2.2f, 2.2f);
                    break;
                case Slot.MiteHide:
                    albedo = _hideAlbedo;
                    normal = _hideNormal;
                    tint = new Color(0.62f, 0.48f, 0.32f);
                    metallic = 0.06f;
                    smooth = 0.18f;
                    tile = new Vector2(2.4f, 2.4f);
                    break;
                case Slot.LeechHide:
                    albedo = _hideAlbedo;
                    normal = _hideNormal;
                    tint = new Color(0.28f, 0.78f, 0.82f);
                    metallic = 0.12f;
                    smooth = 0.32f;
                    emission = new Color(0.08f, 0.32f, 0.38f);
                    tile = new Vector2(2.0f, 2.0f);
                    break;
                case Slot.WispHide:
                    albedo = _whiteAlbedo;
                    tint = new Color(0.72f, 0.92f, 1f);
                    metallic = 0.04f;
                    smooth = 0.62f;
                    emission = new Color(0.18f, 0.55f, 0.72f);
                    tile = new Vector2(1.2f, 1.2f);
                    break;
                case Slot.CreeperHide:
                    albedo = _graphiteAlbedo;
                    normal = _hideNormal;
                    tint = new Color(0.42f, 0.58f, 0.22f);
                    metallic = 0.05f;
                    smooth = 0.22f;
                    tile = new Vector2(2f, 2f);
                    break;
                case Slot.HopperHide:
                    albedo = _graphiteAlbedo;
                    tint = new Color(0.56f, 0.54f, 0.50f);
                    metallic = 0.18f;
                    smooth = 0.28f;
                    tile = new Vector2(1.8f, 1.8f);
                    break;
                case Slot.TickHide:
                    albedo = _graphiteAlbedo;
                    tint = new Color(0.52f, 0.46f, 0.4f);
                    metallic = 0.28f;
                    smooth = 0.24f;
                    tile = new Vector2(2.2f, 2.2f);
                    break;
            }

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTextureScale("_BaseMap", tile);
            }
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_BumpMap") && normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tile);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);

            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            return mat;
        }

        private static Texture2D BuildWhiteHull(int size)
        {
            var tex = NewTex(size, "SM_Art_WhiteHull");
            var px = new Color[size * size];
            Color shell = new Color(0.99f, 0.99f, 1f);
            Color seam = new Color(0.22f, 0.23f, 0.25f);
            Color rivet = new Color(0.12f, 0.12f, 0.13f);
            int panel = 16;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grit = Frac(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f);
                float dirt = Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
                Color c = shell * (0.97f + grit * 0.04f - dirt * 0.02f);
                bool line = (x % panel) == 0 || (y % panel) == 0;
                if (line) c = Color.Lerp(c, seam, 0.7f);
                int mx = x % panel;
                int my = y % panel;
                if ((mx == 2 || mx == panel - 2) && (my == 2 || my == panel - 2))
                    c = rivet;
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildPanelNormal(int size, float strength)
        {
            var tex = NewTex(size, "SM_Art_PanelN");
            var px = new Color[size * size];
            int panel = 16;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hL = PanelH(x - 1, y, panel);
                float hR = PanelH(x + 1, y, panel);
                float hD = PanelH(x, y - 1, panel);
                float hU = PanelH(x, y + 1, panel);
                Vector3 n = new Vector3((hL - hR) * strength, (hD - hU) * strength, 1f).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static float PanelH(int x, int y, int panel)
        {
            x = (x + 4096) % 256;
            y = (y + 4096) % 256;
            bool line = (x % panel) == 0 || (y % panel) == 0;
            return line ? 0f : 1f;
        }

        private static Texture2D BuildCarbon(int size)
        {
            var tex = NewTex(size, "SM_Art_Carbon");
            var px = new Color[size * size];
            Color a = new Color(0.04f, 0.04f, 0.045f);
            Color b = new Color(0.09f, 0.09f, 0.1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float weave = ((x / 2 + y / 2) % 2) * 0.04f;
                float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                px[y * size + x] = Color.Lerp(a, b, n) + new Color(weave, weave, weave);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildBrushed(int size, Color dark, Color light)
        {
            var tex = NewTex(size, "SM_Art_Brushed");
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float stroke = Mathf.PerlinNoise(x * 0.9f, y * 0.04f);
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.2f);
                px[y * size + x] = Color.Lerp(dark, light, stroke * 0.65f + n * 0.35f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildHazard(int size)
        {
            var tex = NewTex(size, "SM_Art_Hazard");
            var px = new Color[size * size];
            Color hi = new Color(0.96f, 0.42f, 0.08f);
            Color lo = new Color(0.72f, 0.22f, 0.04f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float stripe = ((x + y) / 10) % 2 == 0 ? 1f : 0.55f;
                float grit = Frac(Mathf.Sin(x * 3.1f + y * 7.7f) * 43758.5f);
                px[y * size + x] = Color.Lerp(lo, hi, stripe) * (0.92f + grit * 0.1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildSolar(int size)
        {
            var tex = NewTex(size, "SM_Art_Solar");
            var px = new Color[size * size];
            Color cell = new Color(0.08f, 0.12f, 0.22f);
            Color cellB = new Color(0.12f, 0.18f, 0.32f);
            Color bus = new Color(0.55f, 0.58f, 0.62f);
            int cellSize = 16;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool grid = (x % cellSize) < 1 || (y % cellSize) < 1;
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                Color c = Color.Lerp(cell, cellB, n);
                if (grid) c = bus;
                else if ((x % cellSize) == cellSize / 2) c = Color.Lerp(c, bus, 0.35f);
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildHide(int size)
        {
            var tex = NewTex(size, "SM_Art_Hide");
            var px = new Color[size * size];
            Color a = new Color(0.12f, 0.04f, 0.05f);
            Color b = new Color(0.28f, 0.08f, 0.07f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.22f, y * 0.22f);
                float n2 = Mathf.PerlinNoise(x * 0.7f + 8f, y * 0.7f);
                px[y * size + x] = Color.Lerp(a, b, n * 0.7f + n2 * 0.3f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildPebbleNormal(int size)
        {
            var tex = NewTex(size, "SM_Art_HideN");
            var px = new Color[size * size];
            const float s = 0.85f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hL = Mathf.PerlinNoise((x - 1) * 0.25f, y * 0.25f);
                float hR = Mathf.PerlinNoise((x + 1) * 0.25f, y * 0.25f);
                float hD = Mathf.PerlinNoise(x * 0.25f, (y - 1) * 0.25f);
                float hU = Mathf.PerlinNoise(x * 0.25f, (y + 1) * 0.25f);
                Vector3 n = new Vector3((hL - hR) * s, (hD - hU) * s, 1f).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildPanelTint(int size, Color hi, Color lo)
        {
            var tex = NewTex(size, "SM_Art_TintPanel");
            var px = new Color[size * size];
            int panel = 16;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grit = Frac(Mathf.Sin(x * 9.1f + y * 4.7f) * 43758.5f);
                Color c = Color.Lerp(lo, hi, 0.55f + grit * 0.2f);
                if ((x % panel) == 0 || (y % panel) == 0)
                    c = Color.Lerp(c, Color.black, 0.45f);
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D NewTex(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
