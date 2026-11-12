using UnityEditor;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Import settings for civ building FBX dropped in Assets/Resources/Buildings/Civ
    /// (3D Artist). The same model settings as <see cref="CitadelImportRules"/>: scale 1,
    /// hierarchy and empties kept, embedded materials named after the FBX slots
    /// (Trim, Carbon, TeamColour, Accent, CoreEmission, and HAB's ChargerAmber), no cameras, lights, colliders
    /// or animation. Textures stay on the shared citadel trim sheet; this folder does not
    /// reimport them.
    /// </summary>
    public sealed class CivBuildingImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/" + CivBuildingCatalog.Root + "/";

        private bool InRoot => assetPath.Replace('\\', '/').StartsWith(Root);

        private void OnPreprocessModel()
        {
            if (!InRoot) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileUnits = true;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = true;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
        }
    }
}
