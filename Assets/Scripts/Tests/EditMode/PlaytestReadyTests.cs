using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// First-hour playtest contracts. Game-tab smoke still belongs to a human;
    /// these lock the greed beat, CRED copy, and Continue campus blob.
    /// </summary>
    public class PlaytestReadyTests
    {
        private const string SaveFlag = "SM_SaveExists";
        private const string SaveMet = "SM_Save_Metals";
        private bool _hadSave;
        private bool _hadMet;
        private bool _hadBoot;
        private bool _savedExistsFlag;
        private int _savedSave;
        private int _savedMet;
        private int _savedBoot;

        [SetUp]
        public void SetUp()
        {
            _hadSave = GamePrefs.HasKey(SaveFlag);
            _hadMet = GamePrefs.HasKey(SaveMet);
            _hadBoot = GamePrefs.HasKey(DemoSettings.BootPlayKey);
            _savedExistsFlag = DemoSettings.SaveExists;
            _savedSave = GamePrefs.GetInt(SaveFlag, 0);
            _savedMet = GamePrefs.GetInt(SaveMet, 0);
            _savedBoot = GamePrefs.GetInt(DemoSettings.BootPlayKey, 0);
        }

        [TearDown]
        public void TearDown()
        {
            DemoSettings.SaveExists = _savedExistsFlag;
            if (_hadSave) GamePrefs.SetInt(SaveFlag, _savedSave);
            else GamePrefs.DeleteKey(SaveFlag);
            if (_hadMet) GamePrefs.SetInt(SaveMet, _savedMet);
            else GamePrefs.DeleteKey(SaveMet);
            if (_hadBoot) GamePrefs.SetInt(DemoSettings.BootPlayKey, _savedBoot);
            else GamePrefs.DeleteKey(DemoSettings.BootPlayKey);
            GamePrefs.Save();
        }

        [Test]
        public void ContinueCopy_SaysCredNotMet()
        {
            DemoSettings.SaveExists = true;
            GamePrefs.SetInt(SaveFlag, 1);
            GamePrefs.SetInt(SaveMet, 170);
            GamePrefs.Save();

            string label = DemoSettings.ContinueButtonLabel();
            string detail = DemoSettings.ContinueDetail();
            StringAssert.Contains("EU", label);
            StringAssert.Contains("EU", detail);
            Assert.IsFalse(label.Contains("MET"), label);
            Assert.IsFalse(detail.Contains("MET"), detail);
        }

        [Test]
        public void BootStraightIntoPlay_UnsetPrefsMeansOff()
        {
            GamePrefs.DeleteKey(DemoSettings.BootPlayKey);
            Assert.AreEqual(0, GamePrefs.GetInt(DemoSettings.BootPlayKey, 0),
                "player builds must not skip title; BootStraightIntoPlay is editor still / CaptureStill only");
        }

        [Test]
        public void CampusSnapshot_RoundtripsFirstHourPieces()
        {
            var slots = new List<CampusSlot>
            {
                Slot(BuildingCategory.Commons, 4, 4, 6, 6, 1000),
                Slot(BuildingCategory.Power, 10, 10, 4, 4, 1000),
                Slot(BuildingCategory.Habitat, 12, 4, 4, 4, 1000),
                Slot(BuildingCategory.EngineerWorkshop, 12, 8, 4, 4, 1000)
            };

            string blob = CampusSnapshot.Encode(3, slots);
            var decoded = new List<CampusSlot>();
            Assert.IsTrue(CampusSnapshot.TryDecode(blob, out int pop, decoded));
            Assert.AreEqual(3, pop);
            Assert.AreEqual(4, decoded.Count);
            Assert.AreEqual(BuildingCategory.Commons, decoded[0].Category);
            Assert.AreEqual(BuildingCategory.Power, decoded[1].Category);
            Assert.AreEqual(BuildingCategory.Habitat, decoded[2].Category);
            Assert.AreEqual(BuildingCategory.EngineerWorkshop, decoded[3].Category);
            Assert.AreEqual(1000, decoded[3].ProgressMilli);
            Assert.AreEqual(15, (int)BuildingCategory.Commons);
        }

        [Test]
        public void CampusSnapshot_RestoresCommonsBeforeWorkshops()
        {
            Assert.AreEqual(0, CampusSnapshot.Rank(BuildingCategory.Commons));
            Assert.Greater(
                CampusSnapshot.Rank(BuildingCategory.EngineerWorkshop),
                CampusSnapshot.Rank(BuildingCategory.Commons));
            Assert.Greater(
                CampusSnapshot.Rank(BuildingCategory.Habitat),
                CampusSnapshot.Rank(BuildingCategory.Commons));
        }

        [Test]
        public void Engineer_RefusesDefaultBuild_TakesNineHundred()
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            data.specialistClass = SpecialistClass.EngineerBot;
            SpecialistPersonality.Apply(data);

            var brain = new SpecialistBrain();
            var ctx = new SpecialistContext
            {
                Data = data,
                Position = Vector3.zero,
                Fatigue = 0f,
                GreedHunger = 0f,
                HealthNormalized = 1f,
                SafetyPosition = new Vector3(0f, 0f, -5f),
                CurrentAction = SpecialistAction.Idle
            };
            var flagData = ScriptableObject.CreateInstance<FlagData>();
            flagData.flagType = FlagType.Build;
            flagData.stronglyAttracts = null;

            var cheap = Flag(flagData, MajestyEconomy.FlagDefaultBounty(FlagType.Build));
            var dear = Flag(flagData, 900f);

            Assert.IsFalse(brain.WouldTakeFlag(ctx, cheap, 0f, out _), "default $700 Build must be ignored");
            Assert.AreEqual(FlagRefusalKind.Greed, brain.ExplainFlag(ctx, cheap, 0f));
            Assert.IsTrue(brain.WouldTakeFlag(ctx, dear, 0f, out _), "+ toward $900 should tempt Anvil");

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(flagData);
        }

        [Test]
        public void SlowReader_HungryEngineerAcceptsCheapBuild_AndTutorialContinues()
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            var flagData = ScriptableObject.CreateInstance<FlagData>();
            try
            {
                data.specialistClass = SpecialistClass.EngineerBot;
                SpecialistPersonality.Apply(data);
                flagData.flagType = FlagType.Build;
                var ctx = new SpecialistContext
                {
                    Data = data, Position = Vector3.zero, HealthNormalized = 1f,
                    GreedHunger = 0.81f, CurrentAction = SpecialistAction.Idle
                };
                bool accepted = new SpecialistBrain().WouldTakeFlag(ctx, Flag(flagData, MajestyEconomy.FlagDefaultBounty(FlagType.Build)), 0f, out _);
                Assert.IsTrue(accepted, "the real greed gate legitimately accepts after hunger rises");
                Assert.AreEqual(FirstHourTutorial.PestStep,
                    FirstHourTutorial.Advance(1, true, !accepted, accepted, false));
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(flagData);
            }
        }

        [Test]
        public void StarterGuilds_AreFourNamedHalls()
        {
            Assert.AreEqual(4, RobotGuildCatalog.StarterCount);
            Assert.AreEqual("Horizon Lodge", RobotGuildCatalog.Get(RobotGuildId.Horizon).HallName);
            Assert.AreEqual("Anvil Lodge", RobotGuildCatalog.Get(RobotGuildId.Anvil).HallName);
            Assert.AreEqual("Aegis Lodge", RobotGuildCatalog.Get(RobotGuildId.Aegis).HallName);
            Assert.AreEqual("Triage Lodge", RobotGuildCatalog.Get(RobotGuildId.Triage).HallName);
        }

        private static CampusSlot Slot(
            BuildingCategory cat, int x, int y, int w, int h, int progress)
        {
            return new CampusSlot
            {
                Category = cat,
                X = x,
                Y = y,
                W = w,
                H = h,
                ProgressMilli = progress
            };
        }

        private static FlagHandle Flag(FlagData data, float bounty)
        {
            return new FlagHandle
            {
                Data = data,
                WorldPosition = new Vector3(8f, 0f, 0f),
                CurrentBounty = bounty,
                RuntimeId = new object()
            };
        }
    }
}
