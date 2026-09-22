using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards for the Heroes_v2 humanoid roster and its clip playback.
    /// The big one: Blender names FBX takes "SM_Unit_X_Rig|Idle" and Unity keeps that as the clip
    /// name, so UnitClipPlayer must match on the part after '|' or no hero clip ever plays.
    /// </summary>
    public class HeroAnimationTests
    {
        [Test]
        public void ClipKey_StripsBlenderTakePrefix()
        {
            Assert.AreEqual("Idle", UnitClipPlayer.ClipKey("SM_Unit_EngineerBot_Rig|Idle"));
            Assert.AreEqual("Walk", UnitClipPlayer.ClipKey("Armature|Walk"));
            Assert.AreEqual("Strike", UnitClipPlayer.ClipKey("Strike"));
            Assert.AreEqual(string.Empty, UnitClipPlayer.ClipKey(null));
        }

        [Test]
        public void Bind_WithoutClips_LeavesNoDeadPlayer()
        {
            var go = new GameObject("NoClips");
            try
            {
                var player = UnitClipPlayer.Bind(go, "SM_Unit_DoesNotExist");
                Assert.IsNull(player);
                // A lingering component would switch UnitMotion's procedural fallback off.
                Assert.IsNull(go.GetComponent<UnitClipPlayer>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase("SM_Unit_EngineerBot")]
        [TestCase("SM_Unit_DefenseMech")]
        [TestCase("SM_Unit_ScoutDrone")]
        [TestCase("SM_Unit_Medic")]
        [TestCase("SM_Unit_HarvesterBot")]
        [TestCase("SM_Unit_SurveyorBot")]
        [TestCase("SM_Unit_TerraformerBot")]
        [TestCase("SM_Unit_CourierBot")]
        [TestCase("SM_Unit_GeologistBot")]
        [TestCase("SM_Unit_SentinelMech")]
        public void HeroFbx_ShipsAllFiveClips_AndStrideSpeed(string unit)
        {
            var clips = Resources.LoadAll<AnimationClip>("Units/" + unit);
            Assert.IsNotNull(clips, unit);
            foreach (var want in new[] { "Idle", "Walk", "Strike", "Work", "Down" })
            {
                bool found = false;
                foreach (var c in clips)
                {
                    if (c != null && UnitClipPlayer.ClipKey(c.name) == want) found = true;
                }
                Assert.IsTrue(found, $"{unit} missing {want} clip");
            }
            Assert.Greater(UnitClipPlayer.AuthoredWalkSpeed(unit), 1f, $"{unit} stride speed in UnitClipMeta.json");
        }

        [Test]
        public void HeroClasses_UseBipedFallbackGait()
        {
            foreach (SpecialistClass cls in System.Enum.GetValues(typeof(SpecialistClass)))
                Assert.AreEqual(LocomotionKind.Walker, UnitMotion.KindFor(cls), cls.ToString());
        }

        [Test]
        public void LabourStatuses_DriveWorkClip()
        {
            Assert.IsTrue(SpecialistAgent.IsLabourStatus("working_Build"));
            Assert.IsTrue(SpecialistAgent.IsLabourStatus("repairing"));
            Assert.IsTrue(SpecialistAgent.IsLabourStatus("workshop_repair"));
            Assert.IsTrue(SpecialistAgent.IsLabourStatus("party_work"));
            Assert.IsFalse(SpecialistAgent.IsLabourStatus("moving_to_Build"));
            Assert.IsFalse(SpecialistAgent.IsLabourStatus("engaging"));
            Assert.IsFalse(SpecialistAgent.IsLabourStatus(null));
        }
    }
}
