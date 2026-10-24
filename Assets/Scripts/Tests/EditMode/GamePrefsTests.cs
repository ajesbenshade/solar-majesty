using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class GamePrefsTests
    {
        private const string Key = "SM_Test_GamePrefs_Key";
        private const string LegacyKey = "SM_Test_GamePrefs_Legacy";
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sm-gameprefs-" + System.Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _dir;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(LegacyKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(LegacyKey);
            GamePrefs.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Values_RoundTripThroughTheFile()
        {
            GamePrefs.SetInt(Key, 7);
            GamePrefs.SetFloat(Key + "f", 0.25f);
            GamePrefs.SetString(Key + "s", "Earth,Luna");
            GamePrefs.Save();
            Assert.That(File.Exists(GamePrefs.FilePath), Is.True);

            GamePrefs.Reload();
            Assert.That(GamePrefs.GetInt(Key), Is.EqualTo(7));
            Assert.That(GamePrefs.GetFloat(Key + "f"), Is.EqualTo(0.25f));
            Assert.That(GamePrefs.GetString(Key + "s"), Is.EqualTo("Earth,Luna"));
        }

        [Test]
        public void ProgressDoesNotLandInTheRegistry()
        {
            GamePrefs.SetInt(Key, 3);
            GamePrefs.Save();
            Assert.That(PlayerPrefs.HasKey(Key), Is.False, "Steam Cloud cannot sync the registry");
        }

        [Test]
        public void LegacyRegistryValue_MigratesOnFirstRead()
        {
            PlayerPrefs.SetInt(LegacyKey, 4);
            Assert.That(GamePrefs.HasKey(LegacyKey), Is.True);
            Assert.That(GamePrefs.GetInt(LegacyKey), Is.EqualTo(4));
            GamePrefs.Save();

            PlayerPrefs.DeleteKey(LegacyKey); // e.g. a fresh PC restored from Steam Cloud
            GamePrefs.Reload();
            Assert.That(GamePrefs.GetInt(LegacyKey), Is.EqualTo(4));
        }

        [Test]
        public void DeleteKey_DoesNotResurrectTheLegacyCopy()
        {
            PlayerPrefs.SetString(LegacyKey, "old");
            Assert.That(GamePrefs.GetString(LegacyKey), Is.EqualTo("old"));

            GamePrefs.DeleteKey(LegacyKey);
            Assert.That(GamePrefs.HasKey(LegacyKey), Is.False);
            Assert.That(GamePrefs.GetString(LegacyKey, "none"), Is.EqualTo("none"));
        }

        [Test]
        public void WrongType_ReturnsDefault_LikePlayerPrefs()
        {
            GamePrefs.SetString(Key, "text");
            Assert.That(GamePrefs.GetInt(Key, -1), Is.EqualTo(-1));
            Assert.That(GamePrefs.HasKey(Key), Is.True);
        }

        [Test]
        public void DisplaySettings_StayOnThisMachine()
        {
            int before = PlayerPrefs.GetInt(DemoSettings.ResolutionWKey, -123);
            try
            {
                GamePrefs.SetInt(DemoSettings.ResolutionWKey, 2560);
                GamePrefs.Save();
                Assert.That(PlayerPrefs.GetInt(DemoSettings.ResolutionWKey), Is.EqualTo(2560));
                GamePrefs.Reload();
                Assert.That(File.Exists(GamePrefs.FilePath) && File.ReadAllText(GamePrefs.FilePath).Contains(DemoSettings.ResolutionWKey),
                    Is.False, "resolution must not roam to other PCs");
            }
            finally
            {
                if (before == -123) PlayerPrefs.DeleteKey(DemoSettings.ResolutionWKey);
                else PlayerPrefs.SetInt(DemoSettings.ResolutionWKey, before);
            }
        }

        [Test]
        public void CorruptFile_FallsBackToBackup()
        {
            GamePrefs.SetInt(Key, 1);
            GamePrefs.Save();
            GamePrefs.SetInt(Key, 2);
            GamePrefs.Save(); // first write is now the .bak

            File.WriteAllText(GamePrefs.FilePath, "{ not json");
            GamePrefs.Reload();
            Assert.That(GamePrefs.GetInt(Key), Is.EqualTo(1));
        }

        [Test]
        public void Save_WhenClean_DoesNotCreateAFile()
        {
            GamePrefs.Save();
            Assert.That(File.Exists(GamePrefs.FilePath), Is.False);
        }
    }
}
