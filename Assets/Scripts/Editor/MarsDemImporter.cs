using System.IO;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Applies readable R16 import settings to the vendored Mars heightmap.
    /// Credit: NASA/JPL-Caltech/UArizona HiRISE DTEEC_001414_1780 (Victoria crater sample).
    /// Re-crop from a 16-bit PNG with Tools/bake_mars_dem.py, then run this menu.
    /// </summary>
    public static class MarsDemImporter
    {
        public const string HeightAssetPath = "Assets/Resources/World/MarsDem/MarsHeight.png";
        public const string SettingsAssetPath = "Assets/Resources/World/MarsDem/MarsDemSettings.asset";

        [MenuItem("Solar Majesty/World/Import Mars DEM", priority = 210)]
        public static void Import()
        {
            if (!File.Exists(HeightAssetPath))
            {
                Debug.LogError("[MarsDEM] Missing " + HeightAssetPath + " — run Tools/bake_mars_dem.py first.");
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
                Debug.LogError("[MarsDEM] TextureImporter missing for " + HeightAssetPath);
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

            var so = AssetDatabase.LoadAssetAtPath<MarsDemSettings>(SettingsAssetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<MarsDemSettings>();
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath) ?? "Assets/Resources/World/MarsDem");
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
            Debug.Log("[MarsDEM] Imported " + HeightAssetPath + " (NASA/JPL-Caltech/UArizona HiRISE DTEEC_001414_1780).");
        }
    }
}
