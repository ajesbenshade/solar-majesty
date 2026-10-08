using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// A committed Clear Threat hero has to leave the colony. EditMode has no navmesh,
    /// so the agent steps directly via <see cref="SpecialistAgent.Simulate"/>.
    /// </summary>
    public class ClearThreatTravelTests
    {
        private const string ReleaseLog = "[ClearThreat] Defense Mech released the claim — no progress.";

        private string _saveRoot;
        private bool _voices;
        private readonly List<Object> _created = new List<Object>();
        private FlagManager _flags;

        [SetUp]
        public void SetUp()
        {
            _voices = DemoSettings.CharacterVoices;
            DemoSettings.CharacterVoices = false;
            _saveRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "sm-saves-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = System.IO.Path.Combine(_saveRoot, "Playtest");
            _flags = new FlagManager();
        }

        [TearDown]
        public void TearDown()
        {
            DemoSettings.CharacterVoices = _voices;
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
            DestroyNamed("DemoAudio");
            DestroyNamed("SM_SpatialAudio");
            DestroyNamed("SM_VfxPool");
            DestroyNamed("SM_CharacterVoice");
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            if (!string.IsNullOrEmpty(_saveRoot) && System.IO.Directory.Exists(_saveRoot))
                System.IO.Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void Approach_StandsOutsideTheFootprint_AndInsideAttackRange()
        {
            Vector3 den = new Vector3(48f, 0f, 0f);
            Vector3 mouth = ClearThreatTravel.DefaultMouthDirection;
            Vector3 approach = ClearThreatTravel.ApproachPoint(den, mouth);
            float stand = Vector3.Distance(approach, den);
            Assert.Greater(stand, OverseerRules.DenFootprintMeters * 0.5f);
            Assert.Greater(stand, 5f, "the old 5 m nav sample cannot represent this stand");
            Assert.Less(stand, OverseerRules.ClearThreatAttackRange);

            Vector3 other = ClearThreatTravel.ApproachPoint(den, new Vector3(1f, 0f, 0f));
            Assert.AreEqual(stand, Vector3.Distance(other, den), 0.001f);
        }

        [Test]
        public void FarDen_CommittedDefenseMech_EndsInsideAttackRange()
        {
            AssertTravelsToFarDen(SpecialistClass.DefenseMech);
        }

        [Test]
        public void FarDen_CommittedSentinelMech_EndsInsideAttackRange()
        {
            AssertTravelsToFarDen(SpecialistClass.SentinelMech);
        }

        [Test]
        public void TravellingHero_EngagesAnAttackerInReach_ThenResumes()
        {
            Vector3 den = new Vector3(48f, 0f, 0f);
            var hero = MakeHero(SpecialistClass.DefenseMech);
            PostClearThreat(den);

            var mite = new GameObject("Mite").AddComponent<DustStalkerAgent>();
            _created.Add(mite.gameObject);
            // Give it an authored-looking body first. The placeholder path calls Destroy and
            // renderer.material, which EditMode rejects; SM_Art_ skips the tint.
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(mite.transform, false);
            var bodyMat = new Material(Shader.Find("Sprites/Default")) { name = "SM_Art_TestMite" };
            _created.Add(bodyMat);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            mite.Initialize(null, null, new Vector3(1.2f, 0f, 0f), null);
            hero.ThreatsOverride = new List<DustStalkerAgent> { mite };

            hero.Simulate(1f);

            Assert.AreEqual(SpecialistAction.PursueFlag, hero.CurrentAction);
            Assert.AreEqual("engaging", hero.Status);
            Assert.Less(mite.Health01, 0.99f);
            Assert.IsTrue(mite.IsAlive);
            Assert.Greater(Flat(hero.transform.position, den), 40f);

            mite.transform.position = new Vector3(200f, 0f, 200f);
            Sim(hero, 20f, 0.5f);

            float end = Flat(hero.transform.position, den);
            Assert.Less(end, 24f);
            Assert.Less(end, OverseerRules.ClearThreatAttackRange);
            Assert.AreEqual(SpecialistAction.PursueFlag, hero.CurrentAction);
        }

        [Test]
        public void UnreachableDen_WatchdogReleasesTheClaim_ExactlyOnce()
        {
            Vector3 den = new Vector3(48f, 0f, 0f);
            var hero = MakeHero(SpecialistClass.DefenseMech);
            var flag = PostClearThreat(den);
            hero.SuppressTravelForTests = true;

            int hits = 0;
            string last = null;
            Application.LogCallback onLog = (cond, stack, type) =>
            {
                if (cond != null &&
                    cond.IndexOf("[ClearThreat]", System.StringComparison.Ordinal) >= 0 &&
                    cond.IndexOf("released", System.StringComparison.Ordinal) >= 0)
                {
                    hits++;
                    last = cond;
                }
            };
            Application.logMessageReceived += onLog;
            try
            {
                // First sample records distance. Ten seconds later, one silent repath.
                // The release is the ten seconds after that. 12 one-second ticks stay claimed.
                Sim(hero, 12f, 1f);
                Assert.AreEqual(0, hits);
                Assert.IsNotNull(hero.ActiveFlag);
                Assert.AreEqual(1, flag.ClaimCount);
                Assert.AreEqual(SpecialistAction.PursueFlag, hero.CurrentAction);

                Sim(hero, 12f, 1f);
                Assert.AreEqual(1, hits);
                Assert.AreEqual(ReleaseLog, last);
                Assert.AreEqual(0, flag.ClaimCount);
                Assert.IsNull(hero.ActiveFlag);
                Assert.AreNotEqual(SpecialistAction.PursueFlag, hero.CurrentAction);
                Assert.AreEqual(1, _flags.Flags.Count);

                Sim(hero, 12f, 1f);
                Assert.AreEqual(1, hits);
                Assert.IsNull(hero.ActiveFlag);
                Assert.AreNotEqual(SpecialistAction.PursueFlag, hero.CurrentAction);
                Assert.AreEqual(0, flag.ClaimCount);

                var next = MakeHero(SpecialistClass.DefenseMech);
                Sim(next, 2f, 0.5f);
                Assert.AreSame(flag, next.ActiveFlag);
                Assert.AreEqual(1, flag.ClaimCount);
                Assert.AreEqual(1, hits);
                Assert.IsNull(hero.ActiveFlag);
            }
            finally
            {
                Application.logMessageReceived -= onLog;
            }
        }

        private void AssertTravelsToFarDen(SpecialistClass cls)
        {
            Vector3 den = new Vector3(48f, 0f, 0f);
            var hero = MakeHero(cls);
            PostClearThreat(den);
            float start = Flat(hero.transform.position, den);

            Sim(hero, 20f, 0.5f);

            float end = Flat(hero.transform.position, den);
            Assert.Less(end, start * 0.5f, cls.ToString());
            Assert.Less(end, OverseerRules.ClearThreatAttackRange, cls.ToString());
            Assert.AreEqual(SpecialistAction.PursueFlag, hero.CurrentAction, cls.ToString());
            Assert.IsNotNull(hero.ActiveFlag, cls.ToString());
        }

        private SpecialistAgent MakeHero(SpecialistClass cls)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = cls;
            SpecialistPersonality.Apply(data);

            var go = new GameObject("Hero_" + cls);
            _created.Add(go);
            var hero = go.AddComponent<SpecialistAgent>();
            hero.BindForTravelTest(data, _flags, new SpecialistBrain());
            return hero;
        }

        private FlagHandle PostClearThreat(Vector3 den)
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = FlagType.ClearThreat;
            data.displayName = "Clear Threat";
            data.minBounty = 0;
            data.maxBounty = 5000;
            data.defaultBounty = 1000;
            data.workRequired = 100f;
            data.baseRisk = 0.4f;
            data.stronglyAttracts = new[] { SpecialistClass.DefenseMech, SpecialistClass.SentinelMech };
            return _flags.Post(data, den, 1000f);
        }

        private static void Sim(SpecialistAgent hero, float seconds, float step)
        {
            float t = 0f;
            while (t < seconds - 0.0001f)
            {
                float dt = Mathf.Min(step, seconds - t);
                hero.Simulate(dt);
                t += dt;
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void DestroyNamed(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }
}
