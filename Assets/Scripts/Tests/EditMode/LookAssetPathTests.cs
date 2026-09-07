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
            Assert.AreEqual("Art/Materials/SM_Mat_MarsRock_Albedo", EnvironmentMeshCatalog.MarsRockAlbedoPath);
            Assert.AreEqual("Art/Materials/SM_Mat_MarsRock_Normal", EnvironmentMeshCatalog.MarsRockNormalPath);
        }

        [Test]
        public void StillCampus_KeepsSpacedPlayOrtho()
        {
            Assert.AreEqual(10f, StillCampusDensity.PlayCampusOrthoSize);
            Assert.AreEqual(0, StillCampusDensity.StillFrameInsetCells);
        }

        [Test]
        public void HeroKits_ExposeLockedConceptSilhouetteCues()
        {
            var root = new GameObject("LookCueTest");
            try
            {
                var inn = new GameObject("Inn").transform;
                inn.SetParent(root.transform);
                HeroBuildingKits.BuildInn(inn, 6f, 6f);
                Assert.IsNotNull(inn.Find("InnCanvasAwning"), "locked concept tan awning");

                var hab = new GameObject("Hab").transform;
                hab.SetParent(root.transform);
                HeroBuildingKits.BuildHabitat(hab, 6f, 6f, Color.white);
                Assert.IsNotNull(hab.Find("HabAccessStep_0"), "HAB exterior stair");

                var commons = new GameObject("Commons").transform;
                commons.SetParent(root.transform);
                HeroBuildingKits.BuildCommons(commons, 9f, 9f, Color.white);
                Assert.IsNotNull(commons.Find("CommonsGeoBrace_0"), "Commons geodesic brace");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
