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
        public void GroundShader_ExposesGritTapAndCavity()
        {
            var shader = Shader.Find("SolarMajesty/PlanetGround");
            if (shader == null) Assert.Ignore("PlanetGround shader not in this build.");
            var mat = new Material(shader);
            try
            {
                // PlanetaryMapDressing.BindAuthoredGroundDetail drives these. A rename would
                // silently drop the pebble tap and leave the ground on one 8.5 m frequency.
                Assert.IsTrue(mat.HasProperty("_GritScale"));
                Assert.IsTrue(mat.HasProperty("_GritAmount"));
                Assert.IsTrue(mat.HasProperty("_GritCavity"));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void HullShader_ExposesGrimeParameters()
        {
            var shader = Shader.Find("SolarMajesty/Hull");
            if (shader == null) Assert.Ignore("Hull shader not in this build.");
            var mat = new Material(shader);
            try
            {
                Assert.IsTrue(mat.HasProperty("_GrimeColor"));
                Assert.IsTrue(mat.HasProperty("_GrimeAmount"));
                Assert.IsTrue(mat.HasProperty("_GrimeRise"));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void KitHullMaterial_KeepsAuthoredColourAndStaysNeutralOnGrime()
        {
            var orange = new Color(0.96f, 0.42f, 0.08f);
            var mat = IndustrialArtDressing.BuildKitHullMaterial(orange, Color.black);
            if (mat == null) Assert.Ignore("Hull shader not in this build.");
            try
            {
                // Pad / extractor / ship prims route through here. Their authored colour has to
                // survive, and the grime tint must never pick up the body's ground hue.
                Assert.AreEqual(orange, mat.GetColor("_BaseColor"));
                Assert.Greater(mat.GetFloat("_GrimeAmount"), 0f);
                Color grime = mat.GetColor("_GrimeColor");
                Assert.Less(Mathf.Abs(grime.r - grime.b), 0.12f,
                    "Grime must stay neutral — a saturated tint is the orange hull wash again.");
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void DressPrefixedKitPrims_AreSkippedBySlotRemap()
        {
            // This is the coupling that lets HeroBuildingKits give its Dress_* prims a hull
            // surface: if the skip rule stops covering them, they get slot materials instead and
            // the branch in HeroBuildingKits.TryHullSurface goes dead.
            Assert.IsTrue(IndustrialArtDressing.IsSkippedName("Dress_PadDisc"));
            Assert.IsTrue(IndustrialArtDressing.IsSkippedName("Dress_StarshipBody"));
            Assert.IsTrue(IndustrialArtDressing.IsSkippedName("Dress_RegSphere"));

            // The canvas porch is deliberately un-prefixed so the slot remap reaches it.
            Assert.IsFalse(IndustrialArtDressing.IsSkippedName("IceCanvasShade"));
            Assert.IsFalse(IndustrialArtDressing.IsSkippedName("InnCanvasShade"));
        }

        [Test]
        public void CanvasPorchNames_MapToTheFabricSlot()
        {
            // The awning is the only soft material on the concept sheet and it exists purely
            // because these names hit the Canvas slot. Posts and ridge must not follow it there.
            Assert.AreEqual("Canvas", IndustrialArtDressing.SlotNameFor("IceCanvasShade"));
            Assert.AreEqual("Canvas", IndustrialArtDressing.SlotNameFor("InnCanvasShade"));
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("IcePorchPostSteel_0"));
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("IceDeckRailSteel_front"));
            Assert.AreEqual("BlackCarbon", IndustrialArtDressing.SlotNameFor("IceRidgeBand"));
            Assert.AreEqual("BlackCarbon", IndustrialArtDressing.SlotNameFor("InnPorchRidgeBand"));
        }

        [Test]
        public void SilhouetteAddNames_MapToTheirIntendedMaterials()
        {
            // Solar frames read as metal only because "Steel" is in the name; a bare "SolarFrame"
            // fell through to the PV slot and rendered the frames as more glass.
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("SolarSteelFrame_0_0"));
            Assert.AreEqual("Solar", IndustrialArtDressing.SlotNameFor("SolarArray_0_0"));

            // Commons entry: orange hatch in a carbon surround, on a graphite stoop.
            Assert.AreEqual("Orange", IndustrialArtDressing.SlotNameFor("CommonsDoorHatch_0"));
            Assert.AreEqual("Orange", IndustrialArtDressing.SlotNameFor("CommonsSkirtStripe"));
            Assert.AreEqual("BlackCarbon", IndustrialArtDressing.SlotNameFor("CommonsDoorBand_0"));
            Assert.AreEqual("Graphite", IndustrialArtDressing.SlotNameFor("CommonsStoopPlinth_0"));
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("CommonsRailSteel_0"));

            Assert.AreEqual("Graphite", IndustrialArtDressing.SlotNameFor("HabStairPlinth_0"));
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("HabStairRailSteel_1"));
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("PwrMastSteel"));
            Assert.AreEqual("Steel", IndustrialArtDressing.SlotNameFor("PwrMastAntenna"));
        }
    }
}
