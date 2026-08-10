using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Bakes the runtime lunar grade into Resources/Atmosphere/DemoVolumeProfile.asset.
    /// </summary>
    public static class DemoVolumeProfileBaker
    {
        private const string Dir = "Assets/Resources/Atmosphere";
        private const string AssetPath = Dir + "/DemoVolumeProfile.asset";

        [MenuItem("Solar Majesty/Bake Demo Volume Profile")]
        public static void BakeMenu()
        {
            Bake();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Solar Majesty] Volume profile baked → {AssetPath}");
        }

        /// <summary>CLI: -executeMethod SolarMajesty.EditorTools.DemoVolumeProfileBaker.BakeCli</summary>
        public static void BakeCli()
        {
            Bake();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        public static void Bake()
        {
            if (!Directory.Exists(Dir))
                Directory.CreateDirectory(Dir);

            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(AssetPath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DemoVolumeProfile";

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            color.postExposure.Override(0.05f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.55f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.4f);
            vignette.color.Override(new Color(0.05f, 0.06f, 0.1f));

            AssetDatabase.CreateAsset(profile, AssetPath);
            // Components are sub-assets of VolumeProfile when added via Add<>.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
