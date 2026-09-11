using System.IO;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Applies readable R16 import settings to the vendored Luna heightmap.
    /// Credit: NASA/GSFC/Arizona State University LROC NAC_DTM_LINNECRATER.
    /// Re-crop with Tools/bake_luna_dem.py, then run this menu.
    /// </summary>
    public static class LunaDemImporter
    {
        public const string HeightAssetPath = "Assets/Resources/World/LunaDem/LunaHeight.png";
        public const string SettingsAssetPath = "Assets/Resources/World/LunaDem/LunaDemSettings.asset";

        [MenuItem("Solar Majesty/World/Import Luna DEM", priority = 211)]
        public static void Import()
        {
            if (!File.Exists(HeightAssetPath))
            {
                Debug.LogError("[LunaDEM] Missing " + HeightAssetPath + " — run Tools/bake_luna_dem.py first.");
                return;
            }

            var importer = AssetImporter.GetAtPath(HeightAssetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(HeightAssetPath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(HeightAssetPath) as TextureImporter;
            }
            if (importer == null)
            {
                Debug.LogError("[LunaDEM] TextureImporter missing for " + HeightAssetPath);
                return;
            }

            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Default;
            settings.sRGBTexture = false;
            settings.readable = true;
            settings.mipmapEnabled = false;
            settings.alphaSource = TextureImporterAlphaSource.None;
            importer.SetTextureSettings(settings);
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                overridden = true,
                maxTextureSize = 1024,
                format = TextureImporterFormat.R16,
                textureCompression = TextureImporterCompression.Uncompressed
            });
            importer.SaveAndReimport();

            var so = AssetDatabase.LoadAssetAtPath<LunaDemSettings>(SettingsAssetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<LunaDemSettings>();
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath) ?? "Assets/Resources/World/LunaDem");
                AssetDatabase.CreateAsset(so, SettingsAssetPath);
            }
            so.height = AssetDatabase.LoadAssetAtPath<Texture2D>(HeightAssetPath);
            so.campusUv = new Vector2(0.5f, 0.5f);
            so.yawDegrees = 0f;
            so.coverageMetres = 80f;
            so.metresPerPixel = 80f / 512f;
            so.verticalScale = 1f;
            so.heightRange = 32f;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log("[LunaDEM] Imported " + HeightAssetPath + " (NASA/GSFC/ASU LROC NAC_DTM_LINNECRATER).");
        }
    }
}
