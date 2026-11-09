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

        [MenuItem("Solar Majesty/Data/Create World Database", priority = 301)]
        public static void CreateWorldDatabase()
        {
            var db = LoadOrCreate<WorldDatabase>("WorldDatabase");
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            Debug.Log($"[DataExport] World database at {AssetDatabase.GetAssetPath(db)}");
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
