using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Building LOD chains: which parts LOD1 keeps, that the thresholds hold against the iso
    /// camera's real zoom range, and that spawned buildings (not ghosts) carry a valid LODGroup.
    /// </summary>
    public class BuildingLodTests
    {
        /// <summary>Lowest LOD bias in ProjectSettings/QualitySettings (Very Low).</summary>
        private const float LowestLodBias = 0.3f;

        /// <summary>Smallest footprint: 4 cells.</summary>
        private static float SmallestBuilding => 4 * StillCampusDensity.DefaultCellSize;

        [Test]
        public void SelectLod1_DropsSmallParts_KeepsTheShells()
        {
            // 6 m building: hull, roof, two lamps, an antenna.
            var extents = new List<float> { 6f, 5.5f, 0.2f, 0.3f, 1.2f };
            var keep = BuildingLodRules.SelectLod1(extents, null, 6f);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, keep);
        }

        [Test]
        public void SelectLod1_DressingDropsAtALargerSize()
        {
            var extents = new List<float> { 9f, 0.7f, 0.7f };
            var dressing = new List<bool> { false, false, true };
            var keep = BuildingLodRules.SelectLod1(extents, dressing, 9f);
            // 0.7 / 9 = 7.8 %: kit detail stays, world dressing goes.
            CollectionAssert.AreEqual(new[] { 0, 1 }, keep);
        }

        [Test]
        public void SelectLod1_NeverEmpty_AndToleratesBadInput()
        {
            Assert.IsEmpty(BuildingLodRules.SelectLod1(null, null, 5f));
            Assert.IsEmpty(BuildingLodRules.SelectLod1(new List<float>(), null, 5f));

            // A wildly large building size would call everything detail; the largest part stays.
            var keep = BuildingLodRules.SelectLod1(new List<float> { 0.1f, 0.4f, 0.2f }, null, 100f);
            CollectionAssert.AreEqual(new[] { 1 }, keep);

            // Size 0 means "use the largest part"; zero-size bounds are always detail.
            keep = BuildingLodRules.SelectLod1(new List<float> { 0f, 3f, 2.9f }, new List<bool> { false }, 0f);
            CollectionAssert.AreEqual(new[] { 1, 2 }, keep);
        }

        [Test]
        public void Overlays_AreNeverPartOfTheBuilding()
        {
            foreach (var n in new[] { "SelectRing", "StatusOrb", "Kit_StatusPip", "YieldLabel", "Footprint",
                         "Ghost_Habitat", "SelectProxy", "HitVfx" })
                Assert.IsTrue(BuildingLodRules.IsOverlay(n), n);
            foreach (var n in new[] { "Hull", "Dress_S4_Detail_2_0", "Dress_Arch_Wall", "CoreMesh", "", null })
                Assert.IsFalse(BuildingLodRules.IsOverlay(n), n ?? "null");
            Assert.IsTrue(BuildingLodRules.IsDressing("Dress_Arch"));
            Assert.IsTrue(BuildingLodRules.IsDressing("Dress_Arch_spin"));
            Assert.IsFalse(BuildingLodRules.IsDressing("Dress_S4_Detail_0_1"));
        }

        [Test]
        public void Thresholds_KeepLod0AtPlayZoom_AndNeverCullInTheZoomRange()
        {
            Assert.Greater(BuildingLodRules.DetailScreenHeight, BuildingLodRules.CullScreenHeight);

            float play = BuildingLodRules.OrthoScreenHeight(SmallestBuilding, ColonyLayout.CampusOrthoSize, LowestLodBias);
            Assert.Greater(play, BuildingLodRules.DetailScreenHeight,
                "the smallest building keeps full detail at play zoom, even on Very Low");

            float far = BuildingLodRules.OrthoScreenHeight(SmallestBuilding, IsometricCameraController.MaxOrthoSize, LowestLodBias);
            Assert.Greater(far, BuildingLodRules.CullScreenHeight,
                "no building culls at max zoom-out, even on Very Low");
        }

        [Test]
        public void Spawn_PutsALodGroupOnBuildings_NotOnGhosts()
        {
            LogAssert.ignoreFailingMessages = true;
            var root = new GameObject("BuildingLodTestRoot");
            try
            {
                foreach (var cat in new[] { BuildingCategory.Habitat, BuildingCategory.Commons, BuildingCategory.EngineerWorkshop })
                {
                    var go = ModularBuildingFactory.Spawn(cat, Vector3.zero, root.transform);
                    var group = go.GetComponent<LODGroup>();
                    Assert.IsNotNull(group, $"{cat} has a LODGroup");
                    LOD[] lods = group.GetLODs();
                    Assert.That(lods.Length, Is.InRange(1, 2), $"{cat} LOD count");
                    Assert.AreEqual(BuildingLodRules.CullScreenHeight, lods[lods.Length - 1].screenRelativeTransitionHeight, 1e-5f);

                    var lod0 = new HashSet<Renderer>(lods[0].renderers);
                    Assert.Greater(lod0.Count, 0, $"{cat} LOD0 has renderers");
                    foreach (var r in lod0)
                        Assert.IsFalse(BuildingLodRules.IsOverlay(r.name), $"{cat}: {r.name} is an overlay");
                    if (lods.Length == 2)
                    {
                        Assert.Less(lods[1].renderers.Length, lods[0].renderers.Length, $"{cat} LOD1 drops something");
                        foreach (var r in lods[1].renderers)
                            Assert.IsTrue(lod0.Contains(r), $"{cat}: LOD1 part {r.name} is in LOD0");
                    }

                    var ghost = ModularBuildingFactory.Spawn(cat, Vector3.zero, root.transform, ghost: true);
                    Assert.IsNull(ghost.GetComponent<LODGroup>(), $"{cat} ghost has no LODGroup");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
