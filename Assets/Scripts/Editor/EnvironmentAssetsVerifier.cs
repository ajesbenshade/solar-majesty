using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SolarMajesty.EditorTools
{
#if UNITY_EDITOR
    /// <summary>Quick Resources path check for landscape assets.</summary>
    public static class EnvironmentAssetsVerifier
    {
        [MenuItem("Solar Majesty/Verify Environment Assets")]
        public static void VerifyFromMenu()
        {
            int miss = Verify();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Environment Assets",
                    miss == 0 ? "All environment Resources paths OK." : $"{miss} missing — see Console.",
                    "OK");
            }
        }

        /// <summary>Unity -batchmode -executeMethod SolarMajesty.EditorTools.EnvironmentAssetsVerifier.VerifyCli</summary>
        public static void VerifyCli()
        {
            int miss = Verify();
            EditorApplication.Exit(miss > 0 ? 1 : 0);
        }

        private static int Verify()
        {
            int ok = 0;
            int miss = 0;
            void Check(string path, string label)
            {
                Object asset = Resources.Load(path);
                if (asset != null)
                {
                    Debug.Log($"[EnvVerify] OK  {label}: {path}");
                    ok++;
                }
                else
                {
                    Debug.LogWarning($"[EnvVerify] MISSING {label}: {path}");
                    miss++;
                }
            }

            Check(EnvironmentMeshCatalog.EarthAlbedoPath, "Earth albedo");
            Check(EnvironmentMeshCatalog.EarthNormalPath, "Earth normal");
            Check(EnvironmentMeshCatalog.MarsAlbedoPath, "Mars albedo");
            Check(EnvironmentMeshCatalog.MarsNormalPath, "Mars normal");
            Check(EnvironmentMeshCatalog.WhiteHullAlbedoPath, "White hull albedo");
            Check(EnvironmentMeshCatalog.SteelAlbedoPath, "Steel albedo");
            Check(EnvironmentMeshCatalog.SolarAlbedoPath, "Solar albedo");
            Check(EnvironmentMeshCatalog.CanvasAlbedoPath, "Canvas albedo");
            Check(EnvironmentMeshCatalog.DustyMetalAlbedoPath, "Dusty metal albedo");
            Check(EnvironmentMeshCatalog.TreeAPath, "Tree A");
            Check(EnvironmentMeshCatalog.TreeBPath, "Tree B");
            Check(EnvironmentMeshCatalog.RockAPath, "Rock A");
            Check(EnvironmentMeshCatalog.RockBPath, "Rock B");
            Check(EnvironmentMeshCatalog.CraterVistaPath, "Crater vista");
            Check(EnvironmentMeshCatalog.DunePath, "Dune");
            Check(EnvironmentMeshCatalog.CraterSmallPath, "Crater small");
            Check(EnvironmentMeshCatalog.CraterMediumPath, "Crater medium");
            Check(EnvironmentMeshCatalog.CraterLargePath, "Crater large");
            Check(TerrainSplatLayers.ResourcePath, "TDB splat layers");

            void CheckAsset(string path, string label)
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (asset != null)
                {
                    Debug.Log($"[EnvVerify] OK  {label}: {path}");
                    ok++;
                }
                else
                {
                    Debug.LogWarning($"[EnvVerify] MISSING {label}: {path}");
                    miss++;
                }
            }

            CheckAsset(TerrainSplatLayers.SandAssetPath, "TDB Sand albedo");
            CheckAsset(TerrainSplatLayers.GrassAssetPath, "TDB Grass albedo");
            CheckAsset(TerrainSplatLayers.SnowAssetPath, "TDB Snow albedo");
            Debug.Log($"[EnvVerify] Summary OK={ok} MISSING={miss}");
            return miss;
        }
    }
#endif
}
