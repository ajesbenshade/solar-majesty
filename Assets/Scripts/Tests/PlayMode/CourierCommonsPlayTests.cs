using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Session 18: a live Courier aimed at the Commons centre (inside a carved hole ~9 m from the
    /// mesh) never got within hand-in range and stood there carrying levy. This keeps the agent.
    /// </summary>
    public class CourierCommonsPlayTests
    {
        private readonly List<Object> _created = new List<Object>();
        private NavMeshDataInstance _mesh;
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sm-courier-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = System.IO.Path.Combine(_saveRoot, "Playtest");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.Destroy(_created[i]);
            _created.Clear();
            if (_mesh.valid) NavMesh.RemoveNavMeshData(_mesh);
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            if (System.IO.Directory.Exists(_saveRoot)) System.IO.Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator LiveCourier_HandsInAtACommonsWhoseCentreIsOffTheMesh()
        {
            // A 60 m square of ground with an 18 m hole in the middle: the Commons footprint.
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>
            {
                Box(O + new Vector3(0f, 0f, 19.5f), new Vector3(60f, 0.2f, 21f)),
                Box(O + new Vector3(0f, 0f, -19.5f), new Vector3(60f, 0.2f, 21f)),
                Box(O + new Vector3(19.5f, 0f, 0f), new Vector3(21f, 0.2f, 18f)),
                Box(O + new Vector3(-19.5f, 0f, 0f), new Vector3(21f, 0.2f, 18f))
            };
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(O, new Vector3(80f, 10f, 80f)), Vector3.zero, Quaternion.identity);
            _created.Add(data);
            _mesh = NavMesh.AddNavMeshData(data);
            Vector3 commons = O;
            Assert.IsFalse(NavMesh.SamplePosition(commons, out _, 5f, NavMesh.AllAreas),
                "fixture: the Commons centre is more than 5 m from the mesh, like the real footprint");

            var heroData = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(heroData);
            heroData.specialistClass = SpecialistClass.CourierBot;
            SpecialistPersonality.Apply(heroData);

            var go = new GameObject("Hero_CourierCommonsTest");
            _created.Add(go);
            go.transform.position = O + new Vector3(24f, 0.1f, 24f);
            var agent = go.AddComponent<NavMeshAgent>();
            var hero = go.AddComponent<SpecialistAgent>();
            hero.enabled = false;
            hero.BindForTravelTest(heroData, new FlagManager(), new SpecialistBrain());
            hero.UseAgentForTravelTest(agent);
            Assert.IsTrue(agent.isOnNavMesh, "fixture: the courier starts on the mesh");

            string line = null;
            int delivered = 0;
            hero.LevyChestForTest = _ => commons;
            hero.LevyDeliveredForTest = (n, l) => { delivered = n; line = l; };
            hero.AddLevyCarry(40);

            // Agent movement runs on frame time, so let real frames pass (sped up) rather than counting ticks.
            Time.timeScale = 4f;
            float start = Time.realtimeSinceStartup;
            while (line == null && Time.realtimeSinceStartup - start < 45f)
            {
                hero.Simulate(Time.deltaTime);
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.AreEqual(40, delivered, $"courier stopped at {go.transform.position} status={hero.Status}");
            StringAssert.StartsWith("Courier hauled 40 EU to the till.", line);
            Assert.AreEqual(0, hero.LevyCarry);
        }

        // Far from any scene mesh an earlier test may have left loaded.
        private static readonly Vector3 O = new Vector3(4000f, 0f, 4000f);

        private static NavMeshBuildSource Box(Vector3 c, Vector3 size) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box, size = size, area = 0,
            transform = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one)
        };
    }
}
