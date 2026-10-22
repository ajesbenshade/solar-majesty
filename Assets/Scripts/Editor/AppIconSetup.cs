using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// The application icon: Assets/Art/Icon/SolarMajesty_Icon_*.png, made by
    /// Tools/steam/make_store_art.py (small sizes are drawn separately so they stay readable).
    /// Sets the Default Icon and every Standalone icon slot to the closest hand-made size.
    /// <see cref="BuildCommands"/> calls <see cref="EnsureAssigned"/> before each build; it writes
    /// ProjectSettings only when something differs, so normal builds cause no churn.
    /// </summary>
    public static class AppIconSetup
    {
        public const string Folder = "Assets/Art/Icon";
        public const string MasterPath = Folder + "/SolarMajesty_Icon_1024.png";
        private static readonly int[] MadeSizes = { 16, 24, 32, 48, 64, 128, 256, 512, 1024 };

        [MenuItem("Solar Majesty/Build/Assign App Icon", priority = 210)]
        public static void AssignMenu()
        {
            bool changed = EnsureAssigned();
            Debug.Log(changed ? "[Icon] Application icon assigned." : "[Icon] Application icon already assigned.");
        }

        /// <summary>CI / batch: -executeMethod SolarMajesty.EditorTools.AppIconSetup.AssignBatch</summary>
        public static void AssignBatch()
        {
            bool ok = File.Exists(MasterPath);
            if (ok) EnsureAssigned();
            else Debug.LogError("[Icon] " + MasterPath + " is missing. Run Tools/steam/make_store_art.py icon.");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Assigns the icon if it is not already. True if anything changed.</summary>
        public static bool EnsureAssigned()
        {
            if (!File.Exists(MasterPath))
            {
                Debug.LogWarning("[Icon] " + MasterPath + " is missing; the build keeps Unity's default icon.");
                return false;
            }

            bool changed = ConfigureImporters();

            Texture2D master = AssetDatabase.LoadAssetAtPath<Texture2D>(MasterPath);
            Texture2D[] current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (current == null || current.Length == 0 || current[0] != master)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { master }, IconKind.Any);
                changed = true;
            }

            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            Texture2D[] wanted = sizes.Select(s => AssetDatabase.LoadAssetAtPath<Texture2D>(PathFor(s))).ToArray();
            Texture2D[] have = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
            if (have == null || have.Length != wanted.Length || !have.SequenceEqual(wanted))
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, wanted, IconKind.Any);
                changed = true;
            }

            if (changed) AssetDatabase.SaveAssets();
            return changed;
        }

        /// <summary>The smallest hand-made size at least as large as the slot (or the master).</summary>
        public static string PathFor(int slot)
        {
            int pick = MadeSizes.FirstOrDefault(m => m >= slot);
            if (pick == 0) pick = 1024;
            return $"{Folder}/SolarMajesty_Icon_{pick}.png";
        }

        /// <summary>Icons want exact pixels: no compression, no mipmaps, no power-of-two rescale.</summary>
        private static bool ConfigureImporters()
        {
            bool changed = false;
            foreach (int s in MadeSizes)
            {
                string path = $"{Folder}/SolarMajesty_Icon_{s}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                int max = Mathf.Max(32, Mathf.NextPowerOfTwo(s));
                if (ti.textureType == TextureImporterType.Default && !ti.mipmapEnabled &&
                    ti.textureCompression == TextureImporterCompression.Uncompressed &&
                    ti.npotScale == TextureImporterNPOTScale.None && ti.alphaIsTransparency &&
                    ti.maxTextureSize == max)
                    continue;
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.alphaIsTransparency = true;
                ti.maxTextureSize = max;
                ti.SaveAndReimport();
                changed = true;
            }
            return changed;
        }
    }
}
