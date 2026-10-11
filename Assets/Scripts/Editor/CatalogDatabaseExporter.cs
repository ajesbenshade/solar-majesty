using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Writes the built-in tech, shop and world tables to ScriptableObject databases under
    /// <c>Assets/Resources/Data/</c> so designers can edit them in the Inspector. The game reads
    /// those assets when they exist and falls back to the code tables when they do not.
    /// See Docs/DATA_CATALOGS.md.
    /// </summary>
    public static class CatalogDatabaseExporter
    {
        private const string Folder = "Assets/Resources/" + CatalogDatabase.ResourceFolder;

        [MenuItem("Solar Majesty/Data/Export catalogs to assets", priority = 300)]
        public static void ExportFromMenu()
        {
            var existing = new List<string>();
            foreach (string path in Paths())
            {
                if (File.Exists(path)) existing.Add(Path.GetFileName(path));
            }

            if (existing.Count > 0 && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "Export catalogs",
                    "These databases already exist and will be replaced with the built-in data. " +
                    "Any edits in them will be lost:\n\n" + string.Join("\n", existing),
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            int written = Export();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Export catalogs",
                    $"Wrote {written} database assets to {Folder}.\nThe game now reads these instead of the code tables.",
                    "OK");
            }
        }

        /// <summary>Unity -batchmode -executeMethod SolarMajesty.EditorTools.CatalogDatabaseExporter.ExportCli</summary>
        public static void ExportCli()
        {
            int written = Export();
            EditorApplication.Exit(written == 3 ? 0 : 1);
        }

        /// <summary>Writes all three databases from the built-in defaults. Returns how many were written.</summary>
        public static int Export()
        {
            EnsureFolder();
            int written = 0;

            var bodies = new List<CelestialBodyProfile>();
            foreach (var id in CelestialBodyCatalog.All)
                bodies.Add(CelestialBodyCatalog.Default(id));

            if (Write(TechDatabase.FromDefs(TechCatalog.Defaults), TechDatabase.ResourcePath)) written++;
            if (Write(ShopDatabase.FromDefs(ShopCatalog.Defaults), ShopDatabase.ResourcePath)) written++;
            if (Write(CelestialBodyDatabase.FromProfiles(bodies), CelestialBodyDatabase.ResourcePath)) written++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TechCatalog.ReloadDatabase();
            ShopCatalog.ReloadDatabase();
            CelestialBodyCatalog.ReloadDatabase();
            Debug.Log($"[Catalog] Exported {written} catalog databases to {Folder}.");
            return written;
        }

        private static IEnumerable<string> Paths()
        {
            yield return AssetPath(TechDatabase.ResourcePath);
            yield return AssetPath(ShopDatabase.ResourcePath);
            yield return AssetPath(CelestialBodyDatabase.ResourcePath);
        }

        private static string AssetPath(string resourcePath) => "Assets/Resources/" + resourcePath + ".asset";

        private static bool Write(ScriptableObject db, string resourcePath)
        {
            if (db == null) return false;
            string path = AssetPath(resourcePath);
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(db, path);
            return true;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources", CatalogDatabase.ResourceFolder);
        }
    }
}
