using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    public class SaveSafetyTests
    {
        [UnityTest]
        public IEnumerator ApplySave_RejectsWrongWorldBeforeChangingResources()
        {
            var root = new GameObject("save-boundary-test");
            root.SetActive(false); // Test the load boundary without booting a campaign or writing prefs.
            var loop = root.AddComponent<GameLoop>();
            var resources = new ResourceManager();
            resources.Set(ResourceId.Metals, 91);
            typeof(GameLoop).GetProperty("Resources").SetValue(loop, resources);
            var body = (CelestialBodyId)typeof(GameLoop).GetField("celestialBody", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loop);
            var save = new SaveGame { body = (int)body, seed = BodySeed.Current + 1 };
            save.stockpile.metals = 999;
            Assert.IsFalse(loop.ApplySave(save), "different terrain seed must be rejected");
            save.seed = BodySeed.Current;
            save.body = body == CelestialBodyId.Earth ? (int)CelestialBodyId.Luna : (int)CelestialBodyId.Earth;
            Assert.IsFalse(loop.ApplySave(save), "different planet must be rejected");
            Assert.AreEqual(91, resources.Get(ResourceId.Metals));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamagedStructure_RestoresWithoutBecomingFullyRepaired()
        {
            var root = new GameObject("saved-structure-test");
            var structure = root.AddComponent<ColonyStructure>();
            structure.Configure(default, null, 100f);
            structure.RestoreHealth01(0.35f);
            yield return null;
            Assert.AreEqual(0.35f, structure.Health01, 0.001f);
            Assert.IsTrue(structure.NeedsRepair);
            Assert.AreEqual(10f, structure.Repair(10f), 0.001f);
            Assert.AreEqual(0.45f, structure.Health01, 0.001f);
            Object.Destroy(root);
            yield return null;
        }
    }
}
