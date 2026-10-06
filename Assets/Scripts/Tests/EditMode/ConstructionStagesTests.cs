using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Buildings rise in stages (site prep, foundation, frame, shell, fit-out) and come back
    /// exactly as spawned when the build finishes. The core set are detailed procedural kits.
    /// </summary>
    public class ConstructionStagesTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ConstructionStagesTestRoot");
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void StageAt_WalksTheFiveStagesInOrder()
        {
            int last = -1;
            for (float p = 0f; p < 1f; p += 0.01f)
            {
                int s = ConstructionStages.StageAt(p);
                Assert.GreaterOrEqual(s, last, $"stage went backwards at {p}");
                last = s;
            }
            Assert.AreEqual(ConstructionStages.SitePrep, ConstructionStages.StageAt(0f));
            Assert.AreEqual(ConstructionStages.FitOut, ConstructionStages.StageAt(0.99f));
        }

        [Test]
        public void Show_HidesUnbuiltParts_AndFinishRestoresEveryRenderer()
        {
            foreach (var cat in new[] { BuildingCategory.Habitat, BuildingCategory.Commons, BuildingCategory.EngineerWorkshop })
            {
                var go = ModularBuildingFactory.Spawn(cat, Vector3.zero, _root.transform);
                var before = Snapshot(go);

                ConstructionStages.Show(go, 0.05f);
                Assert.IsTrue(ConstructionStages.IsStaged(go), $"{cat} staged");
                Assert.IsNotNull(go.transform.Find("Dress_BuildOnly"), $"{cat} site kit (stakes, crane)");
                int visible = 0;
                foreach (var kv in before)
                    if (kv.Key != null && kv.Key.enabled) visible++;
                Assert.Less(visible, before.Count / 4, $"{cat}: site prep shows almost none of the building");

                ConstructionStages.Show(go, 0.6f);
                int mid = 0;
                foreach (var kv in before)
                    if (kv.Key != null && kv.Key.enabled) mid++;
                Assert.Greater(mid, visible, $"{cat}: shell stage shows more than site prep");

                ConstructionStages.Show(go, 1f);
                Assert.IsFalse(ConstructionStages.IsStaged(go));
                Assert.IsNull(go.transform.Find("Dress_BuildOnly"), $"{cat}: site kit removed when built");
                foreach (var kv in before)
                    Assert.AreEqual(kv.Value, kv.Key.enabled, $"{cat}: {kv.Key.name} restored");
            }
        }

        [Test]
        public void Commons_DomeWaitsForTheShellStage()
        {
            var go = ModularBuildingFactory.Spawn(BuildingCategory.Commons, Vector3.zero, _root.transform);
            var dome = FindChild(go.transform, "CommonsDome").GetComponent<Renderer>();
            var plinth = FindChild(go.transform, "CommonsPlinth").GetComponent<Renderer>();
            ConstructionStages.Show(go, 0.27f);
            Assert.IsTrue(plinth.enabled, "foundation is down by the end of its stage");
            Assert.IsFalse(dome.enabled, "the dome is shell work");
            ConstructionStages.Show(go, 0.79f);
            Assert.IsTrue(dome.enabled, "shell is up by the end of its stage");
            ConstructionStages.Finish(go);
        }

        [Test]
        public void CoreSet_IsProcedural_AndDefenseNoLongerClonesTheWatchtower()
        {
            var cats = new[]
            {
                BuildingCategory.EngineerWorkshop, BuildingCategory.Defense,
                BuildingCategory.Watchtower, BuildingCategory.Market
            };
            foreach (var cat in cats)
            {
                var go = ModularBuildingFactory.Spawn(cat, Vector3.zero, _root.transform);
                Assert.IsNull(FindChild(go.transform, "CoreMesh"), $"{cat} is the detailed procedural build, not the FBX");
                Assert.Greater(go.GetComponentsInChildren<Renderer>().Length, 25, $"{cat} carries its detail layer");
            }
            var tower = ModularBuildingFactory.Spawn(BuildingCategory.Watchtower, Vector3.zero, _root.transform);
            Assert.IsNotNull(FindChild(tower.transform, "Dress_S3_TowPod"), "watchtower has an observation pod");
            Assert.IsNotNull(FindChild(tower.transform, "Dress_TowerLaser"), "laser mount kept for ShowWatchtowerLasers");
            var def = ModularBuildingFactory.Spawn(BuildingCategory.Defense, Vector3.zero, _root.transform);
            Assert.IsNull(FindChild(def.transform, "Dress_S3_TowPod"));
            Assert.IsNotNull(FindChild(def.transform, "Dress_S3_DefCore"));
        }

        [Test]
        public void Mars_RoofShellsSkipKitsWithTheirOwnRoof()
        {
            ModularBuildingFactory.BindBody(CelestialBodyCatalog.Get(CelestialBodyId.Mars));
            try
            {
                var market = ModularBuildingFactory.Spawn(BuildingCategory.Market, Vector3.zero, _root.transform);
                var shop = ModularBuildingFactory.Spawn(BuildingCategory.EngineerWorkshop, Vector3.zero, _root.transform);
                Assert.IsNull(FindChild(market.transform, "Dress_Arch_PressureDome"), "canopy is not buried under a dome");
                Assert.IsNull(FindChild(shop.transform, "Dress_Arch_PrintedVault"), "arched hangar is not buried under a vault");
                Assert.IsTrue(PlanetArchitecture.IsRoofShell("PressureDome"));
                Assert.IsFalse(PlanetArchitecture.IsRoofShell("DustSkirt"));

                // The dresser strips shells from the part list (parts merge per role, so the
                // child-name checks above cannot see them).
                var parts = PlanetArchitecture.Adapt(CelestialBodyId.Mars, ArchArchetype.Hub, 6f, 6f, 3f, 1);
                Assert.IsTrue(parts.Exists(p => PlanetArchitecture.IsRoofShell(p.Name)), "Mars hubs do author a dome");
                PlanetArchitecture.StripRoofShells(parts);
                Assert.IsFalse(parts.Exists(p => PlanetArchitecture.IsRoofShell(p.Name)));

                var earth = PlanetArchitecture.Adapt(CelestialBodyId.Earth, ArchArchetype.Hub, 6f, 6f, 3f, 1);
                Assert.IsTrue(earth.Exists(p => PlanetArchitecture.IsWindowBand(p.Name)), "Earth authors window bands");
                PlanetArchitecture.StripRoofShells(earth);
                Assert.IsFalse(earth.Exists(p => PlanetArchitecture.IsWindowBand(p.Name)), "no chopsticks through the kits");
            }
            finally
            {
                ModularBuildingFactory.BindBody(null);
            }
        }

        private static Dictionary<Renderer, bool> Snapshot(GameObject go)
        {
            var map = new Dictionary<Renderer, bool>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) map[r] = r.enabled;
            return map;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
