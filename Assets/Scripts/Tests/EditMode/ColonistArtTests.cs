using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards for colonists v1 (art batch 3): the four suited villager prefabs, their Humanoid rig on the
    /// Kevin Iglesias Idle01 and the original colony-stride Walk, +Z facing (fauna v1 shipped facing -Z and
    /// walked backwards), feet planted at 2.4 m/s without a scurry, boots on the (displaced) ground, and the
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

        private const float VillagerSpeed = 2.4f; // VillagerAgent.moveSpeed (design speed, drives the economy)

        private static float LowestVertexY(SkinnedMeshRenderer smr, Mesh tmp)
        {
            smr.BakeMesh(tmp);
            float m = float.MaxValue;
            foreach (var v in tmp.vertices) m = Mathf.Min(m, smr.transform.TransformPoint(v).y);
            return m;
        }

        private static Animator Animate(GameObject go)
        {
            var anim = go.GetComponentInChildren<Animator>();
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            return anim;
        }

        [TestCaseSource(nameof(Variants))]
        public void Prefab_IsHumanoidOnIdle01AndColonyStride(string variant)
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
            Assert.IsTrue(clips.Contains("SM_Colonist@Stride"), "colony stride in " + string.Join(",", clips));
            Assert.IsFalse(clips.Any(n => n.Contains("Walk01")), "Walk01 scurry retired: " + string.Join(",", clips));
            var stride = rac.animationClips.First(c => c.name == "SM_Colonist@Stride");
            Assert.IsTrue(stride.humanMotion && stride.isLooping, "looping humanoid clip");
            Assert.AreEqual(1.2f, stride.length, 0.02f, "two 0.6 s steps per cycle");
        }

        [Test]
        public void Stride_At2_4mps_PlaysNearOneX_AtUnderTwoStepsPerSecond()
        {
            float play = ColonistArt.PlaybackSpeed(VillagerSpeed);
            Assert.That(play, Is.InRange(1.0f, 1.3f), "playback at the villagers' 2.4 m/s");
            Assert.Less(play, ColonistArt.MaxPlayback, "no longer pinned at the cap");
            float stepsPerSecond = VillagerSpeed / ColonistArt.StepLength;
            Assert.That(stepsPerSecond, Is.InRange(1.6f, 2.0f), "long stride, not a scurry");
        }

        [TestCaseSource(nameof(Variants))]
        public void Stride_PlantedFootKeepsUpWithTheVillager_AndBootsTouchGround(string variant)
        {
            var root = new GameObject("StrideRoot");
            try
            {
                var vis = Object.Instantiate(LoadPrefab(variant), root.transform, false);
                var anim = Animate(vis);
                var smr = vis.GetComponentInChildren<SkinnedMeshRenderer>();
                var tmp = new Mesh();
                // Contact point = toe joint: it stays planted through heel-off while the ankle lifts.
                Transform[] feet = { Bone(vis, "B-toe.L"), Bone(vis, "B-toe.R") };

                // Idle: boots on the ground.
                anim.SetFloat(ColonistArt.SpeedHash, 0f);
                anim.Update(1f);
                Assert.AreEqual(0f, LowestVertexY(smr, tmp), 0.03f, "idle boots on the ground");

                // Move the root at 2.4 m/s exactly like VillagerAgent and let ColonistArt pick the playback rate.
                ColonistArt.Drive(anim, VillagerSpeed);
                const float dt = 1f / 240f;
                for (int i = 0; i < 240; i++) { root.transform.position += Vector3.forward * VillagerSpeed * dt; anim.Update(dt); }
                Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"));
                const int n = 480;
                var y = new float[2, n]; var z = new float[2, n]; var lows = new List<float>();
                for (int i = 0; i < n; i++)
                {
                    root.transform.position += Vector3.forward * VillagerSpeed * dt;
                    anim.Update(dt);
                    for (int k = 0; k < 2; k++) { y[k, i] = feet[k].position.y; z[k, i] = feet[k].position.z; }
                    if (i % 12 == 0) lows.Add(LowestVertexY(smr, tmp));
                }
                Object.DestroyImmediate(tmp);
                var slides = new List<float>();
                for (int k = 0; k < 2; k++)
                {
                    float ymin = float.MaxValue;
                    for (int i = 0; i < n; i++) ymin = Mathf.Min(ymin, y[k, i]);
                    for (int i = 1; i < n; i++)
                        if (y[k, i] < ymin + 0.005f && y[k, i - 1] < ymin + 0.005f)
                            slides.Add(Mathf.Abs(z[k, i] - z[k, i - 1]) / dt);
                }
                Assert.Greater(slides.Count, 20, "both feet plant");
                slides.Sort();
                Assert.Less(slides[slides.Count / 2], 0.05f * VillagerSpeed, "planted foot slides under 5% of ground speed");
                lows.Sort();
                Assert.AreEqual(0f, lows[0], 0.03f, "boots touch the ground while striding (no hover, no sinking)");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
                    Assert.AreEqual(0f, v.transform.position.y, 1e-4f, "pivot (boot soles) on the ground");
                }
                Assert.AreEqual(ColonistArt.Variants.Length, roles.Count, "consecutive villagers cycle roles");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void VillagerSpawn_StandsOnDisplacedTerrain_AndFollowsIt()
        {
            var prior = TerrainDataBake.Current;
            var parent = new GameObject("ColonistTerrainTests");
            try
            {
                // 100 m square that slopes from +0.4 m (z = 0) to -0.4 m (z = 100).
                TerrainDataBake.Current = new TerrainBake
                {
                    Resolution = 2, WorldWidth = 100f, WorldHeight = 100f,
                    Heights = new[] { 0.4f, 0.4f, -0.4f, -0.4f },
                };
                Vector3 home = new Vector3(50f, 0f, 25f);           // ground +0.2 m
                var v = VillagerAgent.Spawn(parent.transform, home, home + new Vector3(0f, 0f, 50f));
                Assert.AreEqual(0.2f, v.transform.position.y, 1e-3f, "spawns standing on the terrain, not on y = 0");
                var anim = Animate(v.gameObject);
                anim.Update(0.5f);
                var tmp = new Mesh();
                Assert.AreEqual(0.2f, LowestVertexY(v.GetComponentInChildren<SkinnedMeshRenderer>(), tmp), 0.03f, "boots on the terrain");
                Object.DestroyImmediate(tmp);

                var follow = v.GetComponent<TerrainFollow>();
                Assert.IsNotNull(follow, "keeps following the ground while walking");
                v.transform.position = new Vector3(50f, v.transform.position.y, 75f); // ground -0.2 m
                typeof(TerrainFollow).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(follow, null);
                Assert.AreEqual(-0.2f, v.transform.position.y, 1e-3f, "drops into the dip instead of hovering");
            }
            finally
            {
                TerrainDataBake.Current = prior;
                Object.DestroyImmediate(parent);
            }
        }
    }
}
