using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Dream Loop look tiles + ground paths must stay stable so Verify Environment
    /// Assets and IndustrialArtDressing.LoadOrBuild keep resolving.
    /// </summary>
    public class LookAssetPathTests
    {
        [Test]
        public void GroundTexturePaths_StayUnderEnvironmentTextures()
        {
            Assert.AreEqual("Environment/Textures/SM_Ground_Mars_Albedo", EnvironmentMeshCatalog.MarsAlbedoPath);
            Assert.AreEqual("Environment/Textures/SM_Ground_Mars_Normal", EnvironmentMeshCatalog.MarsNormalPath);
            Assert.AreEqual("Environment/Textures/SM_Ground_Earth_Albedo", EnvironmentMeshCatalog.EarthAlbedoPath);
        }

        [Test]
        public void LookMaterialPaths_StayUnderArtMaterials()
        {
            Assert.AreEqual("Art/Materials/SM_Mat_WhiteHull_Albedo", EnvironmentMeshCatalog.WhiteHullAlbedoPath);
            Assert.AreEqual("Art/Materials/SM_Mat_Steel_Albedo", EnvironmentMeshCatalog.SteelAlbedoPath);
            Assert.AreEqual("Art/Materials/SM_Mat_Solar_Albedo", EnvironmentMeshCatalog.SolarAlbedoPath);
            Assert.AreEqual("Art/Materials/SM_Mat_Canvas_Albedo", EnvironmentMeshCatalog.CanvasAlbedoPath);
            Assert.AreEqual("Art/Materials/SM_Mat_DustyMetal_Albedo", EnvironmentMeshCatalog.DustyMetalAlbedoPath);
        }

        [Test]
        public void StillCampus_KeepsSpacedPlayOrtho()
        {
            Assert.AreEqual(10f, StillCampusDensity.PlayCampusOrthoSize);
            Assert.AreEqual(0, StillCampusDensity.StillFrameInsetCells);
        }

        [Test]
        public void VendorDressingKit_ResourcePathIsStable()
        {
            Assert.AreEqual("Dressing/VendorDressingKit", VendorDressingKit.ResourcePath);
        }

        [Test]
        public void TerrainSplatLayers_ResourcePathIsStable()
        {
            Assert.AreEqual("Dressing/TerrainSplatLayers", TerrainSplatLayers.ResourcePath);
        }

        [Test]
        public void TdbSplatAlbedo_GrassSandSnowAreDefaultSrgbRepeat()
        {
            AssertLayer(TerrainSplatLayers.SandAssetPath, "TDB Sand");
            AssertLayer(TerrainSplatLayers.GrassAssetPath, "TDB Grass");
            AssertLayer(TerrainSplatLayers.SnowAssetPath, "TDB Snow");
        }

        private static void AssertLayer(string path, string label)
        {
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, label);
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            Assert.IsNotNull(importer, label + " importer");
            Assert.AreEqual(UnityEditor.TextureImporterType.Default, importer.textureType, label + " must not be a Normal map");
            Assert.IsTrue(importer.sRGBTexture, label + " sRGB");
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode, label + " Repeat");
        }

        [Test]
        public void VendorHumanMotions_DummyAndWalkClipsExist()
        {
            var dummy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/Prefabs/Human_BasicMotionsDummy_M.prefab");
            Assert.IsNotNull(dummy, "Human Basic Motions dummy");
            var clips = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx");
            bool walk = false;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] is AnimationClip c && c.name.IndexOf("Walk") >= 0 &&
                        !c.name.StartsWith("__preview__"))
                        walk = true;
                }
            }
            Assert.IsTrue(walk, "HumanM@Walk01_Forward clip");
        }

        [Test]
        public void VendorEarthNature_PolytopeTreesExist()
        {
            var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab");
            Assert.IsNotNull(tree, "Polytope fruit tree");
            var pine = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab");
            Assert.IsNotNull(pine, "Polytope pine");
            var grass = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Plants/PT_Grass_02.prefab");
            Assert.IsNotNull(grass, "Polytope grass");
        }

        [Test]
        public void SuitCrossing_FallsBackToMannequinWithoutKit()
        {
            var root = new GameObject("VendorDressingTestRoot");
            try
            {
                var fig = HeroBuildingKits.BuildSpacesuitFigure(root.transform, Vector3.zero, 0f, 0);
                Assert.IsNotNull(fig);
                Assert.IsTrue(fig.name.StartsWith("Dress_SuitCrossing_"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
