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

        public static GameObject LoadTree(int variant)
        {
            return Resources.Load<GameObject>(variant % 2 == 0 ? TreeAPath : TreeBPath)
                   ?? Resources.Load<GameObject>(TreeAPath)
                   ?? Resources.Load<GameObject>(TreeBPath);
        }

        public static GameObject LoadRock(int variant)
        {
            return Resources.Load<GameObject>(variant % 2 == 0 ? RockAPath : RockBPath)
                   ?? Resources.Load<GameObject>(RockAPath)
                   ?? Resources.Load<GameObject>(RockBPath);
        }

        /// <summary>
        /// Low-poly boulder only. Boulder_B is a multi-MB Copilot scan — fine for a dozen vista
        /// rocks, wrong for the near-campus pebble field which instances it ~80 times.
        /// </summary>
        public static GameObject LoadPebbleRock() => Resources.Load<GameObject>(RockAPath);

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
