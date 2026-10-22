using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Watchtower build-list blurb, and Courier Bot levy hand-ins while a flag is open.</summary>
    public class TowerCourierTests
    {
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
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            foreach (var n in new[] { "DemoAudio", "SM_SpatialAudio", "SM_VfxPool", "SM_CharacterVoice" })
            {
                var go = GameObject.Find(n);
                if (go != null) Object.DestroyImmediate(go);
            }
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            if (!string.IsNullOrEmpty(_saveRoot) && System.IO.Directory.Exists(_saveRoot))
                System.IO.Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void Watchtower_BlurbIsCanon_AndStatOnlyWhenItFits()
        {
            Assert.AreEqual("Guard post and levy till. Arm lasers for EU.", HudLayout.WatchtowerBlurb);
            var v = HudLayout.BuildRowVariants("Watchtower", HudLayout.WatchtowerBlurb, HudLayout.WatchtowerStat);
            Assert.AreEqual("Watchtower  ·  Guard post and levy till. Arm lasers for EU.  ·  +1 Haul", v[0]);
            Assert.AreEqual("Watchtower  ·  Guard post and levy till. Arm lasers for EU.", v[1]);

            System.Func<string, float> measure = s => s.Length * 6f;
            Assert.AreEqual(v[0], HudLayout.FitText(v, measure, 1000f), "room for the stat line");
            Assert.AreEqual(v[1], HudLayout.FitText(v, measure, v[1].Length * 6f), "the blurb wins over the stat");
            StringAssert.DoesNotContain("+1 Haul", HudLayout.FitText(v, measure, 200f));
        }

        [Test]
        public void CourierLevyDue_OnlyOnFlags_CarryOrCadence()
        {
            Assert.IsTrue(LevyRun.CourierLevyDue(SpecialistAction.PursueFlag, 10, 0f));
            Assert.IsFalse(LevyRun.CourierLevyDue(SpecialistAction.PursueFlag, 0, LevyRun.CourierLevyCadence - 1f));
            Assert.IsTrue(LevyRun.CourierLevyDue(SpecialistAction.PursueFlag, 0, LevyRun.CourierLevyCadence));
            Assert.IsFalse(LevyRun.CourierLevyDue(SpecialistAction.Flee, 10, 999f));
            Assert.IsFalse(LevyRun.CourierLevyDue(SpecialistAction.Rest, 10, 999f));
        }

        [Test]
        public void Courier_DumpsHaulAtTheTill_WithAFlagOpen()
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.CourierBot;
            SpecialistPersonality.Apply(data);
            var go = new GameObject("Hero_CourierBot");
            _created.Add(go);
            var hero = go.AddComponent<SpecialistAgent>();
            hero.BindForTravelTest(data, _flags, new SpecialistBrain());

            var flagData = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(flagData);
            flagData.flagType = FlagType.Explore;
            flagData.displayName = "Explore";
            flagData.minBounty = 0;
            flagData.maxBounty = 5000;
            flagData.defaultBounty = 1000;
            flagData.workRequired = 100f;
            flagData.baseRisk = 0.05f;
            flagData.stronglyAttracts = new[] { SpecialistClass.CourierBot };
            var flag = _flags.Post(flagData, new Vector3(60f, 0f, 0f), 1000f);

            for (int i = 0; i < 20 && hero.CurrentAction != SpecialistAction.PursueFlag; i++)
                hero.Simulate(0.5f);
            Assert.AreEqual(SpecialistAction.PursueFlag, hero.CurrentAction, "courier must take the flag");

            string line = null;
            int delivered = 0;
            hero.LevyChestForTest = _ => new Vector3(-6f, 0f, 0f);
            hero.LevyDeliveredForTest = (n, l) => { delivered = n; line = l; };
            hero.AddLevyCarry(50);

            for (int i = 0; i < 80 && line == null; i++)
                hero.Simulate(0.25f);

            Assert.AreEqual(50, delivered);
            Assert.IsNotNull(line, "carried levy must reach the till while a flag is open");
            StringAssert.StartsWith("Haul dumped 50 EU at the till.", line);
            CollectionAssert.Contains(_flags.Flags, flag, "the flag is still open");
        }
    }
}
