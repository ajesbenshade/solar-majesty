using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards for colonists v1 (art batch 3): the four suited villager prefabs, their Humanoid rig on the
    /// Kevin Iglesias Idle/Walk clips, +Z facing (fauna v1 shipped facing -Z and walked backwards), and the
    /// VillagerAgent.Spawn hookup that replaced the capsule placeholder.
    /// </summary>
    public class ColonistArtTests
    {
        private static IEnumerable<string> Variants => ColonistArt.Variants;

        private static GameObject LoadPrefab(string variant)
        {
            var prefab = Resources.Load<GameObject>(ColonistArt.ResourceDir + ColonistArt.PrefabPrefix + variant);
            Assert.IsNotNull(prefab, "missing Resources/Colonists/SM_Colonist_" + variant);
            return prefab;
        }

        private static Transform Bone(GameObject root, string name)
        {
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(t, "bone " + name);
            return t;
        }

        [TestCaseSource(nameof(Variants))]
        public void Prefab_IsBudgetedSuitWithSuitAndRoleMaterials(string variant)
        {
            var prefab = LoadPrefab(variant);
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "skinned suit");
            Assert.IsNotNull(smr.sharedMesh);
            int tris = smr.sharedMesh.triangles.Length / 3;
            Assert.That(tris, Is.InRange(1000, 2000), "triangle budget");
            var mats = smr.sharedMaterials;
            Assert.AreEqual(2, mats.Length);
            Assert.AreEqual("SM_Art_Colonist_Suit", mats[0].name);
            Assert.AreEqual("SM_Art_Colonist_Role_" + variant, mats[1].name);
            foreach (var m in mats)
                StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name);
            Assert.IsNotNull(mats[0].GetTexture("_BaseMap"), "suit palette atlas");
        }

        [TestCaseSource(nameof(Variants))]
        public void Prefab_IsHumanoidOnKevinIglesiasIdleWalk(string variant)
        {
            var anim = LoadPrefab(variant).GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(anim);
            Assert.IsNotNull(anim.avatar);
            Assert.IsTrue(anim.avatar.isValid && anim.avatar.isHuman, "humanoid avatar");
            Assert.IsFalse(anim.applyRootMotion, "VillagerAgent moves the transform");
            var rac = anim.runtimeAnimatorController;
            Assert.IsNotNull(rac);
            var baseCtrl = (rac is AnimatorOverrideController ov ? ov.runtimeAnimatorController : rac) as AnimatorController;
            Assert.IsNotNull(baseCtrl, "SM_Colonist.controller");
            Assert.IsTrue(baseCtrl.parameters.Any(p => p.name == ColonistArt.SpeedParam && p.type == AnimatorControllerParameterType.Float));
            var clips = rac.animationClips.Select(c => c.name).ToArray();
            Assert.IsTrue(clips.Any(n => n.Contains("Idle01")), "Idle01 in " + string.Join(",", clips));
            Assert.IsTrue(clips.Any(n => n.Contains("Walk01_Forward")), "Walk01 in " + string.Join(",", clips));
        }

        [TestCaseSource(nameof(Variants))]
        public void Prefab_FacesPlusZ_LeftSideAtMinusX(string variant)
        {
            var go = Object.Instantiate(LoadPrefab(variant));
            try
            {
                var root = go.transform;
                Vector3 foot = root.InverseTransformPoint(Bone(go, "B-foot.L").position);
                Vector3 toe = root.InverseTransformPoint(Bone(go, "B-toe.L").position);
                Assert.Greater(toe.z - foot.z, 0.03f, "toes point +Z");
                Assert.Less(root.InverseTransformPoint(Bone(go, "B-thigh.L").position).x, 0f, "left leg on -X when facing +Z");
                Assert.Greater(root.InverseTransformPoint(Bone(go, "B-head").position).y, 0.85f, "upright");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCaseSource(nameof(Variants))]
        public void Walk_PlaysOnSpeed_AndKeepsFacing(string variant)
        {
            var go = Object.Instantiate(LoadPrefab(variant));
            try
            {
                var anim = go.GetComponentInChildren<Animator>();
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Rebind();
                anim.SetFloat(ColonistArt.SpeedHash, 0f);
                anim.Update(0.5f);
                Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Idle at rest");

                ColonistArt.Drive(anim, 2.4f);
                anim.Update(0.5f);
                anim.Update(0.25f);
                Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "Walk when moving");

                var footL = Bone(go, "B-foot.L");
                var toeL = Bone(go, "B-toe.L");
                var head = Bone(go, "B-head");
                float minZ = float.MaxValue, maxZ = float.MinValue, toeAhead = 0f;
                for (int i = 0; i < 12; i++)
                {
                    anim.Update(0.08f);
                    float z = go.transform.InverseTransformPoint(footL.position).z;
                    minZ = Mathf.Min(minZ, z);
                    maxZ = Mathf.Max(maxZ, z);
                    toeAhead += go.transform.InverseTransformPoint(toeL.position).z - z;
                    Assert.Greater(go.transform.InverseTransformPoint(head.position).y, 0.8f, "stays upright");
                }
                Assert.Greater(maxZ - minZ, 0.12f, "left foot strides");
                Assert.Greater(toeAhead / 12f, 0.02f, "walks toes-first along +Z");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlaybackSpeed_IdlesAtOneAndClamps()
        {
            Assert.AreEqual(1f, ColonistArt.PlaybackSpeed(0f));
            Assert.AreEqual(1f, ColonistArt.PlaybackSpeed(ColonistArt.StrideSpeed), 1e-4f);
            Assert.AreEqual(ColonistArt.MaxPlayback, ColonistArt.PlaybackSpeed(50f));
            Assert.AreEqual(0.75f, ColonistArt.PlaybackSpeed(0.2f));
            Assert.AreEqual(ColonistArt.PrefabName(0), ColonistArt.PrefabName(ColonistArt.Variants.Length));
            Assert.AreEqual(ColonistArt.PrefabName(3), ColonistArt.PrefabName(-1));
        }

        [Test]
        public void VillagerSpawn_WearsSuit_FacesWork_AndCyclesRoles()
        {
            var parent = new GameObject("ColonistArtTests");
            try
            {
                var roles = new HashSet<string>();
                for (int i = 0; i < ColonistArt.Variants.Length; i++)
                {
                    Vector3 home = new Vector3(i * 3f, 0f, 0f);
                    Vector3 work = home + new Vector3(4f, 0f, 4f);
                    var v = VillagerAgent.Spawn(parent.transform, home, work);
                    Assert.IsNotNull(v);
                    Assert.IsNull(v.transform.Find("Body"), "capsule placeholder replaced");
                    var visual = v.transform.Find("Visual");
                    Assert.IsNotNull(visual, "suit mounted as Visual");
                    Assert.IsNotNull(visual.GetComponentInChildren<Animator>());
                    Assert.Greater(Vector3.Dot(v.transform.forward, (work - home).normalized), 0.99f, "faces its work site");
                    var smr = visual.GetComponentInChildren<SkinnedMeshRenderer>();
                    Assert.AreEqual("SM_Art_Colonist_Suit", smr.sharedMaterials[0].name, "no runtime material remap");
                    roles.Add(smr.sharedMaterials[1].name);
                    Assert.AreEqual(0f, smr.bounds.min.y, 0.06f, "boots on the ground");
                }
                Assert.AreEqual(ColonistArt.Variants.Length, roles.Count, "consecutive villagers cycle roles");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
