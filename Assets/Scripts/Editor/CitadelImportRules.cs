using UnityEditor;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Import settings for the civ castles in Assets/Resources/Citadels (3D Artist, PR #130),
    /// so they follow the folder instead of being clicked per file and every machine imports
    /// them the same way. <see cref="CivCastleLoader"/> relies on these:
    /// - models: file units at scale 1 (CASTLE_SCALE 1.44 is baked into the vertices), embedded
    ///   materials named after the FBX slots (Trim, Carbon, TeamColour, Accent, CoreEmission),
    ///   empties kept (the anchors), no cameras, lights, colliders or animation;
    /// - trim sheet: the normal map as a normal map, the data maps linear.
    /// </summary>
    public sealed class CitadelImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Citadels/";

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

        private void OnPreprocessTexture()
        {
            if (!InRoot) return;
            var importer = (TextureImporter)assetImporter;
            string path = assetPath.Replace('\\', '/');
            if (path.EndsWith("_Normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = path.EndsWith("_BaseColor.png") || path.EndsWith("_Emission.png");
                // URP Lit reads smoothness from the alpha of the metallic map.
                importer.alphaSource = path.EndsWith("_MetallicSmoothness.png")
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
            }
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
        }
    }
}
