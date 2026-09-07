using NUnit.Framework;

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
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize, StillCampusDensity.StillMinOrtho);
            Assert.AreEqual(4, StillCampusDensity.MinYardGapCells);
            Assert.AreEqual(16f, StillCampusDensity.MaxCenterSeparationCells);
        }
    }
}
