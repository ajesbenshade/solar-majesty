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
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            return go;
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
