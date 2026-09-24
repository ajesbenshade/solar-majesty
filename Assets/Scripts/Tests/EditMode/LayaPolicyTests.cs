using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Local Laya model integration: wire format, option gating and fallback. No server needed —
    /// these lock the "model proposes, utility gates guard" contract.
    /// </summary>
    public class LayaPolicyTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private const string Response =
            "{\"model\":\"laya-rl-agent\",\"answers\":{\"action\":{\"type\":\"choice\",\"confidence\":0.41," +
            "\"action\":{\"act_probability\":0.9},\"choice\":\"hunt\",\"probabilities\":{\"rest\":0.2,\"hunt\":0.8}}}," +
            "\"usage\":{\"input_tokens\":88,\"output_tokens\":0}}";

        private static List<LayaOption> RestHunt() => new List<LayaOption>
        {
            new LayaOption { Label = "rest", Description = "Go \"home\"\n now" },
            new LayaOption { Label = "hunt", Description = "Hunt" }
        };

        private SpecialistContext Mech(out List<FlagHandle> flags)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.DefenseMech;
            data.baseGreed = 0.3f;
            data.courage = 0.7f;
            data.combatPreference = 0.9f;

            var fd = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(fd);
            fd.flagType = FlagType.ClearThreat;
            flags = new List<FlagHandle>
            {
                new FlagHandle { Data = fd, WorldPosition = new Vector3(10, 0, 0), CurrentBounty = 900f, RuntimeId = 1 }
            };
            return new SpecialistContext
            {
                Data = data,
                HealthNormalized = 1f,
                Fatigue = 0.5f,
                SafetyPosition = new Vector3(5, 0, 0),
                HasHunt = true,
                HuntPosition = new Vector3(8, 0, 0),
                HuntDistance = 8f
            };
        }

        [Test]
        public void Request_EscapesStateAndCriteria()
        {
            string json = LayaProtocol.BuildChoiceRequest("state", "Pick", RestHunt());
            StringAssert.Contains("\"criteria\":{\"rest\":\"Go \\\"home\\\"\\n now\",\"hunt\":\"Hunt\"}", json);
            StringAssert.StartsWith("{\"state\":\"state\",\"questions\":{\"action\":{\"type\":\"choice\"", json);
        }

        [Test]
        public void Response_ParsesChoiceAndProbabilities()
        {
            Assert.IsTrue(LayaProtocol.TryParseChoice(Response, out var c));
            Assert.AreEqual("hunt", c.Choice);
            Assert.AreEqual(0.8f, c.Probabilities["hunt"], 1e-6f);
            Assert.AreEqual(0.41f, c.Confidence, 1e-6f);
        }

        [Test]
        public void Response_RejectsMalformed()
        {
            Assert.IsFalse(LayaProtocol.TryParseChoice("not json", out _));
            Assert.IsFalse(LayaProtocol.TryParseChoice("{\"answers\":{}}", out _));
            Assert.IsFalse(LayaProtocol.TryParseChoice(Response.Replace("0.8}", "1.8}"), out _));
        }

        [Test]
        public void Resolve_FallsBackWhenUnsureOrOffList()
        {
            LayaProtocol.TryParseChoice(Response, out var c);
            Assert.AreEqual(1, LayaHeroPolicy.Resolve(RestHunt(), c, 0.35f));
            Assert.AreEqual(-1, LayaHeroPolicy.Resolve(RestHunt(), c, 0.9f));
            var restOnly = new List<LayaOption> { new LayaOption { Label = "rest" } };
            Assert.AreEqual(0, LayaHeroPolicy.Resolve(restOnly, c, 0.1f), "labels Laya invents are ignored");
            Assert.AreEqual(-1, LayaHeroPolicy.Resolve(RestHunt(), null, 0.1f));
        }

        [Test]
        public void CollectOptions_FirstIsEvaluatePick()
        {
            var brain = new SpecialistBrain();
            var ctx = Mech(out var flags);
            var list = new List<BrainDecision>();
            Assert.IsTrue(brain.CollectOptions(ctx, flags, 0.2f, list));
            var ev = brain.Evaluate(ctx, flags, 0.2f);
            Assert.AreEqual(ev.Action, list[0].Action);
            Assert.AreEqual(ev.Reason, list[0].Reason);
            Assert.GreaterOrEqual(list.Count, 2);

            var labels = new HashSet<string>();
            foreach (var o in LayaHeroPolicy.BuildOptions(ctx, list))
                Assert.IsTrue(labels.Add(o.Label), $"duplicate label {o.Label}");
        }

        [Test]
        public void CollectOptions_PanicFleeIsForced()
        {
            var brain = new SpecialistBrain();
            var ctx = Mech(out var flags);
            ctx.HealthNormalized = 0.3f;
            var list = new List<BrainDecision>();
            Assert.IsFalse(brain.CollectOptions(ctx, flags, 0.5f, list));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(SpecialistAction.Flee, list[0].Action);
        }

        [Test]
        public void CollectOptions_OnlyOffersFlagsTheGreedGatePasses()
        {
            var brain = new SpecialistBrain();
            var ctx = Mech(out var flags);
            flags[0].CurrentBounty = 1f; // far below any greed threshold
            var list = new List<BrainDecision>();
            brain.CollectOptions(ctx, flags, 0.2f, list);
            foreach (var o in list)
                Assert.AreNotEqual(SpecialistAction.PursueFlag, o.Action);
        }

        [Test]
        public void Mob_OffersAmbushOnlyWithCollectorAndFallsBackToRole()
        {
            var tactics = new List<MobTactic>();
            var s = new LayaMobPolicy.MobState { Kind = "Mite", RaidTarget = "mining camp", RaidTargetNearby = true };
            var opts = LayaMobPolicy.BuildOptions(s, tactics);
            CollectionAssert.AreEqual(new[] { MobTactic.Role, MobTactic.Prowl }, tactics);

            s.CollectorNearby = true;
            LayaMobPolicy.BuildOptions(s, tactics);
            CollectionAssert.AreEqual(new[] { MobTactic.Role, MobTactic.Ambush, MobTactic.Prowl }, tactics);

            var prowl = new LayaChoice { Choice = "prowl" };
            prowl.Probabilities["raid"] = 0.3f;
            prowl.Probabilities["prowl"] = 0.7f;
            tactics.Clear();
            opts = LayaMobPolicy.BuildOptions(new LayaMobPolicy.MobState { Kind = "Mite" }, tactics);
            Assert.AreEqual(MobTactic.Prowl, LayaMobPolicy.Resolve(opts, tactics, prowl, 0.35f));
            Assert.AreEqual(MobTactic.Role, LayaMobPolicy.Resolve(opts, tactics, null, 0.35f));
        }
    }
}
