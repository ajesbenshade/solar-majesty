using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Free-text flag orders: the built-in parser, the brain gate, greedy heroes bending soft orders,
    /// and the optional Laya reading. None of this needs a model running.
    /// </summary>
    public class FlagOrdersTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private static int Bit(SpecialistClass c) => FlagOrders.Bit(c);
        private static readonly int Mechs = Bit(SpecialistClass.DefenseMech) | Bit(SpecialistClass.SentinelMech);

        [TestCase("stay away from this flag unless you are level 9 or higher", 9, 0)]
        [TestCase("Stay away unless you're L9+", 9, 0)]
        [TestCase("level 5 and up", 5, 0)]
        [TestCase("at least level 4", 4, 0)]
        [TestCase("no one below level 5", 5, 0)]
        [TestCase("not for anyone under lvl 6", 6, 0)]
        [TestCase("stay away, unless level 7", 7, 0)]
        [TestCase("strictly level 3 or lower", 0, 3)]
        [TestCase("don't go if you're level 9 or higher", 0, 8)]
        [TestCase("level 3-6 engineers only", 3, 6)]
        public void Parse_Levels(string text, int min, int max)
        {
            var o = FlagOrdersParser.Parse(text);
            Assert.AreEqual(min, o.minLevel, "minLevel");
            Assert.AreEqual(max, o.maxLevel, "maxLevel");
        }

        [Test]
        public void Parse_Classes()
        {
            Assert.AreEqual(Mechs, FlagOrdersParser.Parse("mechs only").onlyClassMask);
            Assert.AreEqual(Mechs, FlagOrdersParser.Parse("stay away except mechs").onlyClassMask);
            Assert.AreEqual(Bit(SpecialistClass.ScoutDrone), FlagOrdersParser.Parse("no scouts").bannedClassMask);
            Assert.AreEqual(Bit(SpecialistClass.ScoutDrone) | Bit(SpecialistClass.Medic),
                FlagOrdersParser.Parse("no scouts and medics").bannedClassMask);
            Assert.AreEqual(Bit(SpecialistClass.CourierBot), FlagOrdersParser.Parse("everyone except couriers").bannedClassMask);
            Assert.AreEqual(Bit(SpecialistClass.CourierBot), FlagOrdersParser.Parse("keep couriers away").bannedClassMask);
            Assert.AreEqual(Bit(SpecialistClass.DefenseMech),
                FlagOrdersParser.Parse("only defense mechs above level 4").onlyClassMask);
        }

        [Test]
        public void Parse_HealthStrictAndNothing()
        {
            Assert.AreEqual(0.7f, FlagOrdersParser.Parse("L5+ and healthy").minHealth, 1e-4f);
            Assert.AreEqual(0.5f, FlagOrdersParser.Parse("stay away if hurt").minHealth, 1e-4f);
            Assert.AreEqual(0.6f, FlagOrdersParser.Parse("above 60% health only").minHealth, 1e-4f);
            Assert.IsTrue(FlagOrdersParser.Parse("mechs only, no exceptions").strict);
            Assert.IsFalse(FlagOrdersParser.Parse("mechs only").strict);

            var none = FlagOrdersParser.Parse("go get em");
            Assert.IsFalse(none.HasRules);
            Assert.AreEqual("go get em", none.text);
            Assert.AreEqual("", none.Summary());
        }

        [Test]
        public void Summary_ReadsBack()
        {
            Assert.AreEqual("L9+", FlagOrdersParser.Parse("stay away unless you're level 9 or higher").Summary());
            Assert.AreEqual("L5+ · defense mechs only · strict",
                FlagOrdersParser.Parse("only defense mechs above level 4, no exceptions").Summary());
        }

        private SpecialistContext Hero(int level, float greed = 0.3f, float hunger = 0f)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.DefenseMech;
            data.baseGreed = greed;
            data.courage = 0.7f;
            data.combatPreference = 0.9f;
            return new SpecialistContext
            {
                Data = data,
                Level = level,
                HealthNormalized = 1f,
                GreedHunger = hunger,
                SafetyPosition = new Vector3(5, 0, 0)
            };
        }

        private FlagHandle Flag(string orders)
        {
            var fd = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(fd);
            fd.flagType = FlagType.ClearThreat;
            return new FlagHandle
            {
                Data = fd,
                WorldPosition = new Vector3(10, 0, 0),
                CurrentBounty = 900f,
                RuntimeId = 1,
                Orders = orders == null ? null : FlagOrdersParser.Parse(orders)
            };
        }

        [Test]
        public void Brain_SkipsFlagsTheOrdersForbid()
        {
            var brain = new SpecialistBrain();
            var flags = new List<FlagHandle> { Flag("stay away unless you are level 9 or higher") };

            var rookie = Hero(level: 3);
            Assert.AreNotEqual(SpecialistAction.PursueFlag, brain.Evaluate(rookie, flags, 0.2f).Action);
            Assert.IsFalse(brain.WouldTakeFlag(rookie, flags[0], 0.2f, out _));
            Assert.AreEqual(FlagRefusalKind.Orders, brain.ExplainFlag(rookie, flags[0], 0.2f));

            var veteran = Hero(level: 9);
            var d = brain.Evaluate(veteran, flags, 0.2f);
            Assert.AreEqual(SpecialistAction.PursueFlag, d.Action);
            Assert.AreSame(flags[0], d.TargetFlag);
        }

        [Test]
        public void Brain_NoOrdersBehavesAsBefore()
        {
            var brain = new SpecialistBrain();
            var ctx = Hero(level: 1);
            var plain = new List<FlagHandle> { Flag(null) };
            var unread = new List<FlagHandle> { Flag("go get em") };
            var a = brain.Evaluate(ctx, plain, 0.2f);
            var b = brain.Evaluate(ctx, unread, 0.2f);
            Assert.AreEqual(a.Action, b.Action);
            Assert.AreEqual(a.Score, b.Score, 1e-6f);
        }

        [Test]
        public void GreedyBrokeHeroBendsSoftOrdersButNotStrict()
        {
            var greedy = Hero(level: 2, greed: 0.8f, hunger: 0.8f);
            Assert.IsTrue(FlagOrdersRules.Permits(greedy, Flag("level 9 or higher")));
            Assert.IsFalse(FlagOrdersRules.Permits(greedy, Flag("strictly level 9 or higher")));
            Assert.IsFalse(FlagOrdersRules.Permits(Hero(level: 2, greed: 0.8f, hunger: 0.2f), Flag("level 9 or higher")));
        }

        [Test]
        public void Laya_FillsOnlyConfidentMenuPicks()
        {
            var answers = new Dictionary<string, LayaChoice>();
            var lvl = new LayaChoice { Choice = "level_7" };
            lvl.Probabilities["any"] = 0.1f;
            lvl.Probabilities["level_7"] = 0.8f;
            answers[LayaOrdersPolicy.MinLevelId] = lvl;
            var who = new LayaChoice { Choice = "Medic" };
            who.Probabilities["anyone"] = 0.55f;
            who.Probabilities["Medic"] = 0.45f; // not confident → ignored
            answers[LayaOrdersPolicy.ClassId] = who;
            answers[LayaOrdersPolicy.StrictId] = new LayaChoice { Noul = 0.9f };

            var o = FlagOrdersParser.Parse("only the seasoned ones please");
            Assert.IsFalse(o.HasRules);
            Assert.IsTrue(LayaOrdersPolicy.Apply(o, answers));
            Assert.AreEqual(7, o.minLevel);
            Assert.AreEqual(0, o.onlyClassMask);
            Assert.IsTrue(o.strict);
            Assert.AreEqual("laya", o.source);
        }

        [Test]
        public void Laya_RequestAndMultiAnswerRoundTrip()
        {
            string req = LayaProtocol.BuildRequest(LayaOrdersPolicy.BuildState("x"), LayaOrdersPolicy.BuildQuestions());
            StringAssert.Contains("\"min_level\":{\"type\":\"choice\"", req);
            StringAssert.Contains("\"strict\":{\"type\":\"noul\",\"instructions\":", req);

            const string reply =
                "{\"answers\":{\"min_level\":{\"type\":\"choice\",\"choice\":\"level_9\",\"probabilities\":{\"any\":0.1,\"level_9\":0.9}}," +
                "\"strict\":{\"type\":\"noul\",\"noul\":0.2,\"confidence\":0.8}}}";
            Assert.IsTrue(LayaProtocol.TryParseAnswers(reply, out var answers));
            Assert.AreEqual("level_9", answers["min_level"].Choice);
            Assert.AreEqual(0.2f, answers["strict"].Noul, 1e-6f);
        }
    }
}
