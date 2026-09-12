using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class RobotGuildTests
    {
        [Test]
        public void Starter_HasFourDistinctHalls()
        {
            var starter = RobotGuildCatalog.Starter;
            Assert.AreEqual(4, starter.Length);

            var names = new System.Collections.Generic.HashSet<string>();
            var classes = new System.Collections.Generic.HashSet<SpecialistClass>();
            for (int i = 0; i < starter.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(starter[i].HallName));
                Assert.IsFalse(string.IsNullOrEmpty(starter[i].Motto));
                Assert.IsFalse(string.IsNullOrEmpty(starter[i].CatalogLine));
                Assert.IsTrue(names.Add(starter[i].HallName), starter[i].HallName);
                Assert.IsTrue(classes.Add(starter[i].Class), starter[i].Class.ToString());
            }
        }

        [Test]
        public void Starter_CoversScoutEngineerDefenseMedic()
        {
            Assert.AreEqual(SpecialistClass.ScoutDrone, RobotGuildCatalog.Get(RobotGuildId.Horizon).Class);
            Assert.AreEqual(SpecialistClass.EngineerBot, RobotGuildCatalog.Get(RobotGuildId.Anvil).Class);
            Assert.AreEqual(SpecialistClass.DefenseMech, RobotGuildCatalog.Get(RobotGuildId.Aegis).Class);
            Assert.AreEqual(SpecialistClass.Medic, RobotGuildCatalog.Get(RobotGuildId.Triage).Class);
        }

        [Test]
        public void HallNames_MatchColonyStructureCallsigns()
        {
            Assert.AreEqual("Horizon Lodge", ColonyStructure.GuildNameFor(SpecialistClass.ScoutDrone));
            Assert.AreEqual("Anvil Compact", ColonyStructure.GuildNameFor(SpecialistClass.EngineerBot));
            Assert.AreEqual("Aegis Lodge", ColonyStructure.GuildNameFor(SpecialistClass.DefenseMech));
            Assert.AreEqual("Triage Compact", ColonyStructure.GuildNameFor(SpecialistClass.Medic));

            for (int i = 0; i < RobotGuildCatalog.Starter.Length; i++)
            {
                var g = RobotGuildCatalog.Starter[i];
                Assert.AreEqual(ColonyStructure.GuildNameFor(g.Class), g.HallName);
            }
        }

        [Test]
        public void TryMatch_ReadsPreferredOccupants()
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.displayName = "Anvil Compact";
            data.category = BuildingCategory.GuildHall;
            data.preferredOccupants = new[] { SpecialistClass.EngineerBot };

            Assert.IsTrue(RobotGuildCatalog.TryMatch(data, out var guild));
            Assert.AreEqual(RobotGuildId.Anvil, guild.Id);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void NamedHall_LocksClassAndKeepsCallsign()
        {
            var go = new GameObject("Hall");
            var st = go.AddComponent<ColonyStructure>();
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.displayName = "Horizon Lodge";
            data.category = BuildingCategory.GuildHall;
            data.preferredOccupants = new[] { SpecialistClass.ScoutDrone };

            st.Configure(StructureRole.Guild, null, 70f, BuildingCategory.GuildHall, data);

            Assert.IsTrue(st.IsGuild);
            Assert.IsTrue(st.ClassLocked);
            Assert.IsTrue(st.HasPreferredClass);
            Assert.AreEqual(SpecialistClass.ScoutDrone, st.PreferredClass);
            Assert.AreEqual("Horizon Lodge", st.DisplayName);

            st.SetPreferredClass(SpecialistClass.EngineerBot);
            Assert.AreEqual(SpecialistClass.ScoutDrone, st.PreferredClass);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void GenericHall_StaysAssignable()
        {
            var go = new GameObject("Hall");
            var st = go.AddComponent<ColonyStructure>();
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.displayName = "Guild Hall";
            data.category = BuildingCategory.GuildHall;

            st.Configure(StructureRole.Guild, null, 70f, BuildingCategory.GuildHall, data);

            Assert.IsFalse(st.ClassLocked);
            Assert.IsFalse(st.HasPreferredClass);
            st.SetPreferredClass(SpecialistClass.Medic);
            Assert.AreEqual("Triage Compact", st.DisplayName);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void PaintGuildAccents_AddsBannerWhenMissing()
        {
            var go = new GameObject("Hall");
            HeroBuildingKits.PaintGuildAccents(go.transform, new Color(0.22f, 0.84f, 0.98f));
            Assert.IsNotNull(go.transform.Find("GuildBanner"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void LaterCallsigns_AreNotStarterHalls()
        {
            Assert.IsFalse(RobotGuildCatalog.IsStarterClass(SpecialistClass.HarvesterBot));
            Assert.IsFalse(RobotGuildCatalog.IsStarterClass(SpecialistClass.SentinelMech));
            Assert.IsNull(RobotGuildCatalog.ForClass(SpecialistClass.CourierBot));
        }
    }
}
