using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Gameplay hookup for fauna v2 art: keep authored materials, mount the animated stalker
    /// FBX without a yaw flip, play Down before destroy, and do not add procedural legs.
    /// The loop stays inactive so GameLoop.Awake never reads the real save folder or PlayerPrefs.
    /// </summary>
    public class FaunaV2HookupTests
    {
        private string _saveRoot;
        private GameObject _stage;
        private GameLoop _loop;
        private int _kills;

        [SetUp]
        public void UseTempSaveRoot()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "sm-saves-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = Path.Combine(_saveRoot, "Playtest");

            var loopGo = new GameObject("fauna-v2-loop");
            loopGo.SetActive(false);
            _loop = loopGo.AddComponent<GameLoop>();
            _loop.ArmThreat(new ThreatPressure());
            // NUnit reuses the fixture instance, so the counter must start fresh per test.
            _kills = 0;
            _loop.FaunaKilledHook += NoteKill;
            _stage = new GameObject("fauna-v2-stage");
        }

        [TearDown]
        public void ClearTempSaveRoot()
        {
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            if (_stage != null) Object.DestroyImmediate(_stage);
            if (_loop != null) Object.DestroyImmediate(_loop.gameObject);
            SweepVfx();
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
                Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void IndustrialArtDressing_LeavesFaunaMaterialsUntouched()
        {
            var fbx = Resources.Load<GameObject>("Units/SM_Unit_DustStalker");
            Assert.IsNotNull(fbx);
            var go = Object.Instantiate(fbx, _stage.transform);
            go.name = "DustStalker";
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr);
            var before = smr.sharedMaterials;
            Assert.AreEqual(2, before.Length);
            Material glow = null;
            for (int i = 0; i < before.Length; i++)
            {
                Assert.IsTrue(IndustrialArtDressing.IsFaunaArtMaterial(before[i]), before[i].name);
                if (IndustrialArtDressing.IsFaunaGlowMaterial(before[i]))
                    glow = before[i];
            }
            Assert.IsNotNull(glow, "shared glow slot");
            Assert.IsTrue(glow.HasProperty("_EmissionColor"));
            Color authoredEmission = glow.GetColor("_EmissionColor");

            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            other.name = "Trim";
            other.transform.SetParent(go.transform, false);
            var col = other.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader);
            var white = new Material(shader) { name = "SM_White" };
            var trim = other.GetComponent<Renderer>();
            trim.sharedMaterial = white;

            IndustrialArtDressing.Apply(go);

            var after = smr.sharedMaterials;
            Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++)
                Assert.AreSame(before[i], after[i], "fauna slot " + i + " was replaced");
            Assert.AreEqual(authoredEmission, glow.GetColor("_EmissionColor"));

            Assert.AreNotSame(white, trim.sharedMaterial, "non-fauna slots must still be dressed");
            Assert.IsFalse(IndustrialArtDressing.IsFaunaArtMaterial(trim.sharedMaterial));

            Color tint = new Color(0.1f, 0.2f, 0.9f);
            IndustrialArtDressing.SetUrpColor(smr, tint);
            var tinted = smr.sharedMaterials;
            Assert.AreEqual(2, tinted.Length, "tint must not collapse material slots");
            Material glowNow = null;
            Material bodyNow = null;
            int bodyIndex = -1;
            for (int i = 0; i < tinted.Length; i++)
            {
                Assert.AreSame(before[i], tinted[i], "SetUrpColor replaced fauna slot " + i);
                if (tinted[i] != null && tinted[i].name.IndexOf("Glow", System.StringComparison.Ordinal) >= 0)
                    glowNow = tinted[i];
                else
                {
                    bodyNow = tinted[i];
                    bodyIndex = i;
                }
            }
            Assert.IsNotNull(glowNow);
            Assert.IsNotNull(bodyNow);
            Assert.AreSame(glow, glowNow, "glow slot must stay the shared accent");
            Assert.AreEqual(authoredEmission, glow.GetColor("_EmissionColor"), "shared glow asset emission");
            Assert.AreEqual(authoredEmission, glowNow.GetColor("_EmissionColor"), "glow instance emission");
            Assert.Greater(
                Mathf.Abs(glowNow.GetColor("_EmissionColor").r - tint.r) +
                Mathf.Abs(glowNow.GetColor("_EmissionColor").b - tint.b),
                0.2f,
                "glow emission must not be replaced with the tint");
            if (bodyNow.HasProperty("_BaseColor"))
            {
                Color authoredBody = bodyNow.GetColor("_BaseColor");
                Assert.Greater(
                    Mathf.Abs(authoredBody.r - tint.r) + Mathf.Abs(authoredBody.b - tint.b),
                    0.2f,
                    "shared fauna body atlas was recolored");
            }

            Assert.GreaterOrEqual(bodyIndex, 0);
            var bodyBlock = new MaterialPropertyBlock();
            smr.GetPropertyBlock(bodyBlock, bodyIndex);
            Color body = bodyBlock.GetColor("_BaseColor");
            Assert.AreEqual(tint.r, body.r, 0.02f);
            Assert.AreEqual(tint.g, body.g, 0.02f);
            Assert.AreEqual(tint.b, body.b, 0.02f);

            Color dim = new Color(0.25f, 0.05f, 0.02f);
            IndustrialArtDressing.SetTintOverlay(go, dim);
            int glowIndex = -1;
            var slots = smr.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].name.IndexOf("Glow", System.StringComparison.Ordinal) >= 0)
                    glowIndex = i;
            }
            Assert.GreaterOrEqual(glowIndex, 0);
            var block = new MaterialPropertyBlock();
            smr.GetPropertyBlock(block, glowIndex);
            Color scaled = block.GetColor("_EmissionColor");
            Color expect = authoredEmission * dim.maxColorComponent;
            Assert.AreEqual(expect.r, scaled.r, 0.02f);
            Assert.AreEqual(expect.g, scaled.g, 0.02f);
            Assert.AreEqual(expect.b, scaled.b, 0.02f);
        }

        [Test]
        public void AnimatedFauna_SpawnUsesFbx_NotYawFlipped_NoProceduralLegs()
        {
            AssertSpawn(FaunaKind.Stalker, "SM_Unit_DustStalker", stalker: true);
            AssertSpawn(FaunaKind.Hopper, "SM_Unit_AshHopper", stalker: false);
            AssertSpawn(FaunaKind.Creeper, "SM_Unit_SoilCreeper", stalker: false);
            AssertSpawn(FaunaKind.Tick, "SM_Unit_RockTick", stalker: false);
            AssertSpawn(FaunaKind.Mite, "SM_Unit_RegolithMite", stalker: false);
            AssertSpawn(FaunaKind.Leech, "SM_Unit_WattLeech", stalker: false);
            AssertSpawn(FaunaKind.Wisp, "SM_Unit_IceWisp", stalker: false);
        }

        [Test]
        public void PrimitiveBody_StillGetsProceduralLegs()
        {
            var go = new GameObject("DustStalker");
            go.transform.SetParent(_stage.transform, false);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Visual";
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(0.5f, 1.1f, 0.7f);
            var col = vis.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
                vis.GetComponent<Renderer>().sharedMaterial = new Material(shader) { name = "SM_White" };

            var agent = go.AddComponent<DustStalkerAgent>();
            agent.Initialize(new ThreatPressure(), null, Vector3.zero);
            Assert.IsFalse(FaunaDressing.UsesAuthoredFaunaArt(go));
            Assert.IsNotNull(agent.GetComponent<ProceduralLegs>(), "primitive bodies keep IK legs");
            Assert.AreEqual(6, agent.GetComponent<ProceduralLegs>().LegCount);
        }

        [Test]
        public void Die_SetsDowned_StopsAi_AwardsOnce_DestroysAfterDelay()
        {
            var threat = new ThreatPressure();
            _loop.ArmThreat(threat);
            var agent = _loop.SpawnStalkerAt(new Vector3(2f, 0f, 3f), _stage.transform);
            Assert.IsNotNull(agent);
            var clips = agent.GetComponentInChildren<UnitClipPlayer>(true);
            Assert.IsNotNull(clips);
            Assert.IsTrue(clips.HasDownClip);
            var col = agent.gameObject.AddComponent<BoxCollider>();

            agent.Tick(0.35f);
            Assert.Greater(threat.ActiveSources, 0, "a living stalker reports threat");
            Vector3 living = agent.transform.position;

            agent.ApplyCombatDamage(100000f);
            Assert.AreEqual(1.2f, DustStalkerAgent.DownCorpseSeconds, 0.001f);
            Assert.IsTrue(agent.IsDowned);
            Assert.IsTrue(clips.IsDowned);
            Assert.IsFalse(agent.IsAlive);
            Assert.IsFalse(agent.IsTargetable);
            Assert.IsFalse(agent.IsAggro);
            Assert.IsFalse(agent.IsRaiding);
            Assert.IsFalse(col.enabled, "downed fauna are not physics targets");
            Assert.AreEqual(0, threat.ActiveSources, "death clears the threat contribution");
            Assert.AreEqual(1, _kills, "kill credit fires at death");
            Assert.AreEqual(1, agent.KillCreditsIssued);
            Assert.Greater(agent.LastKillRewardMul, 0f);
            Assert.AreEqual(living, agent.transform.position);

            agent.ApplyCombatDamage(100000f);
            agent.ApplyClearThreatKill();
            Assert.AreEqual(1, _kills, "a downed fauna cannot be killed twice");
            Assert.AreEqual(1, agent.KillCreditsIssued);

            agent.Tick(0.5f);
            Assert.AreEqual(living, agent.transform.position, "downed fauna do not move");
            Assert.AreEqual(1, _kills, "the corpse delay must not award again");
            Assert.IsFalse(agent.CorpseReleased);
            Assert.Greater(agent.DownSecondsLeft, 0.6f);

            agent.Tick(0.69f);
            Assert.IsFalse(agent == null);
            Assert.IsFalse(agent.CorpseReleased);

            agent.Tick(0.02f);
            Assert.IsTrue(agent == null, "corpse is destroyed after ~1.2s");
            Assert.AreEqual(1, _kills);
        }

        [Test]
        public void Die_EarlyDestroy_DoesNotThrow()
        {
            var agent = _loop.SpawnStalkerAt(new Vector3(-4f, 0f, 1f), _stage.transform);
            Assert.IsNotNull(agent);
            agent.ApplyCombatDamage(100000f);
            Assert.AreEqual(1, agent.KillCreditsIssued);
            Assert.DoesNotThrow(() => Object.DestroyImmediate(agent.gameObject));
            Assert.AreEqual(1, _kills);
        }

        private void AssertSpawn(FaunaKind kind, string unit, bool stalker)
        {
            var fbx = Resources.Load<GameObject>("Units/" + unit);
            Assert.IsNotNull(fbx, unit);
            var agent = stalker
                ? _loop.SpawnStalkerAt(new Vector3(8f, 0f, 0f), _stage.transform)
                : _loop.SpawnFaunaAt(kind, new Vector3(8f, 0f, kind == FaunaKind.Hopper ? 4f : 8f), _stage.transform);
            Assert.IsNotNull(agent, unit);
            Assert.AreEqual(kind, agent.Kind, unit);

            var visual = FaunaDressing.FindVisual(agent.gameObject);
            Assert.IsNotNull(visual, unit + " visual");
            Assert.Less(Quaternion.Angle(Quaternion.identity, agent.transform.rotation), 1f,
                unit + " locomotion root was yawed at spawn");
            float yaw = Quaternion.Angle(fbx.transform.localRotation, visual.localRotation);
            Assert.Less(yaw, 1f, unit + " visual yaw drifted " + yaw + " degrees from the FBX");

            Quaternion before = visual.localRotation;
            FaunaDressing.AlignHead(agent.gameObject);
            float after = Quaternion.Angle(before, visual.localRotation);
            Assert.Less(after, 0.1f, unit + " AlignHead flipped the new model");

            var clips = agent.GetComponentInChildren<UnitClipPlayer>(true);
            Assert.IsNotNull(clips, unit + " clip player");
            Assert.IsTrue(clips.Ready, unit);
            Assert.IsTrue(clips.HasDownClip, unit);
            Assert.IsNotNull(agent.GetComponentInChildren<SkinnedMeshRenderer>(true), unit + " rig");
            Assert.IsNull(agent.GetComponent<ProceduralLegs>(), unit + " procedural legs");
            Assert.IsNull(agent.GetComponentInChildren<ProceduralLegs>(true), unit);
            Assert.IsNull(FindNamed(agent.transform, FaunaDressing.AccentRootName), unit + " dress accents");

            var smr = agent.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mats = smr.sharedMaterials;
            Assert.AreEqual(2, mats.Length, unit + " slots");
            for (int i = 0; i < mats.Length; i++)
                Assert.IsTrue(IndustrialArtDressing.IsFaunaArtMaterial(mats[i]), unit + " " + mats[i].name);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                if (ts[i] != null && ts[i].name == name)
                    return ts[i];
            }
            return null;
        }

        private static void SweepVfx()
        {
            var pulses = Object.FindObjectsByType<VfxPulse>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < pulses.Length; i++)
            {
                if (pulses[i] != null)
                    Object.DestroyImmediate(pulses[i].gameObject);
            }
            var bits = Object.FindObjectsByType<VfxBurstBit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] != null)
                    Object.DestroyImmediate(bits[i].gameObject);
            }
        }

        private void NoteKill(FaunaKind kind, float rewardMul) => _kills++;
    }
}
