using System.IO;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>Writes the built-in catalogs out as assets so they can be edited as data.</summary>
    public static class DataExport
    {
        private const string Folder = "Assets/Resources/Data";

        [MenuItem("Solar Majesty/Data/Export Tech Database", priority = 300)]
        public static void ExportTechDatabase()
        {
            // Export the built-in tree, not whatever asset is already overriding it.
            TechCatalog.Reload(useAsset: false);
            var db = LoadOrCreate<TechDatabase>("TechDatabase");
            db.CopyFrom(TechCatalog.All);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            TechCatalog.Reload();
            Debug.Log($"[DataExport] {db.entries.Count} techs -> {AssetDatabase.GetAssetPath(db)}");
        }

        [MenuItem("Solar Majesty/Data/Export Shop Database", priority = 301)]
        public static void ExportShopDatabase()
        {
            ShopCatalog.Reload(useAsset: false);
            var db = LoadOrCreate<ShopDatabase>("ShopDatabase");
            db.CopyFrom(ShopCatalog.All);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            ShopCatalog.Reload();
            Debug.Log($"[DataExport] {db.entries.Count} shop items -> {AssetDatabase.GetAssetPath(db)}");
        }

        /// <summary>Writes the five built-in Sol worlds. Worlds beyond the five in the asset are kept.</summary>
        [MenuItem("Solar Majesty/Data/Export World Database", priority = 302)]
        public static void ExportWorldDatabase()
        {
            var db = LoadOrCreate<WorldDatabase>("WorldDatabase");
            db.sol.Clear();
            CelestialBodyId[] bodies = CelestialBodyCatalog.All;
            for (int i = 0; i < bodies.Length; i++)
                db.sol.Add(CelestialBodyCatalog.BuiltIn(bodies[i]));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            CelestialBodyCatalog.Reload();
            Selection.activeObject = db;
            Debug.Log($"[DataExport] {db.sol.Count} Sol worlds -> {AssetDatabase.GetAssetPath(db)}");
        }

        /// <summary>All three databases from the built-in catalogs. Also the batch-mode entry point.</summary>
        [MenuItem("Solar Majesty/Data/Export All Databases", priority = 320)]
        public static void ExportAll()
        {
            ExportTechDatabase();
            ExportShopDatabase();
            ExportWorldDatabase();
        }

        private static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Folder);
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
