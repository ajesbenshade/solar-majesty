using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class SaveMigrationTests
    {
        private string _path;
        private readonly Dictionary<string, byte[]> _backup = new Dictionary<string, byte[]>();

        [SetUp]
        public void PreserveSlot()
        {
            _path = SaveSystem.SlotPath(3);
            Directory.CreateDirectory(SaveSystem.SaveDirectory);
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string p = _path + suffix;
                _backup[p] = File.Exists(p) ? File.ReadAllBytes(p) : null;
                if (File.Exists(p)) File.Delete(p);
            }
        }

        [TearDown]
        public void RestoreSlot()
        {
            foreach (var entry in _backup)
                if (entry.Value == null) { if (File.Exists(entry.Key)) File.Delete(entry.Key); }
                else File.WriteAllBytes(entry.Key, entry.Value);
            _backup.Clear();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        public void OlderSnapshot_MigratesWithoutLosingActiveResearch(int version)
        {
            File.WriteAllText(_path, "{\"version\":" + version + ",\"body\":0,\"seed\":9001,\"research\":{\"activeTech\":1,\"activeProgress\":12.5,\"bankedScience\":3}}");
            Assert.IsTrue(SaveSystem.TryRead(3, out var saved));
            Assert.AreEqual(SaveGame.CurrentVersion, saved.version);
            Assert.AreEqual(12.5f, saved.research.activeProgress);
            Assert.AreEqual(3f, saved.research.bankedScience);
            Assert.IsNotNull(saved.research.progress);
            Assert.IsNotNull(saved.buildings);
            Assert.IsNull(saved.villageGrowth);
            Assert.IsNull(saved.collectors);
        }

        [TestCase("{}")]
        [TestCase("{\"version\":4,\"body\":99,\"seed\":9001}")]
        [TestCase("{\"version\":4,\"body\":0,\"seed\":9001,\"buildings\":[null]}")]
        public void InvalidButParseableSnapshot_RecoversPreviousVersion(string bad)
        {
            var good = new SaveGame { body = 0, seed = 9001, label = "previous" };
            File.WriteAllText(_path + ".bak", JsonUtility.ToJson(good));
            File.WriteAllText(_path, bad);
            Assert.IsTrue(SaveSystem.TryRead(3, out var restored));
            Assert.AreEqual("previous", restored.label);
        }

        [Test]
        public void FuturePrimary_IsRefusedWithoutRollingBackToBackup()
        {
            var good = new SaveGame { body = 0, seed = 9001 };
            File.WriteAllText(_path + ".bak", JsonUtility.ToJson(good));
            good.version = SaveGame.CurrentVersion + 1;
            string future = JsonUtility.ToJson(good);
            File.WriteAllText(_path, future);
            Assert.IsFalse(SaveSystem.TryRead(3, out _));
            Assert.IsTrue(SaveSystem.IsNewerVersion(3));
            Assert.IsFalse(SaveSystem.Write(3, new SaveGame { body = 0, seed = 9001 }));
            Assert.AreEqual(future, File.ReadAllText(_path));
        }

        [Test]
        public void LegacyFauna_AssociatesOnlyStalkersWithNearestUnclearedDen()
        {
            var old = new SaveGame { version = 3, body = 0, seed = 9001 };
            old.lairs.Add(new SaveLair { px = 0, cleared = true });
            old.lairs.Add(new SaveLair { px = 10 });
            old.lairs.Add(new SaveLair { px = 30 });
            old.fauna.Add(new SaveFauna { kind = (int)FaunaKind.Stalker, px = 12, health = 1 });
            old.fauna.Add(new SaveFauna { kind = (int)FaunaKind.Mite, px = 12, health = 1 });
            File.WriteAllText(_path, JsonUtility.ToJson(old));
            Assert.IsTrue(SaveSystem.TryRead(3, out var migrated));
            Assert.AreEqual(1, migrated.fauna[0].lairIndex);
            Assert.AreEqual(-1, migrated.fauna[1].lairIndex);
        }

        [Test]
        public void InterruptedTemporaryWrite_DoesNotReplaceCompletePrimary()
        {
            Assert.IsTrue(SaveSystem.Write(3, new SaveGame { body = 0, seed = 9001, label = "complete" }));
            File.WriteAllText(_path + ".tmp", "{\"version\":");
            Assert.IsTrue(SaveSystem.TryRead(3, out var restored));
            Assert.AreEqual("complete", restored.label);
        }
    }
}
