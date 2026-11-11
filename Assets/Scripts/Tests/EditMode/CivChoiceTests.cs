using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>The civ picked for a new campaign survives the scene reload and reaches the save.</summary>
    public class CivChoiceTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sm-civchoice-" + System.Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _dir;
            PlayerPrefs.DeleteKey(CivChoice.PrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(CivChoice.PrefsKey);
            GamePrefs.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void NothingPicked_IsTheNeutralCiv()
        {
            Assert.AreEqual(CivId.None, CivChoice.Load());
            Assert.IsTrue(CivModifiers.For(CivChoice.Load()).IsIdentity);
        }

        [TestCase(CivId.LumenConcord)]
        [TestCase(CivId.TetherExchange)]
        [TestCase(CivId.BastionMandate)]
        [TestCase(CivId.None)]
        public void Choice_SurvivesAReload(CivId civ)
        {
            CivChoice.Set(civ);
            GamePrefs.Reload(); // what the next scene sees
            Assert.AreEqual(civ, CivChoice.Load());
        }

        [Test]
        public void UnknownNumber_ReadsAndWritesAsNeutral()
        {
            GamePrefs.SetInt(CivChoice.PrefsKey, 99);
            Assert.AreEqual(CivId.None, CivChoice.Load());
            CivChoice.Set((CivId)77);
            Assert.AreEqual((int)CivId.None, GamePrefs.GetInt(CivChoice.PrefsKey, -1));
        }

        [TestCase("StartNewGame")]
        [TestCase("StartNewGameOn")]
        public void NewGameMethods_DefaultToTheNeutralCiv(string method)
        {
            // Autoplay, benchmark and the shot harnesses call these without a civ.
            var info = typeof(GameLoop).GetMethod(method,
                new[] { typeof(CelestialBodyId), typeof(int), typeof(CivId) });
            Assert.IsNotNull(info, method);
            var parameters = info.GetParameters();
            Assert.IsTrue(parameters[1].IsOptional);
            Assert.IsTrue(parameters[2].IsOptional);
            Assert.AreEqual((int)CivId.None, System.Convert.ToInt32(parameters[2].DefaultValue));
        }

        [Test]
        public void ChosenCiv_ReachesTheSave()
        {
            // The path a new campaign takes: the dialog stores the choice, the reloaded scene
            // builds its service from it, and the save records that service's id.
            CivChoice.Set(CivId.TetherExchange);
            GamePrefs.Reload();
            var civ = CivModifiers.For(CivChoice.Load());
            var save = new SaveGame { body = 0, seed = 9001, civ = (int)civ.Id };
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            Assert.AreEqual((int)CivId.TetherExchange, loaded.civ);
            Assert.AreEqual(CivId.TetherExchange, CivModifiers.For(CivCatalog.FromSaved(loaded.civ)).Id);
        }
    }
}
