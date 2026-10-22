using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Session 15: a Defense Mech with a live NavMeshAgent stood on the near bank of a ford
    /// 22 m from StalkerLair and released its claim again and again. EditMode travel tests run
    /// without an agent, so this one keeps the agent and lets it update between ticks.
    /// </summary>
    public class FordCrossingPlayTests
    {
        private readonly List<Object> _created = new List<Object>();
        private NavMeshDataInstance _mesh;
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sm-ford-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = System.IO.Path.Combine(_saveRoot, "Playtest");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.Destroy(_created[i]);
            _created.Clear();
            if (_mesh.valid) NavMesh.RemoveNavMeshData(_mesh);
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            if (System.IO.Directory.Exists(_saveRoot)) System.IO.Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator LiveAgent_FordsTheRiver_AndReachesTheDen()
        {
            // Two banks with an 18 m river between them (z 14..32); the den sits on the far bank,
            // ~22 m from the near water's edge, like StalkerLair.
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>
            {
                Box(new Vector3(0f, 0f, 0f), new Vector3(24f, 0.2f, 28f)),
                Box(new Vector3(0f, 0f, 46f), new Vector3(24f, 0.2f, 28f))
            };
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(new Vector3(0f, 0f, 23f), new Vector3(80f, 10f, 100f)), Vector3.zero, Quaternion.identity);
            _created.Add(data);
            _mesh = NavMesh.AddNavMeshData(data);
            Vector3 den = new Vector3(0f, 0f, 50f);

            var flags = new FlagManager();
            var flagData = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(flagData);
            flagData.flagType = FlagType.ClearThreat;
            flagData.displayName = "Clear Threat";
            flagData.maxBounty = 5000;
            flagData.defaultBounty = 1000;
            flagData.workRequired = 100f;
            flagData.baseRisk = 0.4f;
            flagData.stronglyAttracts = new[] { SpecialistClass.DefenseMech };

            var heroData = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(heroData);
            heroData.specialistClass = SpecialistClass.DefenseMech;
            SpecialistPersonality.Apply(heroData);

            var go = new GameObject("Hero_FordTest");
            _created.Add(go);
            go.transform.position = new Vector3(0f, 0.1f, 0f);
            var agent = go.AddComponent<NavMeshAgent>();
            var hero = go.AddComponent<SpecialistAgent>();
            hero.enabled = false; // ticked by hand below; the agent still updates every frame
            hero.BindForTravelTest(heroData, flags, new SpecialistBrain());
            hero.UseAgentForTravelTest(agent);
            hero.TravelBlocked = p => p.z > 14f && p.z < 32f;
            Assert.IsTrue(agent.isOnNavMesh, "fixture: the agent starts on the near bank");
            flags.Post(flagData, den, 1000f);

            bool crossed = false;
            for (int i = 0; i < 400; i++) // 40 s of 0.1 s ticks, one frame each
            {
                hero.Simulate(0.1f);
                yield return null;
                if (go.transform.position.z > 32f) crossed = true;
                if (Flat(go.transform.position, den) <= OverseerRules.ClearThreatAttackRange) break;
            }

            Assert.IsTrue(crossed, $"the mech must wade across; it stopped at z={go.transform.position.z:0.0}");
            Assert.LessOrEqual(Flat(go.transform.position, den), OverseerRules.ClearThreatAttackRange,
                "the mech must reach the den after the ford");
            Assert.IsNotNull(hero.ActiveFlag, "the claim must not be released");
            Assert.IsFalse(hero.FordDetached, "the agent is re-seated on the far bank");
            Assert.IsTrue(agent.isOnNavMesh && agent.updatePosition, "the agent drives the mech again");
        }

        private static NavMeshBuildSource Box(Vector3 c, Vector3 size) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box, size = size, area = 0,
            transform = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one)
        };

        private static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
