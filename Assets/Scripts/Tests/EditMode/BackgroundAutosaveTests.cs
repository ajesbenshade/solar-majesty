using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>Autosave writes on a background queue; every reader must still see the finished file.</summary>
    public class BackgroundAutosaveTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sm-bgsave-" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Flush();
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static SaveGame Snapshot(int seed) => new SaveGame { body = (int)CelestialBodyId.Mars, seed = seed, label = "autosave" };

        [Test]
        public void Autosave_WritesEveryTarget_AndReadsSeeIt()
        {
            var paths = new List<string> { SaveSystem.WorldPath(CelestialBodyId.Mars), SaveSystem.SlotPath(0), SaveSystem.SlotPath(2) };
            SaveSystem.WriteAutosave(Snapshot(42), paths);

            Assert.That(SaveSystem.TryRead(0, out SaveGame slot0), Is.True);
            Assert.That(slot0.seed, Is.EqualTo(42));
            Assert.That(SaveSystem.TryRead(2, out SaveGame slot2), Is.True);
            Assert.That(slot2.seed, Is.EqualTo(42));
            Assert.That(SaveSystem.TryReadWorld(CelestialBodyId.Mars, out SaveGame world), Is.True);
            Assert.That(world.seed, Is.EqualTo(42));
        }

        [Test]
        public void BackToBackAutosaves_LandInOrder_WithThePreviousAsBackup()
        {
            var paths = new List<string> { SaveSystem.SlotPath(0) };
            for (int i = 1; i <= 5; i++)
                SaveSystem.WriteAutosave(Snapshot(i), paths);

            Assert.That(SaveSystem.TryRead(0, out SaveGame latest), Is.True);
            Assert.That(latest.seed, Is.EqualTo(5));
            Assert.That(File.Exists(SaveSystem.SlotPath(0) + ".bak"), Is.True);
            Assert.That(File.Exists(SaveSystem.SlotPath(0) + ".tmp"), Is.False);
        }

        [Test]
        public void Autosave_DoesNotOverwriteANewerBuildsSave()
        {
            Directory.CreateDirectory(_dir);
            string path = SaveSystem.SlotPath(0);
            File.WriteAllText(path, "{\"version\":" + (SaveGame.CurrentVersion + 1) + ",\"body\":2,\"seed\":7}");

            SaveSystem.WriteAutosave(Snapshot(99), new List<string> { path });
            SaveSystem.Flush();

            StringAssert.Contains("\"seed\":7", File.ReadAllText(path));
        }

        [Test]
        public void FileReplacedAfterOurAutosave_IsCheckedAgain()
        {
            string path = SaveSystem.SlotPath(0);
            SaveSystem.WriteAutosave(Snapshot(1), new List<string> { path });
            SaveSystem.Flush();

            // Someone else (a newer build, a cloud restore) replaces the file we wrote.
            File.WriteAllText(path, "{\"version\":" + (SaveGame.CurrentVersion + 1) + ",\"body\":2,\"seed\":7}");
            File.SetLastWriteTimeUtc(path, System.DateTime.UtcNow.AddMinutes(1));

            SaveSystem.WriteAutosave(Snapshot(2), new List<string> { path });
            SaveSystem.Flush();
            StringAssert.Contains("\"seed\":7", File.ReadAllText(path));
        }

        [Test]
        public void ChangingTheSaveFolder_WaitsForQueuedWrites()
        {
            SaveSystem.WriteAutosave(Snapshot(5), new List<string> { SaveSystem.SlotPath(0) });
            SaveSystem.DirectoryOverride = _dir; // same folder; the setter alone must flush
            Assert.That(File.Exists(Path.Combine(_dir, "slot0.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "slot0.json.tmp")), Is.False);
        }

        [Test]
        public void Delete_WaitsForAPendingAutosave()
        {
            SaveSystem.WriteAutosave(Snapshot(3), new List<string> { SaveSystem.SlotPath(1) });
            SaveSystem.Delete(1);
            SaveSystem.Flush();
            Assert.That(SaveSystem.Exists(1), Is.False, "a queued write must not resurrect a deleted slot");
        }
    }
}
