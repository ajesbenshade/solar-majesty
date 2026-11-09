using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class ColonySimTests
    {
        private const int TenMinutes = 12000; // 20 Hz

        private readonly List<Object> _created = new List<Object>();
        private FlagData _explore;
        private FlagData _clearThreat;
        private BuildingData[] _buildings;

        [SetUp]
        public void SetUp()
        {
            _explore = Flag(FlagType.Explore);
            _clearThreat = Flag(FlagType.ClearThreat);

            var hab = ScriptableObject.CreateInstance<BuildingData>();
            _created.Add(hab);
            hab.displayName = "Habitat";
            hab.category = BuildingCategory.Habitat;
            hab.footprintWidth = 2;
            hab.footprintHeight = 2;
            hab.buildTimeSeconds = 30f;
            hab.buildCost = new[] { new ResourceAmount(ResourceId.Metals, 120) };
            _buildings = new[] { hab };
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private FlagData Flag(FlagType type)
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = type;
            data.displayName = type.ToString();
            data.minBounty = 10;
            data.maxBounty = 2000;
            data.defaultBounty = 100;
            return data;
        }

        private ColonySim NewSim(int seed = 5)
        {
            var sim = new ColonySim(seed)
            {
                BuildCatalog = _buildings,
                FlagCatalog = type => type == FlagType.ClearThreat ? _clearThreat : _explore
            };
            sim.Resources.Set(ResourceId.Metals, 5000);
            return sim;
        }

        /// <summary>Plays a short session on <paramref name="sim"/>: commands issued at set ticks, then run on.</summary>
        private static void PlaySession(ColonySim sim, bool cancelFirstFlag = true)
        {
            sim.Commands.Enqueue(OverseerCommand.PostFlag(FlagType.Explore, new Vector3(12f, 0f, 4f), 400f));
            sim.RunSteps(40);
            sim.Commands.Enqueue(OverseerCommand.PlaceBuilding(0, new Vector2Int(2, 3), new Vector3(5f, 0f, 7f)));
            sim.RunSteps(60);
            sim.Commands.Enqueue(OverseerCommand.StepBounty(1, 50f));
            sim.RunSteps(200);
            sim.Commands.Enqueue(OverseerCommand.PostFlag(FlagType.ClearThreat, new Vector3(-20f, 0f, 9f), 900f, "L9+ only"));
            sim.RunSteps(300);
            if (cancelFirstFlag)
                sim.Commands.Enqueue(OverseerCommand.CancelFlag(1));
            sim.RunSteps(TenMinutes - 600);
        }

        [Test]
        public void Step_WithoutAHost_RunsThePureSystems()
        {
            ColonySim sim = NewSim();

            sim.RunSteps(200);

            Assert.AreEqual(200, sim.Clock.TotalSteps);
        }

        [Test]
        public void Commands_ChangeTreasuryFlagsAndOrders()
        {
            ColonySim sim = NewSim();

            sim.Commands.Enqueue(OverseerCommand.PostFlag(FlagType.Explore, new Vector3(12f, 0f, 4f), 400f, "no scouts"));
            sim.Commands.Enqueue(OverseerCommand.PlaceBuilding(0, new Vector2Int(2, 3), new Vector3(5f, 0f, 7f)));
            sim.RunSteps(1);

            Assert.AreEqual(1, sim.Flags.Flags.Count);
            Assert.AreEqual(400, sim.Flags.Flags[0].EscrowMetals);
            Assert.IsNotNull(sim.Flags.Flags[0].Orders);
            Assert.AreEqual(1, sim.Placer.Orders.Count);
            Assert.AreEqual(5000 - 400 - 120, sim.Resources.Get(ResourceId.Metals));

            sim.Commands.Enqueue(OverseerCommand.CancelFlag(1));
            sim.RunSteps(1);

            Assert.AreEqual(0, sim.Flags.Flags.Count);
            Assert.AreEqual(5000 - 120, sim.Resources.Get(ResourceId.Metals), "the escrow comes back");
        }

        /// <summary>
        /// The Phase 6A exit test for the scene-free state: a seed and a command log reproduce ten
        /// minutes of simulation exactly.
        /// </summary>
        [Test]
        public void Replay_OfTheRecordedLog_ReachesTheSameState()
        {
            ColonySim live = NewSim();
            live.Commands.Record = true;
            PlaySession(live);

            ColonySim replayed = NewSim();
            replayed.Replay(live.Commands.Log, TenMinutes);

            Assert.AreEqual(5, live.Commands.Log.Count);
            Assert.AreEqual(TenMinutes, replayed.Clock.TotalSteps);
            Assert.AreEqual(live.StateHash(), replayed.StateHash());
        }

        [Test]
        public void TwoRunsOfTheSameSession_Agree()
        {
            ColonySim a = NewSim();
            ColonySim b = NewSim();

            PlaySession(a);
            PlaySession(b);

            Assert.AreEqual(a.StateHash(), b.StateHash());
        }

        [Test]
        public void StateHash_SeesADifferentCommandOrADifferentSeed()
        {
            ColonySim baseline = NewSim();
            ColonySim keptFlag = NewSim();
            ColonySim otherSeed = NewSim(seed: 6);

            PlaySession(baseline);
            PlaySession(keptFlag, cancelFirstFlag: false);
            PlaySession(otherSeed);

            Assert.AreNotEqual(baseline.StateHash(), keptFlag.StateHash());
            Assert.AreNotEqual(baseline.StateHash(), otherSeed.StateHash());
        }

        [Test]
        public void RunSteps_InOneCallOrMany_ReachesTheSameState()
        {
            ColonySim whole = NewSim();
            ColonySim chunks = NewSim();
            whole.Commands.Enqueue(OverseerCommand.PlaceBuilding(0, new Vector2Int(2, 3), Vector3.zero));
            chunks.Commands.Enqueue(OverseerCommand.PlaceBuilding(0, new Vector2Int(2, 3), Vector3.zero));

            whole.RunSteps(1000);
            for (int i = 0; i < 100; i++) chunks.RunSteps(10);

            Assert.AreEqual(whole.StateHash(), chunks.StateHash());
        }

        private sealed class CountingHost : IColonySimHost
        {
            public readonly List<string> Calls = new List<string>();
            public int Beats;

            public void TickVillage(float dt) => Calls.Add("village");

            public void ResearchInputs(out int labs, out int workers, out float rateMultiplier, out float extraRate)
            {
                Calls.Add("research");
                labs = 0;
                workers = 0;
                rateMultiplier = 1f;
                extraRate = 0f;
            }

            public void AfterResearch() => Calls.Add("after-research");
            public void TickWorld(float dt) => Calls.Add("world");
            public void TickBoard(float dt) => Calls.Add("board");
            public void OnConstructionCompleted(List<ConstructionOrder> completed) => Calls.Add("construction");

            public IReadOnlyList<SpecialistData> PrepareEconomyBeat()
            {
                Beats++;
                Calls.Add("economy");
                return null;
            }

            public void AfterEconomyBeat() => Calls.Add("after-economy");
        }

        /// <summary>The scene's share of a step keeps the order the game loop used before the sim was split out.</summary>
        [Test]
        public void Step_CallsTheHostInOrder_AndTheEconomyOnItsBeat()
        {
            ColonySim sim = NewSim();
            var host = new CountingHost();

            sim.Step(0.05f, host);
            CollectionAssert.AreEqual(
                new[] { "village", "research", "after-research", "world", "board" }, host.Calls);

            for (int i = 0; i < 4; i++) sim.Step(0.05f, host);

            Assert.AreEqual(1, host.Beats, "0.25 s of steps is one economy beat");
            CollectionAssert.AreEqual(
                new[] { "construction", "economy", "after-economy" },
                host.Calls.GetRange(host.Calls.Count - 3, 3));
        }
    }
}
