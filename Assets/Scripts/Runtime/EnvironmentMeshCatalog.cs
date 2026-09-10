using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Environment meshes under Resources/Environment (trees, rocks, craters, dunes).
    /// Dressing only — InstantiateClean strips colliders.
    /// </summary>
    public static class EnvironmentMeshCatalog
    {
        public const string TreeAPath = "Environment/SM_Tree_Broadleaf_A";
        public const string TreeBPath = "Environment/SM_Tree_Broadleaf_B";
        public const string RockAPath = "Environment/SM_Rock_Boulder_A";
        public const string RockBPath = "Environment/SM_Rock_Boulder_B";
        public const string CraterSmallPath = "Environment/SM_Crater_Small";
        public const string CraterMediumPath = "Environment/SM_Crater_Medium";
        public const string CraterLargePath = "Environment/SM_Crater_Large";
        public const string CraterVistaPath = "Environment/SM_Crater_Vista";
        public const string DunePath = "Environment/SM_Dune_Low";

        public const string EarthAlbedoPath = "Environment/Textures/SM_Ground_Earth_Albedo";
        public const string EarthNormalPath = "Environment/Textures/SM_Ground_Earth_Normal";
        public const string MarsAlbedoPath = "Environment/Textures/SM_Ground_Mars_Albedo";
        public const string MarsNormalPath = "Environment/Textures/SM_Ground_Mars_Normal";

        public const string WhiteHullAlbedoPath = "Art/Materials/SM_Mat_WhiteHull_Albedo";
        public const string WhiteHullNormalPath = "Art/Materials/SM_Mat_WhiteHull_Normal";
        public const string SteelAlbedoPath = "Art/Materials/SM_Mat_Steel_Albedo";
        public const string SteelNormalPath = "Art/Materials/SM_Mat_Steel_Normal";
        public const string SolarAlbedoPath = "Art/Materials/SM_Mat_Solar_Albedo";
        public const string CanvasAlbedoPath = "Art/Materials/SM_Mat_Canvas_Albedo";
        public const string CanvasNormalPath = "Art/Materials/SM_Mat_Canvas_Normal";
        public const string DustyMetalAlbedoPath = "Art/Materials/SM_Mat_DustyMetal_Albedo";

        /// <summary>Authored tree height in meters (Blender / Copilot export target).</summary>
        public const float TreeNativeHeight = 2.4f;
        public const float RockNativeSize = 1.0f;
        public const float CraterVistaNativeDiameter = 10f;
        public const float DuneNativeLength = 6f;

        public static GameObject LoadTree(int variant) => LoadTree(variant, CelestialBodyId.Mars);

        public static GameObject LoadTree(int variant, CelestialBodyId body)
        {
            if (body == CelestialBodyId.Earth)
            {
                var kit = VendorDressingKit.Load();
                var vendor = kit != null ? VendorDressingKit.Pick(kit.earthTrees, variant) : null;
                if (vendor != null) return vendor;
            }
            return Resources.Load<GameObject>(variant % 2 == 0 ? TreeAPath : TreeBPath)
                   ?? Resources.Load<GameObject>(TreeAPath)
                   ?? Resources.Load<GameObject>(TreeBPath);
        }

        public static GameObject LoadRock(int variant) => LoadRock(variant, CelestialBodyId.Mars);

        public static GameObject LoadRock(int variant, CelestialBodyId body)
        {
            if (body == CelestialBodyId.Earth)
            {
                var kit = VendorDressingKit.Load();
                var vendor = kit != null ? VendorDressingKit.Pick(kit.earthRocks, variant) : null;
                if (vendor != null) return vendor;
            }
            return Resources.Load<GameObject>(variant % 2 == 0 ? RockAPath : RockBPath)
                   ?? Resources.Load<GameObject>(RockAPath)
                   ?? Resources.Load<GameObject>(RockBPath);
        }

        public static GameObject LoadEarthShrub(int salt)
        {
            var kit = VendorDressingKit.Load();
            return kit != null ? VendorDressingKit.Pick(kit.earthShrubs, salt) : null;
        }

        public static GameObject LoadEarthGrass(int salt)
        {
            var kit = VendorDressingKit.Load();
            return kit != null ? VendorDressingKit.Pick(kit.earthGrass, salt) : null;
        }

        public static GameObject LoadEarthFlower(int salt)
        {
            var kit = VendorDressingKit.Load();
            return kit != null ? VendorDressingKit.Pick(kit.earthFlowers, salt) : null;
        }

        public static bool IsVendorNature(GameObject prefab)
        {
            if (prefab == null) return false;
            string n = prefab.name;
            return n.StartsWith("PT_") || n.IndexOf("Polytope", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Instantiate a Polytope nature prefab. Keep authored textures; remap Built-in
        /// surface shaders onto URP Lit so they are not magenta in this project's pipeline.
        /// </summary>
        public static GameObject InstantiateVendorNature(GameObject prefab, string name, float targetHeight = 0f)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.name = name;
            StripImportJunk(go);
            RemapVendorToUrp(go);
            if (targetHeight > 0.05f)
                FitHeight(go, targetHeight);
            ColonyVisualUtility.SnapToGround(go);
            return go;
        }

        public static void FitHeight(GameObject go, float targetHeight)
        {
            if (go == null || targetHeight <= 0.01f) return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null) b.Encapsulate(rends[i].bounds);
            }
            float h = b.size.y;
            if (h < 0.05f) return;
            go.transform.localScale *= targetHeight / h;
        }

        private static void RemapVendorToUrp(GameObject root)
        {
            if (root == null) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (lit == null) return;

            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null) continue;
                var src = rend.sharedMaterials;
                if (src == null || src.Length == 0) continue;
                var next = new Material[src.Length];
                for (int m = 0; m < src.Length; m++)
                {
                    var old = src[m];
                    if (old == null)
                    {
                        next[m] = MakeEnvMat(lit, rend.name, EnvColorFor(rend.name));
                        continue;
                    }
                    string shaderName = old.shader != null ? old.shader.name : "";
                    if (shaderName.IndexOf("Universal Render Pipeline", System.StringComparison.Ordinal) >= 0 ||
                        shaderName.IndexOf("Shader Graphs", System.StringComparison.Ordinal) >= 0)
                    {
                        next[m] = old;
                        continue;
                    }

                    Color c = EnvColorFor(old.name + " " + rend.name);
                    if (old.HasProperty("_BaseColor"))
                    {
                        Color authored = old.GetColor("_BaseColor");
                        if (authored.r + authored.g + authored.b < 2.7f) c = authored;
                    }
                    else if (old.HasProperty("_Color"))
                    {
                        Color authored = old.color;
                        if (authored.r + authored.g + authored.b < 2.7f) c = authored;
                    }
                    Texture tex = null;
                    if (old.HasProperty("_BaseTexture")) tex = old.GetTexture("_BaseTexture");
                    if (tex == null && old.HasProperty("_MainTex")) tex = old.GetTexture("_MainTex");
                    if (tex == null && old.HasProperty("_BaseMap")) tex = old.GetTexture("_BaseMap");
                    var mat = MakeEnvMat(lit, old.name, c);
                    if (tex != null && mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", tex);
                    string token = (old.name + " " + rend.name).ToLowerInvariant();
                    bool cutout = token.Contains("leaf") || token.Contains("grass") || token.Contains("flower") ||
                                  token.Contains("foliage") || token.Contains("plant") || token.Contains("poppy");
                    if (old.HasProperty("_Cutoff") && mat.HasProperty("_Cutoff"))
                    {
                        mat.SetFloat("_Cutoff", old.GetFloat("_Cutoff"));
                        cutout = true;
                    }
                    if (cutout && mat.HasProperty("_AlphaClip"))
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.EnableKeyword("_ALPHATEST_ON");
                    }
                    next[m] = mat;
                }
                rend.sharedMaterials = next;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }

        public static GameObject LoadCrater(int sizeClass)
        {
            switch (Mathf.Clamp(sizeClass, 0, 2))
            {
                case 0: return Resources.Load<GameObject>(CraterSmallPath);
                case 1: return Resources.Load<GameObject>(CraterMediumPath);
                default: return Resources.Load<GameObject>(CraterLargePath);
            }
        }

        public static GameObject LoadCraterVista()
        {
            return Resources.Load<GameObject>(CraterVistaPath)
                   ?? Resources.Load<GameObject>(CraterMediumPath)
                   ?? Resources.Load<GameObject>(CraterLargePath);
        }

        public static GameObject LoadDune() => Resources.Load<GameObject>(DunePath);

        public static Texture2D LoadEarthAlbedo() => Resources.Load<Texture2D>(EarthAlbedoPath);
        public static Texture2D LoadEarthNormal() => Resources.Load<Texture2D>(EarthNormalPath);
        public static Texture2D LoadMarsAlbedo() => Resources.Load<Texture2D>(MarsAlbedoPath);
        public static Texture2D LoadMarsNormal() => Resources.Load<Texture2D>(MarsNormalPath);

        /// <summary>Instantiate mesh root, strip cameras/lights/colliders, remap URP mats.</summary>
        public static GameObject InstantiateClean(GameObject meshPrefab, string name)
        {
            if (meshPrefab == null) return null;
            var go = Object.Instantiate(meshPrefab);
            go.name = name;
            StripImportJunk(go);
            // Do not run IndustrialArtDressing — it maps unknown SM_Leaf/SM_Trunk to white hull.
            RemapEnvironmentMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            return go;
        }

        /// <summary>
        /// URP Lit remap for trees/rocks/dunes using imported SM_* environment names.
        /// </summary>
        public static void RemapEnvironmentMaterials(GameObject root)
        {
            if (root == null) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (lit == null) return;

            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null) continue;
                var src = rend.sharedMaterials;
                if (src == null || src.Length == 0)
                {
                    rend.sharedMaterial = MakeEnvMat(lit, "SM_Leaf", EnvColorFor("sm_leaf"));
                    continue;
                }

                var next = new Material[src.Length];
                for (int m = 0; m < src.Length; m++)
                {
                    string token = src[m] != null ? src[m].name : rend.name;
                    Color c = EnvColorFor(token);
                    // Prefer authored BaseColor when present and not near-white stub.
                    if (src[m] != null)
                    {
                        Color authored = src[m].HasProperty("_BaseColor")
                            ? src[m].GetColor("_BaseColor")
                            : src[m].HasProperty("_Color") ? src[m].color : c;
                        if (authored.r + authored.g + authored.b < 2.55f)
                            c = authored;
                    }
                    next[m] = MakeEnvMat(lit, token, c);
                }
                rend.sharedMaterials = next;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }

        private static Material MakeEnvMat(Shader lit, string name, Color c)
        {
            var mat = new Material(lit) { name = "SM_Env_" + (string.IsNullOrEmpty(name) ? "Prop" : name) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private static Color EnvColorFor(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("trunk") || n.Contains("bark"))
                return new Color(0.32f, 0.18f, 0.08f);
            if (n.Contains("leafdark") || n.Contains("leaf_dark"))
                return new Color(0.14f, 0.32f, 0.12f);
            if (n.Contains("leaf") || n.Contains("canopy") || n.Contains("tree"))
                return new Color(0.22f, 0.42f, 0.16f);
            if (n.Contains("rockdark") || n.Contains("rock_dark"))
                return new Color(0.35f, 0.22f, 0.14f);
            if (n.Contains("rock") || n.Contains("boulder"))
                return new Color(0.55f, 0.32f, 0.18f);
            if (n.Contains("dune"))
                return new Color(0.72f, 0.42f, 0.22f);
            if (n.Contains("craterfloor"))
                return new Color(0.34f, 0.14f, 0.07f);
            if (n.Contains("crater") || n.Contains("rim"))
                return new Color(0.58f, 0.30f, 0.14f);
            return new Color(0.22f, 0.42f, 0.16f);
        }

        private static void StripImportJunk(GameObject root)
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (Application.isPlaying) Object.Destroy(cam.gameObject);
                else Object.DestroyImmediate(cam.gameObject);
            }
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (Application.isPlaying) Object.Destroy(light.gameObject);
                else Object.DestroyImmediate(light.gameObject);
            }
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }
    }
}
