using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards for the enemy fauna v2 art (Blender/scripts/sm_fauna_v2.py via sm_animated_roster.py):
    /// every creature ships Idle / Walk / Strike / Down takes, a measured stride speed, two materials
    /// (body atlas + shared orange glow), and its Unit_* prefab still resolves its mesh.
    /// </summary>
    public class FaunaArtTests
    {
        private static readonly string[] Clips = { "Idle", "Walk", "Strike", "Down" };

        [TestCase("SM_Unit_DustStalker")]
        [TestCase("SM_Unit_AshHopper")]
        [TestCase("SM_Unit_SoilCreeper")]
        [TestCase("SM_Unit_RockTick")]
        [TestCase("SM_Unit_RegolithMite")]
        [TestCase("SM_Unit_WattLeech")]
        [TestCase("SM_Unit_IceWisp")]
        public void FaunaFbx_ShipsFourClips_AndStrideSpeed(string unit)
        {
            var clips = Resources.LoadAll<AnimationClip>("Units/" + unit);
            Assert.IsNotNull(clips, unit);
            foreach (var want in Clips)
            {
                AnimationClip hit = null;
                foreach (var c in clips)
                {
                    if (c != null && UnitClipPlayer.ClipKey(c.name) == want) hit = c;
                }
                Assert.IsNotNull(hit, $"{unit} missing {want} clip");
                Assert.Greater(hit.length, 0.3f, $"{unit} {want} clip length");
            }
            Assert.Greater(UnitClipPlayer.AuthoredWalkSpeed(unit), 0.3f, $"{unit} stride speed in UnitClipMeta.json");
        }

        [TestCase("SM_Unit_DustStalker")]
        [TestCase("SM_Unit_AshHopper")]
        [TestCase("SM_Unit_SoilCreeper")]
        [TestCase("SM_Unit_RockTick")]
        [TestCase("SM_Unit_RegolithMite")]
        [TestCase("SM_Unit_WattLeech")]
        [TestCase("SM_Unit_IceWisp")]
        public void FaunaFbx_UsesAuthoredMaterials_AndBindsDownClip(string unit)
        {
            var prefab = Resources.Load<GameObject>("Units/" + unit);
            Assert.IsNotNull(prefab, unit);
            var go = Object.Instantiate(prefab);
            try
            {
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotNull(smr, $"{unit} skinned renderer");
                Assert.IsNotNull(smr.sharedMesh, $"{unit} mesh");
                Assert.That(smr.sharedMesh.triangles.Length / 3, Is.InRange(1500, 3000), $"{unit} triangle budget");
                var mats = smr.sharedMaterials;
                Assert.AreEqual(2, mats.Length, $"{unit} material slots");
                foreach (var m in mats)
                {
                    Assert.IsNotNull(m, $"{unit} material");
                    StringAssert.StartsWith("SM_Art_Fauna", m.name, $"{unit} material name");
                }

                var player = UnitClipPlayer.Bind(go, unit);
                Assert.IsNotNull(player, $"{unit} clip player");
                Assert.IsTrue(player.Ready, $"{unit} clip player ready");
                Assert.IsTrue(player.HasDownClip, $"{unit} Down clip bound");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase("Unit_DustStalker")]
        [TestCase("Unit_AshHopper")]
        [TestCase("Unit_SoilCreeper")]
        [TestCase("Unit_RockTick")]
        [TestCase("Unit_RegolithMite")]
        [TestCase("Unit_WattLeech")]
        [TestCase("Unit_IceWisp")]
        public void FaunaPrefab_KeepsMeshReference(string prefabName)
        {
            var prefab = Resources.Load<GameObject>("Units/" + prefabName);
            Assert.IsNotNull(prefab, prefabName);
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Assert.IsNotEmpty(filters, $"{prefabName} mesh filter");
            foreach (var f in filters)
                Assert.IsNotNull(f.sharedMesh, $"{prefabName}/{f.name} lost its mesh");
        }
    }
}
