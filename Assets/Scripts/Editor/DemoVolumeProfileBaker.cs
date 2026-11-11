using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Bakes the lunar grade into Resources/Atmosphere/DemoVolumeProfile.asset for previewing in
    /// the Scene view. The game no longer loads this asset: <see cref="DemoAtmosphere"/> builds
    /// its profile in code from <see cref="WorldLook"/> through <see cref="LookVolume"/>, and this
    /// bakes from the same source so the preview cannot drift.
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

            var luna = CelestialBodyCatalog.Default(CelestialBodyId.Luna);
            var profile = LookVolume.NewProfile("DemoVolumeProfile");
            LookVolume.Apply(profile, WorldLook.For(CelestialBodyId.Luna), luna.GradeFilter, LookTier.Ultra);

            AssetDatabase.CreateAsset(profile, AssetPath);
            // VolumeProfile.Add<> only creates the components in memory. Unless each one is
            // stored as a sub-asset the profile serializes a list of null references, which is
            // how the previous bake ended up empty.
            for (int i = 0; i < profile.components.Count; i++)
            {
                var component = profile.components[i];
                if (component == null) continue;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
